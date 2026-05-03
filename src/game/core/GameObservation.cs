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

public sealed record NpcBodyBinding(
    string NpcId,
    string TargetEntityId,
    string? SmapiName = null,
    string? DisplayName = null,
    string? AdapterId = null)
{
    public static NpcBodyBinding FromLogicalId(string npcId, string? adapterId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(npcId);
        var trimmed = npcId.Trim();
        return new NpcBodyBinding(trimmed, trimmed, DisplayName: trimmed, AdapterId: adapterId);
    }
}

public sealed record GameEventCursor(string? Since = null, long? Sequence = null)
{
    public static GameEventCursor FromRecord(GameEventRecord record)
        => new(record.EventId, record.Sequence);

    public static GameEventCursor Advance(
        GameEventCursor current,
        IReadOnlyList<GameEventRecord> records,
        long? nextSequence = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(records);

        var lastRecord = records.Count > 0 ? records[^1] : null;
        return new(
            lastRecord?.EventId ?? current.Since,
            nextSequence ?? lastRecord?.Sequence ?? current.Sequence);
    }
}

public sealed record GameEventRecord(
    string EventId,
    string EventType,
    string? NpcId,
    DateTime TimestampUtc,
    string Summary,
    string? CorrelationId = null,
    JsonObject? Payload = null,
    long? Sequence = null);

public sealed record GameEventBatch(
    IReadOnlyList<GameEventRecord> Records,
    GameEventCursor NextCursor);
