using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StardewHermesBridge.Tests;

[TestClass]
public sealed class BridgeNpcTargetResolutionRegressionTests
{
    [TestMethod]
    public void BridgeHttpAndCommandPathsResolveProtocolNpcIdThroughSharedResolver()
    {
        var httpHost = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeHttpHost.cs");
        var commandQueue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");

        StringAssert.Contains(
            httpHost,
            "BridgeNpcResolver.Resolve(npcId)",
            "World snapshot queries must accept Hermes protocol npc ids such as lowercase 'haley'.");
        StringAssert.Contains(
            httpHost,
            "BridgeNpcResolver.Resolve(requestedNpc)",
            "Status queries must accept Hermes protocol npc ids such as lowercase 'haley'.");
        StringAssert.Contains(
            commandQueue,
            "BridgeNpcResolver.Resolve(npcId)",
            "Speak and private-chat commands must accept Hermes protocol npc ids such as lowercase 'haley'.");
        StringAssert.Contains(
            commandQueue,
            "BridgeNpcResolver.Resolve(command.NpcId)",
            "Move command execution must accept Hermes protocol npc ids such as lowercase 'haley'.");

        Assert.IsFalse(
            httpHost.Contains("Game1.getCharacterFromName(requestedNpc", StringComparison.Ordinal),
            "Bridge status lookup must not directly pass lowercase protocol npc ids to Stardew's resolver.");
        Assert.IsFalse(
            commandQueue.Contains("Game1.getCharacterFromName(npcId", StringComparison.Ordinal),
            "Bridge command lookup must not directly pass lowercase protocol npc ids to Stardew's resolver.");
        Assert.IsFalse(
            commandQueue.Contains("Game1.getCharacterFromName(command.NpcId", StringComparison.Ordinal),
            "Bridge move lookup must not directly pass lowercase protocol npc ids to Stardew's resolver.");
    }

    private static string ReadRepositoryFile(params string[] relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(relativePath).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            directory = directory.Parent;
        }

        Assert.Fail($"Could not find repository file: {Path.Combine(relativePath)}");
        return string.Empty;
    }
}
