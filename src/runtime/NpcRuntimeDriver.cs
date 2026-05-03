namespace Hermes.Agent.Runtime;

using Hermes.Agent.Game;

public sealed class NpcRuntimeDriver
{
    private readonly NpcRuntimeInstance _instance;
    private readonly NpcRuntimeStateStore _stateStore;

    public NpcRuntimeDriver(NpcRuntimeInstance instance, NpcRuntimeStateStore stateStore)
    {
        _instance = instance;
        _stateStore = stateStore;
    }

    public NpcRuntimeInstance Instance => _instance;

    public async Task InitializeAsync(CancellationToken ct)
    {
        var state = await _stateStore.LoadAsync(ct);
        if (HasPersistedControllerState(state.Controller))
            _instance.RestoreControllerSnapshot(state.Controller);

        if (state.LeaseSnapshot is not null)
            _instance.RestorePrivateChatSessionLease(state.LeaseSnapshot);
    }

    public NpcRuntimeControllerSnapshot Snapshot()
        => _instance.Snapshot().Controller;

    public async Task AcknowledgeEventCursorAsync(GameEventCursor cursor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        _instance.RecordEventCursor(cursor);
        await SyncAsync(ct);
    }

    public async Task SetPendingWorkItemAsync(NpcRuntimePendingWorkItemSnapshot? pendingWorkItem, CancellationToken ct)
    {
        _instance.SetPendingWorkItem(pendingWorkItem);
        await SyncAsync(ct);
    }

    public async Task SetActionSlotAsync(NpcRuntimeActionSlotSnapshot? actionSlot, CancellationToken ct)
    {
        _instance.SetActionSlot(actionSlot);
        await SyncAsync(ct);
    }

    public async Task SetNextWakeAtUtcAsync(DateTime? nextWakeAtUtc, CancellationToken ct)
    {
        _instance.SetNextWakeAtUtc(nextWakeAtUtc);
        await SyncAsync(ct);
    }

    public async Task EnqueueIngressWorkItemAsync(NpcRuntimeIngressWorkItemSnapshot workItem, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        _instance.EnqueueIngressWorkItem(workItem);
        await SyncAsync(ct);
    }

    public async Task RemoveIngressWorkItemAsync(string workItemId, CancellationToken ct)
    {
        _instance.RemoveIngressWorkItem(workItemId);
        await SyncAsync(ct);
    }

    public async Task SetIngressWorkItemsAsync(IReadOnlyList<NpcRuntimeIngressWorkItemSnapshot> ingressWorkItems, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ingressWorkItems);
        _instance.SetIngressWorkItems(ingressWorkItems);
        await SyncAsync(ct);
    }

    public async Task SetControllerStateAsync(GameEventCursor eventCursor, DateTime? nextWakeAtUtc, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(eventCursor);
        _instance.SetControllerState(eventCursor, nextWakeAtUtc);
        await SyncAsync(ct);
    }

    public Task SyncAsync(CancellationToken ct)
    {
        var snapshot = _instance.Snapshot();
        return _stateStore.SaveAsync(
            new NpcRuntimePersistedState(
                snapshot.Controller,
                snapshot.ActivePrivateChatSessionLease),
            ct);
    }

    private static bool HasPersistedControllerState(NpcRuntimeControllerSnapshot controller)
        => !string.IsNullOrWhiteSpace(controller.EventCursor.Since) ||
           controller.EventCursor.Sequence.HasValue ||
           controller.PendingWorkItem is not null ||
           controller.ActionSlot is not null ||
           controller.NextWakeAtUtc.HasValue ||
           controller.IngressWorkItems.Count > 0;
}
