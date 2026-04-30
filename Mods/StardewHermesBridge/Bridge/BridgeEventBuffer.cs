namespace StardewHermesBridge.Bridge;

using System.Text.Json.Nodes;

public sealed class BridgeEventBuffer
{
    private readonly object _gate = new();
    private readonly List<BridgeEventData> _events = new();
    private readonly Func<DateTime> _nowUtc;
    private long _sequence;

    public BridgeEventBuffer(Func<DateTime>? nowUtc = null)
    {
        _nowUtc = nowUtc ?? (() => DateTime.UtcNow);
    }

    public BridgeEventData Record(
        string eventType,
        string? npcId,
        string summary,
        string? correlationId = null,
        JsonObject? payload = null)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("eventType is required.", nameof(eventType));

        var sequence = Interlocked.Increment(ref _sequence);
        var record = new BridgeEventData(
            $"evt_{sequence:000000000000}",
            eventType,
            string.IsNullOrWhiteSpace(npcId) ? null : npcId,
            DateTime.SpecifyKind(_nowUtc(), DateTimeKind.Utc),
            string.IsNullOrWhiteSpace(summary) ? eventType : summary,
            string.IsNullOrWhiteSpace(correlationId) ? null : correlationId,
            payload);

        lock (_gate)
            _events.Add(record);

        return record;
    }

    public IReadOnlyList<BridgeEventData> Poll(string? since, string? npcId)
    {
        lock (_gate)
        {
            var startIndex = -1;
            if (!string.IsNullOrWhiteSpace(since))
                startIndex = _events.FindIndex(item => string.Equals(item.EventId, since, StringComparison.OrdinalIgnoreCase));

            return _events
                .Skip(startIndex < 0 ? 0 : startIndex + 1)
                .Where(item => string.IsNullOrWhiteSpace(npcId) ||
                               string.IsNullOrWhiteSpace(item.NpcId) ||
                               string.Equals(item.NpcId, npcId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
    }
}
