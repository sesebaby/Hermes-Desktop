namespace StardewHermesBridge.Commands;

using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

public static class TestTeleportTargetResolver
{
    public static bool TryResolve(string input, out string? npcName)
    {
        npcName = input.Trim() switch
        {
            "Haley" or "haley" or "海莉" => "Haley",
            "Penny" or "penny" or "潘妮" => "Penny",
            _ => null
        };

        return npcName is not null;
    }
}

public sealed class TestTeleportCommand
{
    private readonly IMonitor _monitor;

    public TestTeleportCommand(IModHelper helper, IMonitor monitor)
    {
        _monitor = monitor;
        helper.ConsoleCommands.Add(
            "teleport",
            "Test command: teleport <海莉|haley|潘妮|penny>",
            Handle);
    }

    private void Handle(string command, string[] args)
    {
        if (!Context.IsWorldReady || Game1.player is null)
        {
            _monitor.Log("World is not ready. Load a save before using: teleport <海莉|haley|潘妮|penny>", LogLevel.Warn);
            return;
        }

        if (args.Length != 1 || !TestTeleportTargetResolver.TryResolve(args[0], out var npcName))
        {
            _monitor.Log("Usage: teleport <海莉|haley|潘妮|penny>", LogLevel.Info);
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
