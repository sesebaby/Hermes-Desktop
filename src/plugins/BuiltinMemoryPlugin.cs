namespace Hermes.Agent.Plugins;

using Hermes.Agent.Core;
using Hermes.Agent.Memory;

/// <summary>
/// Built-in curated memory provider.
/// Python parity: MEMORY.md and USER.md are loaded into a frozen system-prompt
/// snapshot at session start; writes during the session persist to disk but do
/// not mutate the current prompt snapshot.
/// </summary>
public sealed class BuiltinMemoryPlugin : PluginBase
{
    private readonly MemoryManager _memoryManager;
    private readonly bool _includeMemory;
    private readonly bool _includeUser;
    private string? _sessionSnapshot;

    public BuiltinMemoryPlugin(
        MemoryManager memoryManager,
        bool includeMemory = true,
        bool includeUser = true)
    {
        _memoryManager = memoryManager;
        _includeMemory = includeMemory;
        _includeUser = includeUser;
    }

    public override string Name => "builtin-memory";
    public override bool IsBuiltin => true;
    public override string Category => "memory";

    public override Task<string?> GetSystemPromptBlockAsync(CancellationToken ct)
        => Task.FromResult(_sessionSnapshot);

    public override async Task OnTurnStartAsync(int turnNumber, string userMessage, CancellationToken ct)
    {
        if (turnNumber == 0 || _sessionSnapshot is null)
            _sessionSnapshot = await _memoryManager.BuildSystemPromptSnapshotAsync(_includeMemory, _includeUser, ct);
    }

    public override async Task OnPreCompressAsync(IReadOnlyList<Message> messages, CancellationToken ct)
    {
        _sessionSnapshot = await _memoryManager.BuildSystemPromptSnapshotAsync(_includeMemory, _includeUser, ct);
    }

    public override Task OnSessionEndAsync(IReadOnlyList<Message> messages, CancellationToken ct)
    {
        _sessionSnapshot = null;
        return Task.CompletedTask;
    }
}
