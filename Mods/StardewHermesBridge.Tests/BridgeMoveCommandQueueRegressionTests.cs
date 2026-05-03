using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StardewHermesBridge.Tests;

[TestClass]
public sealed class BridgeMoveCommandQueueRegressionTests
{
    [TestMethod]
    public void StatusQueryExposesShortSafeMoveCandidates()
    {
        var models = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandModels.cs");
        var httpHost = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeHttpHost.cs");

        StringAssert.Contains(
            models,
            "MoveCandidateData",
            "Status data must carry current safe move candidates so the agent can decide before calling another tool.");
        StringAssert.Contains(
            models,
            "IReadOnlyList<MoveCandidateData>? MoveCandidates",
            "Move candidates belong to status observation data, not to a separate event-driven ingress.");
        StringAssert.Contains(
            httpHost,
            "BuildMoveCandidates(npc, blockedReason)",
            "Bridge status must generate candidates from the current NPC/location state.");
        StringAssert.Contains(
            httpHost,
            ".Take(3)",
            "Candidate facts must stay low-noise: at most three current safe targets.");
    }

    [TestMethod]
    public void MovePumpKeepsRunningCommandAcrossTicksBeforeCompletion()
    {
        var commandQueue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");

        StringAssert.Contains(
            commandQueue,
            "private BridgeMoveCommand? _activeMove;",
            "Move execution must keep a running command across UpdateTicked calls instead of completing in the enqueue pump.");
        StringAssert.Contains(
            commandQueue,
            "command.Start();",
            "A queued move must first enter a visible running state.");

        var startIndex = commandQueue.IndexOf("command.Start();", StringComparison.Ordinal);
        var firstRunningReturnIndex = commandQueue.IndexOf("return command.ToStatusData();", startIndex, StringComparison.Ordinal);
        var stepDelayIndex = commandQueue.IndexOf("command.ConsumeStepDelayTick()", startIndex, StringComparison.Ordinal);

        Assert.IsTrue(
            startIndex >= 0 && firstRunningReturnIndex > startIndex && stepDelayIndex > firstRunningReturnIndex,
            "The first running status must be observable before any tile step can complete the move.");
        Assert.IsFalse(
            commandQueue.Contains("command.Start();\r\n        npc.currentLocation?.characters.Remove(npc);", StringComparison.Ordinal),
            "Starting a move must not immediately teleport/remove/re-add the NPC in the same tick.");
    }

    [TestMethod]
    public void MovePumpUsesStardewSchedulePathfindingInsteadOfStraightLineSteps()
    {
        var commandQueue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");

        StringAssert.Contains(
            commandQueue,
            "PathFindController.findPathForNPCSchedules",
            "NPC movement must borrow Stardew's schedule pathing instead of walking a naive straight-line path through blocked tiles.");
        StringAssert.Contains(
            commandQueue,
            "NextScheduleStepFrom",
            "Move commands should consume a prepared schedule path one visible step at a time.");
        Assert.IsFalse(
            commandQueue.Contains("public TileDto NextStepFrom", StringComparison.Ordinal),
            "The old Manhattan stepper bypasses mature Stardew schedule pathing and should not drive NPC movement.");
    }

    [TestMethod]
    public void CrossLocationMoveIsBlockedInsteadOfTeleported()
    {
        var commandQueue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");

        StringAssert.Contains(
            commandQueue,
            "command.Block(\"cross_location_unsupported\")",
            "Phase 1 move only supports same-location short moves; cross-location requests must be blocked.");
        Assert.IsFalse(
            commandQueue.Contains("npc.currentLocation = targetLocation;", StringComparison.Ordinal),
            "Phase 1 must not silently transfer the NPC between locations.");
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
