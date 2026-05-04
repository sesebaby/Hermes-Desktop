namespace StardewHermesBridge.Bridge;

internal sealed record BridgePlaceCandidateDefinition(
    string Label,
    TileDto Tile,
    IReadOnlyList<string> Tags,
    string Reason,
    int? FacingDirection,
    string? EndBehavior,
    string? DestinationId = null);

internal sealed record BridgeResolvedDestinationCandidate(TileDto StandTile, int? FacingDirection);

internal static class BridgeMoveCandidateSelector
{
    public static IReadOnlyList<DestinationData> SelectDestinations(
        string locationName,
        string npcName,
        IEnumerable<BridgePlaceCandidateDefinition> definitions)
        => SelectDestinations(
            locationName,
            npcName,
            definitions,
            definition => new BridgeResolvedDestinationCandidate(definition.Tile, definition.FacingDirection));

    public static IReadOnlyList<DestinationData> SelectDestinations(
        string locationName,
        string npcName,
        IEnumerable<BridgePlaceCandidateDefinition> definitions,
        Func<BridgePlaceCandidateDefinition, BridgeResolvedDestinationCandidate?> resolve)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(resolve);

        var curated = new List<DestinationData>();
        foreach (var definition in definitions.Where(definition => definition.Tile.X >= 0 && definition.Tile.Y >= 0))
        {
            var resolved = resolve(definition);
            if (resolved is null)
                continue;

            curated.Add(new DestinationData(
                definition.Label,
                locationName,
                resolved.StandTile,
                definition.Tags,
                definition.Reason,
                resolved.FacingDirection,
                definition.EndBehavior,
                definition.DestinationId));
            if (curated.Count >= 5)
                break;
        }

        return curated.ToArray();
    }

    public static IReadOnlyList<PlaceCandidateData> SelectPlaceCandidates(
        string locationName,
        string npcName,
        IEnumerable<BridgePlaceCandidateDefinition> definitions,
        IReadOnlyList<MoveCandidateData> moveCandidates,
        Func<TileDto, bool> isRouteValid)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(moveCandidates);
        ArgumentNullException.ThrowIfNull(isRouteValid);

        var curatedCandidates = definitions
            .Where(definition => isRouteValid(definition.Tile))
            .Take(3)
            .Select(definition => new PlaceCandidateData(
                definition.Label,
                locationName,
                definition.Tile,
                definition.Tags,
                definition.Reason,
                definition.FacingDirection,
                definition.EndBehavior))
            .ToArray();

        if (curatedCandidates.Length > 0)
            return curatedCandidates;

        var candidates = new List<PlaceCandidateData>();
        var seenTiles = new HashSet<TileDto>();
        foreach (var move in moveCandidates)
        {
            if (!seenTiles.Add(move.Tile))
                continue;

            candidates.Add(new PlaceCandidateData(
                BuildLocalCandidateLabel(locationName, npcName),
                move.LocationName,
                move.Tile,
                new[] { "nearby", "safe", "current-location" },
                "nearby safe spot that can start a self-directed move",
                null,
                null));
            if (candidates.Count >= 3)
                break;
        }

        return candidates;
    }

    private static string BuildLocalCandidateLabel(string locationName, string npcName)
        => string.Equals(npcName, "Haley", StringComparison.OrdinalIgnoreCase)
            ? $"{locationName} photo angle"
            : $"{locationName} nearby spot";
}
