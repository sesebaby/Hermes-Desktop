namespace StardewHermesBridge.Dialogue;

public sealed class NpcDialogueMenuGuard
{
    private string? _customDialogueOpeningForNpcName;
    private string? _customDialogueActiveNpcName;

    public void MarkCustomDialogueOpening(string npcName)
    {
        _customDialogueOpeningForNpcName = npcName;
    }

    public bool IsCustomDialogue(string? npcName)
        => IsSameNpc(npcName, _customDialogueOpeningForNpcName)
           || IsSameNpc(npcName, _customDialogueActiveNpcName);

    public NpcDialogueMenuGuardResult ConsumeMenuChange(string? oldDialogueNpcName, string? newDialogueNpcName)
    {
        if (IsSameNpc(newDialogueNpcName, _customDialogueOpeningForNpcName))
        {
            _customDialogueActiveNpcName = newDialogueNpcName;
            _customDialogueOpeningForNpcName = null;
            return NpcDialogueMenuGuardResult.CustomDialogueOpening;
        }

        if (!string.IsNullOrWhiteSpace(oldDialogueNpcName) &&
            IsSameNpc(oldDialogueNpcName, _customDialogueActiveNpcName) &&
            !IsSameNpc(newDialogueNpcName, _customDialogueActiveNpcName))
        {
            _customDialogueActiveNpcName = null;
            return NpcDialogueMenuGuardResult.CustomDialogueClosing;
        }

        return NpcDialogueMenuGuardResult.Unhandled;
    }

    public void Clear()
    {
        _customDialogueOpeningForNpcName = null;
        _customDialogueActiveNpcName = null;
    }

    private static bool IsSameNpc(string? left, string? right)
        => !string.IsNullOrWhiteSpace(left)
           && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}

public enum NpcDialogueMenuGuardResult
{
    Unhandled,
    CustomDialogueOpening,
    CustomDialogueClosing
}
