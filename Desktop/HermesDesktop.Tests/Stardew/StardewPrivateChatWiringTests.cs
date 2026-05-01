using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Stardew;

[TestClass]
public class StardewPrivateChatWiringTests
{
    [TestMethod]
    public void AppRegistersAndStartsPrivateChatBackgroundService()
    {
        var app = ReadRepositoryFile("Desktop", "HermesDesktop", "App.xaml.cs");

        StringAssert.Contains(
            app,
            "StardewPrivateChatBackgroundService",
            "Desktop startup must register the private-chat background service so Haley dialogue completion can open input without a manual debug tick.");
        StringAssert.Contains(
            app,
            "StartStardewPrivateChatBackground",
            "Desktop startup must start the private-chat background poller after DI is built.");
    }

    [TestMethod]
    public void PrivateChatRuntimeProvidesRealAgentRunner()
    {
        var runtime = ReadRepositoryFile("src", "games", "stardew", "StardewPrivateChatOrchestrator.cs");

        StringAssert.Contains(
            runtime,
            "StardewNpcPrivateChatAgentRunner",
            "The private-chat path must route player text to a Desktop/core NPC agent runner, not a bridge-side fake reply.");
        StringAssert.Contains(
            runtime,
            "ChatAsync(",
            "The real runner should use the Hermes Agent path so persona, transcript, memory, and model behavior stay in Desktop/core.");
    }

    [TestMethod]
    public void StardewPrivateChatOrchestratorDelegatesReusableStateMachineToGameCore()
    {
        var runtime = ReadRepositoryFile("src", "games", "stardew", "StardewPrivateChatOrchestrator.cs");

        StringAssert.Contains(
            runtime,
            "new PrivateChatOrchestrator(",
            "Stardew must keep only the compatibility wrapper and delegate reusable private-chat state transitions to src/game/core.");
        StringAssert.Contains(
            runtime,
            "new PrivateChatPolicy(",
            "Stardew-specific event names, prompt, and retry codes should be injected through the core private-chat policy.");
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
