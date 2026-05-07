using Microsoft.VisualStudio.TestTools.UnitTesting;
using StardewHermesBridge.Bridge;

namespace StardewHermesBridge.Tests;

[TestClass]
public sealed class BridgeCrossMapNavigationStateTests
{
    [TestMethod]
    public void StartCrossMapSegment_RouteFound_ExecutesFirstSegmentAndPreservesFinalTarget()
    {
        var command = new BridgeMoveCommand(
            "cmd-1",
            "trace-1",
            "Haley",
            "Beach",
            new TileDto(20, 35),
            2,
            null);
        var routeProbe = new RouteProbeData(
            "cross_location",
            "route_found",
            "Town",
            new TileDto(80, 93),
            "Beach",
            new TileDto(20, 35),
            new[] { new TileDto(80, 94) },
            new RouteProbeSegmentData(
                "Town",
                new TileDto(80, 94),
                "warp_to_next_location",
                "Beach"));

        command.RecordRouteProbe(routeProbe);
        command.StartCrossMapSegment(routeProbe);

        Assert.AreEqual("running", command.Status);
        Assert.IsNull(command.BlockedReason);
        Assert.IsFalse(
            string.Equals(command.ErrorCode, "cross_location_unsupported", StringComparison.OrdinalIgnoreCase),
            "Route-found cross-location commands must execute the first segment instead of reusing the probe-phase blocker.");
        Assert.AreEqual("executing_segment", command.Phase);
        Assert.AreEqual("executing_segment", command.CrossMapPhase);
        Assert.IsNotNull(command.FinalTarget);
        Assert.AreEqual("Beach", command.FinalTarget!.LocationName);
        Assert.AreEqual(20, command.FinalTarget.Tile.X);
        Assert.AreEqual(35, command.FinalTarget.Tile.Y);
        Assert.AreEqual(2, command.FinalTarget.FacingDirection);
        Assert.IsNotNull(command.CurrentSegment);
        Assert.AreEqual("Town", command.CurrentSegment!.LocationName);
        Assert.AreEqual("warp_to_next_location", command.CurrentSegment.TargetKind);
        Assert.AreEqual("Beach", command.CurrentSegment.NextLocationName);
        Assert.AreEqual(80, command.CurrentSegment.TargetTile?.X);
        Assert.AreEqual(94, command.CurrentSegment.TargetTile?.Y);
        Assert.AreEqual(80, command.TargetTile.X);
        Assert.AreEqual(94, command.TargetTile.Y);

        var status = command.ToStatusData();
        Assert.AreEqual("executing_segment", status.CrossMapPhase);
        Assert.IsNotNull(status.FinalTarget);
        Assert.AreEqual("Beach", status.FinalTarget!.LocationName);
        Assert.AreEqual(20, status.FinalTarget.Tile.X);
        Assert.IsNotNull(status.CurrentSegment);
        Assert.AreEqual("Town", status.CurrentSegment!.LocationName);
        Assert.AreEqual("warp_to_next_location", status.CurrentSegment.TargetKind);
        Assert.IsNotNull(status.RouteProbe);
        Assert.AreEqual("route_found", status.RouteProbe!.Status);
    }

    [TestMethod]
    public void BeginAwaitingWarp_KeepsCommandRunningAndFailsWithStableTimeoutCode()
    {
        var command = CreateRunningCrossMapCommand();

        command.BeginAwaitingWarp(currentTick: 100, timeoutTicks: 2);

        Assert.AreEqual("running", command.Status);
        Assert.AreEqual("awaiting_warp", command.Phase);
        Assert.AreEqual("awaiting_warp", command.CrossMapPhase);
        Assert.AreEqual("Town", command.CurrentLocationName);
        Assert.IsFalse(command.HasWarpTransitionTimedOut(currentTick: 101));
        Assert.IsTrue(command.HasWarpTransitionTimedOut(currentTick: 103));

        command.FailWarpTransitionTimeout(currentLocationName: "Town");

        Assert.AreEqual("failed", command.Status);
        Assert.AreEqual("warp_transition_timeout", command.ErrorCode);
        Assert.AreEqual("warp_transition_timeout", command.LastFailureCode);
        Assert.AreEqual(
            "warp_transition_timeout:expected=Beach;actual=Town",
            command.BlockedReason);
    }

    [TestMethod]
    public void CompleteAwaitingWarp_WhenExpectedLocationObserved_ReplansWithoutCompletingCommand()
    {
        var command = CreateRunningCrossMapCommand();
        command.BeginAwaitingWarp(currentTick: 100, timeoutTicks: 60);

        var observed = command.TryCompleteAwaitingWarp(currentLocationName: "Beach");

        Assert.IsTrue(observed);
        Assert.AreEqual("running", command.Status);
        Assert.AreEqual("replanning_after_warp", command.Phase);
        Assert.AreEqual("replanning_after_warp", command.CrossMapPhase);
        Assert.AreEqual("Beach", command.CurrentLocationName);
        Assert.AreEqual("Beach", command.FinalTarget?.LocationName);
        Assert.AreEqual(20, command.FinalTarget?.Tile.X);
        Assert.AreEqual(35, command.FinalTarget?.Tile.Y);
    }

