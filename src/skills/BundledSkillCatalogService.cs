namespace Hermes.Agent.Skills;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public enum BundledSkillDisposition
{
    Retain,
    Defer,
    Delete
}

public sealed class BundledSkillManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string GeneratedAtUtc { get; set; } = "";
    public List<BundledSkillManifestEntry> Skills { get; set; } = new();
}

public sealed class BundledSkillManifestEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public BundledSkillDisposition Disposition { get; set; }
    public List<BundledSkillManifestSource> SourceRoots { get; set; } = new();
}

public sealed class BundledSkillManifestSource
{
    public string RootRelativePath { get; set; } = "";
    public string SkillFileRelativePath { get; set; } = "";
    public string DirectorySha256 { get; set; } = "";
    public string SkillFileSha256 { get; set; } = "";
}

public sealed class BundledSkillProvenanceLedger
{
    public int SchemaVersion { get; set; } = 1;
    public string ManifestSha256 { get; set; } = "";
    public string UpdatedAtUtc { get; set; } = "";
    public List<BundledSkillProvenanceEntry> ManagedRoots { get; set; } = new();
}

public sealed class BundledSkillProvenanceEntry
{
    public string SkillId { get; set; } = "";
    public string Name { get; set; } = "";
    public string RootRelativePath { get; set; } = "";
    public string DirectorySha256 { get; set; } = "";
}

public sealed class BundledSkillQuarantineAudit
{
    public int SchemaVersion { get; set; } = 1;
    public List<BundledSkillQuarantineAuditEntry> Entries { get; set; } = new();
}

public sealed class BundledSkillQuarantineAuditEntry
{
    public string TimestampUtc { get; set; } = "";
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Name { get; set; } = "";
    public string RootRelativePath { get; set; } = "";
    public string ObservedDirectorySha256 { get; set; } = "";
    public string? QuarantineRelativePath { get; set; }
}

public sealed class BundledSkillReconcileResult
{
    public List<string> InstalledRoots { get; } = new();
    public List<string> RefreshedRoots { get; } = new();
    public List<string> DeletedRoots { get; } = new();
    public List<string> QuarantinedRoots { get; } = new();
    public List<string> PreservedRoots { get; } = new();
}

public sealed class BundledSkillExportResult
{
    public List<string> ExportedRoots { get; } = new();
    public List<string> ExportedCategoryDescriptions { get; } = new();
}

