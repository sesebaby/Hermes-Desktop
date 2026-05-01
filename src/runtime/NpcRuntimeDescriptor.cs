using Hermes.Agent.Game;

namespace Hermes.Agent.Runtime;

public sealed record NpcRuntimeDescriptor(
    string NpcId,
    string DisplayName,
    string GameId,
    string SaveId,
    string ProfileId,
    string AdapterId,
    string PackRoot,
    string SessionId);

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

public sealed record NpcRuntimeControllerSnapshot(
    GameEventCursor EventCursor,
    NpcRuntimePendingWorkItemSnapshot? PendingWorkItem,
    NpcRuntimeActionSlotSnapshot? ActionSlot,
    DateTime? NextWakeAtUtc,
    int InboxDepth = 0)
{
    public static NpcRuntimeControllerSnapshot Empty { get; } = new(new GameEventCursor(), null, null, null, 0);
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
    NpcRuntimeSessionLeaseSnapshot? ActivePrivateChatSessionLease,
    NpcRuntimeControllerSnapshot Controller);
