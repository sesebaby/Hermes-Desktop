namespace Hermes.Agent.Game;

using System.Text.Json.Nodes;

public enum GameActionType
{
    Move,
    Speak
}

public sealed record GameAction(
    string NpcId,
    string GameId,
    GameActionType Type,
    string TraceId,
    string IdempotencyKey,
    GameActionTarget Target,
    string? Reason = null,
    JsonObject? Payload = null);

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
    string TraceId);

public sealed record GameCommandStatus(
    string CommandId,
    string NpcId,
    string Action,
    string Status,
    double Progress,
    string? BlockedReason,
    string? ErrorCode);
