using Hermes.Agent.Game;
using Hermes.Agent.Runtime;
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

        public Task<GameObservation> ObserveAsync(string npcId, CancellationToken ct)
        {
            LastNpcId = npcId;
            return Task.FromResult(_observation);
        }

        public Task<WorldSnapshot> GetWorldSnapshotAsync(string npcId, CancellationToken ct)
            => Task.FromResult(new WorldSnapshot("stardew-valley", "save-1", _observation.TimestampUtc, [], []));
    }

    private sealed class FakeEventSource : IGameEventSource
    {
        private readonly IReadOnlyList<GameEventRecord> _records;

        public FakeEventSource(IReadOnlyList<GameEventRecord> records)
        {
            _records = records;
        }

        public GameEventCursor? LastCursor { get; private set; }

        public Task<IReadOnlyList<GameEventRecord>> PollAsync(GameEventCursor cursor, CancellationToken ct)
        {
            LastCursor = cursor;
            return Task.FromResult(_records);
        }
    }
}
