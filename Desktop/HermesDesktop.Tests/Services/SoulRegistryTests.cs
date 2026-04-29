using Hermes.Agent.Skills;
using Hermes.Agent.Soul;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Services;

[TestClass]
public sealed class SoulRegistryTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"hermes-soul-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public void SoulRegistry_LoadsTemplates_FromConfiguredSoulPath()
    {
        var soulsDir = Path.Combine(_tempDir, "souls");
        Directory.CreateDirectory(soulsDir);
        File.WriteAllText(Path.Combine(soulsDir, "default.md"), """
            ---
            name: Hermes Default
            description: Default soul
            category: base
            tags: [default, hermes]
            ---

            # Hermes Default

            You are Hermes.
            """);

        var registry = new SoulRegistry(new[] { soulsDir }, NullLogger<SoulRegistry>.Instance);
        var souls = registry.ListSouls();

        Assert.AreEqual(1, souls.Count);
        Assert.AreEqual("Hermes Default", souls[0].Name);
        Assert.AreEqual("base", souls[0].Category);
    }

    [TestMethod]
    public void SkillManager_And_SoulRegistry_UseSeparateDiscoveryChannels()
    {
        var skillsDir = Path.Combine(_tempDir, "skills");
        var skillRoot = Path.Combine(skillsDir, "dev", "real-skill");
        Directory.CreateDirectory(skillRoot);
        File.WriteAllText(Path.Combine(skillRoot, "SKILL.md"), """
            ---
            name: real-skill
            description: Real skill
            tools: session_search
            ---

            Use the real skill.
            """);
        File.WriteAllText(Path.Combine(skillsDir, "legacy-helper.md"), """
            ---
            name: legacy-helper
            description: Legacy flat markdown skill
            tools: session_search
            ---

            This should not load as a skill anymore.
            """);

        var soulsDir = Path.Combine(skillsDir, "souls");
        Directory.CreateDirectory(soulsDir);
        File.WriteAllText(Path.Combine(soulsDir, "default.md"), """
            ---
            name: Hermes Default
            description: Default soul
            category: base
            ---

            # Hermes Default

            You are Hermes.
            """);

        var skillManager = new SkillManager(skillsDir, NullLogger<SkillManager>.Instance);
        var soulRegistry = new SoulRegistry(new[] { soulsDir }, NullLogger<SoulRegistry>.Instance);

        CollectionAssert.AreEqual(
            new[] { "real-skill" },
            skillManager.ListSkills().Select(skill => skill.Name).OrderBy(name => name).ToArray());
        CollectionAssert.AreEqual(
            new[] { "Hermes Default" },
            soulRegistry.ListSouls().Select(soul => soul.Name).ToArray());
    }

    [TestMethod]
    public void RepoBundledSoulTemplates_AreStoredOutsideSkillsTree()
    {
        var repoRoot = FindRepoRoot();
        var shippedSoulsDir = Path.Combine(repoRoot, "souls");
        var legacySkillsSoulsDir = Path.Combine(repoRoot, "skills", "souls");

        Assert.IsTrue(Directory.Exists(shippedSoulsDir), "Bundled soul templates should live in the repo-level souls directory.");
        Assert.IsFalse(Directory.Exists(legacySkillsSoulsDir), "Bundled soul templates should not live under skills/souls.");
        Assert.IsTrue(
            Directory.EnumerateFiles(shippedSoulsDir, "*.md", SearchOption.TopDirectoryOnly).Any(),
            "Bundled soul templates should include markdown templates.");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) &&
                File.Exists(Path.Combine(dir.FullName, "HermesDesktop.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        Assert.Fail("Could not locate repository root from test output directory.");
        return "";
    }
}
