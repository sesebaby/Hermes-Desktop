namespace StardewHermesBridge.Dialogue;

public sealed class NpcDialogueObservationGate
{
    private object? _trackedDialogueInstance;
    private string? _trackedNpcName;
    private bool _claimed;

    public void RecordMenuChanged(object? newMenu, string? dialogueNpcName)
    {
        if (newMenu is null || string.IsNullOrWhiteSpace(dialogueNpcName))
        {
            Clear();
            return;
        }

        if (ReferenceEquals(_trackedDialogueInstance, newMenu) &&
            IsSameNpc(_trackedNpcName, dialogueNpcName))
        {
            return;
        }

        _trackedDialogueInstance = newMenu;
        _trackedNpcName = dialogueNpcName;
        _claimed = false;
    }

    public bool CanClaim(object? dialogueInstance, string? npcName)
        => !_claimed && Matches(dialogueInstance, npcName);

    public bool TryClaim(object? dialogueInstance, string? npcName)
    {
        if (!CanClaim(dialogueInstance, npcName))
            return false;

        _claimed = true;
        return true;
    }

    public void MarkObserved(object? dialogueInstance, string? npcName)
    {
        if (Matches(dialogueInstance, npcName))
            _claimed = true;
    }

    public void Clear()
    {
        _trackedDialogueInstance = null;
        _trackedNpcName = null;
        _claimed = false;
    }

    private bool Matches(object? dialogueInstance, string? npcName)
        => _trackedDialogueInstance is not null &&
           ReferenceEquals(_trackedDialogueInstance, dialogueInstance) &&
           IsSameNpc(_trackedNpcName, npcName);

    private static bool IsSameNpc(string? left, string? right)
        => !string.IsNullOrWhiteSpace(left) &&
           string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
