namespace StardewHermesBridge.Bridge;

public sealed record BridgeEnvelope<TPayload>(
    string RequestId,
    string TraceId,
    string? NpcId,
    string? SaveId,
    string? IdempotencyKey,
    TPayload Payload);

public sealed record BridgeResponse<TData>(
    bool Ok,
    string TraceId,
    string RequestId,
    string? CommandId,
    string? Status,
    TData? Data,
    BridgeError? Error,
    object State);

public sealed record BridgeError(string Code, string Message, bool Retryable);

public sealed record MovePayload(MoveTarget Target, string? Reason);

public sealed record MoveTarget(string LocationName, TileDto Tile);

public sealed record TileDto(int X, int Y);

public sealed record MoveAcceptedData(bool Accepted, MoveClaim Claim);

public sealed record MoveClaim(string NpcId, TileDto TargetTile, TileDto? InteractionTile);

public sealed record TaskStatusRequest(string CommandId);

public sealed record TaskCancelRequest(string CommandId, string Reason);

public sealed record SpeakPayload(string Text, string? Channel);

public sealed record SpeakData(string NpcId, string Text, string Channel, bool Displayed);

public sealed record TaskStatusData(
    string CommandId,
    string TraceId,
    string NpcId,
    string Action,
    string Status,
    DateTime? StartedAtUtc,
    long ElapsedMs,
    double Progress,
    string? BlockedReason,
    string? ErrorCode);
