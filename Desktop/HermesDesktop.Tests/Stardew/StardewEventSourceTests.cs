using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json.Nodes;

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
                    new StardewEventData(
                        "evt-2",
                        "player_private_message_submitted",
                        "haley",
                        at,
                        "Player submitted a private chat message.",
                        "pc_evt_000000000001",
                        new JsonObject
                        {
                            ["conversationId"] = "pc_evt_000000000001",
                            ["text"] = "hi"
                        },
                        Sequence: 12),
                    new StardewEventData("evt-3", "time_changed", null, at.AddMinutes(10), "The clock advanced to 9:10.", Sequence: 13)
                ],
                NextSequence: 14),
            null,
            null);
        var source = new StardewEventSource(client, "save-1", npcId: "haley");

        var batch = await source.PollBatchAsync(new GameEventCursor("evt-1", 11), CancellationToken.None);
        var records = batch.Records;

        Assert.AreEqual(StardewBridgeRoutes.EventsPoll, client.LastRoute);
        Assert.IsInstanceOfType(client.LastEnvelope, typeof(StardewBridgeEnvelope<StardewEventPollQuery>));
        var envelope = (StardewBridgeEnvelope<StardewEventPollQuery>)client.LastEnvelope!;
        Assert.AreEqual("haley", envelope.NpcId);
        Assert.AreEqual("save-1", envelope.SaveId);
        Assert.AreEqual("evt-1", envelope.Payload.Since);
        Assert.AreEqual("haley", envelope.Payload.NpcId);
        Assert.AreEqual(11L, envelope.Payload.Sequence);

        Assert.AreEqual(2, records.Count);
        Assert.AreEqual("evt-2", records[0].EventId);
        Assert.AreEqual("player_private_message_submitted", records[0].EventType);
        Assert.AreEqual("haley", records[0].NpcId);
        Assert.AreEqual("pc_evt_000000000001", records[0].CorrelationId);
        Assert.AreEqual("hi", records[0].Payload?["text"]?.GetValue<string>());
        Assert.AreEqual(12L, records[0].Sequence);
        Assert.AreEqual("time_changed", records[1].EventType);
        Assert.IsNull(records[1].NpcId);
        Assert.AreEqual(13L, records[1].Sequence);
        Assert.AreEqual("evt-3", batch.NextCursor.Since);
        Assert.AreEqual(14L, batch.NextCursor.Sequence);
    }

    [TestMethod]
    public async Task PollBatchAsync_WhenCursorEventIdIsAheadOfSequence_RebasesBeforePolling()
    {
        var at = new DateTime(2026, 5, 10, 13, 28, 37, DateTimeKind.Utc);
        var client = new FakeSmapiClient();
        client.EventResponse = new StardewBridgeResponse<StardewEventPollData>(
            true,
            "trace-events",
            "req-events",
            null,
            null,
            new StardewEventPollData(
                [
                    new StardewEventData(
                        "evt_000000000001",
                        "vanilla_dialogue_completed",
                        "Haley",
                        at,
                        "Haley vanilla dialogue completed.",
                        Sequence: 1)
                ],
                NextSequence: 1),
            null,
            null);
        var source = new StardewEventSource(client, "save-1");

        var batch = await source.PollBatchAsync(new GameEventCursor("evt_000000000003", 1), CancellationToken.None);

        var envelope = (StardewBridgeEnvelope<StardewEventPollQuery>)client.LastEnvelope!;
        Assert.IsNull(envelope.Payload.Since);
        Assert.IsNull(envelope.Payload.Sequence);
        Assert.AreEqual(1, batch.Records.Count);
        Assert.AreEqual("vanilla_dialogue_completed", batch.Records[0].EventType);
        Assert.AreEqual("evt_000000000001", batch.NextCursor.Since);
        Assert.AreEqual(1L, batch.NextCursor.Sequence);
    }

    [TestMethod]
    public async Task PollBatchAsync_WhenBridgeSequenceRollsBackOnEmptyResponse_ReturnsRootCursorForRebase()
    {
        var client = new FakeSmapiClient();
        client.EventResponse = new StardewBridgeResponse<StardewEventPollData>(
            true,
            "trace-events",
            "req-events",
            null,
            null,
            new StardewEventPollData([], NextSequence: 1),
            null,
            null);
        var source = new StardewEventSource(client, "save-1");

        var batch = await source.PollBatchAsync(new GameEventCursor("evt_000000000003", 3), CancellationToken.None);

        var envelope = (StardewBridgeEnvelope<StardewEventPollQuery>)client.LastEnvelope!;
        Assert.AreEqual("evt_000000000003", envelope.Payload.Since);
        Assert.AreEqual(3L, envelope.Payload.Sequence);
        Assert.AreEqual(0, batch.Records.Count);
        Assert.IsNull(batch.NextCursor.Since);
        Assert.IsNull(batch.NextCursor.Sequence);
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
