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

    [TestMethod]
    public void TryParse_WhenSpeechAndTaskUpdateArePresent_AcceptsContract()
    {
        var ok = NpcLocalActionIntent.TryParse(
            """
            {
              "action": "wait",
              "reason": "path is blocked",
              "waitReason": "path_blocked",
              "speech": {
                "shouldSpeak": true,
                "channel": "player",
                "text": "I can't get there yet."
              },
              "taskUpdate": {
                "taskId": "1",
                "status": "blocked",
                "reason": "path_blocked"
              },
              "escalate": false
            }
            """,
            out var intent,
            out var error);

        Assert.IsTrue(ok, error);
        Assert.IsNotNull(intent);
        Assert.AreEqual(NpcLocalActionKind.Wait, intent.Action);
        Assert.IsNotNull(intent.Speech);
        Assert.IsTrue(intent.Speech.ShouldSpeak);
        Assert.AreEqual("player", intent.Speech.Channel);
        Assert.AreEqual("I can't get there yet.", intent.Speech.Text);
        Assert.IsNotNull(intent.TaskUpdate);
        Assert.AreEqual("1", intent.TaskUpdate.TaskId);
        Assert.AreEqual("blocked", intent.TaskUpdate.Status);
        Assert.AreEqual("path_blocked", intent.TaskUpdate.Reason);
    }

    [TestMethod]
    public void TryParse_WhenEscalateIsRequested_AcceptsContract()
    {
        var ok = NpcLocalActionIntent.TryParse(
            """
            {
              "action": "escalate",
              "reason": "needs private conversation",
              "escalate": true
            }
            """,
            out var intent,
            out var error);

        Assert.IsTrue(ok, error);
        Assert.IsNotNull(intent);
        Assert.AreEqual(NpcLocalActionKind.Escalate, intent.Action);
        Assert.IsTrue(intent.Escalate);
    }

    [DataTestMethod]
    [DataRow("not json", "intent_contract_invalid")]
    [DataRow("""{"action":"move","reason":"go"}""", "destinationId_required")]
    [DataRow("""{"action":"gift","reason":"give flowers"}""", "action_not_allowed")]
    [DataRow("""{"action":"wait","reason":"wait","speech":{"shouldSpeak":true}}""", "speech_text_required")]
    [DataRow("""{"action":"wait","reason":"wait","taskUpdate":{"taskId":"1","status":"done"}}""", "task_update_status_not_allowed")]
    public void TryParse_WhenContractIsInvalid_RejectsWithMachineReadableError(string contract, string expectedError)
    {
        var ok = NpcLocalActionIntent.TryParse(contract, out var intent, out var error);

        Assert.IsFalse(ok);
        Assert.IsNull(intent);
        Assert.AreEqual(expectedError, error);
    }
}
