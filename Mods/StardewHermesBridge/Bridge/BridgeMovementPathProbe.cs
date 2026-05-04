namespace StardewHermesBridge.Bridge;

using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;
using StardewValley.TerrainFeatures;

internal enum BridgeRouteProbeStatus
{
    RouteValid,
    TargetUnsafe,
    PathEmpty,
    StepUnsafe
}

internal sealed record BridgeRouteProbeResult(
    BridgeRouteProbeStatus Status,
    IReadOnlyList<TileDto> Route,
    int PathLength,
    TileDto? FailingTile,
    string? FailureKind,
    string? FailureDetail);

internal sealed record BridgeTileSafetyCheck(bool IsSafe, string? FailureKind, string? FailureDetail)
{
    public static BridgeTileSafetyCheck Safe { get; } = new(true, null, null);

    public static BridgeTileSafetyCheck Blocked(string failureKind, string? failureDetail = null)
        => new(false, failureKind, failureDetail);
}

internal static class BridgeMovementPathProbe
{
    private const int MaxSchedulePathSteps = 300;

    public static BridgeRouteProbeResult Probe(
        TileDto currentTile,
        TileDto targetTile,
        Func<TileDto, BridgeTileSafetyCheck> targetAffordanceCheck,
        Func<Stack<TileDto>?> schedulePathFactory,
        Func<TileDto, BridgeTileSafetyCheck> routeStepSafetyCheck)
    {
        ArgumentNullException.ThrowIfNull(targetAffordanceCheck);
        ArgumentNullException.ThrowIfNull(schedulePathFactory);
        ArgumentNullException.ThrowIfNull(routeStepSafetyCheck);

        var targetSafety = targetAffordanceCheck(targetTile);
        if (!targetSafety.IsSafe)
        {
            return new BridgeRouteProbeResult(
                BridgeRouteProbeStatus.TargetUnsafe,
                Array.Empty<TileDto>(),
                0,
                targetTile,
                targetSafety.FailureKind,
                targetSafety.FailureDetail);
        }

        if (SameTile(currentTile, targetTile))
        {
            return new BridgeRouteProbeResult(
                BridgeRouteProbeStatus.RouteValid,
                Array.Empty<TileDto>(),
                0,
                null,
                null,
                null);
        }

        var route = ToRoute(schedulePathFactory(), currentTile);
        if (route.Count == 0)
        {
            return new BridgeRouteProbeResult(
                BridgeRouteProbeStatus.PathEmpty,
                route,
                0,
                null,
                "path_empty",
                null);
        }

        foreach (var step in route)
        {
            var stepSafety = routeStepSafetyCheck(step);
            if (stepSafety.IsSafe)
                continue;

            return new BridgeRouteProbeResult(
                BridgeRouteProbeStatus.StepUnsafe,
                route,
                route.Count,
                step,
                stepSafety.FailureKind,
                stepSafety.FailureDetail);
        }

        return new BridgeRouteProbeResult(
            BridgeRouteProbeStatus.RouteValid,
            route,
            route.Count,
            null,
            null,
            null);
    }

    public static Stack<TileDto> FindSchedulePath(
        NPC npc,
        GameLocation location,
        TileDto currentTile,
        TileDto targetTile)
    {
        var rawPath = PathFindController.findPathForNPCSchedules(
            new Point(currentTile.X, currentTile.Y),
            new Point(targetTile.X, targetTile.Y),
            location,
            MaxSchedulePathSteps,
            npc);

        return ToSchedulePath(rawPath, currentTile);
    }

    public static Stack<TileDto> ToSchedulePath(IEnumerable<TileDto> route)
    {
        var stack = new Stack<TileDto>();
        foreach (var tile in route.Reverse())
            stack.Push(tile);
        return stack;
    }

    private static Stack<TileDto> ToSchedulePath(Stack<Point>? rawPath, TileDto currentTile)
        => ToSchedulePath(ToRoute(rawPath, currentTile));

    private static IReadOnlyList<TileDto> ToRoute(Stack<Point>? schedulePath, TileDto currentTile)
    {
        if (schedulePath is null || schedulePath.Count == 0)
            return Array.Empty<TileDto>();

        return TrimCurrentTile(schedulePath
            .ToArray()
            .Select(point => new TileDto(point.X, point.Y))
            .ToArray(), currentTile);
    }

