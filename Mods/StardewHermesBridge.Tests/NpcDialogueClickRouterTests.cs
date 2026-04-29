using Microsoft.VisualStudio.TestTools.UnitTesting;
using StardewHermesBridge.Dialogue;

namespace StardewHermesBridge.Tests;

[TestClass]
public class NpcDialogueClickRouterTests
{
    [TestMethod]
    public void Route_LeftClickOnHaley_AcceptsDialogueRoute()
    {
        var router = new NpcDialogueClickRouter();

        var result = router.Route(new NpcDialogueClickRouteRequest(true, "Haley", false));

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("Haley", result.NpcName);
        Assert.AreEqual("accepted", result.Reason);
    }

    [TestMethod]
    public void Route_ClickOnGround_RejectsDialogueRoute()
    {
        var router = new NpcDialogueClickRouter();

        var result = router.Route(new NpcDialogueClickRouteRequest(true, null, false));

        Assert.IsFalse(result.IsAccepted);
        Assert.IsNull(result.NpcName);
        Assert.AreEqual("no_npc_hit", result.Reason);
    }

    [TestMethod]
    public void Route_LeftClickOnPenny_RejectsDialogueRouteBecauseNpcIsNotEnabled()
    {
        var router = new NpcDialogueClickRouter();

        var result = router.Route(new NpcDialogueClickRouteRequest(true, "Penny", false));

        Assert.IsFalse(result.IsAccepted);
        Assert.IsNull(result.NpcName);
        Assert.AreEqual("npc_not_enabled", result.Reason);
    }

    [TestMethod]
    public void Route_WhenMenuAlreadyOpen_RejectsDialogueRoute()
    {
        var router = new NpcDialogueClickRouter();

        var result = router.Route(new NpcDialogueClickRouteRequest(true, "Haley", true));

        Assert.IsFalse(result.IsAccepted);
        Assert.IsNull(result.NpcName);
        Assert.AreEqual("menu_open", result.Reason);
    }

    [TestMethod]
    public void Route_WhenButtonIsNotPrimary_RejectsDialogueRoute()
    {
        var router = new NpcDialogueClickRouter();

        var result = router.Route(new NpcDialogueClickRouteRequest(false, "Haley", false));

        Assert.IsFalse(result.IsAccepted);
        Assert.IsNull(result.NpcName);
        Assert.AreEqual("unsupported_button", result.Reason);
    }
}
