using Hermes.Agent.Skills;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Services;

[TestClass]
public sealed class BundledSkillRootDiscoveryTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "hermes-bundled-skill-root-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public void FindBundledSkillsDir_PrefersWorkspaceManifestWhenAppBaseCannotWalkToRepository()
    {
        var workspace = Path.Combine(_tempDir, "repo");
        var skillsDir = Path.Combine(workspace, "skills");
        Directory.CreateDirectory(skillsDir);
        File.WriteAllText(BundledSkillCatalogService.GetManifestPath(skillsDir), "{}");

        var installedAppBase = Path.Combine(_tempDir, "installed", "Hermes", "app");
        Directory.CreateDirectory(installedAppBase);

        var found = BundledSkillRootDiscovery.FindBundledSkillsDir(
            installedAppBase,
            Path.Combine(_tempDir, "unrelated-cwd"),
            workspace);

        Assert.AreEqual(Path.GetFullPath(skillsDir), found);
    }

    [TestMethod]
    public void FindBundledSkillsDir_IgnoresSkillsFolderWithoutManifest()
    {
        var workspace = Path.Combine(_tempDir, "repo");
        Directory.CreateDirectory(Path.Combine(workspace, "skills", "gaming", "stardew-navigation"));

        var found = BundledSkillRootDiscovery.FindBundledSkillsDir(
            Path.Combine(_tempDir, "installed"),
            null,
            workspace);

        Assert.IsNull(found);
    }

    [TestMethod]
    public void ReconcileRequiredSkillDirectories_CopiesStardewSkillsEvenWhenManifestDoesNotListThem()
    {
        var activeSkills = Path.Combine(_tempDir, "active-skills");
        var sourceGaming = Path.Combine(_tempDir, "repo", "skills", "gaming");
        CreateSkill(sourceGaming, "stardew-navigation");
        CreateSkill(sourceGaming, "stardew-core");
        CreateSkill(sourceGaming, "unrelated");

        BundledSkillRootDiscovery.ReconcileRequiredSkillDirectories(
            activeSkills,
            [sourceGaming],
            "gaming",
            ["stardew-navigation", "stardew-core"]);

        Assert.IsTrue(File.Exists(Path.Combine(activeSkills, "gaming", "stardew-navigation", "SKILL.md")));
        Assert.IsTrue(File.Exists(Path.Combine(activeSkills, "gaming", "stardew-core", "SKILL.md")));
        Assert.IsFalse(File.Exists(Path.Combine(activeSkills, "gaming", "unrelated", "SKILL.md")));
    }

    private static void CreateSkill(string sourceGaming, string skillId)
    {
        var root = Path.Combine(sourceGaming, skillId);
        Directory.CreateDirectory(root);
        File.WriteAllText(
            Path.Combine(root, "SKILL.md"),
            $"""
            ---
            name: {skillId}
            description: test
            ---

            test
            """);
    }
}
