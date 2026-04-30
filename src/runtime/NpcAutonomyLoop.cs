using Hermes.Agent.Game;

namespace Hermes.Agent.Runtime;

public sealed class NpcAutonomyLoop
{
    private readonly IGameAdapter _adapter;
    private readonly NpcObservationFactStore _factStore;

    public NpcAutonomyLoop(IGameAdapter adapter, NpcObservationFactStore factStore)
    {
        _adapter = adapter;
        _factStore = factStore;
    }

    public async Task<NpcAutonomyTickResult> RunOneTickAsync(
        NpcRuntimeDescriptor descriptor,
        GameEventCursor eventCursor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(eventCursor);

        var observation = await _adapter.Queries.ObserveAsync(descriptor.NpcId, ct);
        _factStore.RecordObservation(descriptor, observation);

        var eventFacts = 0;
        var events = await _adapter.Events.PollAsync(eventCursor, ct);
        foreach (var record in events)
        {
            if (!BelongsToRuntimeNpc(descriptor, record))
                continue;

            _factStore.RecordEvent(descriptor, record);
            eventFacts++;
        }

        return new NpcAutonomyTickResult(descriptor.NpcId, 1, eventFacts);
    }

    private static bool BelongsToRuntimeNpc(NpcRuntimeDescriptor descriptor, GameEventRecord record)
        => string.IsNullOrWhiteSpace(record.NpcId)
           || string.Equals(descriptor.NpcId, record.NpcId, StringComparison.OrdinalIgnoreCase);
}

public sealed record NpcAutonomyTickResult(
    string NpcId,
    int ObservationFacts,
    int EventFacts);
