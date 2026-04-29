namespace Hermes.Agent.Runtime;

public sealed class NpcRuntimeTrace
{
    private readonly object _gate = new();
    private readonly List<NpcRuntimeTraceEvent> _events = new();

    public string TraceId { get; }

    public NpcRuntimeTrace(string traceId)
    {
        if (string.IsNullOrWhiteSpace(traceId))
            throw new ArgumentException("traceId is required.", nameof(traceId));

        TraceId = traceId;
    }

    public void Add(NpcRuntimeTraceEvent traceEvent)
    {
        if (!string.Equals(traceEvent.TraceId, TraceId, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Trace event id does not match this trace.", nameof(traceEvent));

        lock (_gate)
            _events.Add(traceEvent);
    }

    public IReadOnlyList<NpcRuntimeTraceEvent> Snapshot()
    {
        lock (_gate)
            return _events.OrderBy(item => item.TimestampUtc).ToArray();
    }
}

public sealed record NpcRuntimeTraceEvent(
    DateTime TimestampUtc,
    string TraceId,
    string NpcId,
    string GameId,
    string? SessionId,
    string ActionType,
    string Stage,
    string Result,
    string? CommandId = null,
    string? Error = null);

public sealed class NpcRuntimeTraceIndex
{
    private readonly object _gate = new();
    private readonly Dictionary<string, NpcRuntimeTrace> _traces = new(StringComparer.OrdinalIgnoreCase);

    public NpcRuntimeTrace GetOrCreate(string traceId)
    {
        lock (_gate)
        {
            if (!_traces.TryGetValue(traceId, out var trace))
            {
                trace = new NpcRuntimeTrace(traceId);
                _traces[traceId] = trace;
            }

            return trace;
        }
    }

    public NpcRuntimeTrace? Find(string traceId)
    {
        lock (_gate)
            return _traces.GetValueOrDefault(traceId);
    }
}