public sealed class BundledSkillCatalogService
{
    public const string ManifestFileName = ".bundled-skills-manifest.json";
    public const string ProvenanceFileName = ".bundled-skills-provenance.json";
    public const string QuarantineAuditFileName = "quarantine-audit.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public BundledSkillManifest LoadManifestFromBundledSkillsDir(string bundledSkillsDir)
    {
        var manifestPath = GetManifestPath(bundledSkillsDir);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Bundled skill manifest not found: {manifestPath}", manifestPath);

        var manifest = JsonSerializer.Deserialize<BundledSkillManifest>(File.ReadAllText(manifestPath), JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize bundled skill manifest: {manifestPath}");

        ValidateManifest(manifest);
        return manifest;
    }

    public BundledSkillReconcileResult ReconcileActiveSkills(
        string bundledSkillsDir,
        string activeSkillsDir,
        string provenanceFilePath,
        string quarantineRoot)
    {
        Directory.CreateDirectory(activeSkillsDir);

        var result = new BundledSkillReconcileResult();
        var manifest = LoadManifestFromBundledSkillsDir(bundledSkillsDir);
        var manifestSha = ComputeFileSha256(GetManifestPath(bundledSkillsDir));
        var retainedSources = manifest.Skills
            .Where(skill => skill.Disposition == BundledSkillDisposition.Retain)
            .SelectMany(skill => skill.SourceRoots.Select(source => new ManifestSourceRef(skill, source)))
            .ToDictionary(item => NormalizeRelativePath(item.Source.RootRelativePath), StringComparer.OrdinalIgnoreCase);
        var allSources = manifest.Skills
            .SelectMany(skill => skill.SourceRoots.Select(source => new ManifestSourceRef(skill, source)))
            .ToDictionary(item => NormalizeRelativePath(item.Source.RootRelativePath), StringComparer.OrdinalIgnoreCase);
        var manifestByName = manifest.Skills.ToDictionary(skill => skill.Name, StringComparer.OrdinalIgnoreCase);
        var provenance = LoadProvenance(provenanceFilePath);
        var provenanceByRoot = provenance.ManagedRoots.ToDictionary(entry => NormalizeRelativePath(entry.RootRelativePath), StringComparer.OrdinalIgnoreCase);
        var quarantineAudit = LoadQuarantineAudit(quarantineRoot);
        var activeRoots = DiscoverActiveSkillRoots(activeSkillsDir).ToList();
        var ambiguousUnmanagedNames = activeRoots
            .Where(root => !manifestByName.ContainsKey(root.Name))
            .GroupBy(root => root.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var activeRoot in activeRoots.OrderByDescending(item => item.RootRelativePath.Length))
        {
            var normalizedRoot = NormalizeRelativePath(activeRoot.RootRelativePath);
            var hasRootMatch = allSources.TryGetValue(normalizedRoot, out var sourceRef);
            var hasNameMatch = manifestByName.TryGetValue(activeRoot.Name, out var nameRef);
            var isManagedRoot = provenanceByRoot.TryGetValue(normalizedRoot, out _);

            if (isManagedRoot && !hasRootMatch)
            {
                MoveRootToQuarantine(
                    activeSkillsDir,
                    activeRoot,
                    quarantineRoot,
                    quarantineAudit,
                    "quarantine",
                    "managed-root-missing-from-manifest");
                result.QuarantinedRoots.Add(activeRoot.RootRelativePath);
                continue;
            }

            if (!hasNameMatch && ambiguousUnmanagedNames.Contains(activeRoot.Name))
            {
                MoveRootToQuarantine(
                    activeSkillsDir,
                    activeRoot,
                    quarantineRoot,
                    quarantineAudit,
                    "quarantine",
                    "ambiguous-duplicate-name-outside-manifest");
                result.QuarantinedRoots.Add(activeRoot.RootRelativePath);
                continue;
            }

            if (hasRootMatch)
            {
                if (DirectoryHashMatches(activeRoot.DirectorySha256, sourceRef!.Source.DirectorySha256) || isManagedRoot)
                {
                    if (sourceRef.Entry.Disposition == BundledSkillDisposition.Retain &&
                        DirectoryHashMatches(activeRoot.DirectorySha256, sourceRef.Source.DirectorySha256))
                    {
                        result.PreservedRoots.Add(activeRoot.RootRelativePath);
                    }
                    else if (sourceRef.Entry.Disposition == BundledSkillDisposition.Retain)
                    {
                        MoveRootToQuarantine(
                            activeSkillsDir,
                            activeRoot,
                            quarantineRoot,
                            quarantineAudit,
                            "quarantine",
                            "managed-retain-hash-mismatch");
                        result.QuarantinedRoots.Add(activeRoot.RootRelativePath);
                    }
                    else
                    {
                        DeleteRoot(activeSkillsDir, activeRoot, quarantineAudit, "delete", "managed-nonshipping");
                        result.DeletedRoots.Add(activeRoot.RootRelativePath);
                    }

                    continue;
                }

                MoveRootToQuarantine(
                    activeSkillsDir,
                    activeRoot,
                    quarantineRoot,
                    quarantineAudit,
                    "quarantine",
                    sourceRef.Entry.Disposition == BundledSkillDisposition.Retain
                        ? "root-path-collision-retain"
                        : "root-path-collision-nonshipping");
                result.QuarantinedRoots.Add(activeRoot.RootRelativePath);
                continue;
            }

            if (hasNameMatch)
            {
                MoveRootToQuarantine(
                    activeSkillsDir,
                    activeRoot,
                    quarantineRoot,
                    quarantineAudit,
                    "quarantine",
                    $"name-collision-{nameRef!.Disposition.ToString().ToLowerInvariant()}");
                result.QuarantinedRoots.Add(activeRoot.RootRelativePath);
            }
        }

        foreach (var retainRef in retainedSources.Values.OrderBy(item => item.Source.RootRelativePath, StringComparer.OrdinalIgnoreCase))
        {
            var sourceRoot = Path.Combine(bundledSkillsDir, RelativeToSystemPath(retainRef.Source.RootRelativePath));
            if (!Directory.Exists(sourceRoot))
                throw new DirectoryNotFoundException($"Retained bundled skill root is missing: {sourceRoot}");

            var targetRoot = Path.Combine(activeSkillsDir, RelativeToSystemPath(retainRef.Source.RootRelativePath));
            var targetExists = Directory.Exists(targetRoot);
            var targetHash = targetExists ? ComputeDirectoryFingerprint(targetRoot) : "";
            if (!targetExists)
            {
                CopyDirectoryRecursive(sourceRoot, targetRoot);
                result.InstalledRoots.Add(retainRef.Source.RootRelativePath);
            }
            else if (!DirectoryHashMatches(targetHash, retainRef.Source.DirectorySha256))
            {
                Directory.Delete(targetRoot, recursive: true);
                CopyDirectoryRecursive(sourceRoot, targetRoot);
                result.RefreshedRoots.Add(retainRef.Source.RootRelativePath);
            }

            CopyCategoryDescriptions(bundledSkillsDir, activeSkillsDir, retainRef.Source.RootRelativePath);
        }

        SaveProvenance(
            provenanceFilePath,
            manifestSha,
            manifest.Skills
                .Where(skill => skill.Disposition == BundledSkillDisposition.Retain)
                .SelectMany(skill => skill.SourceRoots.Select(source => new BundledSkillProvenanceEntry
                {
                    SkillId = skill.Id,
                    Name = skill.Name,
                    RootRelativePath = NormalizeRelativePath(source.RootRelativePath),
                    DirectorySha256 = source.DirectorySha256
                }))
                .OrderBy(entry => entry.RootRelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList());
        SaveQuarantineAudit(quarantineRoot, quarantineAudit);

        return result;
    }

    public BundledSkillExportResult ExportRetainedSkills(
        string bundledSkillsDir,
        string destinationSkillsDir,
        bool cleanDestination)
    {
        var manifest = LoadManifestFromBundledSkillsDir(bundledSkillsDir);
        if (cleanDestination && Directory.Exists(destinationSkillsDir))
        {
            Directory.Delete(destinationSkillsDir, recursive: true);
        }

        Directory.CreateDirectory(destinationSkillsDir);
        File.Copy(GetManifestPath(bundledSkillsDir), Path.Combine(destinationSkillsDir, ManifestFileName), overwrite: true);

        var result = new BundledSkillExportResult();
        foreach (var retainRef in manifest.Skills
                     .Where(skill => skill.Disposition == BundledSkillDisposition.Retain)
                     .SelectMany(skill => skill.SourceRoots.Select(source => new ManifestSourceRef(skill, source)))
                     .OrderBy(item => item.Source.RootRelativePath, StringComparer.OrdinalIgnoreCase))
        {
            var sourceRoot = Path.Combine(bundledSkillsDir, RelativeToSystemPath(retainRef.Source.RootRelativePath));
            if (!Directory.Exists(sourceRoot))
                throw new DirectoryNotFoundException($"Retained bundled skill root is missing: {sourceRoot}");

            var targetRoot = Path.Combine(destinationSkillsDir, RelativeToSystemPath(retainRef.Source.RootRelativePath));
            if (Directory.Exists(targetRoot))
                Directory.Delete(targetRoot, recursive: true);

            CopyDirectoryRecursive(sourceRoot, targetRoot);
            result.ExportedRoots.Add(retainRef.Source.RootRelativePath);
            result.ExportedCategoryDescriptions.AddRange(CopyCategoryDescriptions(bundledSkillsDir, destinationSkillsDir, retainRef.Source.RootRelativePath));
        }

        return result;
    }

    public static string GetManifestPath(string bundledSkillsDir)
        => Path.Combine(bundledSkillsDir, ManifestFileName);

    public static string GetDefaultProvenancePath(string projectDir)
        => Path.Combine(projectDir, ProvenanceFileName);

    public static string GetDefaultQuarantineRoot(string projectDir)
        => Path.Combine(projectDir, "skills-quarantine");

    public static string ComputeDirectoryFingerprint(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
            throw new DirectoryNotFoundException(rootDirectory);

        var lines = Directory.EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories)
            .OrderBy(path => Path.GetRelativePath(rootDirectory, path).Replace('\\', '/'), StringComparer.Ordinal)
            .Select(path =>
            {
                var relativePath = Path.GetRelativePath(rootDirectory, path).Replace('\\', '/');
                return $"{relativePath}|{ComputeFileSha256(path)}";
            });

        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(string.Join("\n", lines));
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }

    private static void ValidateManifest(BundledSkillManifest manifest)
    {
        var skillIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skillNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rootPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var skill in manifest.Skills)
        {
            if (string.IsNullOrWhiteSpace(skill.Id))
                throw new InvalidOperationException("Bundled skill manifest contains an entry without id.");
            if (string.IsNullOrWhiteSpace(skill.Name))
                throw new InvalidOperationException("Bundled skill manifest contains an entry without name.");
            if (!skillIds.Add(skill.Id))
                throw new InvalidOperationException($"Bundled skill manifest contains duplicate id '{skill.Id}'.");
            if (!skillNames.Add(skill.Name))
                throw new InvalidOperationException($"Bundled skill manifest contains duplicate name '{skill.Name}'.");
            if (skill.SourceRoots.Count == 0)
                throw new InvalidOperationException($"Bundled skill manifest entry '{skill.Name}' has no source roots.");

            foreach (var source in skill.SourceRoots)
            {
                var root = NormalizeRelativePath(source.RootRelativePath);
                if (!rootPaths.Add(root))
                    throw new InvalidOperationException($"Bundled skill manifest contains duplicate source root '{root}'.");
                if (string.IsNullOrWhiteSpace(source.DirectorySha256) || string.IsNullOrWhiteSpace(source.SkillFileSha256))
                    throw new InvalidOperationException($"Bundled skill manifest source '{root}' is missing hash data.");
            }
        }
    }

