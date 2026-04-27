namespace Hermes.Agent.Memory;

using Microsoft.Extensions.Logging;

/// <summary>
/// Coordinates all memory providers with Python hermes-agent-main lifecycle semantics.
/// </summary>
public sealed class HermesMemoryOrchestrator
{
    private readonly IReadOnlyList<IMemoryProvider> _providers;
    private readonly ILogger<HermesMemoryOrchestrator> _logger;

    public HermesMemoryOrchestrator(
        IEnumerable<IMemoryProvider> providers,
        ILogger<HermesMemoryOrchestrator> logger)
    {
        _providers = providers.ToList();
        _logger = logger;
    }

    public async Task OnTurnStartAsync(int turnNumber, string userMessage, string sessionId, CancellationToken ct)
    {
        foreach (var provider in _providers)
        {
            try
            {
                await provider.OnTurnStartAsync(turnNumber, userMessage, sessionId, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Memory provider {Provider} OnTurnStart failed non-fatally", provider.Name);
            }
        }
    }

    public async Task<string> PrefetchAllAsync(string query, string sessionId, CancellationToken ct)
    {
        var parts = new List<string>();

        foreach (var provider in _providers)
        {
            try
            {
                var result = await provider.PrefetchAsync(query, sessionId, ct);
                if (!string.IsNullOrWhiteSpace(result))
                    parts.Add(result.Trim());
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Memory provider {Provider} Prefetch failed non-fatally", provider.Name);
            }
        }

        return string.Join("\n\n", parts);
    }

    public async Task SyncAllAsync(string userContent, string assistantContent, string sessionId, CancellationToken ct)
    {
        foreach (var provider in _providers)
        {
            try
            {
                await provider.SyncTurnAsync(userContent, assistantContent, sessionId, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Memory provider {Provider} SyncTurn failed non-fatally", provider.Name);
            }
        }
    }

    public async Task QueuePrefetchAllAsync(string query, string sessionId, CancellationToken ct)
    {
        foreach (var provider in _providers)
        {
            try
            {
                await provider.QueuePrefetchAsync(query, sessionId, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Memory provider {Provider} QueuePrefetch failed non-fatally", provider.Name);
            }
        }
    }

    public async Task SyncCompletedTurnAsync(
        string userContent,
        string assistantContent,
        string sessionId,
        bool interrupted,
        CancellationToken ct)
    {
        if (interrupted)
            return;

        if (string.IsNullOrWhiteSpace(userContent) || string.IsNullOrWhiteSpace(assistantContent))
            return;

        await SyncAllAsync(userContent, assistantContent, sessionId, ct);
        await QueuePrefetchAllAsync(userContent, sessionId, ct);
    }
}

