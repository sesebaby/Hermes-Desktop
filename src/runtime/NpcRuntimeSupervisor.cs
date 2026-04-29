namespace Hermes.Agent.Runtime;

public sealed class NpcRuntimeSupervisor
{
    private readonly object _gate = new();
    private readonly Dictionary<string, NpcRuntimeInstance> _instances = new(StringComparer.OrdinalIgnoreCase);

    public NpcRuntimeInstance Register(NpcRuntimeDescriptor descriptor, string runtimeRoot)
    {
        var key = BuildKey(descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId);
        lock (_gate)
        {
            if (_instances.ContainsKey(key))
                throw new InvalidOperationException($"NPC runtime already registered for '{key}'.");

            var npcNamespace = new NpcNamespace(
                runtimeRoot,
                descriptor.GameId,
                descriptor.SaveId,
                descriptor.NpcId,
                descriptor.ProfileId);
            var instance = new NpcRuntimeInstance(descriptor, npcNamespace);
            _instances[key] = instance;
            return instance;
        }
    }

    public async Task StartAsync(NpcRuntimeDescriptor descriptor, string runtimeRoot, CancellationToken ct)
    {
        var instance = Register(descriptor, runtimeRoot);
        await instance.StartAsync(ct);
    }

    public async Task StopAsync(string gameId, string saveId, string npcId, string profileId, CancellationToken ct)
    {
        var key = BuildKey(gameId, saveId, npcId, profileId);
        NpcRuntimeInstance? instance;
        lock (_gate)
            instance = _instances.GetValueOrDefault(key);

        if (instance is not null)
            await instance.StopAsync(ct);
    }

    public IReadOnlyList<NpcRuntimeSnapshot> Snapshot()
    {
        lock (_gate)
            return _instances.Values.Select(instance => instance.Snapshot()).OrderBy(item => item.NpcId).ToArray();
    }

    private static string BuildKey(string gameId, string saveId, string npcId, string profileId)
        => $"{gameId}:{saveId}:{npcId}:{profileId}";
}
