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
}
