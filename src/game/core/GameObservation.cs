namespace Hermes.Agent.Game;

using System.Text.Json.Nodes;

public sealed record GameObservation(
    string NpcId,
    string GameId,
    DateTime TimestampUtc,
    string Summary,
    IReadOnlyList<string> Facts);

public sealed record WorldSnapshot(
    string GameId,
    string SaveId,
    DateTime TimestampUtc,
    IReadOnlyList<GameEntityBinding> Entities,
    IReadOnlyList<string> Facts);

public sealed record GameEntityBinding(
    string NpcId,
    string TargetEntityId,
    string DisplayName,
    string AdapterId);

public sealed record GameEventCursor(string? Since);

public sealed record GameEventRecord(
    string EventId,
    string EventType,
    string? NpcId,
    DateTime TimestampUtc,
    string Summary,
    string? CorrelationId = null,
    JsonObject? Payload = null);
