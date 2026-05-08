using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StardewHermesBridge.Tests;

[TestClass]
public sealed class BridgeIdleMicroActionRouteRegressionTests
{
    [TestMethod]
    public void CommandContractsExposeActionIdleMicroAction()
    {
        var commandContracts = ReadRepositoryFile("src", "games", "stardew", "StardewCommandContracts.cs");
        var desktopDtos = ReadRepositoryFile("src", "games", "stardew", "StardewBridgeDtos.cs");
        var bridgeModels = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandModels.cs");

        StringAssert.Contains(commandContracts, "ActionIdleMicroAction = \"/action/idle_micro_action\"");
        StringAssert.Contains(desktopDtos, "record StardewIdleMicroActionRequest");
        StringAssert.Contains(desktopDtos, "record StardewIdleMicroActionData");
        StringAssert.Contains(bridgeModels, "record IdleMicroActionPayload");
        StringAssert.Contains(bridgeModels, "record IdleMicroActionData");
    }

    [TestMethod]
    public void HttpHostAndQueueHandleIdleMicroActionThroughDedicatedRoute()
    {
        var httpHost = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeHttpHost.cs");
        var queue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");

        StringAssert.Contains(httpHost, "case \"/action/idle_micro_action\":");
        StringAssert.Contains(httpHost, "HandleIdleMicroActionAsync");
        StringAssert.Contains(queue, "IdleMicroActionAsync");
        StringAssert.Contains(queue, "ExecuteIdleMicroAction");
    }

    [TestMethod]
    public void IdleMicroActionUsesIdleMicroChannelAndDoesNotEmitPrivateChatClose()
    {
        var overlay = ReadRepositoryFile("Mods", "StardewHermesBridge", "Ui", "NpcOverheadBubbleOverlay.cs");
        var queue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");

        StringAssert.Contains(overlay, "idle_micro");
        StringAssert.Contains(queue, "\"idle_micro_action\"");
        Assert.IsFalse(
            queue.Contains("private_chat_reply_closed", StringComparison.Ordinal) &&
            queue.Contains("idle_micro_action", StringComparison.Ordinal) &&
            queue.Contains("private_chat_reply_closed", StringComparison.Ordinal),
            "idle micro action 路径不得复用 private_chat_reply_closed。");
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
