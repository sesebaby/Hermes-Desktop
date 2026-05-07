using Microsoft.VisualStudio.TestTools.UnitTesting;
using StardewHermesBridge.Bridge;

namespace StardewHermesBridge.Tests;

[TestClass]
public sealed class BridgeMovementPathProbeTests
{
    [TestMethod]
    public void ProbeReturnsFirstExecutableStepFirstAndScheduleStackPopsInRouteOrder()
    {
        var route = new[] { new TileDto(6, 5), new TileDto(7, 5), new TileDto(8, 5) };

        var result = BridgeMovementPathProbe.Probe(
            new TileDto(5, 5),
            new TileDto(8, 5),
            _ => BridgeTileSafetyCheck.Safe,
            () => BridgeMovementPathProbe.ToSchedulePath(route),
            _ => BridgeTileSafetyCheck.Safe);

        Assert.AreEqual(BridgeRouteProbeStatus.RouteValid, result.Status);
        CollectionAssert.AreEqual(route, result.Route.ToArray());
        Assert.AreEqual(3, result.PathLength);

        var stack = BridgeMovementPathProbe.ToSchedulePath(result.Route);
        Assert.AreEqual(route[0], stack.Pop(), "Peek/Pop must return Route[0], the first executable step from the current tile.");
        Assert.AreEqual(route[1], stack.Pop());
        Assert.AreEqual(route[2], stack.Pop());
    }

    [TestMethod]
    public void ProbeReportsTargetUnsafeBeforePathfinding()
    {
        var pathfinderCalled = false;
        var target = new TileDto(15, 8);

        var result = BridgeMovementPathProbe.Probe(
            new TileDto(10, 8),
            target,
            _ => BridgeTileSafetyCheck.Blocked("target_tile_open_false", "target closed"),
            () =>
            {
                pathfinderCalled = true;
                return BridgeMovementPathProbe.ToSchedulePath(new[] { new TileDto(11, 8), target });
            },
            _ => BridgeTileSafetyCheck.Safe);

        Assert.AreEqual(BridgeRouteProbeStatus.TargetUnsafe, result.Status);
        Assert.AreEqual(target, result.FailingTile);
        Assert.AreEqual("target_tile_open_false", result.FailureKind);
        Assert.AreEqual("target closed", result.FailureDetail);
        Assert.IsFalse(pathfinderCalled, "A target that is not an endpoint affordance should not pay route pathfinding cost.");
    }

    [TestMethod]
    public void ProbeReportsPathEmptyWhenSchedulePathfinderReturnsNoSteps()
    {
        var result = BridgeMovementPathProbe.Probe(
            new TileDto(5, 5),
            new TileDto(15, 8),
            _ => BridgeTileSafetyCheck.Safe,
            () => new Stack<TileDto>(),
            _ => BridgeTileSafetyCheck.Safe);

        Assert.AreEqual(BridgeRouteProbeStatus.PathEmpty, result.Status);
        Assert.AreEqual(0, result.PathLength);
        Assert.IsNull(result.FailingTile);
        Assert.AreEqual("path_empty", result.FailureKind);
    }

    [TestMethod]
    public void ProbeReportsFirstUnsafeRouteStepWithStepFailureKind()
    {
        var blockedStep = new TileDto(7, 7);
        var route = new[] { new TileDto(6, 7), blockedStep, new TileDto(8, 7) };

        var result = BridgeMovementPathProbe.Probe(
            new TileDto(5, 7),
            new TileDto(8, 7),
            _ => BridgeTileSafetyCheck.Safe,
            () => BridgeMovementPathProbe.ToSchedulePath(route),
            point => point == blockedStep
                ? BridgeTileSafetyCheck.Blocked("step_can_spawn_false", "cannot spawn on route step")
                : BridgeTileSafetyCheck.Safe);

        Assert.AreEqual(BridgeRouteProbeStatus.StepUnsafe, result.Status);
        CollectionAssert.AreEqual(route, result.Route.ToArray());
        Assert.AreEqual(blockedStep, result.FailingTile);
        Assert.AreEqual("step_can_spawn_false", result.FailureKind);
        Assert.AreEqual("cannot spawn on route step", result.FailureDetail);
    }

    [TestMethod]
    public void CheckRouteStepSafety_WhenTileIsOpen_DoesNotRequireSpawnAffordance()
    {
        var result = BridgeMovementPathProbe.CheckRouteStepSafety(
            new TileDto(6, 7),
            isTileLocationOpen: true);

        Assert.IsTrue(
            result.IsSafe,
            "Schedule pathfinder already chose route steps; Bridge must not reject them with CanSpawnCharacterHere-only spawn rules.");
    }

    [TestMethod]
    public void CheckRouteStepSafety_WhenScheduleStepTileLocationIsClosed_TrustsSchedulePath()
    {
        var result = BridgeMovementPathProbe.CheckRouteStepSafety(
            new TileDto(7, 20),
            isTileLocationOpen: false);

        Assert.IsTrue(
            result.IsSafe,
            "Stardew schedule pathfinding has already applied NPC schedule passability; Bridge must not reject returned intermediate steps with generic tile-open checks.");
    }

