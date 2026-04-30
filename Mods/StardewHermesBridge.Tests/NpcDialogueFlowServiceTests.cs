using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StardewHermesBridge.Tests;

[TestClass]
public class NpcDialogueFlowServiceTests
{
    [TestMethod]
    public void Advance_WhenOriginalDialogueOpens_MarksObservedWithoutDisplayingCustomDialogue()
    {
        var service = CreateFlowService();
        var state = Invoke(service, "BeginFollowUp", "Haley");
        var request = CreateAdvanceRequest(activeDialogueNpcName: "Haley", isDialogueBoxOpen: true, isDialogueTransitioning: true);

        var result = Invoke(service, "Advance", state, request);

        Assert.IsTrue(GetBool(result, "OriginalDialogueObserved"));
        Assert.IsFalse(GetBool(result, "OriginalDialogueCompleted"));
        Assert.IsFalse(GetBool(result, "ShouldDisplayCustomDialogue"));
    }

    [TestMethod]
    public void Advance_WhenObservedDialogueClosesEvenIfStillTransitioning_DisplaysCustomDialogue()
    {
        var service = CreateFlowService();
        var started = Invoke(service, "BeginFollowUp", "Haley");
        var observed = Invoke(service, "Advance", started, CreateAdvanceRequest(
            activeDialogueNpcName: "Haley",
            isDialogueBoxOpen: true,
            isDialogueTransitioning: true));

        var result = Invoke(service, "Advance", GetProperty(observed, "State"), CreateAdvanceRequest(
            activeDialogueNpcName: null,
            isDialogueBoxOpen: false,
            hasActiveMenu: false,
            isDialogueTransitioning: true));

        Assert.IsFalse(GetBool(result, "OriginalDialogueObserved"));
        Assert.IsTrue(GetBool(result, "OriginalDialogueCompleted"));
        Assert.IsTrue(GetBool(result, "ShouldDisplayCustomDialogue"));
    }

    [TestMethod]
    public void Advance_WhenStartedFromAlreadyOpenOriginalDialogueAndMenuCloses_DisplaysCustomDialogue()
    {
        var service = CreateFlowService();
        var started = Invoke(service, "BeginObservedOriginal", "Haley");

        var result = Invoke(service, "Advance", started, CreateAdvanceRequest(
            activeDialogueNpcName: null,
            isDialogueBoxOpen: false,
            hasActiveMenu: false,
            isDialogueTransitioning: true));

        Assert.IsFalse(GetBool(result, "OriginalDialogueObserved"));
        Assert.IsTrue(GetBool(result, "OriginalDialogueCompleted"));
        Assert.IsTrue(GetBool(result, "ShouldDisplayCustomDialogue"));
    }

    [TestMethod]
    public void Advance_WhenCustomDialogueAlreadyDisplayed_DoesNotDisplayAgain()
    {
        var service = CreateFlowService();
        var started = Invoke(service, "BeginFollowUp", "Haley");
        var observed = Invoke(service, "Advance", started, CreateAdvanceRequest(
            activeDialogueNpcName: "Haley",
            isDialogueBoxOpen: true,
            isDialogueTransitioning: false));
        var displayed = Invoke(service, "Advance", GetProperty(observed, "State"), CreateAdvanceRequest(
            activeDialogueNpcName: null,
            isDialogueBoxOpen: false,
            hasActiveMenu: false,
            isDialogueTransitioning: true));

        var result = Invoke(service, "Advance", GetProperty(displayed, "State"), CreateAdvanceRequest(
            activeDialogueNpcName: null,
            isDialogueBoxOpen: false,
            hasActiveMenu: false,
            isDialogueTransitioning: false));

        Assert.IsFalse(GetBool(result, "ShouldDisplayCustomDialogue"));
    }

    private static object CreateFlowService()
    {
        var type = typeof(StardewHermesBridge.Dialogue.NpcDialogueFollowUpService).Assembly
            .GetType("StardewHermesBridge.Dialogue.NpcDialogueFlowService");

        Assert.IsNotNull(type, "NpcDialogueFlowService type was not found.");
        return Activator.CreateInstance(type!)!;
    }

    [TestMethod]
    public void Advance_WhenObservedDialogueClosesToAnotherMenu_WaitsForMenuToClose()
    {
        var service = CreateFlowService();
        var started = Invoke(service, "BeginFollowUp", "Haley");
        var observed = Invoke(service, "Advance", started, CreateAdvanceRequest(
            activeDialogueNpcName: "Haley",
            isDialogueBoxOpen: true,
            isDialogueTransitioning: false));

        var result = Invoke(service, "Advance", GetProperty(observed, "State"), CreateAdvanceRequest(
            activeDialogueNpcName: null,
            isDialogueBoxOpen: false,
            hasActiveMenu: true,
            isDialogueTransitioning: false));

        Assert.IsFalse(GetBool(result, "OriginalDialogueCompleted"));
        Assert.IsFalse(GetBool(result, "ShouldDisplayCustomDialogue"));
    }

    [TestMethod]
    public void Advance_WhenOriginalDialogueAlreadyObserved_DoesNotReportObservedAgain()
    {
        var service = CreateFlowService();
        var started = Invoke(service, "BeginObservedOriginal", "Haley");

        var result = Invoke(service, "Advance", started, CreateAdvanceRequest(
            activeDialogueNpcName: "Haley",
            isDialogueBoxOpen: true,
            isDialogueTransitioning: false));

        Assert.IsFalse(GetBool(result, "OriginalDialogueObserved"));
    }

    private static object CreateAdvanceRequest(string? activeDialogueNpcName, bool isDialogueBoxOpen, bool isDialogueTransitioning)
        => CreateAdvanceRequest(activeDialogueNpcName, isDialogueBoxOpen, hasActiveMenu: isDialogueBoxOpen, isDialogueTransitioning);

    private static object CreateAdvanceRequest(string? activeDialogueNpcName, bool isDialogueBoxOpen, bool hasActiveMenu, bool isDialogueTransitioning)
    {
        var type = typeof(StardewHermesBridge.Dialogue.NpcDialogueFollowUpService).Assembly
            .GetType("StardewHermesBridge.Dialogue.NpcDialogueAdvanceRequest");

        Assert.IsNotNull(type, "NpcDialogueAdvanceRequest type was not found.");
        return Activator.CreateInstance(type!, activeDialogueNpcName, isDialogueBoxOpen, hasActiveMenu, isDialogueTransitioning)!;
    }

    private static object Invoke(object target, string methodName, params object?[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(method, $"Method {methodName} was not found.");
        return method!.Invoke(target, args)!;
    }

    private static object GetProperty(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(property, $"Property {propertyName} was not found.");
        return property!.GetValue(target)!;
    }

    private static bool GetBool(object target, string propertyName)
        => (bool)GetProperty(target, propertyName);
}
