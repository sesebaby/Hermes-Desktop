using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hermes.Agent.Skills;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Services;

[TestClass]
public sealed class BundledSkillCatalogServiceTests
{
    private string _tempDir = null!;
    private string _bundledDir = null!;
    private string _activeDir = null!;
    private string _quarantineDir = null!;
    private string _provenancePath = null!;
    private BundledSkillCatalogService _service = null!;

    [TestInitialize]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"hermes-bundled-skills-{Guid.NewGuid():N}");
        _bundledDir = Path.Combine(_tempDir, "bundled");
        _activeDir = Path.Combine(_tempDir, "active");
        _quarantineDir = Path.Combine(_tempDir, "quarantine");
        _provenancePath = Path.Combine(_tempDir, "bundled-skills-provenance.json");
        Directory.CreateDirectory(_bundledDir);
        Directory.CreateDirectory(_activeDir);
        _service = new BundledSkillCatalogService();
    }

    [TestCleanup]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public void RepoManifest_HasExpectedDispositionCounts()
    {
        var manifest = _service.LoadManifestFromBundledSkillsDir(FindRepoSkillsDir());

        Assert.AreEqual(90, manifest.Skills.Count);
        Assert.AreEqual(13, manifest.Skills.Count(skill => skill.Disposition == BundledSkillDisposition.Retain));
        Assert.AreEqual(8, manifest.Skills.Count(skill => skill.Disposition == BundledSkillDisposition.Defer));
        Assert.AreEqual(69, manifest.Skills.Count(skill => skill.Disposition == BundledSkillDisposition.Delete));

        var duplicateNames = manifest.Skills.Where(skill => skill.SourceRoots.Count > 1).ToList();
        CollectionAssert.AreEquivalent(
            new[] { "code-review", "plan", "systematic-debugging", "test-driven-development" },
            duplicateNames.Select(skill => skill.Name).ToArray());
    }

    [TestMethod]
    public async Task ReconcileActiveSkills_RemovesNonShippingRoots_And_QuarantinesNameCollisions()
    {
        var keeperRoot = CreateSkillRoot(_bundledDir, "keep/keeper", "keeper", "Keep this skill");
        var obsoleteRoot = CreateSkillRoot(_bundledDir, "old/obsolete", "obsolete", "Delete this skill");
        var deferRoot = CreateSkillRoot(_bundledDir, "future/later", "later", "Defer this skill");
        var collisionRoot = CreateSkillRoot(_bundledDir, "shadow/override", "obsolete", "User collision");
        File.WriteAllText(Path.Combine(_bundledDir, "keep", "DESCRIPTION.md"), """
            ---
            description: keep category
            ---

            keep category
            """);

        WriteManifest(new BundledSkillManifest
        {
            SchemaVersion = 1,
            GeneratedAtUtc = DateTime.UtcNow.ToString("O"),
            Skills =
            {
                CreateEntry("keeper", BundledSkillDisposition.Retain, keeperRoot),
                CreateEntry("obsolete", BundledSkillDisposition.Delete, obsoleteRoot),
                CreateEntry("later", BundledSkillDisposition.Defer, deferRoot)
            }
        });

        CopyRoot(_bundledDir, _activeDir, "keep/keeper");
        CopyRoot(_bundledDir, _activeDir, "old/obsolete");
        CopyRoot(_bundledDir, _activeDir, "future/later");
        CopyRoot(_bundledDir, _activeDir, "shadow/override");

        var result = _service.ReconcileActiveSkills(_bundledDir, _activeDir, _provenancePath, _quarantineDir);

        Assert.IsTrue(result.PreservedRoots.Contains("keep/keeper"));
        Assert.IsTrue(result.DeletedRoots.Contains("old/obsolete"));
        Assert.IsTrue(result.DeletedRoots.Contains("future/later"));
        Assert.IsTrue(result.QuarantinedRoots.Contains("shadow/override"));
        Assert.IsFalse(Directory.Exists(Path.Combine(_activeDir, "old/obsolete")));
        Assert.IsFalse(Directory.Exists(Path.Combine(_activeDir, "future/later")));
        Assert.IsFalse(Directory.Exists(Path.Combine(_activeDir, "shadow/override")));
        Assert.IsTrue(Directory.Exists(Path.Combine(_activeDir, "keep/keeper")));

        var skillManager = new SkillManager(_activeDir, NullLogger<SkillManager>.Instance);
        CollectionAssert.AreEquivalent(new[] { "keeper" }, skillManager.ListSkills().Select(skill => skill.Name).ToArray());

        Assert.IsTrue(File.Exists(_provenancePath));
        var provenance = JsonSerializer.Deserialize<BundledSkillProvenanceLedger>(await File.ReadAllTextAsync(_provenancePath), JsonOptions())
            ?? throw new AssertFailedException("provenance missing");
        Assert.AreEqual(1, provenance.ManagedRoots.Count);
        Assert.AreEqual("keep/keeper", provenance.ManagedRoots[0].RootRelativePath);

        var quarantineAuditPath = Path.Combine(_quarantineDir, BundledSkillCatalogService.QuarantineAuditFileName);
        Assert.IsTrue(File.Exists(quarantineAuditPath));
        var audit = JsonSerializer.Deserialize<BundledSkillQuarantineAudit>(await File.ReadAllTextAsync(quarantineAuditPath), JsonOptions())
            ?? throw new AssertFailedException("audit missing");
        Assert.IsTrue(audit.Entries.Any(entry => entry.Name == "obsolete" && entry.Reason.Contains("collision", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task ReconcileActiveSkills_QuarantinesManagedRootsMissingFromManifest()
    {
        var keeperRoot = CreateSkillRoot(_bundledDir, "keep/keeper", "keeper", "Keep this skill");
        var retiredRoot = CreateSkillRoot(_activeDir, "legacy/retired", "retired", "Former bundled skill");
        WriteManifest(new BundledSkillManifest
        {
            SchemaVersion = 1,
            GeneratedAtUtc = DateTime.UtcNow.ToString("O"),
            Skills =
            {
                CreateEntry("keeper", BundledSkillDisposition.Retain, keeperRoot)
            }
        });
        WriteProvenance(new BundledSkillProvenanceLedger
        {
            SchemaVersion = 1,
            ManifestSha256 = "old",
            UpdatedAtUtc = DateTime.UtcNow.ToString("O"),
            ManagedRoots =
            {
                new BundledSkillProvenanceEntry
                {
                    SkillId = "retired",
                    Name = "retired",
                    RootRelativePath = "legacy/retired",
                    DirectorySha256 = BundledSkillCatalogService.ComputeDirectoryFingerprint(Path.Combine(_activeDir, "legacy/retired"))
                }
            }
        });

        var result = _service.ReconcileActiveSkills(_bundledDir, _activeDir, _provenancePath, _quarantineDir);

        Assert.IsTrue(result.QuarantinedRoots.Contains("legacy/retired"));
        Assert.IsFalse(Directory.Exists(Path.Combine(_activeDir, "legacy/retired")));
        var audit = await ReadAuditAsync();
        Assert.IsTrue(audit.Entries.Any(entry => entry.Name == "retired" && entry.Reason == "managed-root-missing-from-manifest"));
    }

    [TestMethod]
    public void ReconcileActiveSkills_QuarantinesAmbiguousDuplicateNamesOutsideManifest()
    {
        var keeperRoot = CreateSkillRoot(_bundledDir, "keep/keeper", "keeper", "Keep this skill");
        CreateSkillRoot(_activeDir, "custom/alpha", "rogue", "Custom one");
        CreateSkillRoot(_activeDir, "custom/beta", "rogue", "Custom two");
        WriteManifest(new BundledSkillManifest
        {
            SchemaVersion = 1,
            GeneratedAtUtc = DateTime.UtcNow.ToString("O"),
            Skills =
            {
                CreateEntry("keeper", BundledSkillDisposition.Retain, keeperRoot)
            }
        });

        var result = _service.ReconcileActiveSkills(_bundledDir, _activeDir, _provenancePath, _quarantineDir);

        CollectionAssert.AreEquivalent(new[] { "custom/alpha", "custom/beta" }, result.QuarantinedRoots);
        Assert.IsFalse(Directory.Exists(Path.Combine(_activeDir, "custom/alpha")));
        Assert.IsFalse(Directory.Exists(Path.Combine(_activeDir, "custom/beta")));
        var skillManager = new SkillManager(_activeDir, NullLogger<SkillManager>.Instance);
        CollectionAssert.AreEquivalent(new[] { "keeper" }, skillManager.ListSkills().Select(skill => skill.Name).ToArray());
    }

    [TestMethod]
    public void ExportRetainedSkills_WritesOnlyRetainedRoots()
    {
        CreateSkillRoot(_bundledDir, "keep/keeper", "keeper", "Keep this skill");
        CreateSkillRoot(_bundledDir, "old/obsolete", "obsolete", "Delete this skill");
        File.WriteAllText(Path.Combine(_bundledDir, "keep", "DESCRIPTION.md"), """
            ---
            description: keep category
            ---

            keep category
            """);

        WriteManifest(new BundledSkillManifest
        {
            SchemaVersion = 1,
            GeneratedAtUtc = DateTime.UtcNow.ToString("O"),
            Skills =
            {
                CreateEntry("keeper", BundledSkillDisposition.Retain, "keep/keeper"),
                CreateEntry("obsolete", BundledSkillDisposition.Delete, "old/obsolete")
            }
        });

        var exportDir = Path.Combine(_tempDir, "export");
        var result = _service.ExportRetainedSkills(_bundledDir, exportDir, cleanDestination: true);

        Assert.AreEqual(1, result.ExportedRoots.Count);
        Assert.IsTrue(File.Exists(Path.Combine(exportDir, BundledSkillCatalogService.ManifestFileName)));
        Assert.IsTrue(File.Exists(Path.Combine(exportDir, "keep/keeper/SKILL.md")));
        Assert.IsTrue(File.Exists(Path.Combine(exportDir, "keep/DESCRIPTION.md")));
        Assert.IsFalse(Directory.Exists(Path.Combine(exportDir, "old/obsolete")));
    }

    private BundledSkillManifestEntry CreateEntry(string name, BundledSkillDisposition disposition, string rootRelativePath)
    {
        var rootFullPath = Path.Combine(_bundledDir, rootRelativePath.Replace('/', Path.DirectorySeparatorChar));
        return new BundledSkillManifestEntry
        {
            Id = name,
            Name = name,
            Disposition = disposition,
            SourceRoots =
            {
                new BundledSkillManifestSource
                {
                    RootRelativePath = rootRelativePath,
                    SkillFileRelativePath = $"{rootRelativePath}/SKILL.md",
                    DirectorySha256 = BundledSkillCatalogService.ComputeDirectoryFingerprint(rootFullPath),
                    SkillFileSha256 = ComputeFileSha256(Path.Combine(rootFullPath, "SKILL.md"))
                }
            }
        };
    }

    private string CreateSkillRoot(string bundledRoot, string rootRelativePath, string name, string description)
    {
        var root = Path.Combine(bundledRoot, rootRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "SKILL.md"), $"""
            ---
            name: {name}
            description: {description}
            tools: session_search
            ---

            {description}
            """);
        return rootRelativePath;
    }

    private static void CopyRoot(string sourceRoot, string destinationRoot, string rootRelativePath)
    {
        var source = Path.Combine(sourceRoot, rootRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var destination = Path.Combine(destinationRoot, rootRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private void WriteManifest(BundledSkillManifest manifest)
    {
        var manifestPath = Path.Combine(_bundledDir, BundledSkillCatalogService.ManifestFileName);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions()));
    }

    private void WriteProvenance(BundledSkillProvenanceLedger provenance)
    {
        File.WriteAllText(_provenancePath, JsonSerializer.Serialize(provenance, JsonOptions()));
    }

    private async Task<BundledSkillQuarantineAudit> ReadAuditAsync()
    {
        var auditPath = Path.Combine(_quarantineDir, BundledSkillCatalogService.QuarantineAuditFileName);
        return JsonSerializer.Deserialize<BundledSkillQuarantineAudit>(await File.ReadAllTextAsync(auditPath), JsonOptions())
               ?? throw new AssertFailedException("audit missing");
    }

    private static string FindRepoSkillsDir()
    {
        var dir = Directory.GetCurrentDirectory();
        for (var i = 0; i < 10 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "skills");
            if (Directory.Exists(candidate))
                return candidate;

            dir = Path.GetDirectoryName(dir);
        }

        throw new DirectoryNotFoundException("Could not find repo skills directory from test working directory.");
    }

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    private static JsonSerializerOptions JsonOptions()
        => new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
}
