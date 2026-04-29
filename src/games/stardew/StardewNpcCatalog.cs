namespace Hermes.Agent.Games.Stardew;

using Hermes.Agent.Game;

public sealed class StardewNpcCatalog
{
    private readonly Dictionary<string, NpcPackManifest> _byNpcId;
    private readonly Dictionary<string, string> _aliasToNpcId;

    public StardewNpcCatalog(IEnumerable<NpcPackManifest> manifests)
    {
        _byNpcId = new Dictionary<string, NpcPackManifest>(StringComparer.OrdinalIgnoreCase);
        _aliasToNpcId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var manifest in manifests)
        {
            if (!_byNpcId.TryAdd(manifest.NpcId, manifest))
                throw new InvalidOperationException($"Duplicate npcId '{manifest.NpcId}'.");

            AddAlias(manifest.NpcId, manifest.NpcId);
            AddAlias(manifest.NpcId, manifest.DisplayName);
            AddAlias(manifest.NpcId, manifest.SmapiName);
            foreach (var alias in manifest.Aliases)
                AddAlias(manifest.NpcId, alias);
        }
    }

    public bool TryResolve(string rawName, out NpcPackManifest manifest)
    {
        manifest = default!;
        if (string.IsNullOrWhiteSpace(rawName))
            return false;

        if (!_aliasToNpcId.TryGetValue(rawName.Trim(), out var npcId))
            return false;

        return _byNpcId.TryGetValue(npcId, out manifest!);
    }

    private void AddAlias(string npcId, string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return;

        var normalized = alias.Trim();
        if (_aliasToNpcId.TryGetValue(normalized, out var existing) &&
            !string.Equals(existing, npcId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Alias '{alias}' maps to both '{existing}' and '{npcId}'.");
        }

        _aliasToNpcId[normalized] = npcId;
    }
}
