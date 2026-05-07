using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StardewHermesBridge.Tests;

[TestClass]
public sealed class BridgeMoveCommandQueueRegressionTests
{
    [TestMethod]
    public void TaskStatusDataCarriesRouteProbeButMoveAcceptedDataDoesNot()
    {
        var probe = new StardewHermesBridge.Bridge.RouteProbeData(
            "same_location",
            "route_found",
            "Town",
            new StardewHermesBridge.Bridge.TileDto(42, 17),
            "Beach",
            new StardewHermesBridge.Bridge.TileDto(20, 35),
            new[] { new StardewHermesBridge.Bridge.TileDto(43, 17) },
            new StardewHermesBridge.Bridge.RouteProbeSegmentData(
                "Town",
                new StardewHermesBridge.Bridge.TileDto(43, 17),
                "warp",
                "Beach"));
        var status = new StardewHermesBridge.Bridge.TaskStatusData(
            "cmd-1",
            "trace-1",
            "Haley",
            "move",
            "blocked",
            null,
            0,
            0,
            "route_found",
            null,
            RouteProbe: probe);

        Assert.AreSame(probe, status.RouteProbe);
        Assert.IsNotNull(status.RouteProbe);
        Assert.AreEqual("route_found", status.RouteProbe!.Status);
        Assert.AreEqual("Town", status.RouteProbe.NextSegment?.LocationName);
        Assert.IsNull(
            typeof(StardewHermesBridge.Bridge.MoveAcceptedData).GetProperty("RouteProbe"),
            "Route probe belongs to task_status data, not the accepted response.");
    }

    [TestMethod]
    public void FormatRouteProbeLogDetail_WithCrossLocationRoute_IncludesHumanReadableProbeSummary()
    {
        var probe = new StardewHermesBridge.Bridge.RouteProbeData(
            "cross_location",
            "route_found",
            "Town",
            new StardewHermesBridge.Bridge.TileDto(50, 66),
            "Beach",
            new StardewHermesBridge.Bridge.TileDto(32, 34),
            new[] { new StardewHermesBridge.Bridge.TileDto(80, 94) },
            new StardewHermesBridge.Bridge.RouteProbeSegmentData(
                "Town",
                new StardewHermesBridge.Bridge.TileDto(80, 94),
                "warp_to_next_location",
                "Beach"));

        var detail = StardewHermesBridge.Bridge.BridgeCommandQueue.FormatRouteProbeLogDetail(probe);

        Assert.AreEqual(
            "routeProbeStatus=route_found;mode=cross_location;from=Town:50,66;target=Beach:32,34;next=Town:80,94->Beach(warp_to_next_location);routeSteps=1;failure=-",
            detail);
    }

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
            "BuildNearbyTiles(npc, blockedReason, currentTile)",
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

