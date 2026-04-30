namespace Hermes.Agent.Memory;

using Hermes.Agent.Core;

/// <summary>
/// Adapts curated MEMORY.md / USER.md state into the memory lifecycle without
/// making curated memory part of dynamic recall injection.
/// </summary>
public sealed class CuratedMemoryLifecycleProvider : IMemoryProvider, IMemoryCompressionParticipant
{
    private readonly MemoryManager _memoryManager;
    private readonly bool _includeMemory;
    private readonly bool _includeUser;

    public CuratedMemoryLifecycleProvider(
        MemoryManager memoryManager,
        bool includeMemory = true,
        bool includeUser = true)
    {
        _memoryManager = memoryManager;
        _includeMemory = includeMemory;
        _includeUser = includeUser;
    }

    public string Name => "curated-memory";

    public async Task OnTurnStartAsync(int turnNumber, string userMessage, string sessionId, CancellationToken ct)
    {
        if (turnNumber == 0)
            await RefreshSnapshotAsync(ct);
    }

    public Task<string?> PrefetchAsync(string query, string sessionId, CancellationToken ct)
        => Task.FromResult<string?>(null);

    public Task SyncTurnAsync(string userContent, string assistantContent, string sessionId, CancellationToken ct)
        => Task.CompletedTask;

    public Task QueuePrefetchAsync(string query, string sessionId, CancellationToken ct)
        => Task.CompletedTask;

    public Task OnPreCompressAsync(IReadOnlyList<Message> messages, string sessionId, CancellationToken ct)
        => RefreshSnapshotAsync(ct);

    private async Task RefreshSnapshotAsync(CancellationToken ct)
    {
        await _memoryManager.BuildSystemPromptSnapshotAsync(_includeMemory, _includeUser, ct);
    }
}
