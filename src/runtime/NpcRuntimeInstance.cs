namespace Hermes.Agent.Runtime;

public sealed class NpcRuntimeInstance
{
    private readonly object _gate = new();
    private NpcRuntimeAgentHandle? _privateChatHandle;
    private string? _privateChatRebindKey;
    private int _privateChatRebindGeneration;
    private NpcRuntimeAutonomyHandle? _autonomyHandle;
    private string? _autonomyRebindKey;
    private int _autonomyRebindGeneration;

    public NpcRuntimeInstance(NpcRuntimeDescriptor descriptor, NpcNamespace npcNamespace)
    {
        Descriptor = descriptor;
        Namespace = npcNamespace;
    }

    public NpcRuntimeDescriptor Descriptor { get; }

    public NpcNamespace Namespace { get; }

    public NpcRuntimeState State { get; private set; } = NpcRuntimeState.Created;

    public string? LastTraceId { get; private set; }

    public string? LastError { get; private set; }

    public Task StartAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Namespace.EnsureDirectories();
            State = NpcRuntimeState.Running;
            LastError = null;
        }

        return Task.CompletedTask;
    }

    public Task PauseAsync(string reason, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            State = NpcRuntimeState.Paused;
            LastError = string.IsNullOrWhiteSpace(reason) ? null : reason;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            State = NpcRuntimeState.Stopped;
        }

        return Task.CompletedTask;
    }

    public void RecordTrace(string traceId)
    {
        if (string.IsNullOrWhiteSpace(traceId))
            return;

        lock (_gate)
            LastTraceId = traceId;
    }

    public void RecordError(string error)
    {
        lock (_gate)
        {
            State = NpcRuntimeState.Faulted;
            LastError = error;
        }
    }

    public NpcRuntimeAgentHandle GetOrCreatePrivateChatHandle(
        string rebindKey,
        Func<int, NpcRuntimeAgentHandle> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rebindKey);
        ArgumentNullException.ThrowIfNull(factory);

        lock (_gate)
        {
            if (_privateChatHandle is not null &&
                string.Equals(_privateChatRebindKey, rebindKey, StringComparison.Ordinal))
            {
                return _privateChatHandle;
            }

            _privateChatRebindGeneration++;
            var handle = factory(_privateChatRebindGeneration);
            _privateChatHandle = handle;
            _privateChatRebindKey = rebindKey;
            return handle;
        }
    }

    public NpcRuntimeAutonomyHandle GetOrCreateAutonomyHandle(
        string rebindKey,
        Func<int, NpcRuntimeAutonomyHandle> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rebindKey);
        ArgumentNullException.ThrowIfNull(factory);

        lock (_gate)
        {
            if (_autonomyHandle is not null &&
                string.Equals(_autonomyRebindKey, rebindKey, StringComparison.Ordinal))
            {
                return _autonomyHandle;
            }

            _autonomyRebindGeneration++;
            var handle = factory(_autonomyRebindGeneration);
            _autonomyHandle = handle;
            _autonomyRebindKey = rebindKey;
            return handle;
        }
    }

    public NpcRuntimeSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new NpcRuntimeSnapshot(
                Descriptor.NpcId,
                Descriptor.DisplayName,
                Descriptor.GameId,
                Descriptor.SaveId,
                Descriptor.ProfileId,
                Descriptor.SessionId,
                State,
                LastTraceId,
                LastError,
                _privateChatRebindGeneration,
                _autonomyRebindGeneration);
        }
    }
}
