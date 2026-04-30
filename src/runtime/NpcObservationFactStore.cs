using Hermes.Agent.Game;

namespace Hermes.Agent.Runtime;

public sealed class NpcObservationFactStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<NpcObservationFact>> _facts = new(StringComparer.OrdinalIgnoreCase);

    public void RecordObservation(NpcRuntimeDescriptor descriptor, GameObservation observation)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(observation);

        EnsureObservationMatchesRuntime(descriptor, observation);

        Record(new NpcObservationFact(
            descriptor.NpcId,
            descriptor.GameId,
            descriptor.SaveId,
            descriptor.ProfileId,
            "observation",
            null,
            observation.TimestampUtc,
            observation.Summary,
            observation.Facts.ToArray()));
    }

    public void RecordEvent(NpcRuntimeDescriptor descriptor, GameEventRecord record)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(record);

        EnsureEventMatchesRuntime(descriptor, record);

        Record(new NpcObservationFact(
            descriptor.NpcId,
            descriptor.GameId,
            descriptor.SaveId,
            descriptor.ProfileId,
            "event",
            record.EventId,
            record.TimestampUtc,
            record.Summary,
            [record.EventType]));
    }

    public IReadOnlyList<NpcObservationFact> Snapshot(NpcRuntimeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var key = BuildKey(descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId);
        lock (_gate)
        {
            return _facts.TryGetValue(key, out var facts)
                ? facts.ToArray()
                : [];
        }
    }

    private void Record(NpcObservationFact fact)
    {
        var key = BuildKey(fact.GameId, fact.SaveId, fact.NpcId, fact.ProfileId);
        lock (_gate)
        {
            if (!_facts.TryGetValue(key, out var facts))
            {
                facts = [];
                _facts[key] = facts;
            }

            facts.Add(fact);
        }
    }

    private static void EnsureObservationMatchesRuntime(NpcRuntimeDescriptor descriptor, GameObservation observation)
    {
        if (!string.Equals(descriptor.NpcId, observation.NpcId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Observation NPC '{observation.NpcId}' does not match runtime NPC '{descriptor.NpcId}'.");

        if (!string.Equals(descriptor.GameId, observation.GameId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Observation game '{observation.GameId}' does not match runtime game '{descriptor.GameId}'.");
    }

    private static void EnsureEventMatchesRuntime(NpcRuntimeDescriptor descriptor, GameEventRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.NpcId))
            return;

        if (!string.Equals(descriptor.NpcId, record.NpcId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Event NPC '{record.NpcId}' does not match runtime NPC '{descriptor.NpcId}'.");
    }

    private static string BuildKey(string gameId, string saveId, string npcId, string profileId)
        => $"{gameId}:{saveId}:{npcId}:{profileId}";
}

public sealed record NpcObservationFact(
    string NpcId,
    string GameId,
    string SaveId,
    string ProfileId,
    string SourceKind,
    string? SourceId,
    DateTime TimestampUtc,
    string Summary,
    IReadOnlyList<string> Facts);
