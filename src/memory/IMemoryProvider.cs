namespace Hermes.Agent.Memory;

/// <summary>
/// Python-compatible memory provider lifecycle surface.
/// Providers are best-effort: the orchestrator isolates provider failures.
/// </summary>
public interface IMemoryProvider
{
    string Name { get; }

    Task OnTurnStartAsync(int turnNumber, string userMessage, string sessionId, CancellationToken ct);

    Task<string?> PrefetchAsync(string query, string sessionId, CancellationToken ct);

    Task SyncTurnAsync(string userContent, string assistantContent, string sessionId, CancellationToken ct);

    Task QueuePrefetchAsync(string query, string sessionId, CancellationToken ct);
}

