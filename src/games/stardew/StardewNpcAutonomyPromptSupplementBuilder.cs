namespace Hermes.Agent.Games.Stardew;

using System.Text;
using System.Text.Json;
using Hermes.Agent.Game;
using Hermes.Agent.Runtime;

public interface IStardewGamingSkillRootProvider
{
    IReadOnlyList<string> GetRequiredGamingSkillRoots();
}

public sealed class FixedStardewGamingSkillRootProvider : IStardewGamingSkillRootProvider
{
    private readonly string _gamingSkillRoot;

    public FixedStardewGamingSkillRootProvider(string gamingSkillRoot)
    {
        _gamingSkillRoot = gamingSkillRoot;
    }

    public IReadOnlyList<string> GetRequiredGamingSkillRoots()
    {
        if (string.IsNullOrWhiteSpace(_gamingSkillRoot))
            throw new InvalidOperationException("Stardew gaming skill root is required.");

        return [Path.GetFullPath(_gamingSkillRoot)];
    }
}

public sealed class CompositeStardewGamingSkillRootProvider : IStardewGamingSkillRootProvider
{
    private readonly string[] _gamingSkillRoots;

    public CompositeStardewGamingSkillRootProvider(params string?[] gamingSkillRoots)
    {
        _gamingSkillRoots = gamingSkillRoots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(root => Path.GetFullPath(root!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<string> GetRequiredGamingSkillRoots()
    {
        if (_gamingSkillRoots.Length == 0)
            throw new InvalidOperationException("At least one Stardew gaming skill root is required.");

        return _gamingSkillRoots;
    }
}

public sealed class StardewNpcAutonomyPromptSupplementBuilder
{
    private const int MaxFallbackSummaryChars = 280;
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

        builder.AppendLine("## Stardew Runtime Contract");
        var skillRoots = _gamingSkillRootProvider.GetRequiredGamingSkillRoots();
        foreach (var skillId in requiredSkillIds)
        {
            var skillPath = ResolveRequiredSkillPath(descriptor, skillRoots, skillId);
            builder.AppendLine($"### {skillId}");
            builder.AppendLine(BuildCompactSkillContract(File.ReadAllText(skillPath)));
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

    private static string ResolveRequiredSkillPath(
        NpcRuntimeDescriptor descriptor,
        IReadOnlyList<string> skillRoots,
        string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId))
            throw ResourceException(descriptor, "required Stardew skill id cannot be empty.");

        if (skillId.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
            throw ResourceException(descriptor, $"required Stardew skill id '{skillId}' must not contain path separators.");

        if (skillRoots.Count == 0)
            throw ResourceException(descriptor, "at least one Stardew gaming skill root is required.");

        var checkedPaths = new List<string>(skillRoots.Count);
        foreach (var skillRoot in skillRoots)
        {
            var fullRoot = Path.GetFullPath(skillRoot);
            var path = ResolveInsideRoot(descriptor, fullRoot, $"{skillId}.md", $"required skill '{skillId}'");
            checkedPaths.Add(path);
            if (File.Exists(path))
                return path;

            var skillDirectoryPath = ResolveInsideRoot(descriptor, fullRoot, Path.Combine(skillId, "SKILL.md"), $"required skill '{skillId}'");
            checkedPaths.Add(skillDirectoryPath);
            if (File.Exists(skillDirectoryPath))
                return skillDirectoryPath;
        }

        var checkedPathList = string.Join("; ", checkedPaths.Select(path => $"'{path}'"));
        throw ResourceException(
            descriptor,
            $"required skill '{skillId}' was not found at any configured Stardew gaming skill root. Checked: {checkedPathList}.");
    }

    private static string BuildCompactSkillContract(string rawContent)
    {
        var lines = NormalizeSkillLines(rawContent);

        if (lines.Length == 0)
            return "- (empty skill)";

        var summaryLines = SelectCompactContractLines(lines);
        if (summaryLines.Count == 0)
            summaryLines = SelectFallbackSummaryLines(lines);

        return string.Join(
            "\n",
            summaryLines.Select(line => line.StartsWith("- ", StringComparison.Ordinal) ? line : "- " + line));
    }

    private static string[] NormalizeSkillLines(string rawContent)
    {
        var normalized = new List<string>();
        var inFrontmatter = false;
        var firstContentLine = true;
        foreach (var rawLine in rawContent.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = rawLine.Trim();
            if (firstContentLine && line == "---")
            {
                inFrontmatter = true;
                firstContentLine = false;
                continue;
            }

            firstContentLine = false;
            if (inFrontmatter)
            {
                if (line == "---")
                    inFrontmatter = false;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(line) && !IsFrontmatterMetadata(line))
                normalized.Add(line);
        }

        return normalized.ToArray();
    }

    private static List<string> SelectCompactContractLines(IReadOnlyList<string> lines)
    {
        var selected = new List<string>();
        var inCompactSection = false;
        var compactHeadingLevel = 0;
        foreach (var line in lines)
        {
            if (TryParseHeading(line, out var headingLevel, out var headingText))
            {
                if (inCompactSection && headingLevel <= compactHeadingLevel)
                    break;

                if (IsCompactContractHeading(headingText))
                {
                    inCompactSection = true;
                    compactHeadingLevel = headingLevel;
                }

                continue;
            }

            if (!inCompactSection)
                continue;

            if (!line.StartsWith("- ", StringComparison.Ordinal))
                break;

            selected.Add(line);
        }

        return selected;
    }

    private static bool TryParseHeading(string line, out int level, out string text)
    {
        level = 0;
        text = "";
        while (level < line.Length && line[level] == '#')
            level++;

        if (level == 0 || level >= line.Length || line[level] != ' ')
        {
            level = 0;
            return false;
        }

        text = line[(level + 1)..].Trim();
        return !string.IsNullOrWhiteSpace(text);
    }

    private static bool IsCompactContractHeading(string headingText)
        => string.Equals(headingText, "Compact Contract", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(headingText, "精简契约", StringComparison.OrdinalIgnoreCase);

    private static List<string> SelectFallbackSummaryLines(IReadOnlyList<string> lines)
    {
        var selected = new List<string>();
        foreach (var line in lines)
        {
            if (line.StartsWith("---", StringComparison.Ordinal))
                continue;

            if (line.StartsWith("#", StringComparison.Ordinal))
                continue;

            if (IsFrontmatterMetadata(line))
                continue;

            selected.Add(line.Length <= MaxFallbackSummaryChars
                ? line
                : line[..MaxFallbackSummaryChars] + "...");

            if (selected.Count >= 3)
                break;
        }

        return selected;
    }

    private static bool IsFrontmatterMetadata(string line)
        => line.StartsWith("name:", StringComparison.OrdinalIgnoreCase) ||
           line.StartsWith("description:", StringComparison.OrdinalIgnoreCase) ||
           line.StartsWith("tools:", StringComparison.OrdinalIgnoreCase) ||
           line.StartsWith("model:", StringComparison.OrdinalIgnoreCase);

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
