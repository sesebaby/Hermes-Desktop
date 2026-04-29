namespace Hermes.Agent.Runtime;

public sealed class NpcRuntimeInstance
{
    private readonly object _gate = new();

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
                LastError);
        }
    }
}
