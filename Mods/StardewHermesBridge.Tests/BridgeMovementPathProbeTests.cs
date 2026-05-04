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
}
