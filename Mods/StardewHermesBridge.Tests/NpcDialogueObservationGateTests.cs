using Microsoft.VisualStudio.TestTools.UnitTesting;
using StardewHermesBridge.Dialogue;

namespace StardewHermesBridge.Tests;

[TestClass]
public class NpcDialogueObservationGateTests
{
    [TestMethod]
    public void TryClaim_WhenFreshDialogueWasObserved_ReturnsTrueOnlyOnce()
    {
        var gate = new NpcDialogueObservationGate();
        var dialogueInstance = new object();
        gate.RecordMenuChanged(dialogueInstance, "Haley");

        Assert.IsTrue(gate.TryClaim(dialogueInstance, "Haley"));
        Assert.IsFalse(gate.TryClaim(dialogueInstance, "Haley"));
    }

    [TestMethod]
    public void MarkObserved_WhenDialogueWasHandledByMenuChanged_PreventsLaterClaim()
    {
        var gate = new NpcDialogueObservationGate();
        var dialogueInstance = new object();
        gate.RecordMenuChanged(dialogueInstance, "Haley");

        gate.MarkObserved(dialogueInstance, "Haley");

        Assert.IsFalse(gate.CanClaim(dialogueInstance, "Haley"));
    }

    [TestMethod]
    public void RecordMenuChanged_WhenTrackedDialogueCloses_ClearsClaimState()
    {
        var gate = new NpcDialogueObservationGate();
        var dialogueInstance = new object();
        gate.RecordMenuChanged(dialogueInstance, "Haley");

        Assert.IsTrue(gate.TryClaim(dialogueInstance, "Haley"));

        gate.RecordMenuChanged(newMenu: null, dialogueNpcName: null);

        Assert.IsFalse(gate.CanClaim(dialogueInstance, "Haley"));
    }

    [TestMethod]
    public void RecordMenuChanged_WhenNewDialogueInstanceOpens_ResetsClaimForNextConversation()
    {
        var gate = new NpcDialogueObservationGate();
        var firstDialogue = new object();
        var secondDialogue = new object();
        gate.RecordMenuChanged(firstDialogue, "Haley");
        Assert.IsTrue(gate.TryClaim(firstDialogue, "Haley"));

        gate.RecordMenuChanged(secondDialogue, "Haley");

        Assert.IsTrue(gate.TryClaim(secondDialogue, "Haley"));
    }
}
