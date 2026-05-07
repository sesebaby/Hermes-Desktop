namespace Hermes.Agent.Game;

using System.Text.Json.Nodes;

public enum GameActionType
{
    Move,
    Speak,
    OpenPrivateChat
}

public sealed record GameAction(
    string NpcId,
    string GameId,
    GameActionType Type,
    string TraceId,
    string IdempotencyKey,
    GameActionTarget Target,
    string? Reason = null,
    JsonObject? Payload = null,
    NpcBodyBinding? BodyBinding = null);

public sealed record GameActionTarget(
    string Kind,
    string? LocationName = null,
    GameTile? Tile = null,
    string? EntityId = null,
    string? ObjectId = null);

public sealed record GameTile(int X, int Y);

public sealed record GameCommandResult(
    bool Accepted,
    string CommandId,
    string Status,
    string? FailureReason,
    string TraceId,
    bool Retryable = false,
    string? DestinationId = null,
    string? InitialPhase = null);

public sealed record GameCommandStatus(
    string CommandId,
    string NpcId,
    string Action,
    string Status,
    double Progress,
    string? BlockedReason,
    string? ErrorCode,
    DateTime? StartedAtUtc = null,
    DateTime? UpdatedAtUtc = null,
    long? ElapsedMs = null,
    DateTime? RetryAfterUtc = null,
    string? DestinationId = null,
    string? Phase = null,
    string? CurrentLocationName = null,
    GameTile? ResolvedStandTile = null,
    int? RouteRevision = null,
    GameRouteProbe? RouteProbe = null);

public sealed record GameRouteProbe(
    string Mode,
    string Status,
    string? CurrentLocationName,
    GameTile? CurrentTile,
    string TargetLocationName,
    GameTile TargetTile,
    IReadOnlyList<GameTile> Route,
    GameRouteProbeSegment? NextSegment = null,
    string? FailureCode = null,
    string? FailureDetail = null);

public sealed record GameRouteProbeSegment(
    string LocationName,
    GameTile? StandTile,
    string TargetKind,
    string? NextLocationName = null);
