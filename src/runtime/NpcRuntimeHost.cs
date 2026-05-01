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
    {
        foreach (var pack in _packLoader.LoadPacks(packRoot))
        {
            var descriptor = NpcRuntimeDescriptorFactory.Create(pack, saveId);
            var instance = await _supervisor.GetOrStartAsync(descriptor, _runtimeRoot, ct);
            instance.Namespace.SeedPersonaPack(pack);
        }
    }
}
