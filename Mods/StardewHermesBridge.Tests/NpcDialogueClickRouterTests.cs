using Microsoft.VisualStudio.TestTools.UnitTesting;
using StardewHermesBridge.Dialogue;

namespace StardewHermesBridge.Tests;

[TestClass]
public class NpcDialogueClickRouterTests
{
    [TestMethod]
    public void Route_ActionButtonOnHaley_AcceptsDialogueRoute()
    {
        var router = new NpcDialogueClickRouter();

        var result = router.Route(new NpcDialogueClickRouteRequest(
            IsActionButton: true,
            IsUseToolButton: false,
            IsMouseButton: true,
            TargetNpcName: "Haley",
            HasActiveMenu: false,
            IsDialogueBoxOpen: false,
            ActiveDialogueNpcName: null));

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("Haley", result.NpcName);
        Assert.AreEqual("accepted", result.Reason);
    }

    [TestMethod]
    public void Route_UseToolMouseClickOnHaley_AcceptsDialogueRoute()
    {
        var router = new NpcDialogueClickRouter();

        var result = router.Route(new NpcDialogueClickRouteRequest(
            IsActionButton: false,
            IsUseToolButton: true,
            IsMouseButton: true,
            TargetNpcName: "Haley",
            HasActiveMenu: false,
            IsDialogueBoxOpen: false,
            ActiveDialogueNpcName: null));

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("Haley", result.NpcName);
        Assert.AreEqual("accepted", result.Reason);
    }

    [TestMethod]
    public void Route_WhenHaleyDialogueBoxAlreadyOpen_AcceptsObservedDialogueRoute()
    {
        var router = new NpcDialogueClickRouter();

        var result = router.Route(new NpcDialogueClickRouteRequest(
            IsActionButton: false,
            IsUseToolButton: true,
            IsMouseButton: true,
            TargetNpcName: null,
            HasActiveMenu: true,
            IsDialogueBoxOpen: true,
            ActiveDialogueNpcName: "Haley"));

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("Haley", result.NpcName);
        Assert.AreEqual("accepted_active_dialogue", result.Reason);
    }

    [TestMethod]
    public void Route_ClickOnGround_RejectsDialogueRoute()
    {
        var router = new NpcDialogueClickRouter();

        var result = router.Route(new NpcDialogueClickRouteRequest(
            IsActionButton: true,
            IsUseToolButton: false,
            IsMouseButton: true,
            TargetNpcName: null,
            HasActiveMenu: false,
            IsDialogueBoxOpen: false,
            ActiveDialogueNpcName: null));

        Assert.IsFalse(result.IsAccepted);
        Assert.IsNull(result.NpcName);
        Assert.AreEqual("no_npc_hit", result.Reason);
    }

    [TestMethod]
    public void Route_ActionButtonOnPenny_AcceptsDialogueRoute()
    {
        var router = new NpcDialogueClickRouter();

        var result = router.Route(new NpcDialogueClickRouteRequest(
            IsActionButton: true,
            IsUseToolButton: false,
            IsMouseButton: true,
            TargetNpcName: "Penny",
            HasActiveMenu: false,
            IsDialogueBoxOpen: false,
            ActiveDialogueNpcName: null));

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("Penny", result.NpcName);
        Assert.AreEqual("accepted", result.Reason);
    }

    [TestMethod]
    public void Route_WhenMenuAlreadyOpen_RejectsDialogueRoute()
    {
        var router = new NpcDialogueClickRouter();

        var result = router.Route(new NpcDialogueClickRouteRequest(
            IsActionButton: true,
            IsUseToolButton: false,
            IsMouseButton: true,
            TargetNpcName: "Haley",
            HasActiveMenu: true,
            IsDialogueBoxOpen: false,
            ActiveDialogueNpcName: null));

        Assert.IsFalse(result.IsAccepted);
        Assert.IsNull(result.NpcName);
        Assert.AreEqual("menu_open", result.Reason);
    }

    [TestMethod]
    public void Route_WhenButtonIsNotActionOrUseToolButton_RejectsDialogueRoute()
    {
        var router = new NpcDialogueClickRouter();

        var result = router.Route(new NpcDialogueClickRouteRequest(
            IsActionButton: false,
            IsUseToolButton: false,
            IsMouseButton: true,
            TargetNpcName: "Haley",
            HasActiveMenu: false,
            IsDialogueBoxOpen: false,
            ActiveDialogueNpcName: null));

        Assert.IsFalse(result.IsAccepted);
        Assert.IsNull(result.NpcName);
        Assert.AreEqual("unsupported_button", result.Reason);
    }

    [TestMethod]
    public void Route_UseToolKeyboardButtonOnHaley_RejectsDialogueRoute()
    {
        var router = new NpcDialogueClickRouter();

        var result = router.Route(new NpcDialogueClickRouteRequest(
            IsActionButton: false,
            IsUseToolButton: true,
            IsMouseButton: false,
            TargetNpcName: "Haley",
            HasActiveMenu: false,
            IsDialogueBoxOpen: false,
            ActiveDialogueNpcName: null));

        Assert.IsFalse(result.IsAccepted);
        Assert.IsNull(result.NpcName);
        Assert.AreEqual("unsupported_button", result.Reason);
    }
}
