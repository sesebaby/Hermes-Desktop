using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StardewHermesBridge.Tests;

[TestClass]
public sealed class BridgeIdleMicroActionQueueRegressionTests
{
    [TestMethod]
    public void QueueDefinesWhitelistAndAnimationAllowlist()
    {
        var queue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");

        StringAssert.Contains(queue, "AllowedIdleMicroKinds", "Idle micro actions must stay whitelist-only in the bridge queue.");
        StringAssert.Contains(queue, "\"idle_animation_once\"", "The queue should treat idle_animation_once as an explicit contract kind.");
        StringAssert.Contains(queue, "AllowedIdleAnimationAliases", "Structured animation aliases should be allowlisted rather than raw frame-driven.");
        StringAssert.Contains(queue, "\"idle_tinker\"", "The initial allowlist should name concrete reviewed aliases.");
    }

    [TestMethod]
    public void QueueNormalizesDisplayedSkippedBlockedAndInterruptedOutcomes()
    {
        var queue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");

        StringAssert.Contains(queue, "IdleMicroDisplayed", "Displayed should be a dedicated normalized result sink.");
        StringAssert.Contains(queue, "IdleMicroSkipped", "Skipped should stay encoded as completed + result=skipped.");
        StringAssert.Contains(queue, "IdleMicroBlocked", "Blocked should keep a dedicated response path.");
        StringAssert.Contains(queue, "IdleMicroInterrupted", "Interrupted should keep a dedicated response path.");
        StringAssert.Contains(queue, "new { result = \"displayed\" }", "Displayed responses should persist their normalized result.");
        StringAssert.Contains(queue, "new { result = \"skipped\", reasonCode }", "Skipped responses should keep a reasonCode payload.");
        StringAssert.Contains(queue, "new { result = \"blocked\", reasonCode }", "Blocked responses should keep a reasonCode payload.");
        StringAssert.Contains(queue, "new { result = \"interrupted\", reasonCode }", "Interrupted responses should keep a reasonCode payload.");
    }

    [TestMethod]
    public void QueueGuardsVisibilityBusyMenuAndMoveInterruption()
    {
        var queue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");

        StringAssert.Contains(queue, "IsNpcVisibleToPlayer", "Idle micro action execution must gate on player-visible same-map context.");
        StringAssert.Contains(queue, "\"not_visible\"", "Invisible NPC idle actions should complete as skipped, not display.");
        StringAssert.Contains(queue, "\"npc_busy\"", "Moving NPCs should be blocked instead of pretending to idle.");
        StringAssert.Contains(queue, "\"menu_blocked\"", "Open menus must block idle micro action execution.");
        StringAssert.Contains(queue, "\"event_active\"", "Active events must block idle micro action execution.");
        StringAssert.Contains(queue, "\"move_started\"", "An active move for the same NPC should interrupt the idle micro action.");
    }

    [TestMethod]
    public void QueueRoutesPresentationThroughDedicatedIdleMicroOverlayChannel()
    {
        var queue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");
        var overlay = ReadRepositoryFile("Mods", "StardewHermesBridge", "Ui", "NpcOverheadBubbleOverlay.cs");

        StringAssert.Contains(queue, "_bubbleOverlay.ShowIdleMicro", "Idle micro presentation should use a dedicated overlay helper.");
        StringAssert.Contains(overlay, "IdleMicroChannel = \"idle_micro\"", "The overlay should keep an explicit idle_micro channel.");
        StringAssert.Contains(overlay, "ShowIdleMicro", "Idle micro bubbles need a named entrypoint instead of reusing private-chat semantics.");
    }

    private static string ReadRepositoryFile(params string[] relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(relativePath).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            directory = directory.Parent;
        }

        Assert.Fail($"Could not find repository file: {Path.Combine(relativePath)}");
        return string.Empty;
    }
}
