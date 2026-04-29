namespace StardewHermesBridge.Dialogue;

public sealed class NpcDialogueFlowService
{
    public NpcDialogueFlowState BeginFollowUp(string npcName)
        => new(npcName, false, false, false);

    public NpcDialogueFlowAdvanceResult Advance(NpcDialogueFlowState state, NpcDialogueAdvanceRequest request)
    {
        if (state.CustomDialogueDisplayed)
            return new(state, false, false, false);

        if (request.IsDialogueBoxOpen && string.Equals(request.ActiveDialogueNpcName, state.NpcName, StringComparison.OrdinalIgnoreCase))
        {
            var observedState = state with { OriginalDialogueObserved = true };
            return new(observedState, true, false, false);
        }

        if (state.OriginalDialogueObserved && !request.IsDialogueBoxOpen)
        {
            var completedState = state with { OriginalDialogueCompleted = true, CustomDialogueDisplayed = true };
            return new(completedState, false, true, true);
        }

        return new(state, false, false, false);
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
    bool IsDialogueTransitioning);

public sealed record NpcDialogueFlowAdvanceResult(
    NpcDialogueFlowState State,
    bool OriginalDialogueObserved,
    bool OriginalDialogueCompleted,
    bool ShouldDisplayCustomDialogue);
