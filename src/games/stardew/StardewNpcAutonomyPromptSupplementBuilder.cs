namespace Hermes.Agent.Games.Stardew;

using System.Text;
using System.Text.Json;
using Hermes.Agent.Game;
using Hermes.Agent.Runtime;

public interface IStardewGamingSkillRootProvider
{
    string GetRequiredGamingSkillRoot();
}

public sealed class FixedStardewGamingSkillRootProvider : IStardewGamingSkillRootProvider
{
    private readonly string _gamingSkillRoot;

    public FixedStardewGamingSkillRootProvider(string gamingSkillRoot)
    {
        _gamingSkillRoot = gamingSkillRoot;
    }

    public string GetRequiredGamingSkillRoot()
    {
        if (string.IsNullOrWhiteSpace(_gamingSkillRoot))
            throw new InvalidOperationException("Stardew gaming skill root is required.");

        return Path.GetFullPath(_gamingSkillRoot);
    }
}

public sealed class StardewNpcAutonomyPromptSupplementBuilder
{
    private readonly IStardewGamingSkillRootProvider _gamingSkillRootProvider;

    public StardewNpcAutonomyPromptSupplementBuilder(IStardewGamingSkillRootProvider gamingSkillRootProvider)
    {
        ArgumentNullException.ThrowIfNull(gamingSkillRootProvider);

        _gamingSkillRootProvider = gamingSkillRootProvider;
    }

    public string Build(NpcRuntimeDescriptor descriptor, NpcNamespace npcNamespace, NpcPack pack)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(npcNamespace);
        ArgumentNullException.ThrowIfNull(pack);

        npcNamespace.EnsureDirectories();
        var personaRoot = Path.GetFullPath(pack.RootPath);
        var facts = ReadRequiredPersonaFile(descriptor, personaRoot, pack.Manifest.FactsFile, "factsFile");
        var voice = ReadRequiredPersonaFile(descriptor, personaRoot, pack.Manifest.VoiceFile, "voiceFile");
        var boundaries = ReadRequiredPersonaFile(descriptor, personaRoot, pack.Manifest.BoundariesFile, "boundariesFile");
        var requiredSkillIds = ReadRequiredSkillIds(descriptor, personaRoot, pack.Manifest.SkillsFile);

        var builder = new StringBuilder();
        AppendSection(builder, "Persona Facts", facts);
        AppendSection(builder, "Persona Voice", voice);
        AppendSection(builder, "Persona Boundaries", boundaries);

        builder.AppendLine("## Stardew Required Skills");
        var skillRoot = _gamingSkillRootProvider.GetRequiredGamingSkillRoot();
        foreach (var skillId in requiredSkillIds)
        {
            var skillPath = ResolveRequiredSkillPath(descriptor, skillRoot, skillId);
            builder.AppendLine($"### {skillId}");
            builder.AppendLine(File.ReadAllText(skillPath).Trim());
            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }

    private static void AppendSection(StringBuilder builder, string title, string content)
    {
        builder.AppendLine($"## {title}");
        builder.AppendLine(content.Trim());
        builder.AppendLine();
    }

    private static string ReadRequiredPersonaFile(
        NpcRuntimeDescriptor descriptor,
        string personaRoot,
        string relativePath,
        string manifestField)
    {
        var path = ResolveInsideRoot(descriptor, personaRoot, relativePath, manifestField);
        if (!File.Exists(path))
        {
            throw ResourceException(
                descriptor,
                $"persona file '{relativePath}' declared by {manifestField} was not found at '{path}'.");
        }

        return File.ReadAllText(path);
    }

    private static IReadOnlyList<string> ReadRequiredSkillIds(
        NpcRuntimeDescriptor descriptor,
        string personaRoot,
        string relativePath)
    {
        var skillsPath = ResolveInsideRoot(descriptor, personaRoot, relativePath, "skillsFile");
        if (!File.Exists(skillsPath))
        {
            throw ResourceException(
                descriptor,
                $"skills.json file '{relativePath}' was not found at '{skillsPath}'.");
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(skillsPath));
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
                throw ResourceException(descriptor, $"skills.json at '{skillsPath}' must be an object.");

            if (!document.RootElement.TryGetProperty("required", out var required))
                return Array.Empty<string>();

            if (required.ValueKind is not JsonValueKind.Array)
                throw ResourceException(descriptor, $"skills.json required at '{skillsPath}' must be an array.");

            return required
                .EnumerateArray()
                .Select(item => item.ValueKind is JsonValueKind.String ? item.GetString() : null)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!.Trim())
                .ToArray();
        }
        catch (JsonException ex)
        {
            throw ResourceException(descriptor, $"skills.json at '{skillsPath}' is invalid: {ex.Message}");
        }
    }

    private static string ResolveRequiredSkillPath(NpcRuntimeDescriptor descriptor, string skillRoot, string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId))
            throw ResourceException(descriptor, "required Stardew skill id cannot be empty.");

        if (skillId.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
            throw ResourceException(descriptor, $"required Stardew skill id '{skillId}' must not contain path separators.");

        var fullRoot = Path.GetFullPath(skillRoot);
        var path = ResolveInsideRoot(descriptor, fullRoot, $"{skillId}.md", $"required skill '{skillId}'");
        if (!File.Exists(path))
        {
            throw ResourceException(
                descriptor,
                $"required skill '{skillId}' was not found at '{path}'.");
        }

        return path;
    }

    private static string ResolveInsideRoot(
        NpcRuntimeDescriptor descriptor,
        string root,
        string relativePath,
        string label)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw ResourceException(descriptor, $"{label} is required.");

        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        if (!fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw ResourceException(descriptor, $"{label} path '{relativePath}' must stay inside '{fullRoot}'.");
        }

        return fullPath;
    }

    private static StardewNpcAutonomyPromptResourceException ResourceException(
        NpcRuntimeDescriptor descriptor,
        string detail)
        => new($"{StardewNpcAutonomyPromptResourceException.MessagePrefix} for NPC '{descriptor.NpcId}' in save '{descriptor.SaveId}', profile '{descriptor.ProfileId}': {detail}");
}

public sealed class StardewNpcAutonomyPromptResourceException : InvalidOperationException
{
    public const string MessagePrefix = "Stardew autonomy prompt resource error";

    public StardewNpcAutonomyPromptResourceException(string message)
        : base(message)
    {
    }
}
