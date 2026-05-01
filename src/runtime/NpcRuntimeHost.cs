namespace Hermes.Agent.Runtime;

using Hermes.Agent.Game;

public sealed class NpcRuntimeHost
{
    private readonly INpcPackLoader _packLoader;
    private readonly NpcRuntimeSupervisor _supervisor;
    private readonly string _runtimeRoot;

    public NpcRuntimeHost(INpcPackLoader packLoader, NpcRuntimeSupervisor supervisor, string runtimeRoot)
    {
        _packLoader = packLoader;
        _supervisor = supervisor;
        _runtimeRoot = runtimeRoot;
    }

    public IReadOnlyList<NpcRuntimeDescriptor> DiscoverDescriptors(string packRoot, string saveId)
        => _packLoader.LoadPacks(packRoot)
            .Select(pack => NpcRuntimeDescriptorFactory.Create(pack, saveId))
            .ToArray();

    public async Task StartDiscoveredAsync(string packRoot, string saveId, CancellationToken ct)
        => await StartDiscoveredAsync(packRoot, saveId, enabledNpcIds: null, ct);

    public async Task StartDiscoveredAsync(
        string packRoot,
        string saveId,
        IReadOnlyCollection<string>? enabledNpcIds,
        CancellationToken ct)
    {
        HashSet<string>? enabled = null;
        if (enabledNpcIds is { Count: > 0 })
            enabled = new HashSet<string>(enabledNpcIds, StringComparer.OrdinalIgnoreCase);

        foreach (var pack in _packLoader.LoadPacks(packRoot))
        {
            if (enabled is not null && !enabled.Contains(pack.Manifest.NpcId))
                continue;

            var descriptor = NpcRuntimeDescriptorFactory.Create(pack, saveId);
            var instance = await _supervisor.GetOrStartAsync(descriptor, _runtimeRoot, ct);
            instance.Namespace.SeedPersonaPack(pack);
        }
    }
}
