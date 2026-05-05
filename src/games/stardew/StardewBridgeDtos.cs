namespace Hermes.Agent.Games.Stardew;

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

public sealed record StardewBridgeEnvelope<TPayload>(
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("traceId")] string TraceId,
    [property: JsonPropertyName("npcId")] string? NpcId,
    [property: JsonPropertyName("saveId")] string? SaveId,
    [property: JsonPropertyName("idempotencyKey")] string? IdempotencyKey,
    [property: JsonPropertyName("payload")] TPayload Payload);

public sealed record StardewBridgeResponse<TData>(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("traceId")] string TraceId,
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("commandId")] string? CommandId,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("data")] TData? Data,
    [property: JsonPropertyName("error")] StardewBridgeError? Error,
    [property: JsonPropertyName("state")] JsonObject? State);

public sealed record StardewBridgeError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("retryable")] bool Retryable);

public sealed record StardewTile(
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y);

public sealed record StardewMoveCandidateData(
    [property: JsonPropertyName("locationName")] string LocationName,
    [property: JsonPropertyName("tile")] StardewTile Tile,
    [property: JsonPropertyName("reason")] string Reason);

public sealed record StardewPlaceCandidateData(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("locationName")] string LocationName,
    [property: JsonPropertyName("tile")] StardewTile Tile,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("facingDirection")] int? FacingDirection = null,
    [property: JsonPropertyName("endBehavior")] string? EndBehavior = null);

public sealed record StardewDestinationData(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("locationName")] string LocationName,
    [property: JsonPropertyName("tile")] StardewTile Tile,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("facingDirection")] int? FacingDirection = null,
    [property: JsonPropertyName("endBehavior")] string? EndBehavior = null,
    [property: JsonPropertyName("destinationId")] string? DestinationId = null);

public sealed record StardewMoveTarget(
    [property: JsonPropertyName("locationName")] string LocationName,
    [property: JsonPropertyName("tile")] StardewTile Tile);

public sealed record StardewMoveRequest(
    [property: JsonPropertyName("target")] StardewMoveTarget? Target,
    [property: JsonPropertyName("reason")] string? Reason = null,
    [property: JsonPropertyName("destinationId")] string? DestinationId = null,
    [property: JsonPropertyName("facingDirection")] int? FacingDirection = null);

public sealed record StardewMoveAcceptedData(
    [property: JsonPropertyName("accepted")] bool Accepted,
    [property: JsonPropertyName("claim")] StardewMoveClaim Claim);

public sealed record StardewMoveClaim(
    [property: JsonPropertyName("npcId")] string NpcId,
    [property: JsonPropertyName("targetTile")] StardewTile TargetTile,
    [property: JsonPropertyName("interactionTile")] StardewTile? InteractionTile);

public sealed record StardewTaskStatusRequest(
    [property: JsonPropertyName("commandId")] string CommandId);

public sealed record StardewTaskLookupRequest(
    [property: JsonPropertyName("idempotencyKey")] string IdempotencyKey);

public sealed record StardewTaskCancelRequest(
    [property: JsonPropertyName("commandId")] string CommandId,
    [property: JsonPropertyName("reason")] string Reason);

public sealed record StardewSpeakRequest(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("channel")] string Channel,
    [property: JsonPropertyName("conversationId")] string? ConversationId = null);

public sealed record StardewSpeakData(
    [property: JsonPropertyName("npcId")] string NpcId,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("channel")] string Channel,
    [property: JsonPropertyName("displayed")] bool Displayed);

public sealed record StardewOpenPrivateChatRequest(
    [property: JsonPropertyName("prompt")] string? Prompt,
    [property: JsonPropertyName("conversationId")] string? ConversationId);

public sealed record StardewOpenPrivateChatData(
    [property: JsonPropertyName("npcId")] string NpcId,
    [property: JsonPropertyName("opened")] bool Opened,
    [property: JsonPropertyName("threadId")] string? ThreadId = null,
    [property: JsonPropertyName("openState")] string? OpenState = null);

public sealed record StardewTaskStatusData(
    [property: JsonPropertyName("commandId")] string CommandId,
    [property: JsonPropertyName("npcId")] string NpcId,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("startedAtUtc")] DateTime? StartedAtUtc,
    [property: JsonPropertyName("elapsedMs")] long ElapsedMs,
    [property: JsonPropertyName("progress")] double Progress,
    [property: JsonPropertyName("blockedReason")] string? BlockedReason,
    [property: JsonPropertyName("errorCode")] string? ErrorCode,
    [property: JsonPropertyName("interruptionReason")] string? InterruptionReason = null,
    [property: JsonPropertyName("updatedAtUtc")] DateTime? UpdatedAtUtc = null,
    [property: JsonPropertyName("retryAfterUtc")] DateTime? RetryAfterUtc = null,
    [property: JsonPropertyName("destinationId")] string? DestinationId = null,
    [property: JsonPropertyName("phase")] string? Phase = null,
    [property: JsonPropertyName("currentLocationName")] string? CurrentLocationName = null,
    [property: JsonPropertyName("resolvedStandTile")] StardewTile? ResolvedStandTile = null,
    [property: JsonPropertyName("routeRevision")] int? RouteRevision = null);

