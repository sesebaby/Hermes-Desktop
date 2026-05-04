using System.Text.Json;
using Hermes.Agent.Games.Stardew;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Stardew;

[TestClass]
public class StardewCommandContractTests
{
    [TestMethod]
    public void MoveEnvelope_SerializesCommandIdStatusAndTraceFields()
    {
        var envelope = new StardewBridgeEnvelope<StardewMoveRequest>(
            "req-1",
            "trace-1",
            "haley",
            "save-1",
            "idem-1",
            new StardewMoveRequest(new StardewMoveTarget("Town", new StardewTile(42, 17)), "inspect board"));
        var response = new StardewBridgeResponse<StardewMoveAcceptedData>(
            true,
            "trace-1",
            "req-1",
            "cmd-1",
            StardewCommandStatuses.Queued,
            new StardewMoveAcceptedData(true, new StardewMoveClaim("haley", new StardewTile(42, 17), new StardewTile(42, 17))),
            null,
            null);

        var requestJson = JsonSerializer.Serialize(envelope);
        var responseJson = JsonSerializer.Serialize(response);

        StringAssert.Contains(requestJson, "\"traceId\":\"trace-1\"");
        StringAssert.Contains(requestJson, "\"idempotencyKey\":\"idem-1\"");
        StringAssert.Contains(responseJson, "\"commandId\":\"cmd-1\"");
        StringAssert.Contains(responseJson, "\"status\":\"queued\"");
    }

    [TestMethod]
    public void ErrorCodes_UseLowerSnakeCase()
    {
        Assert.AreEqual("bridge_unauthorized", StardewBridgeErrorCodes.BridgeUnauthorized);
        Assert.AreEqual("command_conflict", StardewBridgeErrorCodes.CommandConflict);
        Assert.AreEqual("path_blocked", StardewBridgeErrorCodes.PathBlocked);
        Assert.AreEqual("path_unreachable", StardewBridgeErrorCodes.PathUnreachable);
        Assert.IsFalse(StardewBridgeErrorCodes.FestivalBlocked.Any(char.IsUpper));
    }

    [TestMethod]
    public void BridgeOptions_DefaultsToLoopbackOnly()
    {
        var options = new StardewBridgeOptions();

        Assert.IsTrue(options.IsLoopbackOnly());
        Assert.AreEqual("http://127.0.0.1:8745/", options.BaseUri.ToString());
    }
}
