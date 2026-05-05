using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HermesDesktop.Tests.Stardew;

[TestClass]
public class StardewCommandServiceTests
{
    [TestMethod]
    public async Task SubmitAsync_Move_PostsTypedMoveEnvelopeToTaskMoveRoute()
    {
        var client = new FakeSmapiClient();
        client.MoveResponse = new StardewBridgeResponse<StardewMoveAcceptedData>(
            true,
            "trace-1",
            "req-1",
            "cmd-1",
            StardewCommandStatuses.Queued,
            new StardewMoveAcceptedData(true, new StardewMoveClaim("haley", new StardewTile(42, 17), null)),
            null,
            null);
        var service = new StardewCommandService(client, "save-1");
        var action = new GameAction(
            "haley",
            "stardew-valley",
            GameActionType.Move,
            "trace-1",
            "idem-1",
            new GameActionTarget("tile", "Town", new GameTile(42, 17)),
            "inspect board",
            Payload: new JsonObject { ["facingDirection"] = 2 });

        var result = await service.SubmitAsync(action, CancellationToken.None);

        Assert.IsTrue(result.Accepted);
        Assert.AreEqual("cmd-1", result.CommandId);
        Assert.AreEqual(StardewBridgeRoutes.TaskMove, client.LastRoute);
        Assert.IsInstanceOfType(client.LastEnvelope, typeof(StardewBridgeEnvelope<StardewMoveRequest>));
        var envelope = (StardewBridgeEnvelope<StardewMoveRequest>)client.LastEnvelope!;
        Assert.AreEqual("haley", envelope.NpcId);
        Assert.AreEqual("save-1", envelope.SaveId);
        Assert.AreEqual("idem-1", envelope.IdempotencyKey);
        Assert.AreEqual("Town", envelope.Payload.Target.LocationName);
        Assert.AreEqual(42, envelope.Payload.Target.Tile.X);
        Assert.AreEqual(2, envelope.Payload.FacingDirection);
    }

    [TestMethod]
    public async Task SubmitAsync_Move_WithDestinationIdOnlyPayload_PostsDestinationFirstEnvelope()
    {
        var client = new FakeSmapiClient();
        client.MoveResponse = new StardewBridgeResponse<StardewMoveAcceptedData>(
            true,
            "trace-destination",
            "req-destination",
            "cmd-destination",
            StardewCommandStatuses.Queued,
            new StardewMoveAcceptedData(true, new StardewMoveClaim("haley", new StardewTile(42, 17), null)),
            null,
            null);
        var service = new StardewCommandService(client, "save-1");
        var action = new GameAction(
            "haley",
            "stardew-valley",
            GameActionType.Move,
            "trace-destination",
            "idem-destination",
            new GameActionTarget("destination"),
            "inspect fountain",
            Payload: new JsonObject
            {
                ["destinationId"] = "town.fountain",
                ["facingDirection"] = 2
            });

        var result = await service.SubmitAsync(action, CancellationToken.None);

        Assert.IsTrue(result.Accepted);
        Assert.AreEqual(StardewBridgeRoutes.TaskMove, client.LastRoute);
        Assert.IsInstanceOfType(client.LastEnvelope, typeof(StardewBridgeEnvelope<StardewMoveRequest>));
        var envelope = (StardewBridgeEnvelope<StardewMoveRequest>)client.LastEnvelope!;
        using var payloadJson = JsonDocument.Parse(JsonSerializer.Serialize(envelope.Payload));
        Assert.AreEqual("town.fountain", payloadJson.RootElement.GetProperty("destinationId").GetString());
    }

