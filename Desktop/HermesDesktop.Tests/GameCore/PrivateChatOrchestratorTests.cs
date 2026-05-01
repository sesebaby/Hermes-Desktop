using Hermes.Agent.Game;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json.Nodes;

namespace HermesDesktop.Tests.GameCore;

[TestClass]
public class PrivateChatOrchestratorTests
{
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

        public void Add(params GameEventRecord[] records)
        {
            _records.AddRange(records);
        }

        public Task<IReadOnlyList<GameEventRecord>> PollAsync(GameEventCursor cursor, CancellationToken ct)
        {
            var start = 0;
            if (!string.IsNullOrWhiteSpace(cursor.Since))
            {
                var index = _records.FindIndex(record => string.Equals(record.EventId, cursor.Since, StringComparison.OrdinalIgnoreCase));
                start = index < 0 ? _records.Count : index + 1;
            }

            return Task.FromResult<IReadOnlyList<GameEventRecord>>(_records.Skip(start).ToArray());
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
}
