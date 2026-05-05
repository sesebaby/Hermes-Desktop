using Hermes.Agent.Game;
using Hermes.Agent.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Runtime;

[TestClass]
public class NpcObservationFactStoreTests
{
    [TestMethod]
    public void RecordObservationAndEvent_KeepsFactsSeparatedByNpcRuntime()
    {
        var store = new NpcObservationFactStore();
        var at = new DateTime(2026, 4, 30, 8, 0, 0, DateTimeKind.Utc);
        var haley = CreateDescriptor("haley");
        var penny = CreateDescriptor("penny");

        store.RecordObservation(
            haley,
            new GameObservation(
                "haley",
                "stardew-valley",
                at,
                "Haley is standing near the town fountain.",
                ["location=town", "activity=idle"]));
        store.RecordEvent(
            haley,
            new GameEventRecord(
                "evt-1",
                "proximity",
                "haley",
                at.AddSeconds(1),
                "The farmer entered Haley's proximity."));
        store.RecordEvent(
            penny,
            new GameEventRecord(
                "evt-2",
                "proximity",
                "penny",
                at.AddSeconds(2),
                "The farmer entered Penny's proximity."));

        var haleyFacts = store.Snapshot(haley);
        var pennyFacts = store.Snapshot(penny);

        Assert.AreEqual(2, haleyFacts.Count);
        Assert.AreEqual(1, pennyFacts.Count);
        Assert.IsTrue(haleyFacts.All(fact => fact.NpcId == "haley"));
        Assert.IsTrue(haleyFacts.All(fact => fact.GameId == "stardew-valley"));
        Assert.AreEqual("observation", haleyFacts[0].SourceKind);
        Assert.AreEqual("event", haleyFacts[1].SourceKind);
        Assert.AreEqual("evt-1", haleyFacts[1].SourceId);
        CollectionAssert.Contains(haleyFacts[0].Facts.ToList(), "location=town");
    }

    [TestMethod]
    public void Snapshot_KeepsFactsSeparatedBySession()
    {
        var store = new NpcObservationFactStore();
        var at = new DateTime(2026, 5, 5, 8, 0, 0, DateTimeKind.Utc);
        var autonomy = CreateDescriptor("haley", "sdv_save-1_haley_default");
        var privateChat = CreateDescriptor("haley", "sdv_save-1_haley_default:private_chat:chat-1");

        store.RecordObservation(
            autonomy,
            new GameObservation(
                "haley",
                "stardew-valley",
                at,
                "Haley is near the fountain.",
                ["source=autonomy"]));
        store.RecordObservation(
            privateChat,
            new GameObservation(
                "haley",
                "stardew-valley",
                at.AddSeconds(1),
                "Haley is replying privately.",
                ["source=private_chat"]));

        var autonomyFacts = store.Snapshot(autonomy);
        var privateChatFacts = store.Snapshot(privateChat);

        Assert.AreEqual(1, autonomyFacts.Count);
        Assert.AreEqual("sdv_save-1_haley_default", autonomyFacts[0].SessionId);
        CollectionAssert.Contains(autonomyFacts[0].Facts.ToList(), "source=autonomy");
        Assert.AreEqual(1, privateChatFacts.Count);
        Assert.AreEqual("sdv_save-1_haley_default:private_chat:chat-1", privateChatFacts[0].SessionId);
        CollectionAssert.Contains(privateChatFacts[0].Facts.ToList(), "source=private_chat");
    }

    [TestMethod]
    public void PublicApi_DoesNotAcceptAgentOrCommandCallbacks()
    {
        var constructors = typeof(NpcObservationFactStore).GetConstructors();
        Assert.IsTrue(constructors.All(ctor => ctor.GetParameters().Length == 0));

        var forbiddenFragments = new[] { "Agent", "Chat", "Command", "Tool" };
        var publicParameterTypes = typeof(NpcObservationFactStore)
            .GetMethods()
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType.Name);

        Assert.IsFalse(publicParameterTypes.Any(typeName =>
            forbiddenFragments.Any(fragment => typeName.Contains(fragment, StringComparison.OrdinalIgnoreCase))));
    }

    private static NpcRuntimeDescriptor CreateDescriptor(string npcId, string? sessionId = null)
        => new(
            npcId,
            npcId,
            "stardew-valley",
            "save-1",
            "default",
            "stardew",
            "pack-root",
            sessionId ?? $"sdv_save-1_{npcId}_default");
}
