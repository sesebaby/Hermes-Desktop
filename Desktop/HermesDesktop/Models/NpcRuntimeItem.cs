namespace HermesDesktop.Models;

internal sealed class NpcRuntimeItem
{
    public string NpcId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string State { get; init; } = "";
    public string SessionId { get; init; } = "";
    public string LastTraceId { get; init; } = "";
    public string LastError { get; init; } = "";
    public string LoopAndWaitSummary { get; init; } = "";
    public string LeaseAndActionSummary { get; init; } = "";
    public string PendingAndCursorSummary { get; init; } = "";
}
