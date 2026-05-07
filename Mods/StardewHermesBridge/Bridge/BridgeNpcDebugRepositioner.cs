namespace StardewHermesBridge.Bridge;

using Microsoft.Xna.Framework;
using StardewHermesBridge.Logging;
using StardewModdingAPI;
using StardewValley;

internal static class BridgeNpcDebugRepositioner
{
    private const string TownDestinationId = "town.square";
    private const int SearchRadius = 3;

    public static BridgeDebugRepositionResult RepositionToTown(string? rawNpcId, SmapiBridgeLogger? logger = null)
    {
        var requestedNpc = string.IsNullOrWhiteSpace(rawNpcId) ? "" : rawNpcId.Trim();
        if (!Context.IsWorldReady || Game1.player is null)
            return Failed(requestedNpc, null, "world_not_ready", "The Stardew world is not ready.", logger);

        if (!NpcTargetNameResolver.TryResolveKnownAlias(requestedNpc, out var npcName) || string.IsNullOrWhiteSpace(npcName))
            return Failed(requestedNpc, null, "invalid_target", "Only Haley and Penny are supported by this debug action.", logger);

        var npc = BridgeNpcResolver.Resolve(npcName);
        if (npc?.currentLocation is null)
            return Failed(npcName, null, "invalid_target", "NPC was not found in the current world.", logger);

        var destination = ResolveTownDestination();
        var targetLocation = Game1.getLocationFromName(destination.LocationName);
        if (targetLocation is null)
            return Failed(npcName, null, "location_not_found", "Town location was not found.", logger);

        var targetTile = FindOpenTileNear(
            destination.Tile,
            tile => IsOpenCharacterTile(targetLocation, tile));
        if (targetTile is null)
            return Failed(npcName, null, "target_blocked", "No open Town tile was found near town.square.", logger);

        var fromLocation = npc.currentLocation.NameOrUniqueName ?? npc.currentLocation.Name ?? "unknown";
        var fromTile = new TileDto(npc.TilePoint.X, npc.TilePoint.Y);
        logger?.Write(
            "debug_npc_reposition_started",
            npcName,
            "debug_reposition",
            "debug_npc_reposition",
            null,
            "started",
            $"from={fromLocation}:{fromTile.X},{fromTile.Y};target={destination.LocationName}:{targetTile.X},{targetTile.Y}");

        StopNpcMotion(npc);
        Game1.warpCharacter(npc, destination.LocationName, new Point(targetTile.X, targetTile.Y));
        StopNpcMotion(npc);
        if (destination.FacingDirection is >= 0 and <= 3)
            npc.faceDirection(destination.FacingDirection.Value);

        logger?.Write(
            "debug_npc_reposition_completed",
            npcName,
            "debug_reposition",
            "debug_npc_reposition",
            null,
            "completed",
            $"from={fromLocation}:{fromTile.X},{fromTile.Y};target={destination.LocationName}:{targetTile.X},{targetTile.Y};debugTeleport=true");

        return new BridgeDebugRepositionResult(
            true,
            "completed",
            npcName,
            fromLocation,
            fromTile,
            destination.LocationName,
            targetTile,
            destination.FacingDirection,
            null,
            null);
    }

    public static BridgeDestinationDefinition ResolveTownDestination()
    {
        if (!BridgeDestinationRegistry.TryResolve(TownDestinationId, out var destination))
            throw new InvalidOperationException("town.square destination is missing from BridgeDestinationRegistry.");

        return destination;
    }

    public static TileDto? FindOpenTileNear(TileDto anchor, Func<TileDto, bool> isOpen)
    {
        ArgumentNullException.ThrowIfNull(isOpen);

        foreach (var candidate in EnumerateCandidates(anchor))
        {
            if (isOpen(candidate))
                return candidate;
        }

        return null;
    }

    private static IEnumerable<TileDto> EnumerateCandidates(TileDto anchor)
    {
        yield return anchor;
        yield return new TileDto(anchor.X + 1, anchor.Y);
        yield return new TileDto(anchor.X - 1, anchor.Y);
        yield return new TileDto(anchor.X, anchor.Y + 1);
        yield return new TileDto(anchor.X, anchor.Y - 1);

        for (var radius = 2; radius <= SearchRadius; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                        continue;

                    yield return new TileDto(anchor.X + dx, anchor.Y + dy);
                }
            }
        }
    }

    private static bool IsOpenCharacterTile(GameLocation location, TileDto tile)
    {
        var vector = new Vector2(tile.X, tile.Y);
        return location.isTileLocationOpen(vector) && location.CanSpawnCharacterHere(vector);
    }

    private static BridgeDebugRepositionResult Failed(
        string npcId,
        TileDto? targetTile,
        string code,
        string message,
        SmapiBridgeLogger? logger)
    {
        logger?.Write(
            "debug_npc_reposition_failed",
            string.IsNullOrWhiteSpace(npcId) ? null : npcId,
            "debug_reposition",
            "debug_npc_reposition",
            null,
            "failed",
            $"code={code};target=Town:{targetTile?.X.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}," +
            $"{targetTile?.Y.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}");
        return new BridgeDebugRepositionResult(
            false,
            "failed",
            npcId,
            null,
            null,
            "Town",
            targetTile,
            null,
            code,
            message);
    }

    private static void StopNpcMotion(NPC npc)
    {
        npc.Halt();
        npc.xVelocity = 0f;
        npc.yVelocity = 0f;
        npc.Sprite.StopAnimation();
        npc.controller = null;
        npc.temporaryController = null;
        npc.DirectionsToNewLocation = null;
    }
}

internal sealed record BridgeDebugRepositionResult(
    bool Ok,
    string Status,
    string NpcId,
    string? FromLocationName,
    TileDto? FromTile,
    string TargetLocationName,
    TileDto? TargetTile,
    int? FacingDirection,
    string? ErrorCode,
    string? ErrorMessage);