    private static BundledSkillProvenanceLedger LoadProvenance(string provenanceFilePath)
    {
        if (!File.Exists(provenanceFilePath))
            return new BundledSkillProvenanceLedger();

        return JsonSerializer.Deserialize<BundledSkillProvenanceLedger>(File.ReadAllText(provenanceFilePath), JsonOptions)
               ?? new BundledSkillProvenanceLedger();
    }

    private static void SaveProvenance(string provenanceFilePath, string manifestSha, List<BundledSkillProvenanceEntry> entries)
    {
        var ledger = new BundledSkillProvenanceLedger
        {
            SchemaVersion = 1,
            ManifestSha256 = manifestSha,
            UpdatedAtUtc = DateTime.UtcNow.ToString("O"),
            ManagedRoots = entries
        };

        WriteJsonAtomic(provenanceFilePath, ledger);
    }

    private static BundledSkillQuarantineAudit LoadQuarantineAudit(string quarantineRoot)
    {
        var auditPath = Path.Combine(quarantineRoot, QuarantineAuditFileName);
        if (!File.Exists(auditPath))
            return new BundledSkillQuarantineAudit();

        return JsonSerializer.Deserialize<BundledSkillQuarantineAudit>(File.ReadAllText(auditPath), JsonOptions)
               ?? new BundledSkillQuarantineAudit();
    }

