namespace StardewHermesBridge.Dialogue;

public sealed class NpcDialogueFollowUpService
{
    public bool CanStartFollowUp(NpcClickDialogueState state)
    {
        if (state.HasCustomDialogueStarted)
            return false;

        if (state.IsOriginalDialogueOpen)
            return false;

        return state.IsOriginalDialogueEnded;
    }
}
