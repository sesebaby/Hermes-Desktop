using Microsoft.VisualStudio.TestTools.UnitTesting;
using StardewHermesBridge.Dialogue;

namespace StardewHermesBridge.Tests;

[TestClass]
public class NpcDialogueFollowUpServiceTests
{
    [TestMethod]
    public void CanStartFollowUp_WhenOriginalDialogueIsStillOpen_ReturnsFalse()
    {
        var service = new NpcDialogueFollowUpService();

        var result = service.CanStartFollowUp(new NpcClickDialogueState(
            IsOriginalDialogueOpen: true,
            IsOriginalDialogueEnded: false,
            IsTransitioning: false,
            HasCustomDialogueStarted: false));

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void CanStartFollowUp_WhenDialogueNotEndedYetAndOnlyTransitioningIsFalse_ReturnsFalse()
    {
        var service = new NpcDialogueFollowUpService();

        var result = service.CanStartFollowUp(new NpcClickDialogueState(
            IsOriginalDialogueOpen: false,
            IsOriginalDialogueEnded: false,
            IsTransitioning: false,
            HasCustomDialogueStarted: false));

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void CanStartFollowUp_WhenOriginalDialogueEnded_ReturnsTrue()
    {
        var service = new NpcDialogueFollowUpService();

        var result = service.CanStartFollowUp(new NpcClickDialogueState(
            IsOriginalDialogueOpen: false,
            IsOriginalDialogueEnded: true,
            IsTransitioning: true,
            HasCustomDialogueStarted: false));

        Assert.IsTrue(result);
    }
}
