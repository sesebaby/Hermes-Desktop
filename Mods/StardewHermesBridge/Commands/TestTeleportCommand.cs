namespace StardewHermesBridge.Commands;

using Microsoft.Xna.Framework;
using StardewHermesBridge.Bridge;
using StardewHermesBridge.Logging;
using StardewModdingAPI;
using StardewValley;

public static class TestTeleportTargetResolver
{
    public static bool TryResolve(string input, out string? npcName)
        => NpcTargetNameResolver.TryResolveKnownAlias(input, out npcName);
}

public sealed class TestTeleportCommand
{
    private const string TeleportUsage = "teleport <海莉|haley|潘妮|penny>";
    private const string NpcTownUsage = "npc_town <海莉|haley|潘妮|penny>";

    private readonly SmapiBridgeLogger _bridgeLogger;
    private readonly IMonitor _monitor;

    public TestTeleportCommand(IModHelper helper, IMonitor monitor, SmapiBridgeLogger bridgeLogger)
    {
        _monitor = monitor;
        _bridgeLogger = bridgeLogger;
        helper.ConsoleCommands.Add(
            "teleport",
            $"Test command: {TeleportUsage}",
            HandleTeleport);
        helper.ConsoleCommands.Add(
            "npc_town",
            $"Debug command: {NpcTownUsage}",
            HandleNpcTown);
    }

    private void HandleTeleport(string command, string[] args)
    {
        if (!Context.IsWorldReady || Game1.player is null)
        {
            _monitor.Log($"World is not ready. Load a save before using: {TeleportUsage}", LogLevel.Warn);
            return;
        }

        if (args.Length != 1 || !TestTeleportTargetResolver.TryResolve(args[0], out var npcName))
        {
            _monitor.Log($"Usage: {TeleportUsage}", LogLevel.Info);
            return;
        }

        var npc = Game1.getCharacterFromName(npcName, mustBeVillager: false, includeEventActors: false);
        if (npc?.currentLocation is null)
        {
            _monitor.Log($"NPC not found or not currently placed in the world: {npcName}", LogLevel.Warn);
            return;
        }

        var npcTile = npc.Position / Game1.tileSize;
        var targetTile = FindOpenTileNear(npc.currentLocation, (int)npcTile.X, (int)npcTile.Y);
        if (targetTile is null)
        {
            _monitor.Log($"No open tile found near {npcName}.", LogLevel.Warn);
            return;
        }

        Game1.warpFarmer(npc.currentLocation.Name, targetTile.Value.X, targetTile.Value.Y, false);
        _monitor.Log($"Teleported player near {npcName}.", LogLevel.Info);
    }

    private void HandleNpcTown(string command, string[] args)
    {
        if (args.Length != 1)
        {
            _monitor.Log($"Usage: {NpcTownUsage}", LogLevel.Info);
            return;
        }

        var result = BridgeNpcDebugRepositioner.RepositionToTown(args[0], _bridgeLogger);
        if (!result.Ok)
        {
            _monitor.Log(
                $"debug_npc_reposition failed: {result.ErrorCode ?? "unknown_error"} ({result.ErrorMessage ?? "no detail"})",
                LogLevel.Warn);
            return;
        }

        _monitor.Log(
            $"debug_npc_reposition completed: {result.NpcId} -> {result.TargetLocationName} {result.TargetTile?.X},{result.TargetTile?.Y}",
            LogLevel.Info);
    }

    private static Point? FindOpenTileNear(GameLocation location, int npcTileX, int npcTileY)
    {
        foreach (var candidate in GetNearbyCandidates(npcTileX, npcTileY))
        {
            var tile = new Vector2(candidate.X, candidate.Y);
            if (location.isTileLocationOpen(tile) && location.CanSpawnCharacterHere(tile))
                return candidate;
        }

        return null;
    }

    private static IEnumerable<Point> GetNearbyCandidates(int npcTileX, int npcTileY)
    {
        yield return new Point(npcTileX, npcTileY + 1);
        yield return new Point(npcTileX + 1, npcTileY);
        yield return new Point(npcTileX - 1, npcTileY);
        yield return new Point(npcTileX, npcTileY - 1);

        for (var radius = 2; radius <= 3; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                        continue;

                    yield return new Point(npcTileX + dx, npcTileY + dy);
                }
            }
        }
    }
}
