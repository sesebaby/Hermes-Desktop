using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
            "inspect board");

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
    }

    [TestMethod]
    public async Task GetStatusAsync_MapsBridgeStatusData()
    {
        var client = new FakeSmapiClient();
        client.StatusResponse = new StardewBridgeResponse<StardewTaskStatusData>(
            true,
            "trace-status",
            "req-status",
            "cmd-1",
            StardewCommandStatuses.Running,
            new StardewTaskStatusData("cmd-1", "haley", "move", StardewCommandStatuses.Running, DateTime.UtcNow, 25, 0.5, null, null),
            null,
            null);
        var service = new StardewCommandService(client, "save-1");

        var status = await service.GetStatusAsync("cmd-1", CancellationToken.None);

        Assert.AreEqual("cmd-1", status.CommandId);
        Assert.AreEqual("haley", status.NpcId);
        Assert.AreEqual(0.5, status.Progress);
        Assert.AreEqual(StardewBridgeRoutes.TaskStatus, client.LastRoute);
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

    private sealed class FakeSmapiClient : ISmapiModApiClient
    {
        public string? LastRoute { get; private set; }
        public object? LastEnvelope { get; private set; }
        public StardewBridgeResponse<StardewMoveAcceptedData>? MoveResponse { get; set; }
        public StardewBridgeResponse<StardewTaskStatusData>? StatusResponse { get; set; }

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
                StardewBridgeRoutes.TaskCancel => StatusResponse ?? throw new InvalidOperationException("No cancel response configured."),
                _ => throw new InvalidOperationException($"Unexpected route {route}.")
            };

            return Task.FromResult((StardewBridgeResponse<TData>)response);
        }
    }
}
