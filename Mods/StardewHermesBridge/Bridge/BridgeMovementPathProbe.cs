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

    public static RouteProbeData BuildCrossLocationRouteProbe(
        string? currentLocationName,
        TileDto currentTile,
        string targetLocationName,
        TileDto targetTile,
        IReadOnlyList<string>? locationRoute,
        Func<string, TileDto?> warpPointResolver,
        Func<Stack<TileDto>?> schedulePathFactory,
        Func<TileDto, BridgeTileSafetyCheck> routeStepSafetyCheck)
    {
        ArgumentNullException.ThrowIfNull(warpPointResolver);
        ArgumentNullException.ThrowIfNull(schedulePathFactory);
        ArgumentNullException.ThrowIfNull(routeStepSafetyCheck);

        if (locationRoute is null || locationRoute.Count < 2)
        {
            return CrossLocationFailure(
                currentLocationName,
                currentTile,
                targetLocationName,
                targetTile,
                "route_not_found",
                "no location route was found");
        }

        var nextLocationName = locationRoute[1];
        var warpTile = warpPointResolver(nextLocationName);
        if (warpTile is null)
        {
            return CrossLocationFailure(
                currentLocationName,
                currentTile,
                targetLocationName,
                targetTile,
                "warp_point_not_found",
                $"no warp point from {currentLocationName ?? "<unknown>"} to {nextLocationName}");
        }

        var probe = Probe(
            currentTile,
            warpTile,
            _ => BridgeTileSafetyCheck.Safe,
            schedulePathFactory,
            tile => SameTile(tile, warpTile)
                ? BridgeTileSafetyCheck.Safe
                : routeStepSafetyCheck(tile));

        if (probe.Status != BridgeRouteProbeStatus.RouteValid)
        {
            return CrossLocationFailure(
                currentLocationName,
                currentTile,
                targetLocationName,
                targetTile,
                ToCrossLocationSegmentFailureCode(probe),
                probe.FailureDetail,
                probe.Route);
        }

        return new RouteProbeData(
            "cross_location",
            "route_found",
            currentLocationName,
            currentTile,
            targetLocationName,
            targetTile,
            probe.Route,
            new RouteProbeSegmentData(
                currentLocationName ?? locationRoute[0],
                warpTile,
                "warp_to_next_location",
                nextLocationName));
    }

    private static RouteProbeData CrossLocationFailure(
        string? currentLocationName,
        TileDto currentTile,
        string targetLocationName,
        TileDto targetTile,
        string failureCode,
        string? failureDetail,
        IReadOnlyList<TileDto>? route = null)
        => new(
            "cross_location",
            failureCode,
            currentLocationName,
            currentTile,
            targetLocationName,
            targetTile,
            route ?? Array.Empty<TileDto>(),
            null,
            failureCode,
            failureDetail);

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
    {
        if (tile.X < 0 || tile.Y < 0)
            return BridgeTileSafetyCheck.Blocked("step_tile_open_false", "negative_tile");

        return CheckRouteStepSafety(tile, location.isTileLocationOpen(new Vector2(tile.X, tile.Y)));
    }

    public static BridgeTileSafetyCheck CheckRouteStepSafety(TileDto tile, bool isTileLocationOpen)
    {
        if (tile.X < 0 || tile.Y < 0)
            return BridgeTileSafetyCheck.Blocked("step_tile_open_false", "negative_tile");
        if (!isTileLocationOpen)
            return BridgeTileSafetyCheck.Blocked("step_tile_open_false", "tile_location_open_false");

        return BridgeTileSafetyCheck.Safe;
    }

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
        // Temporarily remove terrain feature at the blocked target tile — oversized features
        // (trees, large bushes) can make adjacent tiles appear blocked when they aren't.
        using var _ = new ScopedTerrainFeatureRemoval(location, blockedTarget);

        var passable = new List<(TileDto Tile, int Direction, int Distance)>();
        foreach (var candidate in EnumerateArrivalFallbackCandidates(blockedTarget))
        {
            if (CheckTargetAffordance(location, candidate.Tile).IsSafe)
            {
                var dist = Math.Abs(candidate.Tile.X - currentTile.X) + Math.Abs(candidate.Tile.Y - currentTile.Y);
                passable.Add((candidate.Tile, candidate.Direction, dist));
            }
        }

        return passable
            .OrderBy(p => p.Distance)
            .Select(p => ((TileDto StandTile, int FacingDirection)?)(p.Tile, p.Direction))
            .FirstOrDefault();
    }

    public static (TileDto StandTile, int FacingDirection, BridgeRouteProbeResult Route)? FindClosestReachableNeighbor(
        NPC npc,
        GameLocation location,
        TileDto target,
        TileDto currentTile)
    {
        var candidates = FindPassableNeighbors(location, target)
            .OrderBy(p => Math.Abs(p.Tile.X - currentTile.X) + Math.Abs(p.Tile.Y - currentTile.Y));

        foreach (var candidate in candidates)
        {
            var route = Probe(
                currentTile,
                candidate.Tile,
                tile => CheckTargetAffordance(location, tile),
                () => FindSchedulePath(npc, location, currentTile, candidate.Tile),
                tile => CheckRouteStepSafety(location, tile));
            if (route.Status == BridgeRouteProbeStatus.RouteValid)
                return (candidate.Tile, candidate.Direction, route);
        }

        return null;
    }

    private static IEnumerable<(TileDto Tile, int Direction)> FindPassableNeighbors(GameLocation location, TileDto target)
    {
        using var _ = new ScopedTerrainFeatureRemoval(location, target);
        foreach (var candidate in EnumerateArrivalFallbackCandidates(target))
        {
            if (CheckTargetAffordance(location, candidate.Tile).IsSafe)
                yield return candidate;
        }
    }

    public static IEnumerable<(TileDto Tile, int Direction)> EnumerateArrivalFallbackCandidates(
        TileDto target,
        int maxRadius = 3)
    {
        if (maxRadius < 1)
            yield break;

        var seen = new HashSet<TileDto>();
        foreach (var offset in EnumerateFallbackOffsets(maxRadius))
        {
            var tile = new TileDto(target.X + offset.X, target.Y + offset.Y);
            if (!seen.Add(tile))
                continue;

            yield return (tile, GetFacingDirectionTowardTarget(tile, target));
        }
    }

    private static IEnumerable<(int X, int Y)> EnumerateFallbackOffsets(int maxRadius)
    {
        yield return (1, 0);
        yield return (-1, 0);
        yield return (0, 1);
        yield return (0, -1);

        for (var radius = 2; radius <= maxRadius; radius++)
        {
            yield return (radius, 0);
            yield return (-radius, 0);
            yield return (0, radius);
            yield return (0, -radius);

            var diagonalOffsets = new List<(int X, int Y)>();
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;
                    if (dx == 0 || dy == 0)
                        continue;
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                        continue;

                    diagonalOffsets.Add((dx, dy));
                }
            }

            foreach (var offset in diagonalOffsets
                         .OrderBy(offset => Math.Abs(offset.X) + Math.Abs(offset.Y))
                         .ThenBy(offset => offset.Y < 0 ? 1 : 0)
                         .ThenBy(offset => offset.X < 0 ? 1 : 0)
                         .ThenBy(offset => Math.Abs(offset.X))
                         .ThenBy(offset => Math.Abs(offset.Y)))
            {
                yield return offset;
            }
        }
    }

    private static int GetFacingDirectionTowardTarget(TileDto from, TileDto target)
    {
        var dx = target.X - from.X;
        var dy = target.Y - from.Y;
        if (Math.Abs(dx) >= Math.Abs(dy))
            return dx > 0 ? 1 : 3;

        return dy > 0 ? 2 : 0;
    }

    private static bool SameTile(TileDto left, TileDto right)
        => left.X == right.X && left.Y == right.Y;

    private static string ToCrossLocationSegmentFailureCode(BridgeRouteProbeResult probe)
        => probe.Status == BridgeRouteProbeStatus.TargetUnsafe
            ? "target_tile_unreachable"
            : "segment_path_unreachable";
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
