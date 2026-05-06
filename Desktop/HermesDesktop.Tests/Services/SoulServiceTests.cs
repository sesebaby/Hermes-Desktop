using Hermes.Agent.Soul;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Services;

[TestClass]
public class SoulServiceTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "hermes-soul-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public async Task SaveFileAsync_User_WritesPythonCompatibleUserMemoryAndLegacyMirror()
    {
        var service = CreateService();
        var content = "# User Profile\n\nUser prefers concise Chinese answers.";

        await service.SaveFileAsync(SoulFileType.User, content);

        Assert.AreEqual(content, await File.ReadAllTextAsync(Path.Combine(_tempDir, "memories", "USER.md")));
        Assert.AreEqual(content, await File.ReadAllTextAsync(Path.Combine(_tempDir, "USER.md")));
    }

    [TestMethod]
    public async Task LoadFileAsync_User_PrefersPythonCompatibleUserMemoryOverLegacyRootFile()
    {
        var service = CreateService();
        Directory.CreateDirectory(Path.Combine(_tempDir, "memories"));
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "USER.md"), "legacy root profile");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "memories", "USER.md"), "curated memory profile");

        var loaded = await service.LoadFileAsync(SoulFileType.User);

        Assert.AreEqual("curated memory profile", loaded);
    }

    [TestMethod]
    public async Task Constructor_MigratesConfiguredLegacyUserProfileToPythonCompatibleMemory()
    {
        var legacyProfile = "# User Profile\n\nUser prefers implementation-first answers.";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "USER.md"), legacyProfile);

        _ = CreateService();

        Assert.AreEqual(legacyProfile, await File.ReadAllTextAsync(Path.Combine(_tempDir, "memories", "USER.md")));
    }

    [TestMethod]
    public async Task AssembleSoulContextAsync_AutonomyCompact_KeepsEssentialFactsOnly()
    {
        var service = CreateService();
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "SOUL.md"), "# Agent Soul\n\nYou are a practical Stardew NPC.");
        Directory.CreateDirectory(Path.Combine(_tempDir, "memories"));
        await File.WriteAllTextAsync(
            Path.Combine(_tempDir, "memories", "USER.md"),
            "# User Profile\n\nThis file is a living document.\n§\nCommunicates in Chinese.\nPrefers concise responses.");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "AGENTS.md"), "Project rules that should not enter autonomy compact mode.");
        Directory.CreateDirectory(Path.Combine(_tempDir, "soul"));
        await File.WriteAllTextAsync(
            Path.Combine(_tempDir, "soul", "mistakes.jsonl"),
            """{"timestamp":"2026-05-01T00:00:00Z","lesson":"Do not spam status tools."}""" + "\n");
        await File.WriteAllTextAsync(
            Path.Combine(_tempDir, "soul", "habits.jsonl"),
            """{"timestamp":"2026-05-01T00:00:00Z","habit":"Verify game facts before speaking."}""" + "\n");

        var compact = await service.AssembleSoulContextAsync(profile: SoulContextProfile.AutonomyCompact);
        var full = await service.AssembleSoulContextAsync();

        Assert.IsTrue(compact.Contains("[Agent Identity]", StringComparison.Ordinal));
        Assert.IsTrue(compact.Contains("Communicates in Chinese.", StringComparison.Ordinal));
        Assert.IsFalse(compact.Contains("[Project Rules]", StringComparison.Ordinal));
        Assert.IsFalse(compact.Contains("[Learned from Mistakes]", StringComparison.Ordinal));
        Assert.IsFalse(compact.Contains("[Good Habits]", StringComparison.Ordinal));
        Assert.IsTrue(compact.Length < full.Length);
    }

    private SoulService CreateService()
        => new(_tempDir, NullLogger<SoulService>.Instance);
}
