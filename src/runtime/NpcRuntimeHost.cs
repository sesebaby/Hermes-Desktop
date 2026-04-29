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
            .Select(pack => new NpcRuntimeDescriptor(
                pack.Manifest.NpcId,
                pack.Manifest.DisplayName,
                pack.Manifest.GameId,
                saveId,
                pack.Manifest.ProfileId,
                pack.Manifest.AdapterId,
                pack.RootPath,
                $"sdv_{saveId}_{pack.Manifest.NpcId}_{pack.Manifest.ProfileId}"))
            .ToArray();

    public async Task StartDiscoveredAsync(string packRoot, string saveId, CancellationToken ct)
    {
        foreach (var descriptor in DiscoverDescriptors(packRoot, saveId))
            await _supervisor.StartAsync(descriptor, _runtimeRoot, ct);
    }
}
