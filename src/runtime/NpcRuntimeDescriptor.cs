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

public sealed record NpcRuntimeSnapshot(
    string NpcId,
    string DisplayName,
    string GameId,
    string SaveId,
    string ProfileId,
    string SessionId,
    NpcRuntimeState State,
    string? LastTraceId,
    string? LastError);
