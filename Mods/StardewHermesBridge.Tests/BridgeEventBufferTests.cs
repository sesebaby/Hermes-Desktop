using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json.Nodes;
using StardewHermesBridge.Bridge;

namespace StardewHermesBridge.Tests;

[TestClass]
public class BridgeEventBufferTests
{
    [TestMethod]
    public void Poll_ReturnsEventsAfterCursorAndFiltersByNpc()
    {
        var buffer = new BridgeEventBuffer(() => new DateTime(2026, 4, 30, 5, 0, 0, DateTimeKind.Utc));
        var first = buffer.Record("vanilla_dialogue_completed", "Haley", "Haley vanilla dialogue completed.");
        var second = buffer.Record("player_private_message_submitted", "Haley", "Player submitted a private chat message.");
        buffer.Record("vanilla_dialogue_completed", "Penny", "Penny vanilla dialogue completed.");

        var allHaley = buffer.Poll(null, "Haley");
        var afterFirst = buffer.Poll(first.EventId, "Haley");
        var penny = buffer.Poll(null, "Penny");

        CollectionAssert.AreEqual(new[] { first.EventId, second.EventId }, allHaley.Select(item => item.EventId).ToArray());
        CollectionAssert.AreEqual(new[] { second.EventId }, afterFirst.Select(item => item.EventId).ToArray());
        CollectionAssert.AreEqual(new[] { "Penny" }, penny.Select(item => item.NpcId).ToArray());
    }

    [TestMethod]
    public void Record_WithCorrelationAndPayload_ReturnsTypedEvent()
    {
        var buffer = new BridgeEventBuffer(() => new DateTime(2026, 4, 30, 5, 0, 0, DateTimeKind.Utc));
        var payload = new JsonObject
        {
            ["conversationId"] = "pc_evt_000000000001",
            ["text"] = "hi"
        };
        var overload = typeof(BridgeEventBuffer).GetMethod(
            "Record",
            new[]
            {
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(JsonObject)
            });

        Assert.IsNotNull(overload, "BridgeEventBuffer must expose a typed Record overload for correlation id and payload.");
        var record = (BridgeEventData)overload.Invoke(
            buffer,
            new object?[]
            {
                "player_private_message_submitted",
                "Haley",
                "Player submitted a private chat message.",
                "pc_evt_000000000001",
                payload
            })!;

        Assert.AreEqual("pc_evt_000000000001", record.CorrelationId);
        Assert.IsNotNull(record.Payload);
        Assert.AreEqual("hi", record.Payload["text"]?.GetValue<string>());
        Assert.AreEqual(
            "Player submitted a private chat message.",
            record.Summary,
            "Raw player private-chat text must stay in payload, not in the human summary.");
    }
}
