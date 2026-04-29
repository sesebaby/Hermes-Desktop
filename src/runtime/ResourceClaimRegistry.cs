namespace Hermes.Agent.Runtime;

public sealed class ResourceClaimRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ResourceClaim> _claimsByCommand = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _commandsByIdempotencyKey = new(StringComparer.OrdinalIgnoreCase);

    public ResourceClaimResult TryClaim(ResourceClaimRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.CommandId))
            throw new ArgumentException("commandId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.NpcId))
            throw new ArgumentException("npcId is required.", nameof(request));

        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey) &&
                _commandsByIdempotencyKey.TryGetValue(request.IdempotencyKey, out var existingCommandId) &&
                _claimsByCommand.TryGetValue(existingCommandId, out var existingClaim))
            {
                return ResourceClaimResult.AlreadyClaimed(existingClaim);
            }

            var conflict = _claimsByCommand.Values.FirstOrDefault(claim => claim.ConflictsWith(request));
            if (conflict is not null)
                return ResourceClaimResult.Conflict(conflict);

            var claim = new ResourceClaim(
                request.CommandId,
                request.NpcId,
                request.TraceId,
                request.IdempotencyKey,
                request.TargetTile,
                request.InteractionTile,
                request.TargetObjectId,
                DateTime.UtcNow);

            _claimsByCommand[claim.CommandId] = claim;
            if (!string.IsNullOrWhiteSpace(claim.IdempotencyKey))
                _commandsByIdempotencyKey[claim.IdempotencyKey] = claim.CommandId;

            return ResourceClaimResult.Created(claim);
        }
    }

    public bool Release(string commandId)
    {
        if (string.IsNullOrWhiteSpace(commandId))
            return false;

        lock (_gate)
        {
            if (!_claimsByCommand.Remove(commandId, out var claim))
                return false;

            if (!string.IsNullOrWhiteSpace(claim.IdempotencyKey))
                _commandsByIdempotencyKey.Remove(claim.IdempotencyKey);

            return true;
        }
    }

    public IReadOnlyList<ResourceClaim> Snapshot()
    {
        lock (_gate)
            return _claimsByCommand.Values.OrderBy(claim => claim.CreatedAtUtc).ToArray();
    }
}

public sealed record ResourceClaimRequest(
    string CommandId,
    string NpcId,
    string TraceId,
    string? IdempotencyKey = null,
    ClaimedTile? TargetTile = null,
    ClaimedTile? InteractionTile = null,
    string? TargetObjectId = null);

public sealed record ResourceClaim(
    string CommandId,
    string NpcId,
    string TraceId,
    string? IdempotencyKey,
    ClaimedTile? TargetTile,
    ClaimedTile? InteractionTile,
    string? TargetObjectId,
    DateTime CreatedAtUtc)
{
    public bool ConflictsWith(ResourceClaimRequest request)
    {
        if (string.Equals(NpcId, request.NpcId, StringComparison.OrdinalIgnoreCase))
            return true;

        return SameTile(TargetTile, request.TargetTile) ||
               SameTile(InteractionTile, request.InteractionTile) ||
               SameTile(TargetTile, request.InteractionTile) ||
               SameTile(InteractionTile, request.TargetTile) ||
               (!string.IsNullOrWhiteSpace(TargetObjectId) &&
                string.Equals(TargetObjectId, request.TargetObjectId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool SameTile(ClaimedTile? left, ClaimedTile? right)
        => left is not null &&
           right is not null &&
           string.Equals(left.LocationName, right.LocationName, StringComparison.OrdinalIgnoreCase) &&
           left.X == right.X &&
           left.Y == right.Y;
}

public sealed record ClaimedTile(string LocationName, int X, int Y);

public sealed record ResourceClaimResult(
    bool Accepted,
    bool WasIdempotentReplay,
    ResourceClaim? Claim,
    ResourceClaim? ConflictingClaim,
    string? ErrorCode)
{
    public static ResourceClaimResult Created(ResourceClaim claim)
        => new(true, false, claim, null, null);

    public static ResourceClaimResult AlreadyClaimed(ResourceClaim claim)
        => new(true, true, claim, null, null);

    public static ResourceClaimResult Conflict(ResourceClaim conflictingClaim)
        => new(false, false, null, conflictingClaim, "command_conflict");
}
