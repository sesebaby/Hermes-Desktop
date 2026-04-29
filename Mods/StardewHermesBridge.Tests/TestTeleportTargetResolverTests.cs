using Microsoft.VisualStudio.TestTools.UnitTesting;
using StardewHermesBridge.Commands;

namespace StardewHermesBridge.Tests;

[TestClass]
public sealed class TestTeleportTargetResolverTests
{
    [DataTestMethod]
    [DataRow("Haley", "Haley")]
    [DataRow("haley", "Haley")]
    [DataRow("海莉", "Haley")]
    [DataRow("Penny", "Penny")]
    [DataRow("penny", "Penny")]
    [DataRow("潘妮", "Penny")]
    public void TryResolve_SupportedNames_ReturnsSmapiNpcName(string input, string expected)
    {
        var resolved = TestTeleportTargetResolver.TryResolve(input, out var npcName);

        Assert.IsTrue(resolved);
        Assert.AreEqual(expected, npcName);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("Abigail")]
    [DataRow("阿比盖尔")]
    public void TryResolve_UnsupportedNames_ReturnsFalse(string input)
    {
        var resolved = TestTeleportTargetResolver.TryResolve(input, out var npcName);

        Assert.IsFalse(resolved);
        Assert.IsNull(npcName);
    }
}
