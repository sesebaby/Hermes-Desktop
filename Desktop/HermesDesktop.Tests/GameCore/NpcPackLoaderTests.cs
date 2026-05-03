using System.Text.Json;
using Hermes.Agent.Game;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.GameCore;

[TestClass]
public class NpcPackLoaderTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "hermes-pack-loader-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public void LoadPacks_LoadsValidNestedManifest()
    {
        CreatePack("haley");

        var packs = new FileSystemNpcPackLoader().LoadPacks(_tempDir);

        Assert.AreEqual(1, packs.Count);
        Assert.AreEqual("haley", packs[0].Manifest.NpcId);
        Assert.IsTrue(packs[0].RootPath.EndsWith(Path.Combine("haley", "default"), StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void LoadPacks_RejectsDuplicateNpcIds()
    {
        CreatePack("haley", "one");
        CreatePack("haley", "two");

        var ex = Assert.ThrowsException<InvalidOperationException>(() => new FileSystemNpcPackLoader().LoadPacks(_tempDir));

        StringAssert.Contains(ex.Message, "Duplicate npcId");
    }

    private void CreatePack(string npcId, string parent = "")
    {
        var root = Path.Combine(_tempDir, parent, npcId, "default");
        Directory.CreateDirectory(root);
        foreach (var file in new[] { "SOUL.md", "facts.md", "voice.md", "boundaries.md", "skills.json" })
            File.WriteAllText(Path.Combine(root, file), file == "skills.json" ? """{"required":[],"optional":[]}""" : "ok");

        var manifest = new NpcPackManifest
        {
            SchemaVersion = 1,
            NpcId = npcId,
            GameId = "stardew-valley",
            ProfileId = "default",
            DefaultProfileId = "default",
            DisplayName = npcId,
            SmapiName = npcId,
            Aliases = new[] { npcId },
            TargetEntityId = npcId,
            AdapterId = "stardew",
            SoulFile = "SOUL.md",
            FactsFile = "facts.md",
            VoiceFile = "voice.md",
            BoundariesFile = "boundaries.md",
            SkillsFile = "skills.json",
            Capabilities = new[] { "move", "speak" }
        };
        File.WriteAllText(Path.Combine(root, FileSystemNpcPackLoader.ManifestFileName), JsonSerializer.Serialize(manifest));
    }
}
