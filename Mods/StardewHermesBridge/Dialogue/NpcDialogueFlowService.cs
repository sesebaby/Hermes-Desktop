namespace StardewHermesBridge.Dialogue;

public sealed class NpcDialogueFlowService
{
    public NpcDialogueFlowState BeginFollowUp(string npcName)
        => new(npcName, false, false, false);

    public NpcDialogueFlowState BeginObservedOriginal(string npcName)
        => new(npcName, true, false, false);

    public NpcDialogueFlowAdvanceResult Advance(NpcDialogueFlowState state, NpcDialogueAdvanceRequest request)
    {
        if (state.CustomDialogueDisplayed)
            return new(state, false, false, false, false);

        if (request.IsDialogueBoxOpen && string.Equals(request.ActiveDialogueNpcName, state.NpcName, StringComparison.OrdinalIgnoreCase))
        {
            var wasAlreadyObserved = state.OriginalDialogueObserved;
            var observedState = state with { OriginalDialogueObserved = true };
            return new(observedState, !wasAlreadyObserved, false, false, false);
        }

        if (state.OriginalDialogueObserved && !request.HasActiveMenu)
        {
            var completedState = state with { OriginalDialogueCompleted = true, CustomDialogueDisplayed = true };
            return new(completedState, false, true, true, false);
        }

        return new(state, false, false, false, false);
    }
}

public sealed record NpcDialogueFlowState(
    string NpcName,
    bool OriginalDialogueObserved,
    bool OriginalDialogueCompleted,
    bool CustomDialogueDisplayed);

public sealed record NpcDialogueAdvanceRequest(
    string? ActiveDialogueNpcName,
    bool IsDialogueBoxOpen,
    bool HasActiveMenu,
    bool IsDialogueTransitioning);

public sealed record NpcDialogueFlowAdvanceResult(
    NpcDialogueFlowState State,
    bool OriginalDialogueObserved,
    bool OriginalDialogueCompleted,
    bool ShouldRecordVanillaDialogueCompleted,
    bool ShouldDisplayCustomDialogue);
