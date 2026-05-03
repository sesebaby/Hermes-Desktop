namespace StardewHermesBridge.Bridge;

using StardewValley;

internal static class BridgeNpcResolver
{
    public static NPC? Resolve(string? rawNpcId)
    {
        foreach (var candidate in NpcTargetNameResolver.EnumerateCandidates(rawNpcId))
        {
            var npc = Game1.getCharacterFromName(candidate, mustBeVillager: false, includeEventActors: false);
            if (npc is not null)
                return npc;
        }

        return null;
    }
}