    [TestMethod]
    public async Task SubmitAsync_Move_WithDestinationIdOnlyPayload_MapsDestinationIdAndInitialPhaseInCommandResult()
    {
        var client = new FakeSmapiClient();
        client.MoveResponse = new StardewBridgeResponse<StardewMoveAcceptedData>(
            true,
            "trace-destination",
            "req-destination",
            "cmd-destination",
            StardewCommandStatuses.Queued,
            new StardewMoveAcceptedData(true, new StardewMoveClaim("haley", new StardewTile(42, 17), null)),
            null,
            null);
        var service = new StardewCommandService(client, "save-1");
        var action = new GameAction(
            "haley",
            "stardew-valley",
            GameActionType.Move,
            "trace-destination",
            "idem-destination",
            new GameActionTarget("destination"),
            "inspect fountain",
            Payload: new JsonObject
            {
                ["destinationId"] = "town.fountain"
            });

        var result = await service.SubmitAsync(action, CancellationToken.None);

        var destinationIdProperty = typeof(GameCommandResult).GetProperty("DestinationId");
        var initialPhaseProperty = typeof(GameCommandResult).GetProperty("InitialPhase");
        Assert.IsNotNull(destinationIdProperty, "GameCommandResult 应暴露 DestinationId，避免 destinationId 只停留在 Bridge DTO。 ");
        Assert.IsNotNull(initialPhaseProperty, "GameCommandResult 应暴露 InitialPhase，避免公共回执面缺少 phase 语义。 ");
        Assert.AreEqual("town.fountain", destinationIdProperty.GetValue(result) as string);
        Assert.AreEqual(StardewCommandStatuses.Queued, initialPhaseProperty.GetValue(result) as string);
    }

    [TestMethod]
    public async Task GetStatusAsync_MapsBridgeStatusData()
    {
        var startedAt = new DateTime(2026, 4, 30, 9, 0, 0, DateTimeKind.Utc);
        var updatedAt = startedAt.AddSeconds(15);
        var retryAfter = updatedAt.AddSeconds(30);
        var client = new FakeSmapiClient();
        client.StatusResponse = new StardewBridgeResponse<StardewTaskStatusData>(
            true,
            "trace-status",
            "req-status",
            "cmd-1",
            StardewCommandStatuses.Running,
            JsonSerializer.Deserialize<StardewTaskStatusData>("""
            {
              "commandId": "cmd-1",
              "npcId": "haley",
              "action": "move",
              "status": "running",
              "startedAtUtc": "2026-04-30T09:00:00Z",
              "elapsedMs": 25,
              "progress": 0.5,
              "blockedReason": "tile_reserved",
              "errorCode": "resource_busy",
              "updatedAtUtc": "2026-04-30T09:00:15Z",
              "retryAfterUtc": "2026-04-30T09:00:45Z",
              "destinationId": "town.fountain",
              "phase": "planning_route",
              "currentLocationName": "Town",
              "resolvedStandTile": { "x": 41, "y": 17 },
              "routeRevision": 3
            }
            """)!,
            null,
            null);
        var service = new StardewCommandService(client, "save-1");

        var status = await service.GetStatusAsync("cmd-1", CancellationToken.None);

        Assert.AreEqual("cmd-1", status.CommandId);
        Assert.AreEqual("haley", status.NpcId);
        Assert.AreEqual(0.5, status.Progress);
        Assert.AreEqual(startedAt, status.StartedAtUtc);
        Assert.AreEqual(updatedAt, status.UpdatedAtUtc);
        Assert.AreEqual(25L, status.ElapsedMs);
        Assert.AreEqual(retryAfter, status.RetryAfterUtc);
        Assert.AreEqual("tile_reserved", status.BlockedReason);
        Assert.AreEqual("resource_busy", status.ErrorCode);
        Assert.AreEqual("town.fountain", status.DestinationId);
        Assert.AreEqual("planning_route", status.Phase);
        Assert.AreEqual("Town", status.CurrentLocationName);
        Assert.IsNotNull(status.ResolvedStandTile);
        Assert.AreEqual(41, status.ResolvedStandTile.X);
        Assert.AreEqual(17, status.ResolvedStandTile.Y);
        Assert.AreEqual(3, status.RouteRevision);
        Assert.AreEqual(StardewBridgeRoutes.TaskStatus, client.LastRoute);
    }

    [TestMethod]
    public async Task SubmitAsync_Move_UsesExplicitBodyBindingTargetForBridgeNpcId()
    {
        var client = new FakeSmapiClient();
        client.MoveResponse = new StardewBridgeResponse<StardewMoveAcceptedData>(
            true,
            "trace-1",
            "req-1",
            "cmd-1",
            StardewCommandStatuses.Queued,
            new StardewMoveAcceptedData(true, new StardewMoveClaim("Haley", new StardewTile(42, 17), null)),
            null,
            null);
        var service = new StardewCommandService(client, "save-1");
        var action = new GameAction(
            "haley",
            "stardew-valley",
            GameActionType.Move,
            "trace-1",
            "idem-1",
            new GameActionTarget("tile", "Town", new GameTile(42, 17)),
            "inspect board",
            BodyBinding: new NpcBodyBinding("haley", "Haley", "Haley", "Haley", "stardew"));

        await service.SubmitAsync(action, CancellationToken.None);

        var envelope = (StardewBridgeEnvelope<StardewMoveRequest>)client.LastEnvelope!;
        Assert.AreEqual("Haley", envelope.NpcId);
    }