    [TestMethod]
    public void EnumerateArrivalFallbackCandidates_SearchesBeyondImmediateNeighbors()
    {
        var target = new TileDto(10, 12);
        var candidates = BridgeMovementPathProbe.EnumerateArrivalFallbackCandidates(target, maxRadius: 2)
            .Select(candidate => candidate.Tile)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                new TileDto(11, 12),
                new TileDto(9, 12),
                new TileDto(10, 13),
                new TileDto(10, 11)
            },
            candidates.Take(4).ToArray(),
            "Immediate neighbors must keep the historical preference order.");
        Assert.IsTrue(
            candidates.Contains(new TileDto(12, 12)) || candidates.Contains(new TileDto(11, 13)),
            "Blocked furniture anchors such as HaleyHouse living room need a wider reachable fallback search.");
    }

    [TestMethod]
    public void BuildCrossLocationRouteProbe_ReturnsFirstWarpSegmentWithoutExecutingWarp()
    {
        var currentTile = new TileDto(80, 93);
        var warpTile = new TileDto(80, 94);
        var routeToWarp = new[] { warpTile };

        var result = BridgeMovementPathProbe.BuildCrossLocationRouteProbe(
            "Town",
            currentTile,
            "Beach",
            new TileDto(20, 35),
            new[] { "Town", "Beach" },
            nextLocationName => nextLocationName == "Beach" ? warpTile : null,
            () => BridgeMovementPathProbe.ToSchedulePath(routeToWarp),
            _ => BridgeTileSafetyCheck.Safe);

        Assert.AreEqual("cross_location", result.Mode);
        Assert.AreEqual("route_found", result.Status);
        CollectionAssert.AreEqual(routeToWarp, result.Route.ToArray());
        Assert.IsNotNull(result.NextSegment);
        Assert.AreEqual("Town", result.NextSegment!.LocationName);
        Assert.AreEqual(warpTile, result.NextSegment.StandTile);
        Assert.AreEqual("warp_to_next_location", result.NextSegment.TargetKind);
        Assert.AreEqual("Beach", result.NextSegment.NextLocationName);
        Assert.IsNull(result.FailureCode);
    }

    [TestMethod]
    public void BuildCrossLocationRouteProbe_AllowsClosedWarpTileWhenScheduleRouteReachesWarp()
    {
        var currentTile = new TileDto(8, 6);
        var warpTile = new TileDto(6, 23);
        var routeToWarp = new[] { new TileDto(8, 7), new TileDto(7, 20), warpTile };

        var result = BridgeMovementPathProbe.BuildCrossLocationRouteProbe(
            "HaleyHouse",
            currentTile,
            "Beach",
            new TileDto(20, 35),
            new[] { "HaleyHouse", "Town", "Beach" },
            nextLocationName => nextLocationName == "Town" ? warpTile : null,
            () => BridgeMovementPathProbe.ToSchedulePath(routeToWarp),
            tile => tile == warpTile
                ? BridgeTileSafetyCheck.Blocked("step_tile_open_false", "tile_location_open_false")
                : BridgeTileSafetyCheck.Safe);

        Assert.AreEqual("cross_location", result.Mode);
        Assert.AreEqual("route_found", result.Status);
        CollectionAssert.AreEqual(routeToWarp, result.Route.ToArray());
        Assert.IsNotNull(result.NextSegment);
        Assert.AreEqual(warpTile, result.NextSegment!.StandTile);
        Assert.AreEqual("warp_to_next_location", result.NextSegment.TargetKind);
        Assert.AreEqual("Town", result.NextSegment.NextLocationName);
        Assert.IsNull(result.FailureCode);
    }

    [TestMethod]
    public void BuildCrossLocationRouteProbe_WhenWarpTilePathEmpty_UsesReachableWarpApproachTile()
    {
        var currentTile = new TileDto(20, 89);
        var warpTile = new TileDto(80, 94);
        var approachTile = new TileDto(80, 93);
        var routeToApproach = new[] { new TileDto(21, 89), approachTile };
        var probedTargets = new List<TileDto>();

        var result = BridgeMovementPathProbe.BuildCrossLocationRouteProbe(
            "Town",
            currentTile,
            "Beach",
            new TileDto(20, 35),
            new[] { "Town", "Beach" },
            nextLocationName => nextLocationName == "Beach" ? warpTile : null,
            target =>
            {
                probedTargets.Add(target);
                return target == approachTile
                    ? BridgeMovementPathProbe.ToSchedulePath(routeToApproach)
                    : BridgeMovementPathProbe.ToSchedulePath(Array.Empty<TileDto>());
            },
            _ => BridgeTileSafetyCheck.Safe);

        Assert.AreEqual("cross_location", result.Mode);
        Assert.AreEqual("route_found", result.Status);
        CollectionAssert.AreEqual(routeToApproach, result.Route.ToArray());
        CollectionAssert.AreEqual(
            new[] { warpTile, new TileDto(81, 94), new TileDto(79, 94), new TileDto(80, 95), approachTile },
            probedTargets.Take(5).ToArray(),
            "The probe should try the real warp tile first, then bounded arrival candidates around it.");
        Assert.IsNotNull(result.NextSegment);
        Assert.AreEqual("Town", result.NextSegment!.LocationName);
        Assert.AreEqual(approachTile, result.NextSegment.StandTile);
        Assert.AreEqual(warpTile, result.NextSegment.WarpTriggerTile);
        Assert.AreEqual("warp_to_next_location", result.NextSegment.TargetKind);
        Assert.AreEqual("Beach", result.NextSegment.NextLocationName);
        Assert.IsNull(result.FailureCode);
    }

    [TestMethod]
    public void BuildCrossLocationRouteProbe_TrustsClosedIntermediateScheduleSteps()
    {
        var currentTile = new TileDto(8, 6);
        var blockedStep = new TileDto(7, 20);
        var warpTile = new TileDto(6, 23);
        var routeToWarp = new[] { new TileDto(8, 7), blockedStep, warpTile };

        var result = BridgeMovementPathProbe.BuildCrossLocationRouteProbe(
            "HaleyHouse",
            currentTile,
            "Beach",
            new TileDto(20, 35),
            new[] { "HaleyHouse", "Town", "Beach" },
            nextLocationName => nextLocationName == "Town" ? warpTile : null,
            () => BridgeMovementPathProbe.ToSchedulePath(routeToWarp),
            tile => BridgeMovementPathProbe.CheckRouteStepSafety(
                tile,
                isTileLocationOpen: tile != blockedStep && tile != warpTile));

        Assert.AreEqual("cross_location", result.Mode);
        Assert.AreEqual("route_found", result.Status);
        CollectionAssert.AreEqual(routeToWarp, result.Route.ToArray());
        Assert.IsNotNull(result.NextSegment);
        Assert.AreEqual(warpTile, result.NextSegment!.StandTile);
        Assert.AreEqual("warp_to_next_location", result.NextSegment.TargetKind);
        Assert.AreEqual("Town", result.NextSegment.NextLocationName);
        Assert.IsNull(result.FailureCode);
    }

    [TestMethod]
    public void BuildCrossLocationRouteProbe_WhenWarpPointMissing_ReturnsStableFailure()
    {
        var result = BridgeMovementPathProbe.BuildCrossLocationRouteProbe(
            "Town",
            new TileDto(80, 93),
            "Beach",
            new TileDto(20, 35),
            new[] { "Town", "Beach" },
            _ => null,
            () => BridgeMovementPathProbe.ToSchedulePath(Array.Empty<TileDto>()),
            _ => BridgeTileSafetyCheck.Safe);

        Assert.AreEqual("cross_location", result.Mode);
        Assert.AreEqual("warp_point_not_found", result.Status);
        Assert.AreEqual("warp_point_not_found", result.FailureCode);
        Assert.IsNull(result.NextSegment);
    }

    [TestMethod]
    public void BuildCrossLocationRouteProbe_WhenSegmentPathEmpty_ReturnsStableSegmentFailure()
    {
        var result = BridgeMovementPathProbe.BuildCrossLocationRouteProbe(
            "Town",
            new TileDto(20, 89),
            "Beach",
            new TileDto(20, 35),
            new[] { "Town", "Beach" },
            nextLocationName => nextLocationName == "Beach" ? new TileDto(80, 94) : null,
            () => BridgeMovementPathProbe.ToSchedulePath(Array.Empty<TileDto>()),
            _ => BridgeTileSafetyCheck.Safe);

        Assert.AreEqual("cross_location", result.Mode);
        Assert.AreEqual("segment_path_unreachable", result.Status);
        Assert.AreEqual("segment_path_unreachable", result.FailureCode);
        Assert.IsNull(result.NextSegment);
    }

    [TestMethod]
    public void BuildCrossLocationRouteProbe_WhenIntermediateStepUnsafe_ReportsFailingTileDetail()
    {
        var blockedStep = new TileDto(7, 20);
        var result = BridgeMovementPathProbe.BuildCrossLocationRouteProbe(
            "HaleyHouse",
            new TileDto(8, 6),
            "Beach",
            new TileDto(20, 35),
            new[] { "HaleyHouse", "Town", "Beach" },
            nextLocationName => nextLocationName == "Town" ? new TileDto(6, 23) : null,
            () => BridgeMovementPathProbe.ToSchedulePath(new[] { new TileDto(8, 7), blockedStep, new TileDto(6, 23) }),
            tile => tile == blockedStep
                ? BridgeTileSafetyCheck.Blocked("step_tile_open_false", "tile_location_open_false")
                : BridgeTileSafetyCheck.Safe);

        Assert.AreEqual("cross_location", result.Mode);
        Assert.AreEqual("segment_path_unreachable", result.Status);
        Assert.AreEqual("segment_path_unreachable", result.FailureCode);
        Assert.AreEqual(
            "step_tile_open_false:7,20:tile_location_open_false",
            result.FailureDetail);
        Assert.IsNull(result.NextSegment);
    }
}
