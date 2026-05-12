namespace StardewHermesBridge.Dialogue;

public sealed class NpcDialogueClickRouter
{
    public NpcDialogueClickRouteResult Route(NpcDialogueClickRouteRequest request)
    {
        if (!request.IsActionButton && !(request.IsUseToolButton && request.IsMouseButton))
            return NpcDialogueClickRouteResult.Rejected("unsupported_button");

        if (request.HasActiveMenu && !request.IsDialogueBoxOpen)
            return NpcDialogueClickRouteResult.Rejected("menu_open");

        if (request.IsDialogueBoxOpen && !request.CanClaimActiveDialogue)
            return NpcDialogueClickRouteResult.Rejected("dialogue_already_claimed");

        var targetNpcName = request.IsDialogueBoxOpen
            ? request.ActiveDialogueNpcName
            : request.TargetNpcName;

        if (string.IsNullOrWhiteSpace(targetNpcName))
            return NpcDialogueClickRouteResult.Rejected("no_npc_hit");

        return NpcDialogueClickRouteResult.Accepted(
            targetNpcName,
            request.IsDialogueBoxOpen ? "accepted_active_dialogue" : "accepted");
    }
}

public sealed record NpcDialogueClickRouteRequest(
    bool IsActionButton,
    bool IsUseToolButton,
    bool IsMouseButton,
    string? TargetNpcName,
    bool HasActiveMenu,
    bool IsDialogueBoxOpen,
    string? ActiveDialogueNpcName,
    bool CanClaimActiveDialogue = false);

public sealed record NpcDialogueClickRouteResult(
    bool IsAccepted,
    string? NpcName,
    string Reason)
{
    public static NpcDialogueClickRouteResult Accepted(string? npcName, string reason = "accepted")
        => new(true, npcName, reason);

    public static NpcDialogueClickRouteResult Rejected(string reason)
        => new(false, null, reason);
}
