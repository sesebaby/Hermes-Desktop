using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

namespace HermesDesktop.Tests.Stardew;

[TestClass]
public class StardewNpcBindingResolverTests
{
    [TestMethod]
    public void Catalog_ResolvesAliasesToStableNpcId()
    {
        var catalog = new StardewNpcCatalog(new[]
        {
            new NpcPackManifest
            {
                NpcId = "haley",
                DisplayName = "Haley",
                SmapiName = "Haley",
                Aliases = new[] { "haley", "Haley" }
            }
        });

        var resolved = catalog.TryResolve("Haley", out var manifest);

        Assert.IsTrue(resolved);
        Assert.AreEqual("haley", manifest.NpcId);
    }

    [TestMethod]
    public void Catalog_RejectsAmbiguousAlias()
    {
        Assert.ThrowsException<InvalidOperationException>(() => new StardewNpcCatalog(new[]
        {
            new NpcPackManifest { NpcId = "haley", DisplayName = "Haley", SmapiName = "Haley", Aliases = new[] { "h" } },
            new NpcPackManifest { NpcId = "penny", DisplayName = "Penny", SmapiName = "Penny", Aliases = new[] { "h" } }
        }));
    }

    [TestMethod]
    public void Resolve_UsesPackProvenanceForPenny()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-npc-binding-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            CreatePack(tempDir, "penny", "Penny");
            CreatePack(tempDir, "haley", "Haley");

            var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), tempDir);
            var binding = resolver.Resolve("Penny", "save-7");

            Assert.AreEqual("penny", binding.Descriptor.NpcId);
            Assert.AreEqual("Penny", binding.Descriptor.DisplayName);
            Assert.IsTrue(binding.Descriptor.PackRoot.EndsWith(Path.Combine("penny", "default"), StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual("sdv_save-7_penny_default", binding.Descriptor.SessionId);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public void Resolve_PopulatesRuntimeBodyBindingFromManifest()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-npc-binding-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            CreatePack(tempDir, "haley", "Haley", targetEntityId: "Haley");

            var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), tempDir);
            var binding = resolver.Resolve("haley", "save-7");

            Assert.IsNotNull(binding.Descriptor.BodyBinding);
            Assert.AreEqual("haley", binding.Descriptor.BodyBinding!.NpcId);
            Assert.AreEqual("Haley", binding.Descriptor.BodyBinding.SmapiName);
            Assert.AreEqual("Haley", binding.Descriptor.BodyBinding.TargetEntityId);
            Assert.AreEqual("stardew", binding.Descriptor.BodyBinding.AdapterId);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public void Resolve_RejectsMissingNpcId()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-npc-binding-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            CreatePack(tempDir, "penny", "Penny");
            var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), tempDir);

            Assert.ThrowsException<ArgumentException>(() => resolver.Resolve("", "save-7"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public void Resolve_RejectsMissingSaveId()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-npc-binding-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            CreatePack(tempDir, "penny", "Penny");
            var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), tempDir);

            Assert.ThrowsException<ArgumentException>(() => resolver.Resolve("Penny", ""));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void CreatePack(string root, string npcId, string displayName, string? targetEntityId = null)
    {
        var packRoot = Path.Combine(root, npcId, "default");
        Directory.CreateDirectory(packRoot);
        foreach (var file in new[] { "SOUL.md", "facts.md", "voice.md", "boundaries.md", "skills.json" })
            File.WriteAllText(Path.Combine(packRoot, file), file == "skills.json" ? """{"required":[],"optional":[]}""" : "ok");

        var manifest = new NpcPackManifest
        {
            SchemaVersion = 1,
            NpcId = npcId,
            GameId = "stardew-valley",
            ProfileId = "default",
            DefaultProfileId = "default",
            DisplayName = displayName,
            SmapiName = displayName,
            Aliases = [npcId, displayName],
            TargetEntityId = targetEntityId ?? npcId,
            AdapterId = "stardew",
            SoulFile = "SOUL.md",
            FactsFile = "facts.md",
            VoiceFile = "voice.md",
            BoundariesFile = "boundaries.md",
            SkillsFile = "skills.json",
            Capabilities = ["move", "speak"]
        };
        File.WriteAllText(Path.Combine(packRoot, FileSystemNpcPackLoader.ManifestFileName), JsonSerializer.Serialize(manifest));
    }
}
