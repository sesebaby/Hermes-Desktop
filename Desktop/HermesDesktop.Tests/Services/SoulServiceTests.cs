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

    private SoulService CreateService()
        => new(_tempDir, NullLogger<SoulService>.Instance);
}
