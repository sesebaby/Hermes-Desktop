using Microsoft.VisualStudio.TestTools.UnitTesting;
using StardewHermesBridge.Dialogue;

namespace StardewHermesBridge.Tests;

[TestClass]
public class NpcDialogueFlowServiceTests
{
    [TestMethod]
    public void Advance_WhenOriginalDialogueOpens_MarksObservedWithoutDisplayingCustomDialogue()
    {
        var service = new NpcDialogueFlowService();
        var state = service.BeginFollowUp("Haley");

        var result = service.Advance(state, CreateAdvanceRequest(
            activeDialogueNpcName: "Haley",
            isDialogueBoxOpen: true,
            isDialogueTransitioning: true));

        Assert.IsTrue(result.OriginalDialogueObserved);
        Assert.IsFalse(result.OriginalDialogueCompleted);
        Assert.IsFalse(result.ShouldDisplayCustomDialogue);
    }

    [TestMethod]
    public void Advance_WhenObservedDialogueClosesEvenIfStillTransitioning_DisplaysCustomDialogue()
    {
        var service = new NpcDialogueFlowService();
        var started = service.BeginFollowUp("Haley");
        var observed = service.Advance(started, CreateAdvanceRequest(
            activeDialogueNpcName: "Haley",
            isDialogueBoxOpen: true,
            isDialogueTransitioning: true));

        var result = service.Advance(observed.State, CreateAdvanceRequest(
            activeDialogueNpcName: null,
            isDialogueBoxOpen: false,
            hasActiveMenu: false,
            isDialogueTransitioning: true));

        Assert.IsFalse(result.OriginalDialogueObserved);
        Assert.IsTrue(result.OriginalDialogueCompleted);
        Assert.IsTrue(result.ShouldDisplayCustomDialogue);
    }

    [TestMethod]
    public void Advance_WhenStartedFromAlreadyOpenOriginalDialogueAndMenuCloses_DisplaysCustomDialogue()
    {
        var service = new NpcDialogueFlowService();
        var started = service.BeginObservedOriginal("Haley");

        var result = service.Advance(started, CreateAdvanceRequest(
            activeDialogueNpcName: null,
            isDialogueBoxOpen: false,
            hasActiveMenu: false,
            isDialogueTransitioning: true));

        Assert.IsFalse(result.OriginalDialogueObserved);
        Assert.IsTrue(result.OriginalDialogueCompleted);
        Assert.IsTrue(result.ShouldDisplayCustomDialogue);
    }

    [TestMethod]
    public void Advance_WhenCustomDialogueAlreadyDisplayed_DoesNotDisplayAgain()
    {
        var service = new NpcDialogueFlowService();
        var started = service.BeginFollowUp("Haley");
        var observed = service.Advance(started, CreateAdvanceRequest(
            activeDialogueNpcName: "Haley",
            isDialogueBoxOpen: true,
            isDialogueTransitioning: false));
        var displayed = service.Advance(observed.State, CreateAdvanceRequest(
            activeDialogueNpcName: null,
            isDialogueBoxOpen: false,
            hasActiveMenu: false,
            isDialogueTransitioning: true));

        var result = service.Advance(displayed.State, CreateAdvanceRequest(
            activeDialogueNpcName: null,
            isDialogueBoxOpen: false,
            hasActiveMenu: false,
            isDialogueTransitioning: false));

        Assert.IsFalse(result.ShouldDisplayCustomDialogue);
    }

    [TestMethod]
    public void Advance_WhenObservedDialogueClosesToAnotherMenu_WaitsForMenuToClose()
    {
        var service = new NpcDialogueFlowService();
        var started = service.BeginFollowUp("Haley");
        var observed = service.Advance(started, CreateAdvanceRequest(
            activeDialogueNpcName: "Haley",
            isDialogueBoxOpen: true,
            isDialogueTransitioning: false));

        var result = service.Advance(observed.State, CreateAdvanceRequest(
            activeDialogueNpcName: null,
            isDialogueBoxOpen: false,
            hasActiveMenu: true,
            isDialogueTransitioning: false));

        Assert.IsFalse(result.OriginalDialogueCompleted);
        Assert.IsFalse(result.ShouldDisplayCustomDialogue);
    }

    [TestMethod]
    public void Advance_WhenOriginalDialogueAlreadyObserved_DoesNotReportObservedAgain()
    {
        var service = new NpcDialogueFlowService();
        var started = service.BeginObservedOriginal("Haley");

        var result = service.Advance(started, CreateAdvanceRequest(
            activeDialogueNpcName: "Haley",
            isDialogueBoxOpen: true,
            isDialogueTransitioning: false));

        Assert.IsFalse(result.OriginalDialogueObserved);
    }

    private static NpcDialogueAdvanceRequest CreateAdvanceRequest(
        string? activeDialogueNpcName,
        bool isDialogueBoxOpen,
        bool isDialogueTransitioning)
        => CreateAdvanceRequest(
            activeDialogueNpcName,
            isDialogueBoxOpen,
            hasActiveMenu: isDialogueBoxOpen,
            isDialogueTransitioning);

    private static NpcDialogueAdvanceRequest CreateAdvanceRequest(
        string? activeDialogueNpcName,
        bool isDialogueBoxOpen,
        bool hasActiveMenu,
        bool isDialogueTransitioning)
        => new(activeDialogueNpcName, isDialogueBoxOpen, hasActiveMenu, isDialogueTransitioning);
}
