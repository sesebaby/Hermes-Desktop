namespace StardewHermesBridge.Dialogue;

public sealed class NpcDialogueClickRouter
{
    public NpcDialogueClickRouteResult Route(NpcDialogueClickRouteRequest request)
    {
        if (!request.IsPrimaryButton)
            return NpcDialogueClickRouteResult.Rejected("unsupported_button");

        if (request.HasActiveMenu)
            return NpcDialogueClickRouteResult.Rejected("menu_open");

        if (string.IsNullOrWhiteSpace(request.TargetNpcName))
            return NpcDialogueClickRouteResult.Rejected("no_npc_hit");

        if (!string.Equals(request.TargetNpcName, "Haley", StringComparison.OrdinalIgnoreCase))
            return NpcDialogueClickRouteResult.Rejected("npc_not_enabled");

        return NpcDialogueClickRouteResult.Accepted(request.TargetNpcName);
    }
}

public sealed record NpcDialogueClickRouteRequest(
    bool IsPrimaryButton,
    string? TargetNpcName,
    bool HasActiveMenu);

public sealed record NpcDialogueClickRouteResult(
    bool IsAccepted,
    string? NpcName,
    string Reason)
{
    public static NpcDialogueClickRouteResult Accepted(string? npcName)
        => new(true, npcName, "accepted");

    public static NpcDialogueClickRouteResult Rejected(string reason)
        => new(false, null, reason);
}
