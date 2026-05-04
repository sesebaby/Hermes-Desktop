namespace Hermes.Agent.Runtime;

public sealed class NpcAutonomyBudget
{
    private readonly SemaphoreSlim _llmSlots;

    public NpcAutonomyBudget(NpcAutonomyBudgetOptions options)
    {
        Options = options;
        _llmSlots = new SemaphoreSlim(options.MaxConcurrentLlmRequests, options.MaxConcurrentLlmRequests);
    }

    public NpcAutonomyBudgetOptions Options { get; }

    public async Task<NpcAutonomyBudgetLease?> TryAcquireLlmSlotAsync(string npcId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(npcId))
            throw new ArgumentException("npcId is required.", nameof(npcId));

        var acquired = await _llmSlots.WaitAsync(millisecondsTimeout: 0, ct);
        return acquired ? new NpcAutonomyBudgetLease(npcId, _llmSlots) : null;
    }

    public NpcAutonomyExitReason CheckToolIterationLimit(int completedToolIterations)
        => completedToolIterations >= Options.MaxToolIterations
            ? NpcAutonomyExitReason.MaxToolIterations
            : NpcAutonomyExitReason.None;

    public NpcAutonomyExitReason CheckRestartLimit(int restartsInScene)
        => restartsInScene >= Options.MaxRestartsPerScene
            ? NpcAutonomyExitReason.MaxRestarts
            : NpcAutonomyExitReason.None;
}

public sealed record NpcAutonomyBudgetOptions(
    int MaxToolIterations = 100,
    int MaxConcurrentLlmRequests = 1,
    TimeSpan? RestartCooldown = null,
    int MaxRestartsPerScene = 3,
    TimeSpan? LlmTurnTimeout = null)
{
    public TimeSpan EffectiveRestartCooldown => RestartCooldown ?? TimeSpan.FromSeconds(5);

    public TimeSpan EffectiveLlmTurnTimeout =>
        LlmTurnTimeout is { } timeout && timeout > TimeSpan.Zero
            ? timeout
            : TimeSpan.FromSeconds(60);
}

public sealed class NpcAutonomyBudgetLease : IAsyncDisposable, IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private bool _disposed;

    internal NpcAutonomyBudgetLease(string npcId, SemaphoreSlim semaphore)
    {
        NpcId = npcId;
        _semaphore = semaphore;
    }

    public string NpcId { get; }

    public void Dispose()
    {
        if (_disposed)
            return;

        _semaphore.Release();
        _disposed = true;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

public enum NpcAutonomyExitReason
{
    None,
    MaxToolIterations,
    MaxRestarts,
    LlmConcurrencyLimit,
    LlmTurnTimeout,
    PausedByWorldState,
    Stopped
}
