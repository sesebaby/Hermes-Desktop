namespace Hermes.Agent.Games.Stardew;

using Hermes.Agent.Game;

public sealed class StardewQueryService : IGameQueryService
{
    private const string GameId = "stardew-valley";
    private const string AdapterId = "stardew";

    private readonly ISmapiModApiClient _client;
    private readonly string _saveId;
    private readonly Func<DateTime> _nowUtc;

    public StardewQueryService(ISmapiModApiClient client, string saveId, Func<DateTime>? nowUtc = null)
    {
        _client = client;
        _saveId = string.IsNullOrWhiteSpace(saveId) ? "unknown-save" : saveId;
        _nowUtc = nowUtc ?? (() => DateTime.UtcNow);
    }

    public async Task<GameObservation> ObserveAsync(string npcId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(npcId))
            throw new ArgumentException("NPC id is required.", nameof(npcId));

        var envelope = new StardewBridgeEnvelope<StardewStatusQuery>(
            $"req_{Guid.NewGuid():N}",
            $"trace_observe_{Guid.NewGuid():N}",
            npcId,
            _saveId,
            null,
            new StardewStatusQuery(npcId));

        var response = await _client.SendAsync<StardewStatusQuery, StardewNpcStatusData>(
            StardewBridgeRoutes.QueryStatus,
            envelope,
            ct);
        var status = RequireData(response, StardewBridgeRoutes.QueryStatus);

        return new GameObservation(
            status.NpcId,
            GameId,
            _nowUtc(),
            BuildStatusSummary(status),
            BuildStatusFacts(status));
    }

    public async Task<WorldSnapshot> GetWorldSnapshotAsync(string npcId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(npcId))
            throw new ArgumentException("NPC id is required.", nameof(npcId));

        var envelope = new StardewBridgeEnvelope<StardewWorldSnapshotQuery>(
            $"req_{Guid.NewGuid():N}",
            $"trace_world_{Guid.NewGuid():N}",
            npcId,
            _saveId,
            null,
            new StardewWorldSnapshotQuery(npcId));

        var response = await _client.SendAsync<StardewWorldSnapshotQuery, StardewWorldSnapshotData>(
            StardewBridgeRoutes.QueryWorldSnapshot,
            envelope,
            ct);
        var data = RequireData(response, StardewBridgeRoutes.QueryWorldSnapshot);

        return new WorldSnapshot(
            data.GameId,
            data.SaveId,
            data.TimestampUtc,
            data.Entities.Select(ToEntityBinding).ToArray(),
            data.Facts.ToArray());
    }

    private static TData RequireData<TData>(StardewBridgeResponse<TData> response, string route)
        where TData : class
    {
        if (response.Ok && response.Data is not null)
            return response.Data;

        throw new InvalidOperationException(response.Error?.Code ?? $"stardew_query_failed:{route}");
    }

    private static string BuildStatusSummary(StardewNpcStatusData status)
        => $"{status.DisplayName} is at {status.LocationName} ({status.Tile.X},{status.Tile.Y}); " +
           $"available={Bool(status.IsAvailableForControl)}; moving={Bool(status.IsMoving)}; " +
           $"inDialogue={Bool(status.IsInDialogue)}.";

    private static IReadOnlyList<string> BuildStatusFacts(StardewNpcStatusData status)
    {
        var facts = new List<string>
        {
            $"displayName={status.DisplayName}",
            $"smapiName={status.SmapiName}",
            $"location={status.LocationName}",
            $"tile={status.Tile.X},{status.Tile.Y}",
            $"isMoving={Bool(status.IsMoving)}",
            $"isInDialogue={Bool(status.IsInDialogue)}",
            $"isAvailableForControl={Bool(status.IsAvailableForControl)}"
        };

        if (!string.IsNullOrWhiteSpace(status.BlockedReason))
            facts.Add($"blockedReason={status.BlockedReason}");

        if (!string.IsNullOrWhiteSpace(status.CurrentCommandId))
            facts.Add($"currentCommandId={status.CurrentCommandId}");

        if (!string.IsNullOrWhiteSpace(status.LastTraceId))
            facts.Add($"lastTraceId={status.LastTraceId}");

        return facts;
    }

    private static GameEntityBinding ToEntityBinding(StardewWorldEntityData data)
        => new(data.NpcId, data.TargetEntityId, data.DisplayName, string.IsNullOrWhiteSpace(data.AdapterId) ? AdapterId : data.AdapterId);

    private static string Bool(bool value) => value ? "true" : "false";
}