    [TestMethod]
    public async Task TryGetByIdempotencyKeyAsync_MapsBridgeStatusData()
    {
        var startedAt = new DateTime(2026, 5, 2, 10, 0, 0, DateTimeKind.Utc);
        var updatedAt = startedAt.AddSeconds(8);
        var client = new FakeSmapiClient();
        client.LookupResponse = new StardewBridgeResponse<StardewTaskStatusData>(
            true,
            "trace-status",
            "req-lookup",
            "cmd-lookup-1",
            StardewCommandStatuses.Running,
            new StardewTaskStatusData(
                "cmd-lookup-1",
                "haley",
                "move",
                StardewCommandStatuses.Running,
                startedAt,
                8,
                0.4,
                BlockedReason: null,
                ErrorCode: null,
                InterruptionReason: null,
                UpdatedAtUtc: updatedAt,
                RetryAfterUtc: null),
            null,
            null);
        var service = new StardewCommandService(client, "save-1");

        var status = await service.TryGetByIdempotencyKeyAsync("idem-lookup-1", CancellationToken.None);

        Assert.IsNotNull(status);
        Assert.AreEqual("cmd-lookup-1", status.CommandId);
        Assert.AreEqual("haley", status.NpcId);
        Assert.AreEqual(StardewCommandStatuses.Running, status.Status);
        Assert.AreEqual(StardewBridgeRoutes.TaskLookup, client.LastRoute);
        Assert.IsInstanceOfType(client.LastEnvelope, typeof(StardewBridgeEnvelope<StardewTaskLookupRequest>));
        var envelope = (StardewBridgeEnvelope<StardewTaskLookupRequest>)client.LastEnvelope!;
        Assert.AreEqual("idem-lookup-1", envelope.Payload.IdempotencyKey);
        Assert.AreEqual("save-1", envelope.SaveId);
    }

    [TestMethod]
    public async Task TryGetByIdempotencyKeyAsync_CommandNotFoundReturnsNull()
    {
        var client = new FakeSmapiClient();
        client.LookupResponse = new StardewBridgeResponse<StardewTaskStatusData>(
            false,
            "trace-status",
            "req-lookup",
            null,
            StardewCommandStatuses.Failed,
            null,
            new StardewBridgeError(StardewBridgeErrorCodes.CommandNotFound, "No command for idempotency key.", false),
            null);
        var service = new StardewCommandService(client, "save-1");

        var status = await service.TryGetByIdempotencyKeyAsync("idem-missing", CancellationToken.None);

        Assert.IsNull(status);
        Assert.AreEqual(StardewBridgeRoutes.TaskLookup, client.LastRoute);
    }

    [TestMethod]
    public async Task SubmitAsync_Speak_PostsTypedSpeakEnvelopeToActionSpeakRoute()
    {
        var client = new FakeSmapiClient();
        client.SpeakResponse = new StardewBridgeResponse<StardewSpeakData>(
            true,
            "trace-speak",
            "req-speak",
            null,
            StardewCommandStatuses.Completed,
            new StardewSpeakData("haley", "Hi there.", "player", true),
            null,
            null);
        var service = new StardewCommandService(client, "save-1");
        var payload = new JsonObject
        {
            ["text"] = "Hi there.",
            ["channel"] = "player",
            ["conversationId"] = "pc_evt_000000000001"
        };
        var action = new GameAction(
            "haley",
            "stardew-valley",
            GameActionType.Speak,
            "trace-speak",
            "idem-speak",
            new GameActionTarget("player"),
            Payload: payload);

        var result = await service.SubmitAsync(action, CancellationToken.None);

        Assert.IsTrue(result.Accepted);
        Assert.AreEqual(StardewCommandStatuses.Completed, result.Status);
        Assert.AreEqual(StardewBridgeRoutes.ActionSpeak, client.LastRoute);
        Assert.IsInstanceOfType(client.LastEnvelope, typeof(StardewBridgeEnvelope<StardewSpeakRequest>));
        var envelope = (StardewBridgeEnvelope<StardewSpeakRequest>)client.LastEnvelope!;
        Assert.AreEqual("haley", envelope.NpcId);
        Assert.AreEqual("save-1", envelope.SaveId);
        Assert.AreEqual("idem-speak", envelope.IdempotencyKey);
        Assert.AreEqual("Hi there.", envelope.Payload.Text);
        Assert.AreEqual("player", envelope.Payload.Channel);
        Assert.AreEqual("pc_evt_000000000001", envelope.Payload.ConversationId);
    }

