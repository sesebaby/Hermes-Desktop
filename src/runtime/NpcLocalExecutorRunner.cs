namespace Hermes.Agent.Runtime;

public interface INpcLocalExecutorRunner
{
    Task<NpcLocalExecutorResult> ExecuteAsync(
        NpcRuntimeDescriptor descriptor,
        NpcLocalActionIntent intent,
        IReadOnlyList<NpcObservationFact> facts,
        string traceId,
        CancellationToken ct);
}

public sealed record NpcLocalExecutorResult(
    string Target,
    string Stage,
    string Result,
    string DecisionResponse,
    string? MemorySummary = null,
    string? CommandId = null,
    string? Error = null);
