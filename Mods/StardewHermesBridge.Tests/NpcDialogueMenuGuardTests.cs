using Microsoft.VisualStudio.TestTools.UnitTesting;
using StardewHermesBridge.Dialogue;

namespace StardewHermesBridge.Tests;

[TestClass]
public class NpcDialogueMenuGuardTests
{
    [TestMethod]
    public void ConsumeMenuChange_WhenCustomDialogueOpens_ConsumesOpeningEvenWithoutPendingFlow()
    {
        var guard = new NpcDialogueMenuGuard();
        guard.MarkCustomDialogueOpening("Haley");

        var result = guard.ConsumeMenuChange(oldDialogueNpcName: null, newDialogueNpcName: "Haley");

        Assert.AreEqual(NpcDialogueMenuGuardResult.CustomDialogueOpening, result);
    }

    [TestMethod]
    public void IsCustomDialogue_WhenCustomDialogueIsOpening_ReturnsTrue()
    {
        var guard = new NpcDialogueMenuGuard();
        guard.MarkCustomDialogueOpening("Haley");

        Assert.IsTrue(guard.IsCustomDialogue("Haley"));
    }

    [TestMethod]
    public void IsCustomDialogue_WhenCustomDialogueIsActive_ReturnsTrueUntilCloseIsConsumed()
    {
        var guard = new NpcDialogueMenuGuard();
        guard.MarkCustomDialogueOpening("Haley");
        _ = guard.ConsumeMenuChange(oldDialogueNpcName: null, newDialogueNpcName: "Haley");

        Assert.IsTrue(guard.IsCustomDialogue("Haley"));

        _ = guard.ConsumeMenuChange(oldDialogueNpcName: "Haley", newDialogueNpcName: null);

        Assert.IsFalse(guard.IsCustomDialogue("Haley"));
    }

    [TestMethod]
    public void ConsumeMenuChange_AfterCustomOpeningWasConsumed_DoesNotConsumeNextHaleyDialogueAsCustom()
    {
        var guard = new NpcDialogueMenuGuard();
        guard.MarkCustomDialogueOpening("Haley");
        _ = guard.ConsumeMenuChange(oldDialogueNpcName: null, newDialogueNpcName: "Haley");
        _ = guard.ConsumeMenuChange(oldDialogueNpcName: "Haley", newDialogueNpcName: null);

        var result = guard.ConsumeMenuChange(oldDialogueNpcName: null, newDialogueNpcName: "Haley");

        Assert.AreEqual(NpcDialogueMenuGuardResult.Unhandled, result);
    }

    [TestMethod]
    public void ConsumeMenuChange_WhenCustomDialogueCloses_ConsumesClosingOnce()
    {
        var guard = new NpcDialogueMenuGuard();
        guard.MarkCustomDialogueOpening("Haley");
        _ = guard.ConsumeMenuChange(oldDialogueNpcName: null, newDialogueNpcName: "Haley");

        var closing = guard.ConsumeMenuChange(oldDialogueNpcName: "Haley", newDialogueNpcName: null);
        var nextHaleyDialogue = guard.ConsumeMenuChange(oldDialogueNpcName: null, newDialogueNpcName: "Haley");

        Assert.AreEqual(NpcDialogueMenuGuardResult.CustomDialogueClosing, closing);
        Assert.AreEqual(NpcDialogueMenuGuardResult.Unhandled, nextHaleyDialogue);
    }

    [TestMethod]
    public void Clear_RemovesPendingCustomOpening()
    {
        var guard = new NpcDialogueMenuGuard();
        guard.MarkCustomDialogueOpening("Haley");

        guard.Clear();
        var result = guard.ConsumeMenuChange(oldDialogueNpcName: null, newDialogueNpcName: "Haley");

        Assert.AreEqual(NpcDialogueMenuGuardResult.Unhandled, result);
    }
}
