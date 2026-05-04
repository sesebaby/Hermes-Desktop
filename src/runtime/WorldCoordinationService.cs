namespace Hermes.Agent.Runtime;

public sealed class WorldCoordinationService
{
    private readonly ResourceClaimRegistry _claims;

    public WorldCoordinationService(ResourceClaimRegistry claims)
    {
        _claims = claims;
    }

    public ResourceClaimResult TryClaimMove(
        string commandId,
        string npcId,
        string traceId,
        ClaimedTile? targetTile,
        ClaimedTile? interactionTile,
        string? idempotencyKey)
    {
        return _claims.TryClaim(new ResourceClaimRequest(
            commandId,
            npcId,
            traceId,
            idempotencyKey,
            targetTile,
            interactionTile,
            TargetObjectId: null));
    }

    public bool ReleaseClaim(string claimId)
        => _claims.Release(claimId);

    public bool ReleaseCommand(string commandId)
        => ReleaseClaim(commandId);

    public bool RekeyClaim(string currentCommandId, string newCommandId)
        => _claims.Rekey(currentCommandId, newCommandId);
}
