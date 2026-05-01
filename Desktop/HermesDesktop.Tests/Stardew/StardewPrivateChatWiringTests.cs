using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Stardew;

[TestClass]
public class StardewPrivateChatWiringTests
{
    [TestMethod]
    public void AppUsesNpcAutonomyHostAsSingleBridgePollOwner()
    {
        var app = ReadRepositoryFile("Desktop", "HermesDesktop", "App.xaml.cs");

        StringAssert.Contains(
            app,
            "StardewNpcAutonomyBackgroundService",
            "Desktop startup must register the autonomy background service so Haley and Penny can run without manual debug ticks.");
        StringAssert.Contains(
            app,
            "StartStardewNpcAutonomyBackground",
            "Desktop startup must start the autonomy background owner after DI is built.");
        Assert.IsFalse(
            app.Contains("StardewPrivateChatBackgroundService", StringComparison.Ordinal),
            "Desktop startup must not register a second private-chat background poller once bridge events are owned by the runtime host.");
        Assert.IsFalse(
            app.Contains("StartStardewPrivateChatBackground", StringComparison.Ordinal),
            "Desktop startup must not start a second private-chat background poller once bridge events are owned by the runtime host.");
        Assert.IsFalse(
            app.Contains("TryStopStardewPrivateChatBackground", StringComparison.Ordinal),
            "Desktop shutdown must not stop a second private-chat background poller once bridge events are owned by the runtime host.");
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
        StringAssert.Contains(
            runtime,
            "sessionLeaseCoordinator",
            "Stardew private-chat wrapper must pass the runtime lease coordinator into the reusable core state machine.");
    }

    [TestMethod]
    public void StardewPrivateChatRuntimeIsAnAdapterNotAnIndependentBackgroundLoop()
    {
        var runtime = ReadRepositoryFile("src", "games", "stardew", "StardewPrivateChatOrchestrator.cs");

        StringAssert.Contains(
            runtime,
            "StardewPrivateChatRuntimeAdapter",
            "Private-chat bridge integration should live in a host-internal adapter rather than a second background service.");
        Assert.IsFalse(
            runtime.Contains("public sealed class StardewPrivateChatBackgroundService", StringComparison.Ordinal),
            "The old private-chat background service should be removed once the host owns bridge polling.");
        Assert.IsFalse(
            runtime.Contains("RunLoopAsync", StringComparison.Ordinal),
            "The private-chat path must no longer own an independent polling loop after being folded into the runtime host.");
    }

    [TestMethod]
    public void DesktopAndNpcRuntimesUseSharedCapabilityAssembly()
    {
        var app = ReadRepositoryFile("Desktop", "HermesDesktop", "App.xaml.cs");
        var privateChatRuntime = ReadRepositoryFile("src", "games", "stardew", "StardewPrivateChatOrchestrator.cs");
        var autonomyRuntime = ReadRepositoryFile("src", "games", "stardew", "StardewAutonomyTickDebugService.cs");

        StringAssert.Contains(app, "AgentCapabilityAssembler.CreatePromptBuilder", "Desktop prompt assembly must use the shared capability assembler.");
        StringAssert.Contains(app, "AgentCapabilityAssembler.RegisterAllTools", "Desktop tool registration must use the shared capability assembler.");
        StringAssert.Contains(privateChatRuntime, "GetOrCreatePrivateChatHandleAsync", "NPC private-chat runner must ask the shared supervisor for a persistent runtime-backed agent handle.");
        StringAssert.Contains(privateChatRuntime, "NpcRuntimeSupervisor", "NPC private-chat runner must acquire runtime identity through the shared supervisor instead of scene-local ownership.");
        Assert.IsFalse(privateChatRuntime.Contains("new NpcRuntimeContextFactory().Create", StringComparison.Ordinal), "NPC private-chat runner must not rebuild context inside the scene entrypoint.");
        Assert.IsFalse(privateChatRuntime.Contains("new NpcAgentFactory().Create", StringComparison.Ordinal), "NPC private-chat runner must not rebuild agent state inside the scene entrypoint.");
        StringAssert.Contains(autonomyRuntime, "GetOrCreateAutonomyHandleAsync", "NPC autonomy/debug runner must ask the shared supervisor for a persistent runtime-backed autonomy handle.");
        StringAssert.Contains(autonomyRuntime, "NpcRuntimeSupervisor", "NPC autonomy/debug runner must acquire runtime identity through the shared supervisor instead of scene-local ownership.");
        Assert.IsFalse(autonomyRuntime.Contains("new NpcRuntimeContextFactory().Create", StringComparison.Ordinal), "NPC autonomy/debug runner must not rebuild context inside the debug entrypoint.");
        Assert.IsFalse(autonomyRuntime.Contains("new NpcAgentFactory().Create", StringComparison.Ordinal), "NPC autonomy/debug runner must not rebuild agent state inside the debug entrypoint.");
        Assert.IsFalse(autonomyRuntime.Contains("new NpcAutonomyLoop(", StringComparison.Ordinal), "NPC autonomy/debug runner must not rebuild the autonomy loop outside the shared supervisor.");
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
