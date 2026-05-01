using System.Runtime.CompilerServices;
using System.Text.Json;
using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Hermes.Agent.LLM;
using Hermes.Agent.Runtime;
using Hermes.Agent.Skills;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Stardew;

[TestClass]
public sealed class StardewAutonomyTickDebugServiceTests
{
    private string _tempDir = null!;
    private string _packRoot = null!;
    private SkillManager _skillManager = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-autonomy-tests", Guid.NewGuid().ToString("N"));
        _packRoot = Path.Combine(_tempDir, "packs");

        var skillsDir = Path.Combine(_tempDir, "skills", "autonomy");
        Directory.CreateDirectory(skillsDir);
        File.WriteAllText(
            Path.Combine(skillsDir, "SKILL.md"),
            """
            ---
            name: npc-autonomy-skill
            description: Keep autonomy actions grounded in observed Stardew facts.
            ---
            Use game facts before taking autonomous action.
            """);
        _skillManager = new SkillManager(Path.Combine(_tempDir, "skills"), NullLogger<SkillManager>.Instance);

        CreatePack("haley", "Haley");
        CreatePack("penny", "Penny");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public async Task RunOneTickAsync_PennyUsesSharedPromptToolSurfaceAndPackSeed()
    {
        var chatClient = new ToolSnapshotChatClient("I will wait near the library.");
        var queries = new FakeQueryService(new GameObservation(
            "penny",
            "stardew-valley",
            DateTime.UtcNow,
            "Penny is near the library.",
            ["location=Town", "tile=12,8"]));
        var adapter = new FakeGameAdapter(
            new FakeCommandService(),
            queries,
            new FakeEventSource([
                new GameEventRecord("evt-1", "time_changed", null, DateTime.UtcNow, "The clock advanced.")
            ]));
        var discovery = new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
            new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token" },
            DateTimeOffset.UtcNow,
            1234,
            "save-42"));
        var service = new StardewAutonomyTickDebugService(
            discovery,
            _ => adapter,
            chatClient,
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            new NpcRuntimeSupervisor(),
            new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot),
            discoveredToolProvider: () => [new DiscoveredNoopTool("mcp_dynamic_test")],
            includeMemory: true,
            includeUser: true,
            maxToolIterations: 2,
            runtimeRoot: _tempDir);

        var result = await service.RunOneTickAsync("Penny", CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual("penny", result.NpcId);
        Assert.AreEqual("I will wait near the library.", result.DecisionResponse);
        Assert.AreEqual("penny", queries.LastNpcId);

        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "memory");
        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "session_search");
        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "skills_list");
        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "skill_invoke");
        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "stardew_status");
        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "stardew_move");
        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "stardew_speak");
        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "stardew_open_private_chat");
        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "stardew_task_status");
        CollectionAssert.Contains(chatClient.ToolNames.ToArray(), "mcp_dynamic_test");

        Assert.IsTrue(chatClient.SystemMessages.Any(message =>
            message.Contains("## Skills (mandatory)", StringComparison.Ordinal) &&
            message.Contains("npc-autonomy-skill", StringComparison.Ordinal)));

        var runtimeSoulPath = Path.Combine(
            _tempDir,
            "runtime",
            "stardew",
            "games",
            "stardew-valley",
            "saves",
            "save-42",
            "npc",
            "penny",
            "profiles",
            "default",
            "SOUL.md");
        Assert.IsTrue(File.Exists(runtimeSoulPath));
        StringAssert.Contains(await File.ReadAllTextAsync(runtimeSoulPath), "penny-pack-soul");
    }

    [TestMethod]
    public async Task RunOneTickAsync_MissingNpcIdFailsInsteadOfDefaultingToHaley()
    {
        var service = new StardewAutonomyTickDebugService(
            new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
                new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token" },
                DateTimeOffset.UtcNow,
                1234,
                "save-42")),
            _ => throw new AssertFailedException("Factory should not run when npcId is missing."),
            new ToolSnapshotChatClient("unused"),
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            new NpcRuntimeSupervisor(),
            new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot),
            discoveredToolProvider: null,
            includeMemory: true,
            includeUser: true,
            maxToolIterations: 2,
            runtimeRoot: _tempDir);

        var result = await service.RunOneTickAsync("", CancellationToken.None);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(StardewBridgeErrorCodes.InvalidTarget, result.FailureReason);
    }

    [TestMethod]
    public async Task RunOneTickAsync_MissingDiscoverySaveIdFailsInsteadOfUsingManualDebug()
    {
        var service = new StardewAutonomyTickDebugService(
            new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
                new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token" },
                DateTimeOffset.UtcNow,
                1234,
                null)),
            _ => throw new AssertFailedException("Factory should not run when discovery saveId is missing."),
            new ToolSnapshotChatClient("unused"),
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            new NpcRuntimeSupervisor(),
            new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot),
            discoveredToolProvider: null,
            includeMemory: true,
            includeUser: true,
            maxToolIterations: 2,
            runtimeRoot: _tempDir);

        var result = await service.RunOneTickAsync("Penny", CancellationToken.None);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(StardewBridgeErrorCodes.BridgeStaleDiscovery, result.FailureReason);
    }

    private void CreatePack(string npcId, string displayName)
    {
        var root = Path.Combine(_packRoot, npcId, "default");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "SOUL.md"), $"# {displayName}\n\n{npcId}-pack-soul");
        File.WriteAllText(Path.Combine(root, "facts.md"), $"{displayName} facts");
        File.WriteAllText(Path.Combine(root, "voice.md"), $"{displayName} voice");
        File.WriteAllText(Path.Combine(root, "boundaries.md"), $"{displayName} boundaries");
        File.WriteAllText(Path.Combine(root, "skills.json"), "[]");

        var manifest = new NpcPackManifest
        {
            SchemaVersion = 1,
            NpcId = npcId,
            GameId = "stardew-valley",
            ProfileId = "default",
            DefaultProfileId = "default",
            DisplayName = displayName,
            SmapiName = displayName,
            Aliases = [npcId, displayName],
            TargetEntityId = npcId,
            AdapterId = "stardew",
            SoulFile = "SOUL.md",
            FactsFile = "facts.md",
            VoiceFile = "voice.md",
            BoundariesFile = "boundaries.md",
            SkillsFile = "skills.json",
            Capabilities = ["move", "speak"]
        };
        File.WriteAllText(
            Path.Combine(root, FileSystemNpcPackLoader.ManifestFileName),
            JsonSerializer.Serialize(manifest));
    }

    private sealed class FakeDiscovery : IStardewBridgeDiscovery
    {
        private readonly StardewBridgeDiscoverySnapshot _snapshot;

        public FakeDiscovery(StardewBridgeDiscoverySnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public string DiscoveryFilePath => "fake-discovery.json";

        public bool TryReadLatest(out StardewBridgeDiscoverySnapshot? snapshot, out string? failureReason)
        {
            snapshot = _snapshot;
            failureReason = null;
            return true;
        }
    }

    private sealed class FakeGameAdapter : IGameAdapter
    {
        public FakeGameAdapter(IGameCommandService commands, IGameQueryService queries, IGameEventSource events)
        {
            Commands = commands;
            Queries = queries;
            Events = events;
        }

        public string AdapterId => "stardew";

        public IGameCommandService Commands { get; }

        public IGameQueryService Queries { get; }

        public IGameEventSource Events { get; }
    }

    private sealed class FakeQueryService : IGameQueryService
    {
        private readonly GameObservation _observation;

        public FakeQueryService(GameObservation observation)
        {
            _observation = observation;
        }

        public string? LastNpcId { get; private set; }

        public Task<GameObservation> ObserveAsync(string npcId, CancellationToken ct)
        {
            LastNpcId = npcId;
            return Task.FromResult(_observation);
        }

        public Task<WorldSnapshot> GetWorldSnapshotAsync(string npcId, CancellationToken ct)
            => Task.FromResult(new WorldSnapshot("stardew-valley", "save-42", _observation.TimestampUtc, [], []));
    }

    private sealed class FakeEventSource : IGameEventSource
    {
        private readonly IReadOnlyList<GameEventRecord> _records;

        public FakeEventSource(IReadOnlyList<GameEventRecord> records)
        {
            _records = records;
        }

        public Task<IReadOnlyList<GameEventRecord>> PollAsync(GameEventCursor cursor, CancellationToken ct)
            => Task.FromResult(_records);
    }

    private sealed class FakeCommandService : IGameCommandService
    {
        public Task<GameCommandResult> SubmitAsync(GameAction action, CancellationToken ct)
            => Task.FromResult(new GameCommandResult(true, "cmd-1", StardewCommandStatuses.Completed, null, action.TraceId));

        public Task<GameCommandStatus> GetStatusAsync(string commandId, CancellationToken ct)
            => Task.FromResult(new GameCommandStatus(commandId, "Penny", "debug", StardewCommandStatuses.Completed, 1, null, null));

        public Task<GameCommandStatus> CancelAsync(string commandId, string reason, CancellationToken ct)
            => Task.FromResult(new GameCommandStatus(commandId, "Penny", "debug", StardewCommandStatuses.Cancelled, 1, reason, null));
    }

    private sealed class ToolSnapshotChatClient : IChatClient
    {
        private readonly string _response;

        public ToolSnapshotChatClient(string response)
        {
            _response = response;
        }

        public List<string> ToolNames { get; } = new();

        public List<string> SystemMessages { get; } = new();

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult(_response);

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            ToolNames.Clear();
            ToolNames.AddRange(tools.Select(tool => tool.Name));
            SystemMessages.Clear();
            SystemMessages.AddRange(messages
                .Where(message => string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase))
                .Select(message => message.Content));

            return Task.FromResult(new ChatResponse
            {
                Content = _response,
                FinishReason = "stop"
            });
        }

        public async IAsyncEnumerable<string> StreamAsync(IEnumerable<Message> messages, [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<StreamEvent> StreamAsync(
            string? systemPrompt,
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition>? tools = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class NoopCronScheduler : ICronScheduler
    {
        public event EventHandler<CronTaskDueEventArgs>? TaskDue;

        public void Schedule(CronTask task) => TaskDue?.Invoke(this, new CronTaskDueEventArgs { Task = task, FiredAt = DateTimeOffset.UtcNow });

        public void Cancel(string taskId)
        {
        }

        public CronTask? GetTask(string taskId) => null;

        public IReadOnlyList<CronTask> GetAllTasks() => Array.Empty<CronTask>();

        public DateTimeOffset? GetNextRun(string taskId) => null;
    }

    private sealed class DiscoveredNoopTool : ITool
    {
        public DiscoveredNoopTool(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public string Description => "Dynamic discovered test tool";

        public Type ParametersType => typeof(NoopParameters);

        public Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
            => Task.FromResult(ToolResult.Ok("ok"));
    }

    private sealed class NoopParameters
    {
    }
}
