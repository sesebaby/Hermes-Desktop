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

public sealed record MovePayload(MoveTarget? Target, string? Reason, string? DestinationId = null, int? FacingDirection = null, string? Thought = null);

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

public sealed record DestinationData(
    string Label,
    string LocationName,
    TileDto Tile,
    IReadOnlyList<string> Tags,
    string Reason,
    int? FacingDirection = null,
    string? EndBehavior = null,
    string? DestinationId = null);

public sealed record MoveAcceptedData(bool Accepted, MoveClaim Claim);

public sealed record MoveClaim(string NpcId, TileDto TargetTile, TileDto? InteractionTile);

public sealed record TaskStatusRequest(string CommandId);

public sealed record TaskLookupRequest(string IdempotencyKey);

public sealed record TaskCancelRequest(string CommandId, string Reason);

public sealed record SpeakPayload(string Text, string? Channel, string? ConversationId = null, string? Source = null);

public sealed record SpeakData(string NpcId, string Text, string Channel, bool Displayed);

public sealed record OpenPrivateChatPayload(string? Prompt, string? ConversationId);

public sealed record OpenPrivateChatData(string NpcId, bool Opened, string? ThreadId = null, string? OpenState = null);

public sealed record DebugRepositionPayload(string? Target);

public sealed record DebugRepositionData(
    string NpcId,
    string? FromLocationName,
    TileDto? FromTile,
    string TargetLocationName,
    TileDto? TargetTile,
    int? FacingDirection,
    bool DebugTeleport);

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
    string? ErrorCode,
    string? InterruptionReason = null,
    string? DestinationId = null,
    string? Phase = null,
    string? CurrentLocationName = null,
    TileDto? ResolvedStandTile = null,
    int? RouteRevision = null,
    RouteProbeData? RouteProbe = null,
    string? CrossMapPhase = null,
    BridgeMoveFinalTargetData? FinalTarget = null,
    BridgeMoveSegmentData? CurrentSegment = null,
    string? LastFailureCode = null);

public sealed record BridgeMoveFinalTargetData(
    string LocationName,
    TileDto Tile,
    int? FacingDirection = null);

public sealed record BridgeMoveSegmentData(
    string LocationName,
    TileDto? TargetTile,
    string TargetKind,
    string? NextLocationName = null);

public sealed record RouteProbeData(
    string Mode,
    string Status,
    string? CurrentLocationName,
    TileDto? CurrentTile,
    string TargetLocationName,
    TileDto TargetTile,
    IReadOnlyList<TileDto> Route,
    RouteProbeSegmentData? NextSegment = null,
    string? FailureCode = null,
    string? FailureDetail = null);

public sealed record RouteProbeSegmentData(
    string LocationName,
    TileDto? StandTile,
    string TargetKind,
    string? NextLocationName = null);

public sealed record StatusQuery(string? NpcId);

public sealed record WorldSnapshotQuery(string? NpcId);

public sealed record EmptyStatusQuery();

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
    IReadOnlyList<PlaceCandidateData>? PlaceCandidates = null,
    IReadOnlyList<DestinationData>? Destinations = null,
    IReadOnlyList<MoveCandidateData>? NearbyTiles = null,
    int? GameTime = null,
    string? Season = null,
    int? DayOfMonth = null,
    string? Weather = null,
    PlayerSceneData? Player = null);

public sealed record PlayerSceneData(
    string LocationName,
    TileDto Tile,
    bool SameLocation,
    int? DistanceTiles,
    string Reachability,
    string Availability,
    string? HeldItem);

public sealed record StatusFactResponseData(
    string Summary,
    IReadOnlyList<string> Facts,
    string Status = "completed",
    IReadOnlyList<string>? UnknownFields = null);

public sealed record PlayerStatusData(
    string Summary,
    IReadOnlyList<string> Facts,
    string Status,
    IReadOnlyList<string> UnknownFields);

public sealed record ProgressStatusData(
    string Summary,
    IReadOnlyList<string> Facts,
    string Status,
    IReadOnlyList<string> UnknownFields);

public sealed record SocialStatusQuery(string? TargetNpcId);

public sealed record SocialStatusData(
    string Summary,
    IReadOnlyList<string> Facts,
    string Status,
    IReadOnlyList<string> UnknownFields);

public sealed record QuestStatusData(
    string Summary,
    IReadOnlyList<string> Facts,
    string Status,
    IReadOnlyList<string> UnknownFields);

public sealed record FarmStatusData(
    string Summary,
    IReadOnlyList<string> Facts,
    string Status,
    IReadOnlyList<string> UnknownFields);

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
