using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
}
