using Hermes.Agent.Game;
using System.Text.Json.Nodes;

namespace Hermes.Agent.Runtime;

public sealed record NpcRuntimeDescriptor(
    string NpcId,
    string DisplayName,
    string GameId,
    string SaveId,
    string ProfileId,
    string AdapterId,
    string PackRoot,
    string SessionId,
    NpcBodyBinding? BodyBinding = null)
{
    public NpcBodyBinding EffectiveBodyBinding
        => BodyBinding ?? new NpcBodyBinding(NpcId, NpcId, DisplayName: DisplayName, AdapterId: AdapterId);
}

public enum NpcRuntimeState
{
    Created,
    Running,
    Paused,
    Stopped,
    Faulted
}

public enum NpcAutonomyLoopState
{
    NotStarted,
    Running,
    Paused,
    Faulted,
    Stopped
}

public sealed record NpcRuntimeSessionLeaseSnapshot(
    string ConversationId,
    string Owner,
    string Reason,
    int Generation,
    DateTime AcquiredAtUtc);

public sealed record NpcRuntimePendingWorkItemSnapshot(
    string WorkItemId,
    string WorkType,
    string? CommandId,
    string Status,
    DateTime CreatedAtUtc,
    string? IdempotencyKey = null);

public sealed record NpcRuntimeActionSlotSnapshot(
    string SlotName,
    string WorkItemId,
    string? CommandId,
    string? TraceId,
    DateTime StartedAtUtc,
    DateTime? TimeoutAtUtc);

public sealed record NpcRuntimeIngressWorkItemSnapshot(
    string WorkItemId,
    string WorkType,
    string Status,
    DateTime CreatedAtUtc,
    string? IdempotencyKey = null,
    string? TraceId = null,
    JsonObject? Payload = null);

public sealed record NpcRuntimeControllerSnapshot
{
    public NpcRuntimeControllerSnapshot(
        GameEventCursor eventCursor,
        NpcRuntimePendingWorkItemSnapshot? pendingWorkItem,
        NpcRuntimeActionSlotSnapshot? actionSlot,
        DateTime? nextWakeAtUtc,
        int inboxDepth = 0,
        IReadOnlyList<NpcRuntimeIngressWorkItemSnapshot>? ingressWorkItems = null,
        GameCommandStatus? lastTerminalCommandStatus = null)
    {
        EventCursor = eventCursor;
        PendingWorkItem = pendingWorkItem;
        ActionSlot = actionSlot;
        NextWakeAtUtc = nextWakeAtUtc;
        InboxDepth = Math.Max(0, inboxDepth);
        IngressWorkItems = ingressWorkItems?.ToArray() ?? [];
        LastTerminalCommandStatus = lastTerminalCommandStatus;
    }

    public GameEventCursor EventCursor { get; init; }

    public NpcRuntimePendingWorkItemSnapshot? PendingWorkItem { get; init; }

    public NpcRuntimeActionSlotSnapshot? ActionSlot { get; init; }

    public DateTime? NextWakeAtUtc { get; init; }

    public int InboxDepth { get; init; }

    public IReadOnlyList<NpcRuntimeIngressWorkItemSnapshot> IngressWorkItems { get; init; }

    public GameCommandStatus? LastTerminalCommandStatus { get; init; }

    public static NpcRuntimeControllerSnapshot Empty { get; } = new(new GameEventCursor(), null, null, null);
}

public sealed record NpcRuntimeSnapshot(
    string NpcId,
    string DisplayName,
    string GameId,
    string SaveId,
    string ProfileId,
    string SessionId,
    NpcRuntimeState State,
    string? LastTraceId,
    string? LastError,
    int PrivateChatRebindGeneration,
    int AutonomyRebindGeneration,
    NpcAutonomyLoopState AutonomyLoopState,
    string? PauseReason,
    DateTime? LastAutomaticTickAtUtc,
    string? CurrentBridgeKey,
    int CurrentAutonomyHandleGeneration,
    int AutonomyRestartCount,
    NpcBodyBinding? BodyBinding,
    NpcRuntimeSessionLeaseSnapshot? ActivePrivateChatSessionLease,
    NpcRuntimeControllerSnapshot Controller);
