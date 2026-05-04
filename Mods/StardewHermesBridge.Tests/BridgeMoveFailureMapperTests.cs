using Microsoft.VisualStudio.TestTools.UnitTesting;
using StardewHermesBridge.Bridge;

namespace StardewHermesBridge.Tests;

[TestClass]
public sealed class BridgeMoveFailureMapperTests
{
    [TestMethod]
    public void FromProbe_RuntimePathEmptyUsesBlockedStepInDiagnosticDetail()
    {
        var target = new TileDto(15, 8);
        var blockedStep = new TileDto(7, 7);
        var probe = new BridgeRouteProbeResult(
            BridgeRouteProbeStatus.PathEmpty,
            Array.Empty<TileDto>(),
            0,
            null,
            "path_empty",
            null);

        var failure = BridgeMoveFailureMapper.FromProbe(
            probe,
            initial: false,
            "HaleyHouse",
            target,
            fallbackTile: blockedStep,
            fallbackFailureKind: "step_tile_open_false");

        Assert.AreEqual("path_blocked", failure.ErrorCode);
        Assert.AreEqual("path_blocked:HaleyHouse:7,7;path_empty", failure.BlockedReason);
    }

    [TestMethod]
    public void FromProbe_InitialPathEmptyUsesTargetTileAndPathUnreachableCode()
    {
        var target = new TileDto(15, 8);
        var probe = new BridgeRouteProbeResult(
            BridgeRouteProbeStatus.PathEmpty,
            Array.Empty<TileDto>(),
            0,
            null,
            "path_empty",
            null);

        var failure = BridgeMoveFailureMapper.FromProbe(
            probe,
            initial: true,
            "HaleyHouse",
            target);

        Assert.AreEqual("path_unreachable", failure.ErrorCode);
        Assert.AreEqual("path_unreachable:HaleyHouse:15,8;path_empty", failure.BlockedReason);
    }
}
