namespace Hermes.Agent.Games.Stardew;

using Hermes.Agent.Game;

public sealed class StardewEventSource : IGameEventSource
{
    private readonly ISmapiModApiClient _client;
    private readonly string _saveId;
    private readonly string? _npcId;

    public StardewEventSource(ISmapiModApiClient client, string saveId, string? npcId = null)
    {
        _client = client;
        _saveId = string.IsNullOrWhiteSpace(saveId) ? "unknown-save" : saveId;
        _npcId = string.IsNullOrWhiteSpace(npcId) ? null : npcId;
    }

    public async Task<IReadOnlyList<GameEventRecord>> PollAsync(GameEventCursor cursor, CancellationToken ct)
        => (await PollBatchAsync(cursor, ct)).Records;

    public async Task<GameEventBatch> PollBatchAsync(GameEventCursor cursor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        var pollCursor = NormalizeCursorForPoll(cursor);

        var envelope = new StardewBridgeEnvelope<StardewEventPollQuery>(
            $"req_{Guid.NewGuid():N}",
            $"trace_events_{Guid.NewGuid():N}",
            _npcId,
            _saveId,
            null,
            new StardewEventPollQuery(pollCursor.Since, _npcId, pollCursor.Sequence));

        var response = await _client.SendAsync<StardewEventPollQuery, StardewEventPollData>(
            StardewBridgeRoutes.EventsPoll,
            envelope,
            ct);

        if (!response.Ok || response.Data is null)
            throw new InvalidOperationException(response.Error?.Code ?? $"stardew_query_failed:{StardewBridgeRoutes.EventsPoll}");

        var records = response.Data.Events.Select(ToGameEventRecord).ToArray();
        var nextCursor = BuildNextCursor(pollCursor, records, response.Data.NextSequence);
        return new GameEventBatch(records, nextCursor);
    }

    private static GameEventRecord ToGameEventRecord(StardewEventData data)
        => new(data.EventId, data.EventType, data.NpcId, data.TimestampUtc, data.Summary, data.CorrelationId, data.Payload, data.Sequence);

    private static GameEventCursor NormalizeCursorForPoll(GameEventCursor cursor)
        => IsCanonicalEventIdAheadOfSequence(cursor.Since, cursor.Sequence)
            ? new GameEventCursor()
            : cursor;

    private static GameEventCursor BuildNextCursor(
        GameEventCursor cursor,
        IReadOnlyList<GameEventRecord> records,
        long? nextSequence)
    {
        if (records.Count == 0 &&
            cursor.Sequence.HasValue &&
            nextSequence.HasValue &&
            nextSequence.Value < cursor.Sequence.Value)
        {
            return new GameEventCursor();
        }

        return GameEventCursor.Advance(cursor, records, nextSequence);
    }

    private static bool IsCanonicalEventIdAheadOfSequence(string? eventId, long? sequence)
        => sequence.HasValue &&
           TryParseCanonicalEventSequence(eventId, out var eventSequence) &&
           eventSequence > sequence.Value;

    private static bool TryParseCanonicalEventSequence(string? eventId, out long sequence)
    {
        sequence = 0;
        const string Prefix = "evt_";
        if (string.IsNullOrWhiteSpace(eventId) ||
            !eventId.StartsWith(Prefix, StringComparison.Ordinal) ||
            eventId.Length == Prefix.Length)
        {
            return false;
        }

        return long.TryParse(eventId[Prefix.Length..], out sequence);
    }
}
