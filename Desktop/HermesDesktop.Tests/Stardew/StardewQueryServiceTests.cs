using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Stardew;

[TestClass]
public class StardewQueryServiceTests
{
    [TestMethod]
    public async Task ObserveAsync_PostsStatusQueryAndMapsFacts()
    {
        var at = new DateTime(2026, 4, 30, 8, 30, 0, DateTimeKind.Utc);
        var client = new FakeSmapiClient();
        client.StatusResponse = new StardewBridgeResponse<StardewNpcStatusData>(
            true,
            "trace-status",
            "req-status",
            null,
            null,
            new StardewNpcStatusData(
                "haley",
                "Haley",
                "Haley",
                "Town",
                new StardewTile(42, 17),
                false,
                false,
                true,
                null,
                "cmd-1",
                "trace-1"),
            null,
            null);
        var service = new StardewQueryService(client, "save-1", nowUtc: () => at);

        var observation = await service.ObserveAsync("haley", CancellationToken.None);

        Assert.AreEqual(StardewBridgeRoutes.QueryStatus, client.LastRoute);
        Assert.IsInstanceOfType(client.LastEnvelope, typeof(StardewBridgeEnvelope<StardewStatusQuery>));
        var envelope = (StardewBridgeEnvelope<StardewStatusQuery>)client.LastEnvelope!;
        Assert.AreEqual("haley", envelope.NpcId);
        Assert.AreEqual("save-1", envelope.SaveId);
        Assert.AreEqual("haley", envelope.Payload.NpcId);

        Assert.AreEqual("haley", observation.NpcId);
        Assert.AreEqual("stardew-valley", observation.GameId);
        Assert.AreEqual(at, observation.TimestampUtc);
        CollectionAssert.Contains(observation.Facts.ToList(), "location=Town");
        CollectionAssert.Contains(observation.Facts.ToList(), "tile=42,17");
        CollectionAssert.Contains(observation.Facts.ToList(), "isAvailableForControl=true");
        CollectionAssert.Contains(observation.Facts.ToList(), "currentCommandId=cmd-1");
    }

    [TestMethod]
    public async Task ObserveAsync_WithBodyBinding_PostsTargetEntityId()
    {
        var at = new DateTime(2026, 4, 30, 8, 30, 0, DateTimeKind.Utc);
        var client = new FakeSmapiClient();
        client.StatusResponse = new StardewBridgeResponse<StardewNpcStatusData>(
            true,
            "trace-status",
            "req-status",
            null,
            null,
            new StardewNpcStatusData(
                "haley",
                "Haley",
                "Haley",
                "Town",
                new StardewTile(42, 17),
                false,
                false,
                true,
                null,
                null,
                null),
            null,
            null);
        var service = new StardewQueryService(client, "save-1", nowUtc: () => at);

        await service.ObserveAsync(new NpcBodyBinding("haley", "Haley", "Haley", "Haley", "stardew"), CancellationToken.None);

        Assert.IsInstanceOfType(client.LastEnvelope, typeof(StardewBridgeEnvelope<StardewStatusQuery>));
        var envelope = (StardewBridgeEnvelope<StardewStatusQuery>)client.LastEnvelope!;
        Assert.AreEqual("Haley", envelope.NpcId);
        Assert.AreEqual("Haley", envelope.Payload.NpcId);
    }

    [TestMethod]
    public async Task GetWorldSnapshotAsync_PostsWorldSnapshotQueryAndMapsCoreSnapshot()
    {
        var at = new DateTime(2026, 4, 30, 8, 45, 0, DateTimeKind.Utc);
        var client = new FakeSmapiClient();
        client.WorldSnapshotResponse = new StardewBridgeResponse<StardewWorldSnapshotData>(
            true,
            "trace-world",
            "req-world",
            null,
            null,
            new StardewWorldSnapshotData(
                "stardew-valley",
                "save-1",
                at,
                [new StardewWorldEntityData("haley", "Haley", "Haley", "stardew")],
                ["weather=sunny", "time=0830"]),
            null,
            null);
        var service = new StardewQueryService(client, "save-1");

        var snapshot = await service.GetWorldSnapshotAsync("haley", CancellationToken.None);

        Assert.AreEqual(StardewBridgeRoutes.QueryWorldSnapshot, client.LastRoute);
        Assert.IsInstanceOfType(client.LastEnvelope, typeof(StardewBridgeEnvelope<StardewWorldSnapshotQuery>));
        var envelope = (StardewBridgeEnvelope<StardewWorldSnapshotQuery>)client.LastEnvelope!;
        Assert.AreEqual("haley", envelope.NpcId);
        Assert.AreEqual("save-1", envelope.SaveId);
        Assert.AreEqual("haley", envelope.Payload.NpcId);

        Assert.AreEqual("stardew-valley", snapshot.GameId);
        Assert.AreEqual("save-1", snapshot.SaveId);
        Assert.AreEqual(at, snapshot.TimestampUtc);
        Assert.AreEqual("haley", snapshot.Entities.Single().NpcId);
        CollectionAssert.Contains(snapshot.Facts.ToList(), "weather=sunny");
    }

    [TestMethod]
    public async Task GetWorldSnapshotAsync_WithBodyBinding_PostsTargetEntityId()
    {
        var at = new DateTime(2026, 4, 30, 8, 45, 0, DateTimeKind.Utc);
        var client = new FakeSmapiClient();
        client.WorldSnapshotResponse = new StardewBridgeResponse<StardewWorldSnapshotData>(
            true,
            "trace-world",
            "req-world",
            null,
            null,
            new StardewWorldSnapshotData(
                "stardew-valley",
                "save-1",
                at,
                [new StardewWorldEntityData("haley", "Haley", "Haley", "stardew")],
                []),
            null,
            null);
        var service = new StardewQueryService(client, "save-1");

        await service.GetWorldSnapshotAsync(new NpcBodyBinding("haley", "Haley", "Haley", "Haley", "stardew"), CancellationToken.None);

        var envelope = (StardewBridgeEnvelope<StardewWorldSnapshotQuery>)client.LastEnvelope!;
        Assert.AreEqual("Haley", envelope.NpcId);
        Assert.AreEqual("Haley", envelope.Payload.NpcId);
    }

    private sealed class FakeSmapiClient : ISmapiModApiClient
    {
        public string? LastRoute { get; private set; }
        public object? LastEnvelope { get; private set; }
        public StardewBridgeResponse<StardewNpcStatusData>? StatusResponse { get; set; }
        public StardewBridgeResponse<StardewWorldSnapshotData>? WorldSnapshotResponse { get; set; }

        public Task<StardewBridgeResponse<TData>> SendAsync<TPayload, TData>(
            string route,
            StardewBridgeEnvelope<TPayload> envelope,
            CancellationToken ct)
        {
            LastRoute = route;
            LastEnvelope = envelope;

            object response = route switch
            {
                StardewBridgeRoutes.QueryStatus => StatusResponse ?? throw new InvalidOperationException("No status response configured."),
                StardewBridgeRoutes.QueryWorldSnapshot => WorldSnapshotResponse ?? throw new InvalidOperationException("No world snapshot response configured."),
                _ => throw new InvalidOperationException($"Unexpected route {route}.")
            };

            return Task.FromResult((StardewBridgeResponse<TData>)response);
        }
    }
}