    [TestMethod]
    public void FailUnexpectedWarpLocation_UsesStableFailureCodeAndDiagnostic()
    {
        var command = CreateRunningCrossMapCommand();
        command.BeginAwaitingWarp(currentTick: 100, timeoutTicks: 60);

        command.FailUnexpectedWarpLocation(currentLocationName: "Forest");

        Assert.AreEqual("failed", command.Status);
        Assert.AreEqual("unexpected_location_after_warp", command.ErrorCode);
        Assert.AreEqual("unexpected_location_after_warp", command.LastFailureCode);
        Assert.AreEqual(
            "unexpected_location_after_warp:expected=Beach;actual=Forest",
            command.BlockedReason);
    }

    [TestMethod]
    public void StartPostWarpFinalSegment_UsesSavedFinalTargetInsteadOfPreviousWarpTile()
    {
        var command = CreateRunningCrossMapCommand();
        command.BeginAwaitingWarp(currentTick: 100, timeoutTicks: 60);
        command.TryCompleteAwaitingWarp(currentLocationName: "Beach");

        var started = command.TryStartPostWarpFinalTargetSegment(
            "Beach",
            new BridgeRouteProbeResult(
                BridgeRouteProbeStatus.RouteValid,
                new[] { new TileDto(21, 35), new TileDto(20, 35) },
                2,
                null,
                null,
                null),
            out var failureCode);

        Assert.IsTrue(started);
        Assert.IsNull(failureCode);
        Assert.AreEqual("running", command.Status);
        Assert.AreEqual("executing_segment", command.Phase);
        Assert.AreEqual("executing_segment", command.CrossMapPhase);
        Assert.AreEqual("Beach", command.CurrentLocationName);
        Assert.IsNotNull(command.CurrentSegment);
        Assert.AreEqual("Beach", command.CurrentSegment!.LocationName);
        Assert.AreEqual("final_target_tile", command.CurrentSegment.TargetKind);
        Assert.IsNull(command.CurrentSegment.NextLocationName);
        Assert.AreEqual(20, command.CurrentSegment.TargetTile?.X);
        Assert.AreEqual(35, command.CurrentSegment.TargetTile?.Y);
        Assert.AreEqual(20, command.TargetTile.X);
        Assert.AreEqual(35, command.TargetTile.Y);
        Assert.AreEqual("Beach", command.FinalTarget?.LocationName);
    }

    [TestMethod]
    public void StartNextCrossMapSegment_AfterWarp_KeepsOriginalFinalTarget()
    {
        var command = new BridgeMoveCommand(
            "cmd-2",
            "trace-1",
            "Haley",
            "IslandSouth",
            new TileDto(10, 20),
            2,
            null);
        var firstProbe = new RouteProbeData(
            "cross_location",
            "route_found",
            "Town",
            new TileDto(80, 93),
            "IslandSouth",
            new TileDto(10, 20),
            new[] { new TileDto(80, 94) },
            new RouteProbeSegmentData(
                "Town",
                new TileDto(80, 94),
                "warp_to_next_location",
                "Beach"));
        command.RecordRouteProbe(firstProbe);
        command.StartCrossMapSegment(firstProbe);
        command.BeginAwaitingWarp(currentTick: 100, timeoutTicks: 60);
        command.TryCompleteAwaitingWarp(currentLocationName: "Beach");

        var secondProbe = new RouteProbeData(
            "cross_location",
            "route_found",
            "Beach",
            new TileDto(20, 35),
            "IslandSouth",
            new TileDto(10, 20),
            new[] { new TileDto(100, 30) },
            new RouteProbeSegmentData(
                "Beach",
                new TileDto(100, 30),
                "warp_to_next_location",
                "IslandSouth"));

        command.StartCrossMapSegment(secondProbe);

        Assert.AreEqual("IslandSouth", command.FinalTarget?.LocationName);
        Assert.AreEqual(10, command.FinalTarget?.Tile.X);
        Assert.AreEqual(20, command.FinalTarget?.Tile.Y);
        Assert.AreEqual("Beach", command.CurrentSegment?.LocationName);
        Assert.AreEqual("IslandSouth", command.CurrentSegment?.NextLocationName);
        Assert.AreEqual(100, command.TargetTile.X);
        Assert.AreEqual(30, command.TargetTile.Y);
    }

    private static BridgeMoveCommand CreateRunningCrossMapCommand()
    {
        var command = new BridgeMoveCommand(
            "cmd-1",
            "trace-1",
            "Haley",
            "Beach",
            new TileDto(20, 35),
            2,
            null);
        var routeProbe = new RouteProbeData(
            "cross_location",
            "route_found",
            "Town",
            new TileDto(80, 93),
            "Beach",
            new TileDto(20, 35),
            new[] { new TileDto(80, 94) },
            new RouteProbeSegmentData(
                "Town",
                new TileDto(80, 94),
                "warp_to_next_location",
                "Beach"));

        command.RecordRouteProbe(routeProbe);
        command.StartCrossMapSegment(routeProbe);
        return command;
    }
}
