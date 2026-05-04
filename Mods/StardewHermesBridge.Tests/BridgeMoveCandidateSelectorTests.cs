using Microsoft.VisualStudio.TestTools.UnitTesting;
using StardewHermesBridge.Bridge;

namespace StardewHermesBridge.Tests;

[TestClass]
public sealed class BridgeMoveCandidateSelectorTests
{
    [TestMethod]
    public void DestinationRegistryResolvesStableIdToExecutableMoveTarget()
    {
        Assert.IsTrue(BridgeDestinationRegistry.TryResolve("town.fountain", out var destination));
        Assert.AreEqual("Town", destination.LocationName);
        Assert.AreEqual(new TileDto(47, 56), destination.Tile);
        Assert.AreEqual(2, destination.FacingDirection);
        Assert.AreEqual("Town fountain", destination.Label);
    }

    [TestMethod]
    public void DestinationRegistryRejectsUnknownStableId()
    {
        Assert.IsFalse(BridgeDestinationRegistry.TryResolve("town.not_real", out _));
    }

    [TestMethod]
    public void SelectPlaceCandidates_WhenAnyCuratedRouteValid_ReturnsOnlyValidCuratedCandidates()
    {
        var definitions = new[]
        {
            new BridgePlaceCandidateDefinition("Bedroom mirror", new TileDto(6, 4), new[] { "home" }, "check mirror", 2, "Haley_Mirror"),
            new BridgePlaceCandidateDefinition("Living room", new TileDto(10, 12), new[] { "social" }, "go downstairs", 2, null),
            new BridgePlaceCandidateDefinition("Front door", new TileDto(15, 8), new[] { "transition" }, "consider outside", 2, null)
        };
        var moves = new[]
        {
            new MoveCandidateData("HaleyHouse", new TileDto(11, 8), "nearby"),
            new MoveCandidateData("HaleyHouse", new TileDto(12, 8), "nearby")
        };

        var candidates = BridgeMoveCandidateSelector.SelectPlaceCandidates(
            "HaleyHouse",
            "Haley",
            definitions,
            moves,
            tile => tile.X is 6 or 10);

        CollectionAssert.AreEqual(new[] { "Bedroom mirror", "Living room" }, candidates.Select(candidate => candidate.Label).ToArray());
        Assert.IsFalse(candidates.Any(candidate => candidate.Tags.Contains("nearby")), "Nearby fallback must not fill remaining slots while any curated endpoint is route-valid.");
    }

    [TestMethod]
    public void SelectPlaceCandidates_WhenNoCuratedRouteValid_FallsBackToNearbyMoveCandidates()
    {
        var definitions = new[]
        {
            new BridgePlaceCandidateDefinition("Bedroom mirror", new TileDto(6, 4), new[] { "home" }, "check mirror", 2, "Haley_Mirror"),
            new BridgePlaceCandidateDefinition("Living room", new TileDto(10, 12), new[] { "social" }, "go downstairs", 2, null)
        };
        var moves = new[]
        {
            new MoveCandidateData("HaleyHouse", new TileDto(11, 8), "nearby"),
            new MoveCandidateData("HaleyHouse", new TileDto(12, 8), "nearby"),
            new MoveCandidateData("HaleyHouse", new TileDto(13, 8), "nearby"),
            new MoveCandidateData("HaleyHouse", new TileDto(14, 8), "nearby")
        };

        var candidates = BridgeMoveCandidateSelector.SelectPlaceCandidates(
            "HaleyHouse",
            "Haley",
            definitions,
            moves,
            _ => false);

        Assert.AreEqual(3, candidates.Count);
        Assert.IsTrue(candidates.All(candidate => candidate.Label == "HaleyHouse photo angle"));
        CollectionAssert.AreEqual(
            new[] { new TileDto(11, 8), new TileDto(12, 8), new TileDto(13, 8) },
            candidates.Select(candidate => candidate.Tile).ToArray());
    }

    [TestMethod]
    public void SelectDestinations_FiltersCuratedDefinitionsThroughExecutableRouteResolver()
    {
        var definitions = new[]
        {
            new BridgePlaceCandidateDefinition("Town fountain", new TileDto(47, 56), new[] { "public" }, "photogenic", 2, null, "town.fountain"),
            new BridgePlaceCandidateDefinition("Clinic path", new TileDto(30, 55), new[] { "errands" }, "quiet", 2, null, "town.clinic_path")
        };

        var destinations = BridgeMoveCandidateSelector.SelectDestinations(
            "Town",
            "Haley",
            definitions,
            definition => definition.DestinationId == "town.clinic_path"
                ? new BridgeResolvedDestinationCandidate(new TileDto(27, 54), 1)
                : null);

        Assert.AreEqual(1, destinations.Count);
        Assert.AreEqual("Clinic path", destinations[0].Label);
        Assert.AreEqual("town.clinic_path", destinations[0].DestinationId);
        Assert.AreEqual(new TileDto(27, 54), destinations[0].Tile);
        Assert.AreEqual(1, destinations[0].FacingDirection);
    }
}
