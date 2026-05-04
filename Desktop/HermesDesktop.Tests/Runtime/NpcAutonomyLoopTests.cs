using System.Runtime.CompilerServices;
using System.Text.Json;
using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.LLM;
using Hermes.Agent.Memory;
using Hermes.Agent.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Runtime;

[TestClass]
public class NpcAutonomyLoopTests
{
    [TestMethod]
    public async Task RunOneTickAsync_GathersObservationAndEventsWithoutCallingCommands()
    {
        var at = new DateTime(2026, 4, 30, 9, 30, 0, DateTimeKind.Utc);
        var descriptor = CreateDescriptor("haley");
        var factStore = new NpcObservationFactStore();
        var commands = new CountingCommandService();
        var queries = new FakeQueryService(new GameObservation(
            "haley",
            "stardew-valley",
            at,
            "Haley is near the town fountain.",
            ["location=Town", "tile=42,17"]));
        var events = new FakeEventSource(
            [
                new GameEventRecord("evt-2", "proximity", "haley", at.AddSeconds(1), "The farmer entered Haley's proximity."),
                new GameEventRecord("evt-3", "time_changed", null, at.AddMinutes(10), "The clock advanced."),
                new GameEventRecord("evt-4", "proximity", "penny", at.AddSeconds(2), "The farmer entered Penny's proximity.")
            ]);
        var adapter = new FakeGameAdapter(commands, queries, events);
        var loop = new NpcAutonomyLoop(adapter, factStore);

        var result = await loop.RunOneTickAsync(descriptor, new GameEventCursor("evt-1"), CancellationToken.None);

        Assert.AreEqual("haley", queries.LastNpcId);
        Assert.AreEqual("evt-1", events.LastCursor?.Since);
        Assert.AreEqual(0, commands.SubmitCalls);
        Assert.AreEqual(0, commands.StatusCalls);
        Assert.AreEqual(0, commands.CancelCalls);

        Assert.AreEqual(1, result.ObservationFacts);
        Assert.AreEqual(2, result.EventFacts);
        var facts = factStore.Snapshot(descriptor);
        Assert.AreEqual(3, facts.Count);
        Assert.IsTrue(facts.Any(fact => fact.SourceKind == "observation"));
        Assert.IsTrue(facts.Any(fact => fact.SourceId == "evt-2"));
        Assert.IsTrue(facts.Any(fact => fact.SourceId == "evt-3"));
        Assert.IsFalse(facts.Any(fact => fact.SourceId == "evt-4"));
    }

    [TestMethod]
    public async Task RunOneTickAsync_AdvancesCursorUsingEventSequenceWhenPresent()
    {
        var at = new DateTime(2026, 4, 30, 9, 40, 0, DateTimeKind.Utc);
        var descriptor = CreateDescriptor("haley");
        var factStore = new NpcObservationFactStore();
        var commands = new CountingCommandService();
        var queries = new FakeQueryService(new GameObservation(
            "haley",
            "stardew-valley",
            at,
            "Haley is near the town fountain.",
            ["location=Town"]));
        var events = new FakeEventSource(
            new GameEventBatch(
                [
                    new GameEventRecord("evt-11", "proximity", "haley", at.AddSeconds(1), "The farmer entered Haley's proximity.", Sequence: 11),
                    new GameEventRecord("evt-12", "proximity", "penny", at.AddSeconds(2), "The farmer entered Penny's proximity.", Sequence: 12)
                ],
                new GameEventCursor("evt-12", 15)));
        var adapter = new FakeGameAdapter(commands, queries, events);
        var loop = new NpcAutonomyLoop(adapter, factStore);

        var result = await loop.RunOneTickAsync(descriptor, new GameEventCursor("evt-10", 10), CancellationToken.None);

        Assert.AreEqual("evt-12", result.NextEventCursor?.Since);
        Assert.AreEqual(15L, result.NextEventCursor?.Sequence);
    }

