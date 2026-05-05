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
        => await ObserveAsync(NpcBodyBinding.FromLogicalId(npcId, AdapterId), ct);

    public async Task<GameObservation> ObserveAsync(NpcBodyBinding bodyBinding, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bodyBinding);
        var targetEntityId = ResolveTargetEntityId(bodyBinding);

        var envelope = new StardewBridgeEnvelope<StardewStatusQuery>(
            $"req_{Guid.NewGuid():N}",
            $"trace_observe_{Guid.NewGuid():N}",
            targetEntityId,
            _saveId,
            null,
            new StardewStatusQuery(targetEntityId));

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
        => await GetWorldSnapshotAsync(NpcBodyBinding.FromLogicalId(npcId, AdapterId), ct);

    public async Task<WorldSnapshot> GetWorldSnapshotAsync(NpcBodyBinding bodyBinding, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bodyBinding);
        var targetEntityId = ResolveTargetEntityId(bodyBinding);

        var envelope = new StardewBridgeEnvelope<StardewWorldSnapshotQuery>(
            $"req_{Guid.NewGuid():N}",
            $"trace_world_{Guid.NewGuid():N}",
            targetEntityId,
            _saveId,
            null,
            new StardewWorldSnapshotQuery(targetEntityId));

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
           (status.GameTime is { } gameTime ? $"gameTime={gameTime} ({FormatGameClock(gameTime)}); " : "") +
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

        if (status.GameTime is { } gameTime)
        {
            facts.Add($"gameTime={gameTime}");
            facts.Add($"gameClock={FormatGameClock(gameTime)}");
        }

        if (!string.IsNullOrWhiteSpace(status.Season))
            facts.Add($"season={status.Season}");

        if (status.DayOfMonth is { } dayOfMonth)
            facts.Add($"dayOfMonth={dayOfMonth}");

        if (!string.IsNullOrWhiteSpace(status.Weather))
            facts.Add($"weather={status.Weather}");

        if (!string.IsNullOrWhiteSpace(status.BlockedReason))
            facts.Add($"blockedReason={status.BlockedReason}");

        if (!string.IsNullOrWhiteSpace(status.CurrentCommandId))
            facts.Add($"currentCommandId={status.CurrentCommandId}");

        if (!string.IsNullOrWhiteSpace(status.LastTraceId))
            facts.Add($"lastTraceId={status.LastTraceId}");

        foreach (var (candidate, index) in (status.Destinations ?? Array.Empty<StardewDestinationData>())
                 .Take(5)
                 .Select((candidate, index) => (candidate, index)))
        {
            if (string.IsNullOrWhiteSpace(candidate.LocationName) || string.IsNullOrWhiteSpace(candidate.Label))
                continue;

            var reason = string.IsNullOrWhiteSpace(candidate.Reason)
                ? "a place of interest in the current location"
                : candidate.Reason;
            var tags = candidate.Tags is null
                ? ""
                : string.Join("|", candidate.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()));
            var parts = new List<string>
            {
                $"destination[{index}]=label={candidate.Label}",
                $"locationName={candidate.LocationName}",
                $"x={candidate.Tile.X}",
                $"y={candidate.Tile.Y}",
                $"tags={tags}",
                $"reason={reason}"
            };
            if (!string.IsNullOrWhiteSpace(candidate.DestinationId))
                parts.Add($"destinationId={candidate.DestinationId}");
            if (candidate.FacingDirection.HasValue)
                parts.Add($"facingDirection={candidate.FacingDirection.Value}");
            if (!string.IsNullOrWhiteSpace(candidate.EndBehavior))
                parts.Add($"endBehavior={candidate.EndBehavior}");
            facts.Add(string.Join(",", parts));
        }

        foreach (var (candidate, index) in (status.NearbyTiles ?? Array.Empty<StardewMoveCandidateData>())
                 .Take(3)
                 .Select((candidate, index) => (candidate, index)))
        {
            if (string.IsNullOrWhiteSpace(candidate.LocationName))
                continue;

            var reason = string.IsNullOrWhiteSpace(candidate.Reason)
                ? "same_location_safe_reposition"
                : candidate.Reason;
            facts.Add($"nearby[{index}]=locationName={candidate.LocationName},x={candidate.Tile.X},y={candidate.Tile.Y},reason={reason}");
        }

        return facts;
    }

    private static GameEntityBinding ToEntityBinding(StardewWorldEntityData data)
        => new(data.NpcId, data.TargetEntityId, data.DisplayName, string.IsNullOrWhiteSpace(data.AdapterId) ? AdapterId : data.AdapterId);

    private static string ResolveTargetEntityId(NpcBodyBinding bodyBinding)
    {
        if (!string.IsNullOrWhiteSpace(bodyBinding.TargetEntityId))
            return bodyBinding.TargetEntityId;

        if (!string.IsNullOrWhiteSpace(bodyBinding.SmapiName))
            return bodyBinding.SmapiName;

        if (!string.IsNullOrWhiteSpace(bodyBinding.NpcId))
            return bodyBinding.NpcId;

        throw new ArgumentException("NPC body binding target is required.", nameof(bodyBinding));
    }

    private static string Bool(bool value) => value ? "true" : "false";

    private static string FormatGameClock(int gameTime)
        => $"{gameTime / 100:00}:{gameTime % 100:00}";
}
