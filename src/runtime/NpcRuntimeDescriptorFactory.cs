using Hermes.Agent.Game;

namespace Hermes.Agent.Runtime;

public static class NpcRuntimeDescriptorFactory
{
    public static NpcRuntimeDescriptor Create(NpcPack pack, string saveId)
    {
        ArgumentNullException.ThrowIfNull(pack);
        if (string.IsNullOrWhiteSpace(saveId))
            throw new ArgumentException("Save id is required.", nameof(saveId));

        var manifest = pack.Manifest;
        var effectiveSaveId = saveId.Trim();

        return new NpcRuntimeDescriptor(
            manifest.NpcId,
            manifest.DisplayName,
            manifest.GameId,
            effectiveSaveId,
            manifest.ProfileId,
            manifest.AdapterId,
            pack.RootPath,
            $"sdv_{effectiveSaveId}_{manifest.NpcId}_{manifest.ProfileId}",
            new NpcBodyBinding(
                manifest.NpcId,
                manifest.TargetEntityId,
                manifest.SmapiName,
                manifest.DisplayName,
                manifest.AdapterId));
    }
}
