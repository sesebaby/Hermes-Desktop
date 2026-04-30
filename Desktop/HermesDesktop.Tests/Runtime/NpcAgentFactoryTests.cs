using System.Runtime.CompilerServices;
using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Hermes.Agent.LLM;
using Hermes.Agent.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Runtime;

[TestClass]
public class NpcAgentFactoryTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "hermes-npc-agent-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public void Create_RegistersOnlyNpcSafeTools()
    {
        var chatClient = new FakeChatClient();
        var context = new NpcRuntimeContextFactory().Create(
            new NpcNamespace(_tempDir, "stardew-valley", "save-1", "haley", "default"),
            chatClient,
            NullLoggerFactory.Instance);
        var tools = StardewNpcToolFactory.CreateDefault(
            new FakeGameAdapter(new FakeCommandService(), new FakeQueryService(), new FakeEventSource()),
            CreateDescriptor("haley"));

        var agent = new NpcAgentFactory().Create(
            chatClient,
            context,
            tools,
            NullLoggerFactory.Instance,
            maxToolIterations: 4);

        CollectionAssert.AreEqual(
            new[] { "stardew_status", "stardew_move", "stardew_speak", "stardew_task_status" },
            agent.GetToolDefinitions().Select(tool => tool.Name).ToArray());
        Assert.AreEqual(4, agent.MaxToolIterations);
        Assert.IsFalse(agent.GetToolDefinitions().Any(tool => tool.Name is "agent" or "todo" or "memory" or "ask_user" or "schedule_cron"));
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

    private sealed class FakeCommandService : IGameCommandService
    {
        public Task<GameCommandResult> SubmitAsync(GameAction action, CancellationToken ct)
            => Task.FromResult(new GameCommandResult(true, "cmd-1", StardewCommandStatuses.Queued, null, action.TraceId));

        public Task<GameCommandStatus> GetStatusAsync(string commandId, CancellationToken ct)
            => Task.FromResult(new GameCommandStatus(commandId, "haley", "move", StardewCommandStatuses.Running, 0, null, null));

        public Task<GameCommandStatus> CancelAsync(string commandId, string reason, CancellationToken ct)
            => Task.FromResult(new GameCommandStatus(commandId, "haley", "move", StardewCommandStatuses.Cancelled, 0, reason, null));
    }

    private sealed class FakeQueryService : IGameQueryService
    {
        public Task<GameObservation> ObserveAsync(string npcId, CancellationToken ct)
            => Task.FromResult(new GameObservation(npcId, "stardew-valley", DateTime.UtcNow, "status", []));

        public Task<WorldSnapshot> GetWorldSnapshotAsync(string npcId, CancellationToken ct)
            => Task.FromResult(new WorldSnapshot("stardew-valley", "save-1", DateTime.UtcNow, [], []));
    }

    private sealed class FakeEventSource : IGameEventSource
    {
        public Task<IReadOnlyList<GameEventRecord>> PollAsync(GameEventCursor cursor, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<GameEventRecord>>([]);
    }
}
