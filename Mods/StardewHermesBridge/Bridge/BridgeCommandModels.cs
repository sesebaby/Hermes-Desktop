namespace StardewHermesBridge.Bridge;

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

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

public sealed record MovePayload(MoveTarget Target, string? Reason, int? FacingDirection = null);

public sealed record MoveTarget(string LocationName, TileDto Tile);

public sealed record TileDto(int X, int Y);

public sealed record MoveCandidateData(string LocationName, TileDto Tile, string Reason);

public sealed record PlaceCandidateData(
    string Label,
    string LocationName,
    TileDto Tile,
    IReadOnlyList<string> Tags,
    string Reason,
    int? FacingDirection = null,
    string? EndBehavior = null);

public sealed record MoveAcceptedData(bool Accepted, MoveClaim Claim);

public sealed record MoveClaim(string NpcId, TileDto TargetTile, TileDto? InteractionTile);

public sealed record TaskStatusRequest(string CommandId);

public sealed record TaskLookupRequest(string IdempotencyKey);

public sealed record TaskCancelRequest(string CommandId, string Reason);

public sealed record SpeakPayload(string Text, string? Channel, string? ConversationId = null);

public sealed record SpeakData(string NpcId, string Text, string Channel, bool Displayed);

public sealed record OpenPrivateChatPayload(string? Prompt, string? ConversationId);

public sealed record OpenPrivateChatData(string NpcId, bool Opened);

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

public sealed record StatusQuery(string? NpcId);

public sealed record WorldSnapshotQuery(string? NpcId);

public sealed record NpcStatusData(
    string NpcId,
    string SmapiName,
    string DisplayName,
    string LocationName,
    TileDto Tile,
    bool IsMoving,
    bool IsInDialogue,
    bool IsAvailableForControl,
    string? BlockedReason,
    string? CurrentCommandId,
    string? LastTraceId,
    IReadOnlyList<MoveCandidateData>? MoveCandidates = null,
    IReadOnlyList<PlaceCandidateData>? PlaceCandidates = null);

public sealed record WorldEntityData(
    string NpcId,
    string TargetEntityId,
    string DisplayName,
    string AdapterId);

public sealed record WorldSnapshotData(
    string GameId,
    string SaveId,
    DateTime TimestampUtc,
    IReadOnlyList<WorldEntityData> Entities,
    IReadOnlyList<string> Facts);

public sealed record EventPollQuery(
    string? Since,
    string? NpcId,
    [property: JsonPropertyName("seq")] long? Sequence = null);

public sealed record BridgeEventData(
    string EventId,
    string EventType,
    string? NpcId,
    DateTime TimestampUtc,
    string Summary,
    string? CorrelationId = null,
    JsonObject? Payload = null,
    [property: JsonPropertyName("seq")] long? Sequence = null);

public sealed record EventPollData(
    IReadOnlyList<BridgeEventData> Events,
    [property: JsonPropertyName("next_seq")] long? NextSequence = null);
