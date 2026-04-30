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

    private static NpcRuntimeDescriptor CreateDescriptor(string npcId)
        => new(
            npcId,
            npcId,
            "stardew-valley",
            "save-1",
            "default",
            "stardew",
            "pack-root",
            $"sdv_save-1_{npcId}_default");
}