    private static void SaveQuarantineAudit(string quarantineRoot, BundledSkillQuarantineAudit audit)
    {
        Directory.CreateDirectory(quarantineRoot);
        WriteJsonAtomic(Path.Combine(quarantineRoot, QuarantineAuditFileName), audit);
    }

    private static IEnumerable<string> CopyCategoryDescriptions(string bundledSkillsDir, string destinationSkillsDir, string rootRelativePath)
    {
        var copied = new List<string>();
        var segments = NormalizeRelativePath(rootRelativePath).Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 1)
            return copied;

        for (var index = 0; index < segments.Length - 1; index++)
        {
            var categoryRelative = string.Join(Path.DirectorySeparatorChar, segments.Take(index + 1));
            var sourceDescription = Path.Combine(bundledSkillsDir, categoryRelative, "DESCRIPTION.md");
            if (!File.Exists(sourceDescription))
                continue;

            var destinationDescription = Path.Combine(destinationSkillsDir, categoryRelative, "DESCRIPTION.md");
            Directory.CreateDirectory(Path.GetDirectoryName(destinationDescription)!);
            if (!File.Exists(destinationDescription) ||
                !string.Equals(ComputeFileSha256(destinationDescription), ComputeFileSha256(sourceDescription), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourceDescription, destinationDescription, overwrite: true);
            }

            copied.Add(Path.GetRelativePath(destinationSkillsDir, destinationDescription).Replace('\\', '/'));
        }

