using System.Runtime.CompilerServices;
using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.LLM;
using Hermes.Agent.Runtime;
using Hermes.Agent.Skills;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Runtime;

[TestClass]
public class NpcRuntimeSupervisorTests
{
    private string _tempDir = null!;
    private string _packRoot = null!;
    private SkillManager _skillManager = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "hermes-runtime-supervisor-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _packRoot = Path.Combine(_tempDir, "packs");
        Directory.CreateDirectory(_packRoot);
        _skillManager = CreateSkillManager();
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public async Task StartAsync_RegistersRunningInstanceAndCreatesNamespace()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var descriptor = CreateDescriptor("haley");

        await supervisor.StartAsync(descriptor, _tempDir, CancellationToken.None);

        var snapshot = supervisor.Snapshot().Single();
        Assert.AreEqual("haley", snapshot.NpcId);
        Assert.AreEqual(NpcRuntimeState.Running, snapshot.State);
        Assert.IsTrue(Directory.Exists(Path.Combine(_tempDir, "runtime", "stardew", "games", "stardew-valley", "saves", "save-1", "npc", "haley", "profiles", "default")));
    }

    [TestMethod]
    public async Task StartAsync_RejectsDuplicateNpcRuntimeForSameProfile()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var descriptor = CreateDescriptor("haley");
        await supervisor.StartAsync(descriptor, _tempDir, CancellationToken.None);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            await supervisor.StartAsync(descriptor, _tempDir, CancellationToken.None));
    }

    [TestMethod]
    public async Task GetOrStartAsync_ReusesExistingRuntimeForSameProfile()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var descriptor = CreateDescriptor("penny");

        var first = await supervisor.GetOrStartAsync(descriptor, _tempDir, CancellationToken.None);
        var second = await supervisor.GetOrStartAsync(descriptor, _tempDir, CancellationToken.None);

        Assert.AreSame(first, second);
        Assert.AreEqual(1, supervisor.Snapshot().Count);
        Assert.AreEqual(NpcRuntimeState.Running, supervisor.Snapshot().Single().State);
    }

    [TestMethod]
    public async Task GetOrCreatePrivateChatHandleAsync_ReusesHandleUntilToolSurfaceChanges()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var pack = CreatePack("penny", "Penny");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");
        var services = new NpcRuntimeCompositionServices(
            new FakeChatClient(),
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler());

        var first = await supervisor.GetOrCreatePrivateChatHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAgentBindingRequest(
                ChannelKey: "private_chat",
                SystemPromptSupplement: "Reply directly.",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")])),
            CancellationToken.None);
        var second = await supervisor.GetOrCreatePrivateChatHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAgentBindingRequest(
                ChannelKey: "private_chat",
                SystemPromptSupplement: "Reply directly.",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")])),
            CancellationToken.None);
        var third = await supervisor.GetOrCreatePrivateChatHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAgentBindingRequest(
                ChannelKey: "private_chat",
                SystemPromptSupplement: "Reply directly.",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_b")])),
            CancellationToken.None);

        Assert.AreSame(first, second);
        Assert.AreNotSame(first, third);
        Assert.AreEqual(2, supervisor.Snapshot().Single().PrivateChatRebindGeneration);
    }

    [TestMethod]
    public async Task GetOrCreatePrivateChatHandleAsync_RebindsWhenToolSnapshotVersionChangesEvenIfFingerprintIsStable()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var pack = CreatePack("penny", "Penny");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");
        var services = new NpcRuntimeCompositionServices(
            new FakeChatClient(),
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler());

        var first = await supervisor.GetOrCreatePrivateChatHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAgentBindingRequest(
                ChannelKey: "private_chat",
                SystemPromptSupplement: "Reply directly.",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                ToolSurfaceSnapshotVersion: 1),
            CancellationToken.None);
        var second = await supervisor.GetOrCreatePrivateChatHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAgentBindingRequest(
                ChannelKey: "private_chat",
                SystemPromptSupplement: "Reply directly.",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                ToolSurfaceSnapshotVersion: 1),
            CancellationToken.None);
        var third = await supervisor.GetOrCreatePrivateChatHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAgentBindingRequest(
                ChannelKey: "private_chat",
                SystemPromptSupplement: "Reply directly.",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                ToolSurfaceSnapshotVersion: 2),
            CancellationToken.None);

        Assert.AreSame(first, second);
        Assert.AreNotSame(first, third);
        Assert.AreEqual(2, supervisor.Snapshot().Single().PrivateChatRebindGeneration);
    }

    [TestMethod]
    public async Task GetOrCreateAutonomyHandleAsync_ReusesLoopUntilAdapterKeyChanges()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var pack = CreatePack("haley", "Haley");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");
        var services = new NpcRuntimeCompositionServices(
            new FakeChatClient(),
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler());
        var adapterFactoryCalls = 0;

        var first = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () =>
                {
                    adapterFactoryCalls++;
                    return new FakeGameAdapter();
                },
                GameToolFactory: adapter => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")])),
            CancellationToken.None);
        var second = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () =>
                {
                    adapterFactoryCalls++;
                    return new FakeGameAdapter();
                },
                GameToolFactory: adapter => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")])),
            CancellationToken.None);
        var third = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-b",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () =>
                {
                    adapterFactoryCalls++;
                    return new FakeGameAdapter();
                },
                GameToolFactory: adapter => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")])),
            CancellationToken.None);

        Assert.AreSame(first, second);
        Assert.AreNotSame(first, third);
        Assert.AreEqual(2, adapterFactoryCalls);
        Assert.AreEqual(2, supervisor.Snapshot().Single().AutonomyRebindGeneration);
    }

    [TestMethod]
    public async Task GetOrCreateAutonomyHandleAsync_RebindsWhenToolSnapshotVersionChangesEvenIfAdapterKeyIsStable()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var pack = CreatePack("haley", "Haley");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");
        var services = new NpcRuntimeCompositionServices(
            new FakeChatClient(),
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler());
        var adapterFactoryCalls = 0;

        var first = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () =>
                {
                    adapterFactoryCalls++;
                    return new FakeGameAdapter();
                },
                GameToolFactory: adapter => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                ToolSurfaceSnapshotVersion: 1),
            CancellationToken.None);
        var second = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () =>
                {
                    adapterFactoryCalls++;
                    return new FakeGameAdapter();
                },
                GameToolFactory: adapter => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                ToolSurfaceSnapshotVersion: 1),
            CancellationToken.None);
        var third = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () =>
                {
                    adapterFactoryCalls++;
                    return new FakeGameAdapter();
                },
                GameToolFactory: adapter => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                ToolSurfaceSnapshotVersion: 2),
            CancellationToken.None);

        Assert.AreSame(first, second);
        Assert.AreNotSame(first, third);
        Assert.AreEqual(2, adapterFactoryCalls);
        Assert.AreEqual(2, supervisor.Snapshot().Single().AutonomyRebindGeneration);
    }

    [TestMethod]
    public async Task GetOrCreateAutonomyHandleAsync_RebindsWhenSystemPromptSupplementChanges()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var pack = CreatePack("haley", "Haley");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");
        var services = new NpcRuntimeCompositionServices(
            new FakeChatClient(),
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler());
        var adapterFactoryCalls = 0;

        var first = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                SystemPromptSupplement: "Persona facts v1",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () =>
                {
                    adapterFactoryCalls++;
                    return new FakeGameAdapter();
                },
                GameToolFactory: adapter => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                ToolSurfaceSnapshotVersion: 1),
            CancellationToken.None);
        var second = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                SystemPromptSupplement: "Persona facts v1",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () =>
                {
                    adapterFactoryCalls++;
                    return new FakeGameAdapter();
                },
                GameToolFactory: adapter => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                ToolSurfaceSnapshotVersion: 1),
            CancellationToken.None);
        var third = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                SystemPromptSupplement: "Persona facts v2",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () =>
                {
                    adapterFactoryCalls++;
                    return new FakeGameAdapter();
                },
                GameToolFactory: adapter => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                ToolSurfaceSnapshotVersion: 1),
            CancellationToken.None);

        Assert.AreSame(first, second);
        Assert.AreNotSame(first, third);
        Assert.AreEqual(2, adapterFactoryCalls);
        Assert.AreEqual(2, supervisor.Snapshot().Single().AutonomyRebindGeneration);
    }

    private static NpcRuntimeDescriptor CreateDescriptor(string npcId)
        => new(
            npcId,
            npcId,
            "stardew-valley",
            "save-1",
            "default",
            "stardew",
            "pack-root",
            $"sdv_save-1_{npcId}_default");

    private SkillManager CreateSkillManager()
    {
        var skillsDir = Path.Combine(_tempDir, "skills", "memory");
        Directory.CreateDirectory(skillsDir);
        File.WriteAllText(
            Path.Combine(skillsDir, "SKILL.md"),
            """
            ---
            name: supervisor-test-skill
            description: Test skill.
            ---
            Use memory carefully.
            """);
        return new SkillManager(Path.Combine(_tempDir, "skills"), NullLogger<SkillManager>.Instance);
    }

    private NpcPack CreatePack(string npcId, string displayName)
    {
        var root = Path.Combine(_packRoot, npcId, "default");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "SOUL.md"), $"# {displayName}\n\n{npcId}-pack-soul");
        File.WriteAllText(Path.Combine(root, "facts.md"), $"{displayName} facts");
        File.WriteAllText(Path.Combine(root, "voice.md"), $"{displayName} voice");
        File.WriteAllText(Path.Combine(root, "boundaries.md"), $"{displayName} boundaries");
        File.WriteAllText(Path.Combine(root, "skills.json"), """{"required":[],"optional":[]}""");

        return new NpcPack(
            new NpcPackManifest
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
            },
            root);
    }

    private sealed class FakeChatClient : IChatClient
    {
        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("ok");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
            => Task.FromResult(new ChatResponse { Content = "ok", FinishReason = "stop" });

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
        public void Cancel(string taskId) { }
        public CronTask? GetTask(string taskId) => null;
        public IReadOnlyList<CronTask> GetAllTasks() => Array.Empty<CronTask>();
        public DateTimeOffset? GetNextRun(string taskId) => null;
    }

    private sealed class FakeTool(string name) : ITool
    {
        public string Name { get; } = name;
        public string Description => "test tool";
        public Type ParametersType => typeof(NoopParameters);
        public Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
            => Task.FromResult(ToolResult.Ok("ok"));
    }

    private sealed class NoopParameters
    {
    }

    private sealed class FakeGameAdapter : IGameAdapter
    {
        public string AdapterId => "stardew";
        public IGameCommandService Commands { get; } = new FakeCommandService();
        public IGameQueryService Queries { get; } = new FakeQueryService();
        public IGameEventSource Events { get; } = new FakeEventSource();
    }

    private sealed class FakeCommandService : IGameCommandService
    {
        public Task<GameCommandResult> SubmitAsync(GameAction action, CancellationToken ct)
            => Task.FromResult(new GameCommandResult(true, "cmd-1", "completed", null, action.TraceId));

        public Task<GameCommandStatus> GetStatusAsync(string commandId, CancellationToken ct)
            => Task.FromResult(new GameCommandStatus(commandId, "Haley", "autonomy", "completed", 1, null, null));

        public Task<GameCommandStatus> CancelAsync(string commandId, string reason, CancellationToken ct)
            => Task.FromResult(new GameCommandStatus(commandId, "Haley", "autonomy", "cancelled", 1, reason, null));
    }

    private sealed class FakeQueryService : IGameQueryService
    {
        public Task<GameObservation> ObserveAsync(string npcId, CancellationToken ct)
            => Task.FromResult(new GameObservation(npcId, "stardew-valley", DateTime.UtcNow, "standing still", ["location=Town"]));

        public Task<WorldSnapshot> GetWorldSnapshotAsync(string npcId, CancellationToken ct)
            => Task.FromResult(new WorldSnapshot("stardew-valley", "save-1", DateTime.UtcNow, [], []));
    }

    private sealed class FakeEventSource : IGameEventSource
    {
        public Task<IReadOnlyList<GameEventRecord>> PollAsync(GameEventCursor cursor, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<GameEventRecord>>(Array.Empty<GameEventRecord>());
    }
}
