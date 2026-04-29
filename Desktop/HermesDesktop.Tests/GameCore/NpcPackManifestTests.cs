using Hermes.Agent.Game;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.GameCore;

[TestClass]
public class NpcPackManifestTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "hermes-pack-manifest-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public void Validate_RequiresPhaseOneIdentityAndFiles()
    {
        var loader = new FileSystemNpcPackLoader();
        var manifest = new NpcPackManifest
        {
            SchemaVersion = 1,
            NpcId = "haley",
            GameId = "stardew-valley",
            ProfileId = "default",
            DefaultProfileId = "default",
            DisplayName = "Haley",
            SmapiName = "Haley",
            Aliases = new[] { "haley", "Haley" },
            TargetEntityId = "Haley",
            AdapterId = "stardew",
            SoulFile = "SOUL.md",
            FactsFile = "facts.md",
            VoiceFile = "voice.md",
            BoundariesFile = "boundaries.md",
            SkillsFile = "skills.json",
            Capabilities = new[] { "move", "speak" }
        };
        foreach (var file in new[] { "SOUL.md", "facts.md", "voice.md", "boundaries.md", "skills.json" })
            File.WriteAllText(Path.Combine(_tempDir, file), "ok");

        var result = loader.Validate(_tempDir, manifest);

        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
    }

    [TestMethod]
    public void Validate_RejectsPathTraversalAndUnknownCapability()
    {
        var loader = new FileSystemNpcPackLoader();
        var manifest = new NpcPackManifest
        {
            SchemaVersion = 1,
            NpcId = "haley",
            GameId = "stardew-valley",
            ProfileId = "default",
            DefaultProfileId = "default",
            DisplayName = "Haley",
            SmapiName = "Haley",
            Aliases = new[] { "haley" },
            TargetEntityId = "Haley",
            AdapterId = "stardew",
            SoulFile = "..\\SOUL.md",
            FactsFile = "facts.md",
            VoiceFile = "voice.md",
            BoundariesFile = "boundaries.md",
            SkillsFile = "skills.json",
            Capabilities = new[] { "move", "collect" }
        };

        var result = loader.Validate(_tempDir, manifest);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("inside the pack root", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(result.Errors.Any(error => error.Contains("collect", StringComparison.OrdinalIgnoreCase)));
    }
}