    [TestMethod]
    public async Task RunOneTickAsync_ObservesBeforeAgentDecision()
    {
        var steps = new List<string>();
        var at = new DateTime(2026, 4, 30, 10, 0, 0, DateTimeKind.Utc);
        var descriptor = CreateDescriptor("haley");
        var factStore = new NpcObservationFactStore();
        var commands = new CountingCommandService();
        var queries = new FakeQueryService(new GameObservation(
            "haley",
            "stardew-valley",
            at,
            "Haley is at the fountain.",
            ["location=Town"]));
        queries.OnObserve = () => steps.Add("observe");
        var events = new FakeEventSource(
            [new GameEventRecord("evt-2", "time_changed", null, at.AddMinutes(10), "The clock advanced.")]);
        events.OnPoll = () => steps.Add("poll");
        var agent = new FakeAgent(() => steps.Add("decide"));
        var adapter = new FakeGameAdapter(commands, queries, events);
        var loop = new NpcAutonomyLoop(adapter, factStore, agent);

        var result = await loop.RunOneTickAsync(descriptor, new GameEventCursor("evt-1"), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "observe", "poll", "decide" }, steps);
        Assert.AreEqual(1, agent.ChatCalls);
        Assert.IsTrue(agent.LastMessage?.Contains("location=Town", StringComparison.Ordinal) ?? false);
        Assert.IsTrue(agent.LastMessage?.Contains("evt-2", StringComparison.Ordinal) ?? false);
        Assert.AreEqual("wait", result.DecisionResponse);
        Assert.AreEqual(0, commands.SubmitCalls);
    }

    [TestMethod]
    public async Task RunOneTickAsync_DecisionMessageDoesNotReuseHistoricalMoveCandidates()
    {
        var descriptor = CreateDescriptor("haley");
        var factStore = new NpcObservationFactStore();
        var agent = new FakeAgent(() => { });
        var loop = new NpcAutonomyLoop(
            new FakeGameAdapter(
                new CountingCommandService(),
                new FakeQueryService(new GameObservation("haley", "stardew-valley", DateTime.UtcNow, "unused", [])),
                new FakeEventSource([])),
            factStore,
            agent);

        await loop.RunOneTickAsync(
            descriptor,
            new GameObservation(
                "haley",
                "stardew-valley",
                new DateTime(2026, 4, 30, 10, 0, 0, DateTimeKind.Utc),
                "Haley can step east.",
                ["location=HaleyHouse", "moveCandidate[0]=locationName=HaleyHouse,x=9,y=7,reason=same_location_safe_reposition"]),
            new GameEventBatch([], new GameEventCursor()),
            CancellationToken.None);

        await loop.RunOneTickAsync(
            descriptor,
            new GameObservation(
                "haley",
                "stardew-valley",
                new DateTime(2026, 4, 30, 10, 1, 0, DateTimeKind.Utc),
                "Haley can step south.",
                ["location=HaleyHouse", "moveCandidate[0]=locationName=HaleyHouse,x=8,y=8,reason=same_location_safe_reposition"]),
            new GameEventBatch([], new GameEventCursor()),
            CancellationToken.None);

        Assert.AreEqual(2, agent.Messages.Count);
        StringAssert.Contains(agent.Messages[1], "x=8,y=8");
        Assert.IsFalse(
            agent.Messages[1].Contains("x=9,y=7", StringComparison.Ordinal),
            "Old move candidates must remain in persisted debug facts but must not re-enter the current decision prompt.");
        Assert.IsTrue(
            factStore.Snapshot(descriptor).Any(fact => fact.Facts.Any(value => value.Contains("x=9,y=7", StringComparison.Ordinal))),
            "The store can retain historical observations for diagnostics; only the live decision prompt must be current-only.");
    }

    [TestMethod]
    public async Task RunOneTickAsync_WithRuntimeInstance_WritesActivityLogAndUpdatesTrace()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-npc-loop-trace-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var descriptor = CreateDescriptor("haley");
            var ns = new NpcNamespace(tempDir, descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId);
            var instance = new NpcRuntimeInstance(descriptor, ns);
            await instance.StartAsync(CancellationToken.None);
            var logPath = Path.Combine(ns.ActivityPath, "runtime.jsonl");
            var factStore = new NpcObservationFactStore();
            var adapter = new FakeGameAdapter(
                new CountingCommandService(),
                new FakeQueryService(new GameObservation(
                    "haley",
                    "stardew-valley",
                    DateTime.UtcNow,
                    "Haley is idle.",
                    ["location=Town"])),
                new FakeEventSource([]));
            var loop = new NpcAutonomyLoop(
                adapter,
                factStore,
                logWriter: new NpcRuntimeLogWriter(logPath),
                traceIdFactory: () => "trace-1");

            var result = await loop.RunOneTickAsync(instance, new GameEventCursor(null), CancellationToken.None);

            Assert.AreEqual("trace-1", result.TraceId);
            Assert.AreEqual("trace-1", instance.Snapshot().LastTraceId);
            var line = File.ReadAllLines(logPath).Single();
            using var doc = JsonDocument.Parse(line);
            Assert.AreEqual("trace-1", doc.RootElement.GetProperty("traceId").GetString());
            Assert.AreEqual("haley", doc.RootElement.GetProperty("npcId").GetString());
            Assert.AreEqual("completed", doc.RootElement.GetProperty("stage").GetString());
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task RunOneTickAsync_WhenDecisionNarratesMovementWithoutMoveToolCall_WritesDiagnosticLog()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-npc-loop-diagnostic-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var descriptor = CreateDescriptor("haley");
            var ns = new NpcNamespace(tempDir, descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId);
            ns.EnsureDirectories();
            var logPath = Path.Combine(ns.ActivityPath, "runtime.jsonl");
            var factStore = new NpcObservationFactStore();
            var adapter = new FakeGameAdapter(
                new CountingCommandService(),
                new FakeQueryService(new GameObservation(
                    "haley",
                    "stardew-valley",
                    DateTime.UtcNow,
                    "Haley is in her room with a safe candidate.",
                    [
                        "location=HaleyHouse",
                        "moveCandidate[0]=locationName=HaleyHouse,x=6,y=4,reason=same_location_safe_reposition"
                    ])),
                new FakeEventSource([]));
            var agent = new FakeAgent(() => { }, "她转身走向床铺。");
            var loop = new NpcAutonomyLoop(
                adapter,
                factStore,
                agent,
                logWriter: new NpcRuntimeLogWriter(logPath),
                traceIdFactory: () => "trace-narrative-move");

            await loop.RunOneTickAsync(descriptor, new GameEventCursor(null), CancellationToken.None);

            var records = File.ReadAllLines(logPath)
                .Select(line => JsonDocument.Parse(line))
                .ToArray();
            try
            {
                Assert.IsTrue(records.Any(doc =>
                    doc.RootElement.GetProperty("actionType").GetString() == "diagnostic" &&
                    doc.RootElement.GetProperty("target").GetString() == "stardew_move" &&
                    doc.RootElement.GetProperty("stage").GetString() == "warning" &&
                    doc.RootElement.GetProperty("result").GetString() == "narrative_move_without_stardew_move"),
                    "A movement-looking final reply without a stardew_move tool call should be visible in runtime.jsonl.");
            }
            finally
            {
                foreach (var record in records)
                    record.Dispose();
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task RunOneTickAsync_WithDecisionResponse_WritesNpcLocalMemory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-npc-loop-memory-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var descriptor = CreateDescriptor("haley");
            var ns = new NpcNamespace(tempDir, descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId);
            ns.EnsureDirectories();
            var memoryManager = ns.CreateMemoryManager(new FakeChatClient(), NullLogger<MemoryManager>.Instance);
            var factStore = new NpcObservationFactStore();
            var adapter = new FakeGameAdapter(
                new CountingCommandService(),
                new FakeQueryService(new GameObservation(
                    "haley",
                    "stardew-valley",
                    DateTime.UtcNow,
                    "Haley is idle.",
                    ["location=Town"])),
                new FakeEventSource([]));
            var agent = new FakeAgent(() => { }, "I will wait near the fountain.");
            var loop = new NpcAutonomyLoop(
                adapter,
                factStore,
                agent,
                memoryManager: memoryManager,
                traceIdFactory: () => "trace-memory");

            await loop.RunOneTickAsync(descriptor, new GameEventCursor(null), CancellationToken.None);

            var entries = await memoryManager.ReadEntriesAsync("memory", CancellationToken.None);
            Assert.AreEqual(1, entries.Count);
            Assert.IsTrue(entries[0].Contains("trace-memory", StringComparison.Ordinal));
            Assert.IsTrue(entries[0].Contains("I will wait near the fountain.", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
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

    private sealed class CountingCommandService : IGameCommandService
    {
        public int SubmitCalls { get; private set; }
        public int StatusCalls { get; private set; }
        public int CancelCalls { get; private set; }

        public Task<GameCommandResult> SubmitAsync(GameAction action, CancellationToken ct)
        {
            SubmitCalls++;
            return Task.FromResult(new GameCommandResult(false, "", "not-called", "unexpected", action.TraceId));
        }

        public Task<GameCommandStatus> GetStatusAsync(string commandId, CancellationToken ct)
        {
            StatusCalls++;
            return Task.FromResult(new GameCommandStatus(commandId, "", "", "not-called", 0, null, "unexpected"));
        }

        public Task<GameCommandStatus> CancelAsync(string commandId, string reason, CancellationToken ct)
        {
            CancelCalls++;
            return Task.FromResult(new GameCommandStatus(commandId, "", "", "not-called", 0, null, "unexpected"));
        }
    }

    private sealed class FakeQueryService : IGameQueryService
    {
        private readonly GameObservation _observation;

        public FakeQueryService(GameObservation observation)
        {
            _observation = observation;
        }

        public string? LastNpcId { get; private set; }

        public Action? OnObserve { get; set; }

        public Task<GameObservation> ObserveAsync(string npcId, CancellationToken ct)
        {
            OnObserve?.Invoke();
            LastNpcId = npcId;
            return Task.FromResult(_observation);
        }

        public Task<WorldSnapshot> GetWorldSnapshotAsync(string npcId, CancellationToken ct)
            => Task.FromResult(new WorldSnapshot("stardew-valley", "save-1", _observation.TimestampUtc, [], []));
    }

    private sealed class FakeEventSource : IGameEventSource
    {
        private readonly GameEventBatch _batch;

        public FakeEventSource(IReadOnlyList<GameEventRecord> records)
        {
            _batch = new GameEventBatch(records, GameEventCursor.Advance(new GameEventCursor(), records));
        }

        public FakeEventSource(GameEventBatch batch)
        {
            _batch = batch;
        }

        public GameEventCursor? LastCursor { get; private set; }

        public Action? OnPoll { get; set; }

        public Task<IReadOnlyList<GameEventRecord>> PollAsync(GameEventCursor cursor, CancellationToken ct)
        {
            OnPoll?.Invoke();
            LastCursor = cursor;
            return Task.FromResult(_batch.Records);
        }

        public Task<GameEventBatch> PollBatchAsync(GameEventCursor cursor, CancellationToken ct)
        {
            OnPoll?.Invoke();
            LastCursor = cursor;
            return Task.FromResult(_batch);
        }
    }

    private sealed class FakeAgent : Hermes.Agent.Core.IAgent
    {
        private readonly Action _onChat;

        private readonly string _response;

        public FakeAgent(Action onChat, string response = "wait")
        {
            _onChat = onChat;
            _response = response;
        }

        public int ChatCalls { get; private set; }

        public string? LastMessage { get; private set; }

        public List<string> Messages { get; } = new();

        public Task<string> ChatAsync(string message, Hermes.Agent.Core.Session session, CancellationToken ct)
        {
            _onChat();
            ChatCalls++;
            LastMessage = message;
            Messages.Add(message);
            return Task.FromResult(_response);
        }

        public async IAsyncEnumerable<Hermes.Agent.LLM.StreamEvent> StreamChatAsync(
            string message,
            Hermes.Agent.Core.Session session,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public void RegisterTool(Hermes.Agent.Core.ITool tool)
        {
        }
    }

    private sealed class FakeChatClient : IChatClient
    {
        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("summary");

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
}