        return copied;
    }

    private static IEnumerable<ActiveSkillRoot> DiscoverActiveSkillRoots(string activeSkillsDir)
    {
        if (!Directory.Exists(activeSkillsDir))
            yield break;

        foreach (var skillFile in Directory.EnumerateFiles(activeSkillsDir, "SKILL.md", SearchOption.AllDirectories))
        {
            var name = ReadSkillName(skillFile);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var root = Path.GetDirectoryName(skillFile)!;
            yield return new ActiveSkillRoot(
                name,
                root,
                Path.GetRelativePath(activeSkillsDir, root).Replace('\\', '/'),
                ComputeDirectoryFingerprint(root));
        }
    }

    private static string? ReadSkillName(string skillFilePath)
    {
        using var reader = new StreamReader(skillFilePath);
        var firstLine = reader.ReadLine();
        if (!string.Equals(firstLine?.Trim(), "---", StringComparison.Ordinal))
            return null;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.Trim();
            if (string.Equals(trimmed, "---", StringComparison.Ordinal))
                break;
            if (!trimmed.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = trimmed["name:".Length..].Trim().Trim('"', '\'');
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
    }

    private static bool DirectoryHashMatches(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static void DeleteRoot(
        string activeSkillsDir,
        ActiveSkillRoot activeRoot,
        BundledSkillQuarantineAudit audit,
        string action,
        string reason)
    {
        if (Directory.Exists(activeRoot.RootFullPath))
        {
            Directory.Delete(activeRoot.RootFullPath, recursive: true);
            PruneEmptyParents(activeSkillsDir, Path.GetDirectoryName(activeRoot.RootFullPath));
        }

        audit.Entries.Add(new BundledSkillQuarantineAuditEntry
        {
            TimestampUtc = DateTime.UtcNow.ToString("O"),
            Action = action,
            Reason = reason,
            Name = activeRoot.Name,
            RootRelativePath = NormalizeRelativePath(activeRoot.RootRelativePath),
            ObservedDirectorySha256 = activeRoot.DirectorySha256
        });
    }

    private static void MoveRootToQuarantine(
        string activeSkillsDir,
        ActiveSkillRoot activeRoot,
        string quarantineRoot,
        BundledSkillQuarantineAudit audit,
        string action,
        string reason)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
        var destination = Path.Combine(
            quarantineRoot,
            timestamp,
            RelativeToSystemPath(activeRoot.RootRelativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        if (Directory.Exists(destination))
            Directory.Delete(destination, recursive: true);

        Directory.Move(activeRoot.RootFullPath, destination);
        PruneEmptyParents(activeSkillsDir, Path.GetDirectoryName(activeRoot.RootFullPath));

        audit.Entries.Add(new BundledSkillQuarantineAuditEntry
        {
            TimestampUtc = DateTime.UtcNow.ToString("O"),
            Action = action,
            Reason = reason,
            Name = activeRoot.Name,
            RootRelativePath = NormalizeRelativePath(activeRoot.RootRelativePath),
            ObservedDirectorySha256 = activeRoot.DirectorySha256,
            QuarantineRelativePath = Path.GetRelativePath(quarantineRoot, destination).Replace('\\', '/')
        });
    }

    private static void PruneEmptyParents(string activeSkillsDir, string? startDirectory)
    {
        var root = Path.GetFullPath(activeSkillsDir);
        var current = startDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            var fullCurrent = Path.GetFullPath(current);
            if (string.Equals(fullCurrent, root, StringComparison.OrdinalIgnoreCase))
                break;
            if (!Directory.Exists(fullCurrent) || Directory.EnumerateFileSystemEntries(fullCurrent).Any())
                break;

            Directory.Delete(fullCurrent);
            current = Path.GetDirectoryName(fullCurrent);
        }
    }

    private static void CopyDirectoryRecursive(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var child in Directory.EnumerateDirectories(source))
        {
            CopyDirectoryRecursive(child, Path.Combine(destination, Path.GetFileName(child)));
        }
    }

    private static void WriteJsonAtomic<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8);
        File.Move(tempPath, path, overwrite: true);
    }

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    private static string NormalizeRelativePath(string path)
        => path.Replace('\\', '/').Trim('/');

    private static string RelativeToSystemPath(string relativePath)
        => NormalizeRelativePath(relativePath).Replace('/', Path.DirectorySeparatorChar);

    private sealed record ActiveSkillRoot(string Name, string RootFullPath, string RootRelativePath, string DirectorySha256);

    private sealed record ManifestSourceRef(BundledSkillManifestEntry Entry, BundledSkillManifestSource Source);
}