    [TestMethod]
    public async Task SubmitAsync_OpenPrivateChat_PostsTypedEnvelopeToActionOpenPrivateChatRoute()
    {
        var client = new FakeSmapiClient();
        client.OpenPrivateChatResponse = new StardewBridgeResponse<StardewOpenPrivateChatData>(
            true,
            "trace-private-chat",
            "req-private-chat",
            null,
            StardewCommandStatuses.Completed,
            new StardewOpenPrivateChatData("haley", true),
            null,
            null);
        var service = new StardewCommandService(client, "save-1");
        var payload = new JsonObject
        {
            ["prompt"] = "Want to keep chatting?",
            ["conversationId"] = "pc_evt_000000000001"
        };
        var action = new GameAction(
            "haley",
            "stardew-valley",
            GameActionType.OpenPrivateChat,
            "trace-private-chat",
            "idem-private-chat",
            new GameActionTarget("player"),
            Payload: payload);

        var result = await service.SubmitAsync(action, CancellationToken.None);

        Assert.IsTrue(result.Accepted);
        Assert.AreEqual(StardewCommandStatuses.Completed, result.Status);
        Assert.AreEqual(StardewBridgeRoutes.ActionOpenPrivateChat, client.LastRoute);
        Assert.IsInstanceOfType(client.LastEnvelope, typeof(StardewBridgeEnvelope<StardewOpenPrivateChatRequest>));
        var envelope = (StardewBridgeEnvelope<StardewOpenPrivateChatRequest>)client.LastEnvelope!;
        Assert.AreEqual("haley", envelope.NpcId);
        Assert.AreEqual("save-1", envelope.SaveId);
        Assert.AreEqual("idem-private-chat", envelope.IdempotencyKey);
        Assert.AreEqual("Want to keep chatting?", envelope.Payload.Prompt);
        Assert.AreEqual("pc_evt_000000000001", envelope.Payload.ConversationId);
    }

    [TestMethod]
    public async Task SubmitAsync_OpenPrivateChat_AcceptsPhoneThreadOpenStates()
    {
        var client = new FakeSmapiClient();
        client.OpenPrivateChatResponse = new StardewBridgeResponse<StardewOpenPrivateChatData>(
            true,
            "trace-private-chat",
            "req-private-chat",
            null,
            StardewCommandStatuses.Completed,
            new StardewOpenPrivateChatData("haley", false, "Haley:pc_evt_1", "thread_opened"),
            null,
            null);
        var service = new StardewCommandService(client, "save-1");
        var action = new GameAction(
            "haley",
            "stardew-valley",
            GameActionType.OpenPrivateChat,
            "trace-private-chat",
            "idem-private-chat",
            new GameActionTarget("player"),
            Payload: new JsonObject { ["conversationId"] = "pc_evt_1" });

        var result = await service.SubmitAsync(action, CancellationToken.None);

        Assert.IsTrue(result.Accepted);
        Assert.AreEqual(StardewCommandStatuses.Completed, result.Status);
    }

