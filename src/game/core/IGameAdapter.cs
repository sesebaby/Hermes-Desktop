namespace Hermes.Agent.Game;

public interface IGameAdapter
{
    string AdapterId { get; }
    IGameCommandService Commands { get; }
    IGameQueryService Queries { get; }
    IGameEventSource Events { get; }
}

public interface IGameCommandService
{
    Task<GameCommandResult> SubmitAsync(GameAction action, CancellationToken ct);

    Task<GameCommandStatus> GetStatusAsync(string commandId, CancellationToken ct);

    Task<GameCommandStatus?> TryGetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
        => Task.FromResult<GameCommandStatus?>(null);

    Task<GameCommandStatus> CancelAsync(string commandId, string reason, CancellationToken ct);
}

public interface IGameQueryService
{
    Task<GameObservation> ObserveAsync(string npcId, CancellationToken ct);

    Task<WorldSnapshot> GetWorldSnapshotAsync(string npcId, CancellationToken ct);

    Task<GameObservation> ObserveAsync(NpcBodyBinding bodyBinding, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bodyBinding);
        return ObserveAsync(bodyBinding.TargetEntityId, ct);
    }

    Task<WorldSnapshot> GetWorldSnapshotAsync(NpcBodyBinding bodyBinding, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bodyBinding);
        return GetWorldSnapshotAsync(bodyBinding.TargetEntityId, ct);
    }
}

public interface IGameEventSource
{
    Task<IReadOnlyList<GameEventRecord>> PollAsync(GameEventCursor cursor, CancellationToken ct);

    async Task<GameEventBatch> PollBatchAsync(GameEventCursor cursor, CancellationToken ct)
    {
        var records = await PollAsync(cursor, ct);
        return new GameEventBatch(records, GameEventCursor.Advance(cursor, records));
    }
}
