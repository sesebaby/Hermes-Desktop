namespace Hermes.Agent.Game;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class FileSystemNpcPackLoader : INpcPackLoader
{
    public const string ManifestFileName = "manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly string[] RequiredCapabilities = { "move", "speak" };

    public IReadOnlyList<NpcPack> LoadPacks(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Pack root cannot be empty.", nameof(rootPath));

        if (!Directory.Exists(rootPath))
            return Array.Empty<NpcPack>();

        var packs = new List<NpcPack>();
        var seenNpcIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var manifestPath in Directory.EnumerateFiles(rootPath, ManifestFileName, SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var packRoot = Path.GetDirectoryName(manifestPath) ?? rootPath;
            var manifest = JsonSerializer.Deserialize<NpcPackManifest>(File.ReadAllText(manifestPath), JsonOptions)
                ?? throw new InvalidOperationException($"Pack manifest '{manifestPath}' could not be read.");

            var validation = Validate(packRoot, manifest);
            if (!validation.IsValid)
                throw new InvalidOperationException($"Invalid NPC pack '{manifestPath}': {string.Join("; ", validation.Errors)}");

            if (!seenNpcIds.Add(manifest.NpcId))
                throw new InvalidOperationException($"Duplicate npcId '{manifest.NpcId}' in NPC packs.");

            packs.Add(new NpcPack(manifest, packRoot));
        }

        return packs;
    }

    public NpcPackValidationResult Validate(string packRoot, NpcPackManifest manifest)
    {
        var errors = new List<string>();
        Require(manifest.SchemaVersion > 0, "schemaVersion is required.", errors);
        RequireNotEmpty(manifest.NpcId, "npcId", errors);
        RequireNotEmpty(manifest.GameId, "gameId", errors);
        RequireNotEmpty(manifest.ProfileId, "profileId", errors);
        RequireNotEmpty(manifest.DefaultProfileId, "defaultProfileId", errors);
        RequireNotEmpty(manifest.DisplayName, "displayName", errors);
        RequireNotEmpty(manifest.SmapiName, "smapiName", errors);
        Require(manifest.Aliases.Count > 0, "aliases must contain at least one value.", errors);
        RequireNotEmpty(manifest.TargetEntityId, "targetEntityId", errors);
        RequireNotEmpty(manifest.AdapterId, "adapterId", errors);
        Require(string.Equals(manifest.ProfileId, "default", StringComparison.OrdinalIgnoreCase), "Phase 1 requires profileId = default.", errors);
        Require(string.Equals(manifest.DefaultProfileId, "default", StringComparison.OrdinalIgnoreCase), "Phase 1 requires defaultProfileId = default.", errors);

        ValidatePackFile(packRoot, manifest.SoulFile, "soulFile", errors);
        ValidatePackFile(packRoot, manifest.FactsFile, "factsFile", errors);
        ValidatePackFile(packRoot, manifest.VoiceFile, "voiceFile", errors);
        ValidatePackFile(packRoot, manifest.BoundariesFile, "boundariesFile", errors);
        ValidatePackFile(packRoot, manifest.SkillsFile, "skillsFile", errors);

        foreach (var capability in manifest.Capabilities)
        {
            if (!RequiredCapabilities.Contains(capability, StringComparer.OrdinalIgnoreCase))
                errors.Add($"Capability '{capability}' is not enabled for Phase 1.");
        }

        return errors.Count == 0 ? NpcPackValidationResult.Valid : NpcPackValidationResult.Invalid(errors);
    }

    private static void ValidatePackFile(string packRoot, string relativePath, string fieldName, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            errors.Add($"{fieldName} is required.");
            return;
        }

        var fullRoot = Path.GetFullPath(packRoot);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        if (!fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{fieldName} must stay inside the pack root.");
            return;
        }

        if (!File.Exists(fullPath))
            errors.Add($"{fieldName} file '{relativePath}' does not exist.");
    }

    private static void RequireNotEmpty(string? value, string fieldName, List<string> errors)
        => Require(!string.IsNullOrWhiteSpace(value), $"{fieldName} is required.", errors);

    private static void Require(bool condition, string message, List<string> errors)
    {
        if (!condition)
            errors.Add(message);
    }
}
