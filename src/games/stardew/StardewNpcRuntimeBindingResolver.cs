using Hermes.Agent.Game;
using Hermes.Agent.Runtime;

namespace Hermes.Agent.Games.Stardew;

public sealed record StardewNpcRuntimeBinding(
    NpcRuntimeDescriptor Descriptor,
    NpcPack Pack);

public sealed class StardewNpcRuntimeBindingResolver
{
    private readonly INpcPackLoader _packLoader;
    private readonly IStardewNpcPackRootProvider _packRootProvider;

    public StardewNpcRuntimeBindingResolver(INpcPackLoader packLoader, string packRoot)
        : this(packLoader, new FixedStardewNpcPackRootProvider(packRoot))
    {
    }

    public StardewNpcRuntimeBindingResolver(INpcPackLoader packLoader, IStardewNpcPackRootProvider packRootProvider)
    {
        _packLoader = packLoader;
        _packRootProvider = packRootProvider;
    }

    public string PackRoot => _packRootProvider.GetRequiredPackRoot();

    public StardewNpcRuntimeBinding Resolve(string? rawNpcId, string saveId)
    {
        if (string.IsNullOrWhiteSpace(rawNpcId))
            throw new ArgumentException("NPC id is required.", nameof(rawNpcId));
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("Save id is required.", nameof(saveId));

        var packRoot = _packRootProvider.GetRequiredPackRoot();
        var packs = _packLoader.LoadPacks(packRoot);
        if (packs.Count == 0)
            throw new InvalidOperationException($"Stardew NPC pack source '{packRoot}' resolved without any loadable packs.");

        var lookup = rawNpcId.Trim();
        var catalog = new StardewNpcCatalog(packs.Select(pack => pack.Manifest));
        if (!catalog.TryResolve(lookup, out var manifest))
            throw new InvalidOperationException($"Could not resolve Stardew NPC pack for '{lookup}'.");

        var pack = packs.First(pack => string.Equals(pack.Manifest.NpcId, manifest.NpcId, StringComparison.OrdinalIgnoreCase));
        return new StardewNpcRuntimeBinding(
            NpcRuntimeDescriptorFactory.Create(pack, saveId),
            pack);
    }

    private sealed class FixedStardewNpcPackRootProvider : IStardewNpcPackRootProvider
    {
        private readonly string _packRoot;

        public FixedStardewNpcPackRootProvider(string packRoot)
        {
            _packRoot = packRoot;
        }

        public StardewNpcPackSourceResolution Locate()
            => new(
                _packRoot,
                StardewNpcPackSourceKind.Workspace,
                [new StardewNpcPackSourceCandidate(_packRoot, StardewNpcPackSourceKind.Workspace)],
                [],
                "Fixed pack root provider.");

        public string GetRequiredPackRoot() => _packRoot;
    }
}
