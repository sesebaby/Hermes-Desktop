namespace Hermes.Agent.Runtime;

using Hermes.Agent.Game;
using Hermes.Agent.Tasks;

public sealed class NpcRuntimeInstance
{
    private readonly object _gate = new();
    private NpcRuntimeAgentHandle? _privateChatHandle;
    private string? _privateChatRebindKey;
    private int _privateChatRebindGeneration;
    private NpcRuntimeAutonomyHandle? _autonomyHandle;
    private string? _autonomyRebindKey;
    private int _autonomyRebindGeneration;
    private int _sessionLeaseGeneration;
    private NpcRuntimeSessionLeaseSnapshot? _activePrivateChatSessionLease;
    private NpcAutonomyLoopState _autonomyLoopState = NpcAutonomyLoopState.NotStarted;
    private string? _pauseReason;
    private DateTime? _lastAutomaticTickAtUtc;
    private string? _currentBridgeKey;
    private int _currentAutonomyHandleGeneration;
    private int _autonomyRestartCount;
    private GameEventCursor _eventCursor = new();
    private NpcRuntimePendingWorkItemSnapshot? _pendingWorkItem;
    private NpcRuntimeActionSlotSnapshot? _actionSlot;
    private DateTime? _nextWakeAtUtc;
    private IReadOnlyList<NpcRuntimeIngressWorkItemSnapshot> _ingressWorkItems = [];
    private GameCommandStatus? _lastTerminalCommandStatus;
    private int _inboxDepth;
    private readonly SessionTodoStore _todoStore = new();
    private Task? _taskHydrationTask;
    private bool _tasksHydrated;
    private Exception? _lastTaskHydrationError;

    public NpcRuntimeInstance(NpcRuntimeDescriptor descriptor, NpcNamespace npcNamespace)
    {
        Descriptor = descriptor;
        Namespace = npcNamespace;
    }

    public NpcRuntimeDescriptor Descriptor { get; }

    public NpcNamespace Namespace { get; }