    private static IReadOnlyList<TileDto> ToRoute(Stack<TileDto>? schedulePath, TileDto currentTile)
    {
        if (schedulePath is null || schedulePath.Count == 0)
            return Array.Empty<TileDto>();

        return TrimCurrentTile(schedulePath.ToArray(), currentTile);
    }

    public static BridgeTileSafetyCheck CheckTargetAffordance(GameLocation location, TileDto tile)
        => CheckTileSafety(location, tile, "target");

    public static BridgeTileSafetyCheck CheckRouteStepSafety(GameLocation location, TileDto tile)
        => CheckTileSafety(location, tile, "step");

    private static BridgeTileSafetyCheck CheckTileSafety(GameLocation location, TileDto tile, string scope)
    {
        if (tile.X < 0 || tile.Y < 0)
            return BridgeTileSafetyCheck.Blocked($"{scope}_tile_open_false", "negative_tile");

        var vector = new Vector2(tile.X, tile.Y);
        if (!location.isTileLocationOpen(vector))
            return BridgeTileSafetyCheck.Blocked($"{scope}_tile_open_false", "tile_location_open_false");
        if (!location.CanSpawnCharacterHere(vector))
            return BridgeTileSafetyCheck.Blocked($"{scope}_can_spawn_false", "can_spawn_character_here_false");

        return BridgeTileSafetyCheck.Safe;
    }

    private static IReadOnlyList<TileDto> TrimCurrentTile(IReadOnlyList<TileDto> route, TileDto currentTile)
    {
        var firstStepIndex = 0;
        while (firstStepIndex < route.Count && SameTile(route[firstStepIndex], currentTile))
            firstStepIndex++;

        if (firstStepIndex == 0)
            return route.ToArray();

        return route.Skip(firstStepIndex).ToArray();
    }

    public static (TileDto StandTile, int FacingDirection)? FindClosestPassableNeighbor(
        NPC npc,
        GameLocation location,
        TileDto blockedTarget,
        TileDto currentTile)
    {
        var candidates = new (int X, int Y, int Direction)[]
        {
            (blockedTarget.X + 1, blockedTarget.Y, 3),  // right neighbor, face left toward target
            (blockedTarget.X - 1, blockedTarget.Y, 1),  // left neighbor, face right toward target
            (blockedTarget.X, blockedTarget.Y + 1, 0),  // below neighbor, face up toward target
            (blockedTarget.X, blockedTarget.Y - 1, 2),  // above neighbor, face down toward target
        };

        // Temporarily remove terrain feature at the blocked target tile — oversized features
        // (trees, large bushes) can make adjacent tiles appear blocked when they aren't.
        using var _ = new ScopedTerrainFeatureRemoval(location, blockedTarget);

        var passable = new List<(TileDto Tile, int Direction, int Distance)>();
        foreach (var (x, y, dir) in candidates)
        {
            var tile = new TileDto(x, y);
            if (CheckTargetAffordance(location, tile).IsSafe)
            {
                var dist = Math.Abs(x - currentTile.X) + Math.Abs(y - currentTile.Y);
                passable.Add((tile, dir, dist));
            }
        }

        return passable
            .OrderBy(p => p.Distance)
            .Select(p => ((TileDto StandTile, int FacingDirection)?)(p.Tile, p.Direction))
            .FirstOrDefault();
    }

    private static bool SameTile(TileDto left, TileDto right)
        => left.X == right.X && left.Y == right.Y;
}

internal sealed class ScopedTerrainFeatureRemoval : IDisposable
{
    private readonly GameLocation _location;
    private readonly Vector2 _tile;
    private readonly TerrainFeature? _removed;

    public ScopedTerrainFeatureRemoval(GameLocation location, TileDto tile)
    {
        _location = location;
        _tile = new Vector2(tile.X, tile.Y);
        if (location.terrainFeatures.TryGetValue(_tile, out var feature))
        {
            _removed = feature;
            location.terrainFeatures.Remove(_tile);
        }
    }

    public void Dispose()
    {
        if (_removed is not null)
            _location.terrainFeatures[_tile] = _removed;
    }
}