    [TestMethod]
    public async Task SubmitAsync_OpenPrivateChat_NotOpenedFailsCommandResult()
    {
        var client = new FakeSmapiClient();
        client.OpenPrivateChatResponse = new StardewBridgeResponse<StardewOpenPrivateChatData>(
            true,
            "trace-private-chat",
            "req-private-chat",
            null,
            StardewCommandStatuses.Completed,
            new StardewOpenPrivateChatData("haley", false),
            null,
            null);
        var service = new StardewCommandService(client, "save-1");
        var action = new GameAction(
            "haley",
            "stardew-valley",
            GameActionType.OpenPrivateChat,
            "trace-private-chat",
            "idem-private-chat",
            new GameActionTarget("player"),
            Payload: new JsonObject { ["conversationId"] = "pc_evt_1" });

        var result = await service.SubmitAsync(action, CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual("open_private_chat_not_opened", result.FailureReason);
    }

    [TestMethod]
    public async Task SubmitAsync_OpenPrivateChat_RetryableBridgeErrorIsPreserved()
    {
        var client = new FakeSmapiClient();
        client.OpenPrivateChatResponse = new StardewBridgeResponse<StardewOpenPrivateChatData>(
            false,
            "trace-private-chat",
            "req-private-chat",
            null,
            StardewCommandStatuses.Failed,
            null,
            new StardewBridgeError(StardewBridgeErrorCodes.MenuBlocked, "A Stardew menu is already open.", true),
            null);
        var service = new StardewCommandService(client, "save-1");
        var action = new GameAction(
            "haley",
            "stardew-valley",
            GameActionType.OpenPrivateChat,
            "trace-private-chat",
            "idem-private-chat",
            new GameActionTarget("player"),
            Payload: new JsonObject { ["conversationId"] = "pc_evt_1" });

        var result = await service.SubmitAsync(action, CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual(StardewBridgeErrorCodes.MenuBlocked, result.FailureReason);
        Assert.IsTrue(result.Retryable);
    }

    [TestMethod]
    public async Task SubmitAsync_InvalidMoveTargetFailsBeforeBridgeCall()
    {
        var client = new FakeSmapiClient();
        var service = new StardewCommandService(client, "save-1");
        var action = new GameAction(
            "haley",
            "stardew-valley",
            GameActionType.Move,
            "trace-1",
            "idem-1",
            new GameActionTarget("tile"));

        var result = await service.SubmitAsync(action, CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual(StardewBridgeErrorCodes.InvalidTarget, result.FailureReason);
        Assert.IsNull(client.LastRoute);
    }

    [TestMethod]
    public async Task SubmitAsync_InvalidSpeakTextFailsBeforeBridgeCall()
    {
        var client = new FakeSmapiClient();
        var service = new StardewCommandService(client, "save-1");
        var action = new GameAction(
            "haley",
            "stardew-valley",
            GameActionType.Speak,
            "trace-speak",
            "idem-speak",
            new GameActionTarget("player"),
            Payload: new JsonObject());

        var result = await service.SubmitAsync(action, CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual(StardewBridgeErrorCodes.InvalidTarget, result.FailureReason);
        Assert.IsNull(client.LastRoute);
    }

    private sealed class FakeSmapiClient : ISmapiModApiClient
    {
        public string? LastRoute { get; private set; }
        public object? LastEnvelope { get; private set; }
        public StardewBridgeResponse<StardewMoveAcceptedData>? MoveResponse { get; set; }
        public StardewBridgeResponse<StardewTaskStatusData>? StatusResponse { get; set; }
        public StardewBridgeResponse<StardewTaskStatusData>? LookupResponse { get; set; }
        public StardewBridgeResponse<StardewSpeakData>? SpeakResponse { get; set; }
        public StardewBridgeResponse<StardewOpenPrivateChatData>? OpenPrivateChatResponse { get; set; }

        public Task<StardewBridgeResponse<TData>> SendAsync<TPayload, TData>(
            string route,
            StardewBridgeEnvelope<TPayload> envelope,
            CancellationToken ct)
        {
            LastRoute = route;
            LastEnvelope = envelope;

            object response = route switch
            {
                StardewBridgeRoutes.TaskMove => MoveResponse ?? throw new InvalidOperationException("No move response configured."),
                StardewBridgeRoutes.TaskStatus => StatusResponse ?? throw new InvalidOperationException("No status response configured."),
                StardewBridgeRoutes.TaskLookup => LookupResponse ?? throw new InvalidOperationException("No lookup response configured."),
                StardewBridgeRoutes.TaskCancel => StatusResponse ?? throw new InvalidOperationException("No cancel response configured."),
                StardewBridgeRoutes.ActionSpeak => SpeakResponse ?? throw new InvalidOperationException("No speak response configured."),
                StardewBridgeRoutes.ActionOpenPrivateChat => OpenPrivateChatResponse ?? throw new InvalidOperationException("No open private chat response configured."),
                _ => throw new InvalidOperationException($"Unexpected route {route}.")
            };

            return Task.FromResult((StardewBridgeResponse<TData>)response);
        }
    }
}
