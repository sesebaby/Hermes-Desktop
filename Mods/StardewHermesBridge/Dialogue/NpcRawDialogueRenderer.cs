namespace StardewHermesBridge.Dialogue;

using StardewValley;

public static class NpcRawDialogueRenderer
{
    public static void Display(NPC npc, string text)
    {
        Game1.DrawDialogue(new Dialogue(npc, null, text));
    }
}
