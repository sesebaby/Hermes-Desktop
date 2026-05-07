using Microsoft.VisualStudio.TestTools.UnitTesting;
using StardewHermesBridge.Bridge;

namespace StardewHermesBridge.Tests;

[TestClass]
public sealed class BridgeDebugNpcRepositionerTests
{
    [TestMethod]
    public void ResolveTownDestination_ReturnsTownSquareOnly()
    {
        var result = BridgeNpcDebugRepositioner.ResolveTownDestination();

        Assert.AreEqual("town.square", result.DestinationId);
        Assert.AreEqual("Town", result.LocationName);
        Assert.AreEqual(new TileDto(52, 68), result.Tile);
    }

    [TestMethod]
    public void FindOpenTileNear_WhenAnchorIsOpen_ReturnsAnchor()
    {
        var anchor = new TileDto(52, 68);

        var result = BridgeNpcDebugRepositioner.FindOpenTileNear(anchor, _ => true);

        Assert.AreEqual(anchor, result);
    }

    [TestMethod]
    public void FindOpenTileNear_WhenAnchorBlocked_UsesStableNearbyOrder()
    {
        var anchor = new TileDto(52, 68);
        var expected = new TileDto(53, 68);

        var result = BridgeNpcDebugRepositioner.FindOpenTileNear(
            anchor,
            tile => tile == expected);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void FindOpenTileNear_WhenAllCandidatesBlocked_ReturnsNull()
    {
        var result = BridgeNpcDebugRepositioner.FindOpenTileNear(
            new TileDto(52, 68),
            _ => false);

        Assert.IsNull(result);
    }
}