    internal SessionTodoStore TodoStore => _todoStore;

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
            if (_autonomyLoopState is NpcAutonomyLoopState.Stopped)
                _autonomyLoopState = NpcAutonomyLoopState.NotStarted;
        }

        return Task.CompletedTask;
    }

    public async Task EnsureTasksHydratedAsync(INpcRuntimeTaskHydrator hydrator, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(hydrator);
        ct.ThrowIfCancellationRequested();

        Task hydrationTask;
        lock (_gate)
        {
            if (_tasksHydrated)
                return;

            if (_taskHydrationTask is null || _taskHydrationTask.IsCompleted)
                _taskHydrationTask = RunTaskHydrationAsync(hydrator);

            hydrationTask = _taskHydrationTask;
        }

        await hydrationTask.WaitAsync(ct);
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
            _autonomyLoopState = NpcAutonomyLoopState.Stopped;
            _pauseReason = string.IsNullOrWhiteSpace(_pauseReason) ? "stopped" : _pauseReason;
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
            _autonomyLoopState = NpcAutonomyLoopState.Faulted;
            _pauseReason = error;
        }
    }

    public IPrivateChatSessionLease AcquirePrivateChatSessionLease(string conversationId, string owner, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        lock (_gate)
        {
            _sessionLeaseGeneration++;
            _activePrivateChatSessionLease = new NpcRuntimeSessionLeaseSnapshot(
                conversationId,
                owner,
                reason,
                _sessionLeaseGeneration,
                DateTime.UtcNow);
            _autonomyLoopState = NpcAutonomyLoopState.Paused;
            _pauseReason = reason;
            return new RuntimePrivateChatSessionLease(this, Descriptor.NpcId, conversationId, owner, _sessionLeaseGeneration);
        }
    }

    public bool TryGetActivePrivateChatSessionLease(out NpcRuntimeSessionLeaseSnapshot? lease)
    {
        lock (_gate)
        {
            lease = _activePrivateChatSessionLease;
            return lease is not null;
        }
    }

    public void MarkAutonomyPaused(string reason, string? bridgeKey = null, int? handleGeneration = null)
    {
        lock (_gate)
        {
            _autonomyLoopState = NpcAutonomyLoopState.Paused;
            _pauseReason = reason;
            if (bridgeKey is not null)
                _currentBridgeKey = bridgeKey;

            if (handleGeneration.HasValue)
                _currentAutonomyHandleGeneration = handleGeneration.Value;
        }
    }

    public void MarkAutonomyRunning(string bridgeKey, int handleGeneration, DateTime tickedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bridgeKey);

        lock (_gate)
        {
            if (State is not NpcRuntimeState.Stopped)
                State = NpcRuntimeState.Running;

            LastError = null;
            _autonomyLoopState = NpcAutonomyLoopState.Running;
            _pauseReason = null;
            _lastAutomaticTickAtUtc = tickedAtUtc;
            _currentBridgeKey = bridgeKey;
            _currentAutonomyHandleGeneration = handleGeneration;
            _nextWakeAtUtc = null;
        }
    }

    public void RecordAutonomyRestart(string? bridgeKey = null, int? handleGeneration = null)
    {
        lock (_gate)
        {
            _autonomyRestartCount++;
            if (bridgeKey is not null)
                _currentBridgeKey = bridgeKey;

            if (handleGeneration.HasValue)
                _currentAutonomyHandleGeneration = handleGeneration.Value;
        }
    }

    internal void RestoreControllerSnapshot(NpcRuntimeControllerSnapshot controller)
    {
        ArgumentNullException.ThrowIfNull(controller);

        lock (_gate)
        {
            _eventCursor = controller.EventCursor ?? new GameEventCursor();
            _pendingWorkItem = controller.PendingWorkItem;
            _actionSlot = controller.ActionSlot;
            _nextWakeAtUtc = controller.NextWakeAtUtc;
            _ingressWorkItems = controller.IngressWorkItems.ToArray();
            _lastTerminalCommandStatus = controller.LastTerminalCommandStatus;
            _inboxDepth = Math.Max(0, controller.InboxDepth);
        }
    }

    internal void RestorePrivateChatSessionLease(NpcRuntimeSessionLeaseSnapshot? lease)
    {
        lock (_gate)
        {
            _activePrivateChatSessionLease = lease;
            if (lease is not null)
            {
                _sessionLeaseGeneration = Math.Max(_sessionLeaseGeneration, lease.Generation);
                _autonomyLoopState = NpcAutonomyLoopState.Paused;
                _pauseReason = lease.Reason;
            }
        }
    }

    internal bool TryReleasePrivateChatSessionLease(NpcRuntimeSessionLeaseSnapshot lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return TryReleasePrivateChatSessionLease(lease.Owner, lease.Generation);
    }

    internal void RecordEventCursor(GameEventCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);

        lock (_gate)
            _eventCursor = cursor;
    }

    internal void SetPendingWorkItem(NpcRuntimePendingWorkItemSnapshot? pendingWorkItem)
    {
        lock (_gate)
            _pendingWorkItem = pendingWorkItem;
    }

    internal void SetActionSlot(NpcRuntimeActionSlotSnapshot? actionSlot)
    {
        lock (_gate)
            _actionSlot = actionSlot;
    }

    internal void SetNextWakeAtUtc(DateTime? nextWakeAtUtc)
    {
        lock (_gate)
            _nextWakeAtUtc = nextWakeAtUtc;
    }

    internal void SetIngressWorkItems(IReadOnlyList<NpcRuntimeIngressWorkItemSnapshot> ingressWorkItems)
    {
        ArgumentNullException.ThrowIfNull(ingressWorkItems);

        lock (_gate)
            _ingressWorkItems = ingressWorkItems.ToArray();
    }

    internal void EnqueueIngressWorkItem(NpcRuntimeIngressWorkItemSnapshot workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);

        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(workItem.IdempotencyKey) &&
                _ingressWorkItems.Any(item => string.Equals(item.IdempotencyKey, workItem.IdempotencyKey, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            if (_ingressWorkItems.Any(item => string.Equals(item.WorkItemId, workItem.WorkItemId, StringComparison.OrdinalIgnoreCase)))
                return;

            _ingressWorkItems = [.. _ingressWorkItems, workItem];
        }
    }

    internal void RemoveIngressWorkItem(string workItemId)
    {
        if (string.IsNullOrWhiteSpace(workItemId))
            return;

        lock (_gate)
            _ingressWorkItems = _ingressWorkItems
                .Where(item => !string.Equals(item.WorkItemId, workItemId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
    }

    internal void SetInboxDepth(int inboxDepth)
    {
        lock (_gate)
            _inboxDepth = Math.Max(0, inboxDepth);
    }

    internal void SetControllerState(GameEventCursor eventCursor, DateTime? nextWakeAtUtc)
    {
        ArgumentNullException.ThrowIfNull(eventCursor);

        lock (_gate)
        {
            _eventCursor = eventCursor;
            _nextWakeAtUtc = nextWakeAtUtc;
        }
    }

    internal void SetLastTerminalCommandStatus(GameCommandStatus? status)
    {
        lock (_gate)
            _lastTerminalCommandStatus = status;
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
                _autonomyRebindGeneration,
                _autonomyLoopState,
                _pauseReason,
                _lastAutomaticTickAtUtc,
                _currentBridgeKey,
                _currentAutonomyHandleGeneration,
                _autonomyRestartCount,
                Descriptor.EffectiveBodyBinding,
                _activePrivateChatSessionLease,
                new NpcRuntimeControllerSnapshot(
                    _eventCursor,
                    _pendingWorkItem,
                    _actionSlot,
                    _nextWakeAtUtc,
                    _inboxDepth,
                    _ingressWorkItems,
                    _lastTerminalCommandStatus));
        }
    }

    internal bool TryGetTaskView(string sessionId, out NpcRuntimeTaskView? taskView)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        if (!MatchesDescriptorSession(sessionId))
        {
            taskView = null;
            return false;
        }

        lock (_gate)
        {
            taskView = new NpcRuntimeTaskView(
                sessionId,
                _todoStore.Read(sessionId));
            return true;
        }
    }

    private bool MatchesDescriptorSession(string sessionId)
        => string.Equals(sessionId, Descriptor.SessionId, StringComparison.OrdinalIgnoreCase) ||
           sessionId.StartsWith(Descriptor.SessionId + ":", StringComparison.OrdinalIgnoreCase);

    private async Task RunTaskHydrationAsync(INpcRuntimeTaskHydrator hydrator)
    {
        try
        {
            await hydrator.HydrateAsync(this, CancellationToken.None);
            lock (_gate)
            {
                _tasksHydrated = true;
                _lastTaskHydrationError = null;
            }
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _taskHydrationTask = null;
                _lastTaskHydrationError = ex;
            }

            throw;
        }
    }

    private bool TryReleasePrivateChatSessionLease(string owner, int generation)
    {
        lock (_gate)
        {
            if (_activePrivateChatSessionLease is null ||
                _activePrivateChatSessionLease.Generation != generation ||
                !string.Equals(_activePrivateChatSessionLease.Owner, owner, StringComparison.Ordinal))
            {
                return false;
            }

            var releasedLease = _activePrivateChatSessionLease;
            _activePrivateChatSessionLease = null;
            if (_autonomyLoopState is NpcAutonomyLoopState.Paused &&
                string.Equals(_pauseReason, releasedLease.Reason, StringComparison.Ordinal))
            {
                _pauseReason = null;
                _autonomyLoopState = _lastAutomaticTickAtUtc.HasValue
                    ? NpcAutonomyLoopState.Running
                    : NpcAutonomyLoopState.NotStarted;
            }

            return true;
        }
    }

    private sealed class RuntimePrivateChatSessionLease : IPrivateChatSessionLease
    {
        private readonly NpcRuntimeInstance _owner;
        private bool _disposed;

        public RuntimePrivateChatSessionLease(
            NpcRuntimeInstance owner,
            string npcId,
            string conversationId,
            string leaseOwner,
            int generation)
        {
            _owner = owner;
            NpcId = npcId;
            ConversationId = conversationId;
            Owner = leaseOwner;
            Generation = generation;
        }

        public string NpcId { get; }

        public string ConversationId { get; }

        public string Owner { get; }

        public int Generation { get; }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _owner.TryReleasePrivateChatSessionLease(Owner, Generation);
        }
    }
}
