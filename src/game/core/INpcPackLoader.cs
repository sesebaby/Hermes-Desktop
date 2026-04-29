namespace Hermes.Agent.Game;

public interface INpcPackLoader
{
    IReadOnlyList<NpcPack> LoadPacks(string rootPath);

    NpcPackValidationResult Validate(string packRoot, NpcPackManifest manifest);
}
