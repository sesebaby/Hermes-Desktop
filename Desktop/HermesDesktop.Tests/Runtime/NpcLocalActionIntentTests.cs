using Hermes.Agent.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Runtime;

[TestClass]
public sealed class NpcLocalActionIntentTests
{
    [TestMethod]
    public void TryParse_WhenMoveHasDestinationId_RejectsContract()
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

        Assert.IsFalse(ok);
        Assert.IsNull(intent);
        Assert.AreEqual("move_destinationId_not_supported", error);
    }

    [TestMethod]
    public void TryParse_WhenMoveHasNoTarget_AcceptsExecutorResolvedContract()
    {
        var ok = NpcLocalActionIntent.TryParse(
            """
            {
              "action": "move",
              "reason": "meet the player at the beach now",
              "destinationText": "海边",
              "escalate": false
            }
            """,
            out var intent,
            out var error);

        Assert.IsTrue(ok, error);
        Assert.IsNotNull(intent);
        Assert.AreEqual(NpcLocalActionKind.Move, intent.Action);
        Assert.IsNull(intent.DestinationId);
        Assert.IsNull(intent.Target);
        Assert.AreEqual("海边", intent.DestinationText);
        Assert.AreEqual("meet the player at the beach now", intent.Reason);
    }

    [TestMethod]
    public void TryParse_WhenMoveHasMechanicalTarget_RejectsContract()
    {
        var ok = NpcLocalActionIntent.TryParse(
            """
            {
              "action": "move",
              "reason": "go to the beach",
              "destinationText": "beach",
              "target": {
                "locationName": "Beach",
                "x": 20,
                "y": 35,
                "facingDirection": 2,
                "source": "map-skill:stardew.navigation.poi.beach.shoreline"
              }
            }
            """,
            out var intent,
            out var error);

        Assert.IsFalse(ok);
        Assert.IsNull(intent);
        Assert.AreEqual("move_target_not_supported", error);
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

    [TestMethod]
    public void TryParse_WhenIdleMicroActionUsesWhitelistedFields_AcceptsContract()
    {
        var ok = NpcLocalActionIntent.TryParse(
            """
            {
              "action": "idle_micro_action",
              "reason": "thinking about the next errand",
              "idleMicroAction": {
                "kind": "look_around",
                "intensity": "light",
                "ttlSeconds": 4
              }
            }
            """,
            out var intent,
            out var error);

        Assert.IsTrue(ok, error);
        Assert.IsNotNull(intent);
        Assert.AreEqual("thinking about the next errand", intent.Reason);
        Assert.AreEqual("look_around", intent.IdleMicroAction?.Kind);
        Assert.AreEqual("light", intent.IdleMicroAction?.Intensity);
        Assert.AreEqual(4, intent.IdleMicroAction?.TtlSeconds);
    }

    [DataTestMethod]
    [DataRow("not json", "intent_contract_invalid")]
    [DataRow("""{"action":"move","reason":"go"}""", "move_destinationText_required")]
    [DataRow("""{"action":"move","reason":"go","destinationText":"beach","target":{"x":20,"y":35,"source":"map-skill:beach"}}""", "move_target_not_supported")]
    [DataRow("""{"action":"move","reason":"go","destinationText":"beach","target":{"locationName":"Beach","y":35,"source":"map-skill:beach"}}""", "move_target_not_supported")]
    [DataRow("""{"action":"move","reason":"go","destinationText":"beach","target":{"locationName":"Beach","x":20,"source":"map-skill:beach"}}""", "move_target_not_supported")]
    [DataRow("""{"action":"move","reason":"go","destinationText":"beach","target":{"locationName":"Beach","x":20,"y":35}}""", "move_target_not_supported")]
    [DataRow("""{"action":"gift","reason":"give flowers"}""", "action_not_allowed")]
    [DataRow("""{"action":"wait","reason":"wait","speech":{"shouldSpeak":true}}""", "speech_text_required")]
    [DataRow("""{"action":"wait","reason":"wait","taskUpdate":{"taskId":"1","status":"done"}}""", "task_update_status_not_allowed")]
    [DataRow("""{"action":"idle_micro_action","reason":"idle","speech":{"shouldSpeak":true,"text":"hi"}}""", "idle_micro_action_forbidden_field")]
    [DataRow("""{"action":"idle_micro_action","reason":"idle","destinationId":"town.square","idleMicroAction":{"kind":"look_around"}}""", "idle_micro_action_forbidden_field")]
    [DataRow("""{"action":"idle_micro_action","reason":"idle","target":{"locationName":"Town","x":1,"y":2,"source":"map"},"idleMicroAction":{"kind":"look_around"}}""", "idle_micro_action_forbidden_field")]
    [DataRow("""{"action":"idle_micro_action","reason":"idle","idleMicroAction":{"kind":"raw_animation","rawAnimationId":"abc"}}""", "idle_micro_action_forbidden_field")]
    [DataRow("""{"action":"idle_micro_action","reason":"idle","idleMicroAction":{"kind":"dance_break"}}""", "idle_micro_action_kind_not_allowed")]
    public void TryParse_WhenContractIsInvalid_RejectsWithMachineReadableError(string contract, string expectedError)
    {
        var ok = NpcLocalActionIntent.TryParse(contract, out var intent, out var error);

        Assert.IsFalse(ok);
        Assert.IsNull(intent);
        Assert.AreEqual(expectedError, error);
    }
}
