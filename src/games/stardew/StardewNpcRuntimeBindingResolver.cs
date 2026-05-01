using Hermes.Agent.Game;
using Hermes.Agent.Runtime;

namespace Hermes.Agent.Games.Stardew;

public sealed record StardewNpcRuntimeBinding(
    NpcRuntimeDescriptor Descriptor,
    NpcPack Pack);

public sealed class StardewNpcRuntimeBindingResolver
{
    private readonly INpcPackLoader _packLoader;
    private readonly string _packRoot;

    public StardewNpcRuntimeBindingResolver(INpcPackLoader packLoader, string packRoot)
    {
        _packLoader = packLoader;
        _packRoot = packRoot;
    }

    public string PackRoot => _packRoot;

    public StardewNpcRuntimeBinding Resolve(string? rawNpcId, string saveId)
    {
        if (string.IsNullOrWhiteSpace(rawNpcId))
            throw new ArgumentException("NPC id is required.", nameof(rawNpcId));
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("Save id is required.", nameof(saveId));

        var packs = _packLoader.LoadPacks(_packRoot);
        if (packs.Count == 0)
            throw new InvalidOperationException($"No Stardew NPC packs were found under '{_packRoot}'.");

        var lookup = rawNpcId.Trim();
        var catalog = new StardewNpcCatalog(packs.Select(pack => pack.Manifest));
        if (!catalog.TryResolve(lookup, out var manifest))
            throw new InvalidOperationException($"Could not resolve Stardew NPC pack for '{lookup}'.");

        var pack = packs.First(pack => string.Equals(pack.Manifest.NpcId, manifest.NpcId, StringComparison.OrdinalIgnoreCase));
        return new StardewNpcRuntimeBinding(
            NpcRuntimeDescriptorFactory.Create(pack, saveId),
            pack);
    }
}
