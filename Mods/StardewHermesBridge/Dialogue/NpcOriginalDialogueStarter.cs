namespace StardewHermesBridge.Dialogue;

using StardewModdingAPI;
using StardewValley;

public sealed class NpcOriginalDialogueStarter
{
    private readonly IModHelper _helper;

    public NpcOriginalDialogueStarter(IModHelper helper)
    {
        _helper = helper;
    }

    public bool TryStart(NPC npc, SButton triggerButton)
    {
        if (Game1.player?.currentLocation is null)
            return false;

        _helper.Input.Suppress(triggerButton);

        var stowed = Game1.player.netItemStowed.Value;
        Game1.player.netItemStowed.Value = true;
        Game1.player.UpdateItemStow();

        try
        {
            return npc.checkAction(Game1.player, Game1.player.currentLocation);
        }
        finally
        {
            Game1.player.netItemStowed.Value = stowed;
            Game1.player.UpdateItemStow();
        }
    }
}
