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

    Task<GameCommandStatus> CancelAsync(string commandId, string reason, CancellationToken ct);
}

public interface IGameQueryService
{
    Task<GameObservation> ObserveAsync(string npcId, CancellationToken ct);

    Task<WorldSnapshot> GetWorldSnapshotAsync(string npcId, CancellationToken ct);
}

public interface IGameEventSource
{
    Task<IReadOnlyList<GameEventRecord>> PollAsync(GameEventCursor cursor, CancellationToken ct);
}
