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
    public async Task ObserveAsync_MapsMoveCandidatesIntoMachineReadableFacts()
    {
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
                "HaleyHouse",
                new StardewTile(8, 7),
                false,
                false,
                true,
                null,
                null,
                null,
                [
                    new StardewMoveCandidateData("HaleyHouse", new StardewTile(9, 7), "same_location_safe_reposition"),
                    new StardewMoveCandidateData("HaleyHouse", new StardewTile(8, 8), "same_location_safe_reposition"),
                    new StardewMoveCandidateData("HaleyHouse", new StardewTile(7, 7), "same_location_safe_reposition"),
                    new StardewMoveCandidateData("HaleyHouse", new StardewTile(8, 6), "same_location_safe_reposition")
                ]),
            null,
            null);
        var service = new StardewQueryService(client, "save-1");

        var observation = await service.ObserveAsync("haley", CancellationToken.None);

        var candidateFacts = observation.Facts
            .Where(fact => fact.StartsWith("moveCandidate[", StringComparison.Ordinal))
            .ToArray();
        Assert.AreEqual(3, candidateFacts.Length, "Autonomy facts should expose at most three current safe move candidates.");
        CollectionAssert.Contains(
            candidateFacts,
            "moveCandidate[0]=locationName=HaleyHouse,x=9,y=7,reason=same_location_safe_reposition");
        CollectionAssert.Contains(
            candidateFacts,
            "moveCandidate[1]=locationName=HaleyHouse,x=8,y=8,reason=same_location_safe_reposition");
    }

    [TestMethod]
    public async Task ObserveAsync_MapsPlaceCandidatesIntoMachineReadableFacts()
    {
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
                "HaleyHouse",
                new StardewTile(8, 7),
                false,
                false,
                true,
                null,
                null,
                null,
                MoveCandidates: null,
                PlaceCandidates:
                [
                    new StardewPlaceCandidateData("Bedroom mirror", "HaleyHouse", new StardewTile(6, 4), ["home", "photogenic"], "check her look before going out"),
                    new StardewPlaceCandidateData("Living room", "HaleyHouse", new StardewTile(10, 12), ["home", "social"], "see what is happening downstairs"),
                    new StardewPlaceCandidateData("Front door", "HaleyHouse", new StardewTile(15, 8), ["transition", "outdoor"], "consider going outside"),
                    new StardewPlaceCandidateData("Kitchen", "HaleyHouse", new StardewTile(4, 11), ["home"], "extra candidate should be clipped")
                ]),
            null,
            null);
        var service = new StardewQueryService(client, "save-1");

        var observation = await service.ObserveAsync("haley", CancellationToken.None);

        var candidateFacts = observation.Facts
            .Where(fact => fact.StartsWith("placeCandidate[", StringComparison.Ordinal))
            .ToArray();
        Assert.AreEqual(3, candidateFacts.Length, "Autonomy facts should expose a compact top-three place candidate set.");
        CollectionAssert.Contains(
            candidateFacts,
            "placeCandidate[0]=label=Bedroom mirror,locationName=HaleyHouse,x=6,y=4,tags=home|photogenic,reason=check her look before going out");
        CollectionAssert.Contains(
            candidateFacts,
            "placeCandidate[2]=label=Front door,locationName=HaleyHouse,x=15,y=8,tags=transition|outdoor,reason=consider going outside");
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
