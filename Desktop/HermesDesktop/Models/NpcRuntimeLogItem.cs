namespace HermesDesktop.Models;

internal sealed class NpcRuntimeLogItem
{
    public string Timestamp { get; init; } = "";
    public string NpcId { get; init; } = "";
    public string TraceId { get; init; } = "";
    public string Stage { get; init; } = "";
    public string Result { get; init; } = "";
}
