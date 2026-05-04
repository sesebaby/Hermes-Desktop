namespace StardewHermesBridge.Bridge;

using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Pathfinding;

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

    private static bool SameTile(TileDto left, TileDto right)
        => left.X == right.X && left.Y == right.Y;
}
