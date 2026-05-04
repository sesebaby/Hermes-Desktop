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
            "IReadOnlyList<MoveCandidateData>? NearbyTiles",
            "Short fallback move candidates belong to nearby tile status facts during the destination-first transition.");
        StringAssert.Contains(
            httpHost,
            "BuildNearbyTiles(npc, blockedReason)",
            "Bridge status must generate nearby candidates from the current NPC/location state.");
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
        var pathProbe = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeMovementPathProbe.cs");

        StringAssert.Contains(
            pathProbe,
            "PathFindController.findPathForNPCSchedules",
            "NPC movement must borrow Stardew's schedule pathing through the shared route probe instead of walking a naive straight-line path through blocked tiles.");
        StringAssert.Contains(
            commandQueue,
            "ProbeRoute(",
            "Move execution should use the same shared route probe that candidate filtering uses.");
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
            "Cross-location requests must not be faked as completed until the transition state machine is implemented.");
        Assert.IsFalse(
            commandQueue.Contains("npc.currentLocation = targetLocation;", StringComparison.Ordinal),
            "Move execution must not silently transfer the NPC between locations.");
        Assert.IsFalse(
            commandQueue.Contains("Game1.warpCharacter", StringComparison.Ordinal),
            "Move execution must not use warpCharacter to fake cross-location completion.");
    }

    [TestMethod]
    public void MovePumpDoesNotDelegateExecutionToNpcController()
    {
        var commandQueue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");

        Assert.IsFalse(
            commandQueue.Contains("npc.controller = new PathFindController", StringComparison.Ordinal),
            "Bridge should use Stardew pathfinding only as a route probe and keep execution in its own stepper.");
        Assert.IsFalse(
            commandQueue.Contains("started;using=PathFindController", StringComparison.Ordinal),
            "Status logs should not indicate delegated PathFindController execution.");
    }

    [TestMethod]
    public void MoveCommandCanKeepStableErrorCodeSeparateFromDiagnosticBlockedReason()
    {
        var command = new StardewHermesBridge.Bridge.BridgeMoveCommand(
            "cmd-1",
            "trace-1",
            "Haley",
            "HaleyHouse",
            new StardewHermesBridge.Bridge.TileDto(15, 8),
            null,
            null);

        command.Fail("path_blocked", "path_blocked:HaleyHouse:7,7;step_tile_open_false");

        var status = command.ToStatusData();
        Assert.AreEqual("failed", status.Status);
        Assert.AreEqual("path_blocked", status.ErrorCode);
        Assert.AreEqual("path_blocked:HaleyHouse:7,7;step_tile_open_false", status.BlockedReason);
    }

    [TestMethod]
    public void MovePumpReplansRuntimeStepBlocksBeforeTerminalPathBlocked()
    {
        var commandQueue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");

        StringAssert.Contains(
            commandQueue,
            "MaxReplanAttempts = 2",
            "Runtime route blockage must be bounded: initial prepare is free, then at most two replans.");
        StringAssert.Contains(
            commandQueue,
            "route_replanned;blockedStep=",
            "A successful runtime replan should be logged and keep the command running.");
        StringAssert.Contains(
            commandQueue,
            "BridgeMoveFailureMapper.PathBlocked",
            "Runtime step blockage after exhausted or invalid replans must use a stable path_blocked error code.");
        StringAssert.Contains(
            commandQueue,
            "BridgeMoveFailureMapper.FromProbe",
            "Dynamic blocked tile and failure kind belong in BlockedReason, not ErrorCode.");
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
