using Hermes.Agent.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Runtime;

[TestClass]
public sealed class NpcLocalActionIntentTests
{
    [TestMethod]
    public void TryParse_WhenMoveHasDestinationId_AcceptsContract()
    {
        var ok = NpcLocalActionIntent.TryParse(
            """
            {
              "action": "move",
              "reason": "meet the player near Pierre",
              "destinationId": "PierreShop",
              "allowedActions": ["move", "observe", "wait", "task_status"],
              "escalate": false
            }
            """,
            out var intent,
            out var error);

        Assert.IsTrue(ok, error);
        Assert.IsNotNull(intent);
        Assert.AreEqual(NpcLocalActionKind.Move, intent.Action);
        Assert.AreEqual("PierreShop", intent.DestinationId);
        Assert.AreEqual("meet the player near Pierre", intent.Reason);
    }

    [TestMethod]
    public void TryParse_WhenWaitHasReason_AcceptsContract()
    {
        var ok = NpcLocalActionIntent.TryParse(
            """
            {
              "action": "wait",
              "reason": "no safe movement right now",
              "waitReason": "player is not nearby",
              "allowedActions": ["move", "observe", "wait", "task_status"],
              "escalate": false
            }
            """,
            out var intent,
            out var error);

        Assert.IsTrue(ok, error);
        Assert.IsNotNull(intent);
        Assert.AreEqual(NpcLocalActionKind.Wait, intent.Action);
        Assert.AreEqual("player is not nearby", intent.WaitReason);
    }

    [DataTestMethod]
    [DataRow("not json", "intent_contract_invalid")]
    [DataRow("""{"action":"move","reason":"go","allowedActions":["move"]}""", "destinationId_required")]
    [DataRow("""{"action":"gift","reason":"give flowers","allowedActions":["move","observe","wait","task_status"]}""", "action_not_allowed")]
    [DataRow("""{"action":"move","reason":"go","destinationId":"Town","allowedActions":["wait"]}""", "action_not_allowed")]
    public void TryParse_WhenContractIsInvalid_RejectsWithMachineReadableError(string contract, string expectedError)
    {
        var ok = NpcLocalActionIntent.TryParse(contract, out var intent, out var error);

        Assert.IsFalse(ok);
        Assert.IsNull(intent);
        Assert.AreEqual(expectedError, error);
    }
}
