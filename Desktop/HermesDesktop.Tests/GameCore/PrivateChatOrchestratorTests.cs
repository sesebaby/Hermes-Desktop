using Hermes.Agent.Game;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json.Nodes;

namespace HermesDesktop.Tests.GameCore;

[TestClass]
public class PrivateChatOrchestratorTests
{
    [TestMethod]
    public async Task ProcessNextAsync_OpenAccepted_AcquiresSessionLeaseAndCancelReleasesIt()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-open",
                "villager_chat_finished",
                "Rowan",
                DateTime.UtcNow,
                "Rowan finished greeting the player.",
                "thread-1"));
        var commands = new FakeCommandService();
        var leases = new FakePrivateChatSessionLeaseCoordinator();
        var orchestrator = new PrivateChatOrchestrator(
            events,
            commands,
            new FakePrivateChatAgentRunner(),
            new PrivateChatOrchestratorOptions(
                new PrivateChatPolicy(
                    NpcId: "rowan",
                    SaveId: "garden-save",
                    GameId: "garden-sim",
                    OpenPrompt: "Whisper to Rowan.",
                    OpenTriggerEventTypes: ["villager_chat_finished"],
                    SubmittedEventType: "player_whisper_sent",
                    CancelledEventType: "player_whisper_cancelled",
                    ReplyClosedEventType: "npc_whisper_reply_closed",
                    GetConversationId: record => GetPayloadString(record.Payload, "thread") ?? record.CorrelationId,
                    GetPlayerText: record => GetPayloadString(record.Payload, "message")),
                ReopenPolicy: PrivateChatSessionReopenPolicy.Never,
                SessionLeaseCoordinator: leases));

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(1, leases.AcquireCalls.Count);
        Assert.AreEqual("Rowan", leases.AcquireCalls[0].NpcId);
        Assert.AreEqual("garden-save", leases.AcquireCalls[0].SaveId);
        Assert.AreEqual("pc_thread-1", leases.AcquireCalls[0].ConversationId);
        Assert.AreEqual(1, leases.ActiveLeaseCount);

        events.Add(new GameEventRecord(
            "evt-cancel",
            "player_whisper_cancelled",
            "Rowan",
            DateTime.UtcNow.AddSeconds(1),
            "Player closed whisper.",
            "pc_thread-1",
            new JsonObject { ["thread"] = "pc_thread-1" }));

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(1, leases.ReleaseCalls.Count);
        Assert.AreEqual("pc_thread-1", leases.ReleaseCalls[0].ConversationId);
        Assert.AreEqual(0, leases.ActiveLeaseCount);
        Assert.AreEqual(PrivateChatState.Idle, orchestrator.State);
    }

    [TestMethod]
    public async Task ProcessNextAsync_ReopenAfterReply_DoesNotAcquireSessionLeaseTwice()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-open",
                "villager_chat_finished",
                "Rowan",
                DateTime.UtcNow,
                "Rowan finished greeting the player.",
                "thread-1"),
            new GameEventRecord(
                "evt-submit",
                "player_whisper_sent",
                "Rowan",
                DateTime.UtcNow.AddSeconds(1),
                "Player sent a whisper.",
                "pc_thread-1",
                new JsonObject
                {
                    ["thread"] = "pc_thread-1",
                    ["message"] = "can we talk?"
                }));
        var commands = new FakeCommandService();
        var leases = new FakePrivateChatSessionLeaseCoordinator();
        var orchestrator = new PrivateChatOrchestrator(
            events,
            commands,
            new FakePrivateChatAgentRunner { ReplyText = "The greenhouse is quiet." },
            new PrivateChatOrchestratorOptions(
                new PrivateChatPolicy(
                    NpcId: "rowan",
                    SaveId: "garden-save",
                    GameId: "garden-sim",
                    OpenPrompt: "Whisper to Rowan.",
                    OpenTriggerEventTypes: ["villager_chat_finished"],
                    SubmittedEventType: "player_whisper_sent",
                    CancelledEventType: "player_whisper_cancelled",
                    ReplyClosedEventType: "npc_whisper_reply_closed",
                    GetConversationId: record => GetPayloadString(record.Payload, "thread") ?? record.CorrelationId,
                    GetPlayerText: record => GetPayloadString(record.Payload, "message")),
                ReopenPolicy: PrivateChatSessionReopenPolicy.OnceAfterReply,
                MaxTurnsPerSession: 2,
                SessionLeaseCoordinator: leases));

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(1, leases.AcquireCalls.Count);
        Assert.AreEqual(PrivateChatState.WaitingReplyDismissal, orchestrator.State);

        events.Add(new GameEventRecord(
            "evt-closed",
            "npc_whisper_reply_closed",
            "Rowan",
            DateTime.UtcNow.AddSeconds(2),
            "Rowan reply closed.",
            "pc_thread-1",
            new JsonObject { ["thread"] = "pc_thread-1" }));

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(1, leases.AcquireCalls.Count);
        Assert.AreEqual(0, leases.ReleaseCalls.Count);
        Assert.AreEqual(1, leases.ActiveLeaseCount);
        Assert.AreEqual(PrivateChatState.AwaitingPlayerInput, orchestrator.State);
        Assert.AreEqual("pc_thread-1_turn2", orchestrator.ConversationId);
    }

    [TestMethod]
    public async Task ProcessNextAsync_WhenLeaseAcquireFails_OpenIsRetriedInsteadOfBeingPermanentlySuppressed()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-open",
                "villager_chat_finished",
                "Rowan",
                DateTime.UtcNow,
                "Rowan finished greeting the player.",
                "thread-1"));
        var commands = new FakeCommandService();
        var leases = new FakePrivateChatSessionLeaseCoordinator { FailNextAcquire = true };
        var orchestrator = new PrivateChatOrchestrator(
            events,
            commands,
            new FakePrivateChatAgentRunner(),
            new PrivateChatOrchestratorOptions(
                new PrivateChatPolicy(
                    NpcId: "rowan",
                    SaveId: "garden-save",
                    GameId: "garden-sim",
                    OpenPrompt: "Whisper to Rowan.",
                    OpenTriggerEventTypes: ["villager_chat_finished"],
                    SubmittedEventType: "player_whisper_sent",
                    CancelledEventType: "player_whisper_cancelled",
                    ReplyClosedEventType: "npc_whisper_reply_closed",
                    GetConversationId: record => GetPayloadString(record.Payload, "thread") ?? record.CorrelationId,
                    GetPlayerText: record => GetPayloadString(record.Payload, "message")),
                ReopenPolicy: PrivateChatSessionReopenPolicy.Never,
                SessionLeaseCoordinator: leases));

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(PrivateChatState.PendingOpen, orchestrator.State);
        Assert.AreEqual(0, commands.Submitted.Count);
        Assert.AreEqual(1, leases.AcquireCalls.Count);

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(1, commands.Submitted.Count);
        Assert.AreEqual(2, leases.AcquireCalls.Count);
        Assert.AreEqual(PrivateChatState.AwaitingPlayerInput, orchestrator.State);
        Assert.AreEqual("pc_thread-1", orchestrator.ConversationId);
    }

    [TestMethod]
    public async Task ProcessNextAsync_CustomGamePolicyRetriesOpenAndRepliesWithoutStardewReferences()
    {
        var events = new FakeEventSource(
            new GameEventRecord(
                "evt-open",
                "villager_chat_finished",
                "Rowan",
                DateTime.UtcNow,
                "Rowan finished greeting the player.",
                "thread-1"));
        var commands = new FakeCommandService();
        commands.Results.Enqueue(new GameCommandResult(false, "", "failed", "ui_busy_cross_game", "trace-open-1"));
        commands.Results.Enqueue(new GameCommandResult(true, "", "completed", null, "trace-open-2"));
        var agent = new FakePrivateChatAgentRunner { ReplyText = "The greenhouse is quiet." };
        var orchestrator = new PrivateChatOrchestrator(
            events,
            commands,
            agent,
            new PrivateChatOrchestratorOptions(
                new PrivateChatPolicy(
                    NpcId: "rowan",
                    SaveId: "garden-save",
                    GameId: "garden-sim",
                    OpenPrompt: "Whisper to Rowan.",
                    OpenTriggerEventTypes: ["villager_chat_finished"],
                    SubmittedEventType: "player_whisper_sent",
                    CancelledEventType: "player_whisper_cancelled",
                    ReplyClosedEventType: "npc_whisper_reply_closed",
                    GetConversationId: record => GetPayloadString(record.Payload, "thread") ?? record.CorrelationId,
                    GetPlayerText: record => GetPayloadString(record.Payload, "message"),
                    IsRetryableOpenFailure: result => string.Equals(result.FailureReason, "ui_busy_cross_game", StringComparison.OrdinalIgnoreCase)),
                ReopenPolicy: PrivateChatSessionReopenPolicy.Never,
                MaxOpenAttempts: 3));

        await orchestrator.ProcessNextAsync(CancellationToken.None);
        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(2, commands.Submitted.Count);
        Assert.IsTrue(commands.Submitted.All(action => action.Type == GameActionType.OpenPrivateChat));
        Assert.AreEqual(PrivateChatState.AwaitingPlayerInput, orchestrator.State);
        Assert.AreEqual("pc_thread-1", orchestrator.ConversationId);
        Assert.AreEqual("garden-sim", commands.Submitted[1].GameId);
        Assert.AreEqual("Whisper to Rowan.", commands.Submitted[1].Payload?["prompt"]?.GetValue<string>());

        events.Add(new GameEventRecord(
            "evt-submit",
            "player_whisper_sent",
            "Rowan",
            DateTime.UtcNow,
            "Player sent a whisper.",
            "pc_thread-1",
            new JsonObject
            {
                ["thread"] = "pc_thread-1",
                ["message"] = "can we talk?"
            }));

        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(1, agent.Requests.Count);
        Assert.AreEqual("Rowan", agent.Requests[0].NpcId);
        Assert.AreEqual("garden-save", agent.Requests[0].SaveId);
        Assert.AreEqual("pc_thread-1", agent.Requests[0].ConversationId);
        Assert.AreEqual("can we talk?", agent.Requests[0].PlayerText);
        Assert.AreEqual(3, commands.Submitted.Count);
        var reply = commands.Submitted[2];
        Assert.AreEqual(GameActionType.Speak, reply.Type);
        Assert.AreEqual("garden-sim", reply.GameId);
        Assert.AreEqual("The greenhouse is quiet.", reply.Payload?["text"]?.GetValue<string>());
        Assert.AreEqual("private_chat", reply.Payload?["channel"]?.GetValue<string>());
        Assert.AreEqual("pc_thread-1", reply.Payload?["conversationId"]?.GetValue<string>());

        var coreSource = ReadRepositoryFile("src", "game", "core", "PrivateChatOrchestrator.cs");
        Assert.IsFalse(coreSource.Contains("Hermes.Agent.Games.Stardew", StringComparison.Ordinal));
        Assert.IsFalse(coreSource.Contains("StardewBridgeErrorCodes", StringComparison.Ordinal));
        Assert.IsFalse(coreSource.Contains("stardew-valley", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ProcessRecordsAsync_ExternallyFedBatchProcessesWithoutPollingSource()
    {
        var events = new FakeEventSource();
        var commands = new FakeCommandService();
        var orchestrator = new PrivateChatOrchestrator(
            events,
            commands,
            new FakePrivateChatAgentRunner(),
            new PrivateChatOrchestratorOptions(
                new PrivateChatPolicy(
                    NpcId: "rowan",
                    SaveId: "garden-save",
                    GameId: "garden-sim",
                    OpenPrompt: "Whisper to Rowan.",
                    OpenTriggerEventTypes: ["villager_chat_finished"],
                    SubmittedEventType: "player_whisper_sent",
                    CancelledEventType: "player_whisper_cancelled",
                    ReplyClosedEventType: "npc_whisper_reply_closed",
                    GetConversationId: record => GetPayloadString(record.Payload, "thread") ?? record.CorrelationId,
                    GetPlayerText: record => GetPayloadString(record.Payload, "message")),
                ReopenPolicy: PrivateChatSessionReopenPolicy.Never));

        await orchestrator.ProcessRecordsAsync(
        [
            new GameEventRecord(
                "evt-open",
                "villager_chat_finished",
                "Rowan",
                DateTime.UtcNow,
                "Rowan finished greeting the player.",
                "thread-1"),
        ],
        CancellationToken.None);

        Assert.AreEqual(0, events.PollCalls);
        Assert.AreEqual(1, commands.Submitted.Count);
        Assert.AreEqual(GameActionType.OpenPrivateChat, commands.Submitted[0].Type);
    }

    [TestMethod]
    public async Task DrainRecords_PreloadedBatchIsNotReprocessedByExternalFeed()
    {
        var events = new FakeEventSource();
        var commands = new FakeCommandService();
        var orchestrator = new PrivateChatOrchestrator(
            events,
            commands,
            new FakePrivateChatAgentRunner(),
            new PrivateChatOrchestratorOptions(
                new PrivateChatPolicy(
                    NpcId: "rowan",
                    SaveId: "garden-save",
                    GameId: "garden-sim",
                    OpenPrompt: "Whisper to Rowan.",
                    OpenTriggerEventTypes: ["villager_chat_finished"],
                    SubmittedEventType: "player_whisper_sent",
                    CancelledEventType: "player_whisper_cancelled",
                    ReplyClosedEventType: "npc_whisper_reply_closed",
                    GetConversationId: record => GetPayloadString(record.Payload, "thread") ?? record.CorrelationId,
                    GetPlayerText: record => GetPayloadString(record.Payload, "message")),
                ReopenPolicy: PrivateChatSessionReopenPolicy.Never));
        var historicalBatch =
            new[]
            {
                new GameEventRecord(
                    "evt-open",
                    "villager_chat_finished",
                    "Rowan",
                    DateTime.UtcNow,
                    "Rowan finished greeting the player.",
                    "thread-1"),
            };

        orchestrator.DrainRecords(historicalBatch);
        await orchestrator.ProcessNextAsync(CancellationToken.None);

        Assert.AreEqual(1, events.PollCalls);
        Assert.AreEqual(0, commands.Submitted.Count);
        Assert.AreEqual(PrivateChatState.Idle, orchestrator.State);
    }

    private static string? GetPayloadString(JsonObject? payload, string propertyName)
        => payload is not null && payload.TryGetPropertyValue(propertyName, out var node)
            ? node?.GetValue<string>()
            : null;

    private static string ReadRepositoryFile(params string[] relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(relativePath).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            directory = directory.Parent;
        }

        Assert.Fail($"Could not find repository file: {Path.Combine(relativePath)}");
        return string.Empty;
    }

    private sealed class FakeEventSource : IGameEventSource
    {
        private readonly List<GameEventRecord> _records;

        public FakeEventSource(params GameEventRecord[] records)
        {
            _records = records.ToList();
        }

        public int PollCalls { get; private set; }

        public void Add(params GameEventRecord[] records)
        {
            _records.AddRange(records);
        }

        public Task<IReadOnlyList<GameEventRecord>> PollAsync(GameEventCursor cursor, CancellationToken ct)
        {
            PollCalls++;
            var start = 0;
            if (!string.IsNullOrWhiteSpace(cursor.Since))
            {
                var index = _records.FindIndex(record => string.Equals(record.EventId, cursor.Since, StringComparison.OrdinalIgnoreCase));
                start = index < 0 ? _records.Count : index + 1;
            }

            var records = _records.Skip(start).ToArray();
            return Task.FromResult<IReadOnlyList<GameEventRecord>>(records);
        }
    }

    private sealed class FakeCommandService : IGameCommandService
    {
        public List<GameAction> Submitted { get; } = new();

        public Queue<GameCommandResult> Results { get; } = new();

        public Task<GameCommandResult> SubmitAsync(GameAction action, CancellationToken ct)
        {
            Submitted.Add(action);
            if (Results.TryDequeue(out var result))
                return Task.FromResult(result);

            return Task.FromResult(new GameCommandResult(true, "", "completed", null, action.TraceId));
        }

        public Task<GameCommandStatus> GetStatusAsync(string commandId, CancellationToken ct)
            => Task.FromResult(new GameCommandStatus(commandId, "Rowan", "private_chat", "completed", 1, null, null));

        public Task<GameCommandStatus> CancelAsync(string commandId, string reason, CancellationToken ct)
            => Task.FromResult(new GameCommandStatus(commandId, "Rowan", "private_chat", "cancelled", 1, reason, null));
    }

    private sealed class FakePrivateChatAgentRunner : IPrivateChatAgentRunner
    {
        public List<PrivateChatAgentRequest> Requests { get; } = new();
        public string ReplyText { get; init; } = "Hello.";

        public Task<PrivateChatAgentReply> ReplyAsync(PrivateChatAgentRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(new PrivateChatAgentReply(ReplyText));
        }
    }

    private sealed class FakePrivateChatSessionLeaseCoordinator : IPrivateChatSessionLeaseCoordinator
    {
        public List<PrivateChatSessionLeaseRequest> AcquireCalls { get; } = new();

        public List<ReleasedLease> ReleaseCalls { get; } = new();

        public int ActiveLeaseCount { get; private set; }

        public bool FailNextAcquire { get; set; }

        public Task<IPrivateChatSessionLease> AcquireAsync(PrivateChatSessionLeaseRequest request, CancellationToken ct)
        {
            AcquireCalls.Add(request);
            if (FailNextAcquire)
            {
                FailNextAcquire = false;
                throw new InvalidOperationException("lease unavailable");
            }

            ActiveLeaseCount++;
            return Task.FromResult<IPrivateChatSessionLease>(new FakePrivateChatSessionLease(this, request, AcquireCalls.Count));
        }

        private sealed class FakePrivateChatSessionLease : IPrivateChatSessionLease
        {
            private readonly FakePrivateChatSessionLeaseCoordinator _owner;
            private bool _disposed;

            public FakePrivateChatSessionLease(
                FakePrivateChatSessionLeaseCoordinator owner,
                PrivateChatSessionLeaseRequest request,
                int generation)
            {
                _owner = owner;
                NpcId = request.NpcId;
                ConversationId = request.ConversationId;
                Owner = request.Owner;
                Generation = generation;
            }

            public string NpcId { get; }

            public string ConversationId { get; }

            public string Owner { get; }

            public int Generation { get; }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                _owner.ActiveLeaseCount--;
                _owner.ReleaseCalls.Add(new ReleasedLease(ConversationId, Generation, Owner));
            }
        }

        public sealed record ReleasedLease(string ConversationId, int Generation, string Owner);
    }
}
