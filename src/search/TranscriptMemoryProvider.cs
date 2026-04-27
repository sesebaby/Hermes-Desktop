namespace Hermes.Agent.Search;

using Hermes.Agent.Memory;

/// <summary>
/// Adapts transcript recall to the Python-style memory provider lifecycle.
/// Transcript writes are already handled by TranscriptStore, so sync/queue are no-ops.
/// </summary>
public sealed class TranscriptMemoryProvider : IMemoryProvider
{
    private readonly TranscriptRecallService _recallService;

    public TranscriptMemoryProvider(TranscriptRecallService recallService)
    {
        _recallService = recallService;
    }

    public string Name => "transcript";

    public Task OnTurnStartAsync(int turnNumber, string userMessage, string sessionId, CancellationToken ct)
        => Task.CompletedTask;

    public async Task<string?> PrefetchAsync(string query, string sessionId, CancellationToken ct)
    {
        var result = await _recallService.RecallAsync(query, sessionId, ct: ct);
        return result.ContextBlock;
    }

    public Task SyncTurnAsync(string userContent, string assistantContent, string sessionId, CancellationToken ct)
        => Task.CompletedTask;

    public Task QueuePrefetchAsync(string query, string sessionId, CancellationToken ct)
        => Task.CompletedTask;
}

