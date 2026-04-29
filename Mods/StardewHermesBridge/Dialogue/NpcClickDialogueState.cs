namespace StardewHermesBridge.Dialogue;

public sealed record NpcClickDialogueState(
    bool IsOriginalDialogueOpen,
    bool IsOriginalDialogueEnded,
    bool IsTransitioning,
    bool HasCustomDialogueStarted);