public sealed record StardewStatusQuery(
    [property: JsonPropertyName("npcId")] string? NpcId);

public sealed record StardewWorldSnapshotQuery(
    [property: JsonPropertyName("npcId")] string? NpcId);

public sealed record StardewEmptyStatusQuery();

public sealed record StardewSocialStatusQuery(
    [property: JsonPropertyName("targetNpcId")] string? TargetNpcId);

public sealed record StardewNpcStatusData(
    [property: JsonPropertyName("npcId")] string NpcId,
    [property: JsonPropertyName("smapiName")] string SmapiName,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("locationName")] string LocationName,
    [property: JsonPropertyName("tile")] StardewTile Tile,
    [property: JsonPropertyName("isMoving")] bool IsMoving,
    [property: JsonPropertyName("isInDialogue")] bool IsInDialogue,
    [property: JsonPropertyName("isAvailableForControl")] bool IsAvailableForControl,
    [property: JsonPropertyName("blockedReason")] string? BlockedReason,
    [property: JsonPropertyName("currentCommandId")] string? CurrentCommandId,
    [property: JsonPropertyName("lastTraceId")] string? LastTraceId,
    [property: JsonPropertyName("moveCandidates")] IReadOnlyList<StardewMoveCandidateData>? MoveCandidates = null,
    [property: JsonPropertyName("placeCandidates")] IReadOnlyList<StardewPlaceCandidateData>? PlaceCandidates = null,
    [property: JsonPropertyName("destinations")] IReadOnlyList<StardewDestinationData>? Destinations = null,
    [property: JsonPropertyName("nearbyTiles")] IReadOnlyList<StardewMoveCandidateData>? NearbyTiles = null,
    [property: JsonPropertyName("gameTime")] int? GameTime = null,
    [property: JsonPropertyName("season")] string? Season = null,
    [property: JsonPropertyName("dayOfMonth")] int? DayOfMonth = null,
    [property: JsonPropertyName("weather")] string? Weather = null,
    [property: JsonPropertyName("player")] StardewPlayerSceneData? Player = null);

public sealed record StardewPlayerSceneData(
    [property: JsonPropertyName("locationName")] string LocationName,
    [property: JsonPropertyName("tile")] StardewTile Tile,
    [property: JsonPropertyName("sameLocation")] bool SameLocation,
    [property: JsonPropertyName("distanceTiles")] int? DistanceTiles,
    [property: JsonPropertyName("reachability")] string Reachability,
    [property: JsonPropertyName("availability")] string Availability,
    [property: JsonPropertyName("heldItem")] string? HeldItem);

public sealed record StardewStatusFactResponseData(
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("facts")] IReadOnlyList<string> Facts,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("unknownFields")] IReadOnlyList<string>? UnknownFields = null);

public sealed record StardewPlayerStatusData(
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("facts")] IReadOnlyList<string> Facts,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("unknownFields")] IReadOnlyList<string> UnknownFields);

public sealed record StardewProgressStatusData(
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("facts")] IReadOnlyList<string> Facts,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("unknownFields")] IReadOnlyList<string> UnknownFields);

public sealed record StardewSocialStatusData(
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("facts")] IReadOnlyList<string> Facts,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("unknownFields")] IReadOnlyList<string> UnknownFields);

public sealed record StardewQuestStatusData(
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("facts")] IReadOnlyList<string> Facts,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("unknownFields")] IReadOnlyList<string> UnknownFields);

public sealed record StardewFarmStatusData(
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("facts")] IReadOnlyList<string> Facts,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("unknownFields")] IReadOnlyList<string> UnknownFields);

public sealed record StardewWorldEntityData(
    [property: JsonPropertyName("npcId")] string NpcId,
    [property: JsonPropertyName("targetEntityId")] string TargetEntityId,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("adapterId")] string AdapterId);

public sealed record StardewWorldSnapshotData(
    [property: JsonPropertyName("gameId")] string GameId,
    [property: JsonPropertyName("saveId")] string SaveId,
    [property: JsonPropertyName("timestampUtc")] DateTime TimestampUtc,
    [property: JsonPropertyName("entities")] IReadOnlyList<StardewWorldEntityData> Entities,
    [property: JsonPropertyName("facts")] IReadOnlyList<string> Facts);

public sealed record StardewEventPollQuery(
    [property: JsonPropertyName("since")] string? Since,
    [property: JsonPropertyName("npcId")] string? NpcId,
    [property: JsonPropertyName("seq")] long? Sequence = null);

public sealed record StardewEventData(
    [property: JsonPropertyName("eventId")] string EventId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("npcId")] string? NpcId,
    [property: JsonPropertyName("timestampUtc")] DateTime TimestampUtc,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("correlationId")] string? CorrelationId = null,
    [property: JsonPropertyName("payload")] JsonObject? Payload = null,
    [property: JsonPropertyName("seq")] long? Sequence = null);

public sealed record StardewEventPollData(
    [property: JsonPropertyName("events")] IReadOnlyList<StardewEventData> Events,
    [property: JsonPropertyName("next_seq")] long? NextSequence = null);
