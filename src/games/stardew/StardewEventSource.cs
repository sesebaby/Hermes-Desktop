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
    {
        ArgumentNullException.ThrowIfNull(cursor);

        var envelope = new StardewBridgeEnvelope<StardewEventPollQuery>(
            $"req_{Guid.NewGuid():N}",
            $"trace_events_{Guid.NewGuid():N}",
            _npcId,
            _saveId,
            null,
            new StardewEventPollQuery(cursor.Since, _npcId));

        var response = await _client.SendAsync<StardewEventPollQuery, StardewEventPollData>(
            StardewBridgeRoutes.EventsPoll,
            envelope,
            ct);

        if (!response.Ok || response.Data is null)
            throw new InvalidOperationException(response.Error?.Code ?? $"stardew_query_failed:{StardewBridgeRoutes.EventsPoll}");

        return response.Data.Events.Select(ToGameEventRecord).ToArray();
    }

    private static GameEventRecord ToGameEventRecord(StardewEventData data)
        => new(data.EventId, data.EventType, data.NpcId, data.TimestampUtc, data.Summary);
}
