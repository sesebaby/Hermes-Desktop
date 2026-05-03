using Hermes.Agent.Game;
using Hermes.Agent.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Runtime;

[TestClass]
public class NpcNamespaceTests
{
    [TestMethod]
    public void RuntimeRoot_IncludesGameSaveNpcAndProfile()
    {
        var root = Path.Combine(Path.GetTempPath(), "hermes-ns-tests");
        var ns = new NpcNamespace(root, "stardew-valley", "save-a", "haley", "default");

        var expected = Path.Combine(root, "runtime", "stardew", "games", "stardew-valley", "saves", "save-a", "npc", "haley", "profiles", "default");

        Assert.AreEqual(expected, ns.RuntimeRoot);
        Assert.AreEqual(Path.Combine(expected, "memory"), ns.MemoryPath);
        Assert.AreEqual(Path.Combine(expected, "transcripts"), ns.TranscriptPath);
        Assert.AreEqual("stardew:stardew-valley:save-a:haley:default", ns.SessionSource);
    }

    [TestMethod]
    public void Sanitize_RejectsTraversalSegments()
    {
        Assert.ThrowsException<ArgumentException>(() => NpcNamespace.Sanitize(".."));
    }

    [TestMethod]
    public void SeedPersonaPack_CopiesPackIntoPersonaDirectoryAndSeedsSoulFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-ns-pack-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var packRoot = Path.Combine(tempDir, "packs", "penny", "default");
            Directory.CreateDirectory(packRoot);
            File.WriteAllText(Path.Combine(packRoot, "SOUL.md"), "# Penny\n\npack-seeded-soul");
            File.WriteAllText(Path.Combine(packRoot, "facts.md"), "facts");
            File.WriteAllText(Path.Combine(packRoot, "voice.md"), "voice");
            File.WriteAllText(Path.Combine(packRoot, "boundaries.md"), "boundaries");
            File.WriteAllText(Path.Combine(packRoot, "skills.json"), """{"required":[],"optional":[]}""");

            var pack = new NpcPack(
                new NpcPackManifest
                {
                    SchemaVersion = 1,
                    NpcId = "penny",
                    GameId = "stardew-valley",
                    ProfileId = "default",
                    DefaultProfileId = "default",
                    DisplayName = "Penny",
                    SmapiName = "Penny",
                    Aliases = ["penny", "Penny"],
                    TargetEntityId = "penny",
                    AdapterId = "stardew",
                    SoulFile = "SOUL.md",
                    FactsFile = "facts.md",
                    VoiceFile = "voice.md",
                    BoundariesFile = "boundaries.md",
                    SkillsFile = "skills.json",
                    Capabilities = ["move", "speak"]
                },
                packRoot);

            var ns = new NpcNamespace(tempDir, "stardew-valley", "save-a", "penny", "default");

            ns.SeedPersonaPack(pack);

            Assert.IsTrue(File.Exists(ns.SoulFilePath));
            Assert.IsTrue(File.Exists(Path.Combine(ns.PersonaPath, "SOUL.md")));
            StringAssert.Contains(File.ReadAllText(ns.SoulFilePath), "pack-seeded-soul");
            StringAssert.Contains(File.ReadAllText(Path.Combine(ns.PersonaPath, "SOUL.md")), "pack-seeded-soul");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
