namespace StardewHermesBridge.Bridge;

internal sealed record BridgeDestinationDefinition(
    string DestinationId,
    string Label,
    string LocationName,
    TileDto Tile,
    IReadOnlyList<string> Tags,
    string Reason,
    int? FacingDirection,
    string? EndBehavior = null,
    string ArrivalPolicy = "stand_on_or_near_anchor",
    string TransitionPolicy = "same_location_only")
{
    public BridgePlaceCandidateDefinition ToPlaceCandidateDefinition()
        => new(Label, Tile, Tags, Reason, FacingDirection, EndBehavior, DestinationId);
}

internal static class BridgeDestinationRegistry
{
    private static readonly BridgeDestinationDefinition[] Destinations =
    {
        new(
            "haley_house.bedroom_mirror",
            "Bedroom mirror",
            "HaleyHouse",
            new TileDto(6, 4),
            new[] { "home", "photogenic", "Haley" },
            "check her look before deciding whether to go out",
            2,
            "Haley_Mirror"),
        new(
            "haley_house.living_room",
            "Living room",
            "HaleyHouse",
            new TileDto(10, 12),
            new[] { "home", "social" },
            "see what is happening downstairs",
            2),
        new(
            "haley_house.front_door",
            "Front door",
            "HaleyHouse",
            new TileDto(15, 8),
            new[] { "transition", "outdoor" },
            "consider stepping outside",
            2),
        new(
            "town.fountain",
            "Town fountain",
            "Town",
            new TileDto(47, 56),
            new[] { "public", "photogenic", "social" },
            "stand somewhere bright and visible in town",
            2),
        new(
            "town.square",
            "Town square",
            "Town",
            new TileDto(52, 68),
            new[] { "public", "social" },
            "notice who is passing through town",
            2),
        new(
            "town.clinic_path",
            "Clinic path",
            "Town",
            new TileDto(30, 55),
            new[] { "public", "errands" },
            "walk near the town services without committing to a visit",
            2),
        new(
            "beach.shore_photo_spot",
            "Shore photo spot",
            "Beach",
            new TileDto(32, 34),
            new[] { "outdoor", "photogenic", "water" },
            "look for good light near the water",
            2),
        new(
            "beach.bridge",
            "Beach bridge",
            "Beach",
            new TileDto(55, 14),
            new[] { "outdoor", "landmark" },
            "check the beach crossing and horizon",
            2),
        new(
            "forest.path",
            "Forest path",
            "Forest",
            new TileDto(34, 48),
            new[] { "outdoor", "quiet" },
            "walk somewhere quieter and greener",
            2),
        new(
            "mountain.lake_overlook",
            "Lake overlook",
            "Mountain",
            new TileDto(32, 20),
            new[] { "outdoor", "water", "photogenic" },
            "look toward the mountain lake",
            2)
    };

    public static IEnumerable<BridgePlaceCandidateDefinition> GetForLocation(string locationName, string npcName)
    {
        var matched = Destinations
            .Where(destination => string.Equals(destination.LocationName, locationName, StringComparison.OrdinalIgnoreCase))
            .Select(destination => destination.ToPlaceCandidateDefinition())
            .ToArray();

        if (matched.Length > 0)
            return matched;

        return new[]
        {
            new BridgePlaceCandidateDefinition(
                $"{npcName} nearby spot",
                new TileDto(-1, -1),
                new[] { "nearby", "current-location" },
                "fallback nearby place",
                null,
                null)
        };
    }

    public static bool TryResolve(string? destinationId, out BridgeDestinationDefinition destination)
    {
        destination = null!;
        if (string.IsNullOrWhiteSpace(destinationId))
            return false;

        foreach (var candidate in Destinations)
        {
            if (!string.Equals(candidate.DestinationId, destinationId, StringComparison.OrdinalIgnoreCase))
                continue;

            destination = candidate;
            return true;
        }

        return false;
    }
}
