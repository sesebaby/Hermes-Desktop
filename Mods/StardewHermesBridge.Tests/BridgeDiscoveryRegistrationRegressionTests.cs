using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StardewHermesBridge.Tests;

[TestClass]
public sealed class BridgeDiscoveryRegistrationRegressionTests
{
    [TestMethod]
    public void DiscoveryFileIsOnlyWrittenWhenHttpBridgeIsReallyListening()
    {
        var modEntry = ReadRepositoryFile("Mods", "StardewHermesBridge", "ModEntry.cs");
        var httpHost = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeHttpHost.cs");

        StringAssert.Contains(
            httpHost,
            "public bool IsRunning",
            "The SMAPI bridge must expose whether the HTTP listener actually started before discovery can be published.");
        StringAssert.Contains(
            modEntry,
            "if (!_httpHost.IsRunning)",
            "A failed second SMAPI instance must not overwrite stardew-bridge.json with a token for a listener that never started.");
        StringAssert.Contains(
            modEntry,
            "bridge_discovery_skipped",
            "Skipped discovery writes need an explicit log line so future port-conflict failures are visible.");
    }

    [TestMethod]
    public void PortConflictDoesNotLeaveBridgeMarkedOnline()
    {
        var httpHost = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeHttpHost.cs");

        StringAssert.Contains(
            httpHost,
            "bridge_start_failed",
            "HttpListener registration conflicts should be logged by the bridge instead of surfacing only as a SMAPI event exception.");
        StringAssert.Contains(
            httpHost,
            "BridgeToken = string.Empty;",
            "A failed listener start must clear the generated token so callers cannot publish an unusable discovery document.");
        StringAssert.Contains(
            httpHost,
            "Port = 0;",
            "A failed listener start must clear the port so the bridge is not mistaken for an online instance.");
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