        Assert.IsTrue(
            startIndex >= 0 && firstRunningReturnIndex > startIndex,
            "The first running status must be observable before any movement tick can complete the move.");
        Assert.IsFalse(
            commandQueue.Contains("command.Start();\r\n        npc.currentLocation?.characters.Remove(npc);", StringComparison.Ordinal),
            "Starting a move must not immediately teleport/remove/re-add the NPC in the same tick.");
    }

    [TestMethod]
    public void MoveQueueResolvesDestinationIdBeforeLegacyTargetFallback()
    {
        var commandQueue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");

        StringAssert.Contains(
            commandQueue,
            "BridgeDestinationRegistry.TryResolve(payload.DestinationId",
            "Bridge must be able to resolve a known destinationId without requiring Agent-supplied target coordinates.");
        StringAssert.Contains(
            commandQueue,
            "invalid_destination_id",
            "Unknown destinationId should fail at the Bridge boundary with a stable error code.");
        StringAssert.Contains(
            commandQueue,
            "payload.Target",
            "Legacy target payload remains a fallback during the destination-first transition.");
    }

    [TestMethod]
    public void DestinationRegistryOwnsCuratedDestinationsUsedByStatusAndMove()
    {
        var registry = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeDestinationRegistry.cs");
        var httpHost = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeHttpHost.cs");
        var commandQueue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");

        StringAssert.Contains(registry, "town.fountain");
        StringAssert.Contains(registry, "haley_house.bedroom_mirror");
        StringAssert.Contains(httpHost, "BridgeDestinationRegistry.GetForLocation");
        StringAssert.Contains(commandQueue, "BridgeDestinationRegistry.TryResolve");
        Assert.IsFalse(
            httpHost.Contains("private static IEnumerable<BridgePlaceCandidateDefinition> BuildPlaceCandidateDefinitions", StringComparison.Ordinal),
            "Destination definitions should live in the shared registry, not inside BridgeHttpHost.");
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
    public void CrossLocationMoveExecutesFirstSegmentInsteadOfStoppingAtProbeBoundary()
    {
        var commandQueue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");

        StringAssert.Contains(
            commandQueue,
            "ProbeCrossLocationRoute(",
            "Cross-location requests should run a real route probe before blocking execution.");
        StringAssert.Contains(
            commandQueue,
            "WarpPathfindingCache.GetLocationRoute",
            "Cross-location route probes should use Stardew's location route cache rather than hardcoded routes.");
        StringAssert.Contains(
            commandQueue,
            "currentLocation.getWarpPointTo",
            "Cross-location route probes should resolve the next warp tile for task_status diagnostics.");
        StringAssert.Contains(
            commandQueue,
            "StartCrossMapSegment",
            "Route-found cross-location requests must execute the current-map warp segment before waiting for transition support.");
        Assert.IsFalse(
            commandQueue.Contains("command.Block(\"cross_location_unsupported\")", StringComparison.Ordinal),
            "The probe-phase cross-location blocker must not remain on the route-found execution path.");
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
    public void MovePumpUsesNaturalAnimatedPixelWalkingInsteadOfTileTeleport()
    {
        var commandQueue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");

        StringAssert.Contains(
            commandQueue,
            "Utility.getVelocityTowardPoint",
            "Natural movement should follow TheStardewSquad style velocity toward the next tile center.");
        StringAssert.Contains(
            commandQueue,
            "var moveSpeed = Math.Max(1, npc.speed)",
            "Bridge-owned movement must keep moving even if the NPC's current speed was zeroed by vanilla state.");
        StringAssert.Contains(
            commandQueue,
            "npc.Position +=",
            "Movement should advance the NPC in pixels instead of snapping directly to the next tile.");
        StringAssert.Contains(
            commandQueue,
            "npc.animateInFacingDirection(Game1.currentGameTime)",
            "Visible walking frames must be advanced while Hermes owns movement.");
        StringAssert.Contains(
            commandQueue,
            "MaintainNpcMovementControl(npc)",
            "Bridge should clear vanilla movement controllers during owned movement, matching the reference mod's control pattern.");
        Assert.IsFalse(
            commandQueue.Contains("npc.setTilePosition", StringComparison.Ordinal),
            "The main move path must not teleport one tile at a time.");
        Assert.IsFalse(
            commandQueue.Contains("private const int StepDelayTicks", StringComparison.Ordinal),
            "Natural walking should move continuously each update tick, not wait several ticks between tile snaps.");
    }

    [TestMethod]
    public void MovePumpClearsVanillaVelocityWhenTakingAndReleasingControl()
    {
        var commandQueue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");

        StringAssert.Contains(
            commandQueue,
            "StopNpcMotion(npc)",
            "Bridge-owned movement must clear vanilla movement velocity when it takes or releases control; otherwise completed moves can keep drifting under vanilla update.");
        StringAssert.Contains(
            commandQueue,
            "npc.xVelocity = 0f;",
            "Clearing xVelocity is required after manual npc.Position movement to prevent east/west drift after completion.");
        StringAssert.Contains(
            commandQueue,
            "npc.yVelocity = 0f;",
            "Clearing yVelocity is required after manual npc.Position movement to prevent north/south drift after completion.");
        StringAssert.Contains(
            commandQueue,
            "npc.Sprite.StopAnimation();",
            "Terminal movement cleanup should leave the NPC in an idle sprite state, matching the reference mod cleanup pattern.");
    }

    [TestMethod]
    public void StatusQueryTreatsMovingNpcAsTemporarilyUnavailable()
    {
        var httpHost = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeHttpHost.cs");

        StringAssert.Contains(
            httpHost,
            "BuildBlockedReason(npc)",
            "Availability must include the requested NPC's movement state, not only global menu/event state.");
        StringAssert.Contains(
            httpHost,
            "npc.isMoving()",
            "A vanilla-scheduled or residual moving NPC must be visible as unavailable before the Agent sends another move.");
        StringAssert.Contains(
            httpHost,
            "\"npc_moving\"",
            "Moving NPCs should expose a stable blockedReason so the Agent observes instead of stacking more movement commands.");
    }

    [TestMethod]
    public void StatusQueryUsesNpcTilePointForCurrentTileAndNearbyCandidates()
    {
        var httpHost = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeHttpHost.cs");

        StringAssert.Contains(
            httpHost,
            "GetCurrentTile(npc)",
            "Status and move execution must share the same current-tile source; pixel position can be between tiles during animated walking.");
        Assert.IsFalse(
            httpHost.Contains("npc.Position.X / Game1.tileSize", StringComparison.Ordinal),
            "Status must not derive NPC tile facts from raw pixel position while Bridge movement uses TilePoint.");
    }

    [TestMethod]
    public void StatusQueryOnlyExposesReachableDestinationStandTiles()
    {
        var httpHost = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeHttpHost.cs");
        var selector = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeMoveCandidateSelector.cs");

        StringAssert.Contains(
            httpHost,
            "ResolveDestinationCandidate",
            "Curated semantic destinations must be resolved against current NPC route state before status exposes them.");
        StringAssert.Contains(
            httpHost,
            "FindClosestReachableNeighbor",
            "Blocked anchors such as Town fountain must expose a reachable stand tile or be hidden.");
        StringAssert.Contains(
            selector,
            "BridgeResolvedDestinationCandidate",
            "The selector should publish executable stand tiles, not raw decorative anchors.");
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
    public void MovePayloadCarriesOptionalThoughtForOverheadBubble()
    {
        var models = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandModels.cs");
        var commandQueue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");

        StringAssert.Contains(
            models,
            "string? Thought",
            "Move payload should carry the NPC's short movement thought through the bridge contract.");
        StringAssert.Contains(
            commandQueue,
            "_bubbleOverlay.ShowMoveThought",
            "Move start should display the optional movement thought through the existing overhead bubble overlay.");
        StringAssert.Contains(
            commandQueue,
            "command.Thought",
            "The queued move command must retain the thought until the game-loop move pump starts execution.");
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
