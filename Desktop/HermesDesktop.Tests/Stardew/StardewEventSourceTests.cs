using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Stardew;

[TestClass]
public class StardewEventSourceTests
{
    [TestMethod]
    public async Task PollAsync_PostsCursorToEventsRouteAndMapsFactRecords()
    {
        var at = new DateTime(2026, 4, 30, 9, 0, 0, DateTimeKind.Utc);
        var client = new FakeSmapiClient();
        client.EventResponse = new StardewBridgeResponse<StardewEventPollData>(
            true,
            "trace-events",
            "req-events",
            null,
            null,
            new StardewEventPollData(
                [
                    new StardewEventData("evt-2", "proximity", "haley", at, "The farmer entered Haley's proximity."),
                    new StardewEventData("evt-3", "time_changed", null, at.AddMinutes(10), "The clock advanced to 9:10.")
                ]),
            null,
            null);
        var source = new StardewEventSource(client, "save-1", npcId: "haley");

        var records = await source.PollAsync(new GameEventCursor("evt-1"), CancellationToken.None);

        Assert.AreEqual(StardewBridgeRoutes.EventsPoll, client.LastRoute);
        Assert.IsInstanceOfType(client.LastEnvelope, typeof(StardewBridgeEnvelope<StardewEventPollQuery>));
        var envelope = (StardewBridgeEnvelope<StardewEventPollQuery>)client.LastEnvelope!;
        Assert.AreEqual("haley", envelope.NpcId);
        Assert.AreEqual("save-1", envelope.SaveId);
        Assert.AreEqual("evt-1", envelope.Payload.Since);
        Assert.AreEqual("haley", envelope.Payload.NpcId);

        Assert.AreEqual(2, records.Count);
        Assert.AreEqual("evt-2", records[0].EventId);
        Assert.AreEqual("proximity", records[0].EventType);
        Assert.AreEqual("haley", records[0].NpcId);
        Assert.AreEqual("time_changed", records[1].EventType);
        Assert.IsNull(records[1].NpcId);
    }

    private sealed class FakeSmapiClient : ISmapiModApiClient
    {
        public string? LastRoute { get; private set; }
        public object? LastEnvelope { get; private set; }
        public StardewBridgeResponse<StardewEventPollData>? EventResponse { get; set; }

        public Task<StardewBridgeResponse<TData>> SendAsync<TPayload, TData>(
            string route,
            StardewBridgeEnvelope<TPayload> envelope,
            CancellationToken ct)
        {
            LastRoute = route;
            LastEnvelope = envelope;

            if (route != StardewBridgeRoutes.EventsPoll)
                throw new InvalidOperationException($"Unexpected route {route}.");

            return Task.FromResult((StardewBridgeResponse<TData>)(object)(EventResponse ?? throw new InvalidOperationException("No event response configured.")));
        }
    }
}
