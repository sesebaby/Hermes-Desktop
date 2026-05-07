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
}
