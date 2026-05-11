using System.Runtime.CompilerServices;
using System.Text.Json;
using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.LLM;
using Hermes.Agent.Runtime;
using Hermes.Agent.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Runtime;

[TestClass]
public sealed class NpcLocalExecutorRunnerTests
{
    [TestMethod]
    public async Task ExecuteAsync_WithMoveIntent_BlocksWithoutCallingModelOrNavigationTools()
    {
        var chatClient = new RecordingChatClient(
            [
                new StreamEvent.ToolUseComplete(
                    "call-nav",
                    "stardew_navigate_to_tile",
                    Json("""{"locationName":"Beach","x":32,"y":34,"source":"fixture","reason":"meet player"}"""))
            ]);
        var skillTool = new RecordingTool(
            "skill_view",
            typeof(SkillViewParameters),
            ToolResult.Ok("""{"success":true,"content":"`target(locationName=Beach,x=32,y=34,source=fixture)`"}"""));
        var navigateTool = new RecordingTool(
            "stardew_navigate_to_tile",
            typeof(NavigateToTileParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-nav-1","status":"queued"}"""));
        var runner = new NpcLocalExecutorRunner(chatClient, [skillTool, navigateTool]);

        var result = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(NpcLocalActionKind.Move, "meet player at the beach now", DestinationText: "beach"),
            [CreateObservationFact("Player asked Haley to go to the beach now.")],
            "trace-move-disabled",
            CancellationToken.None);

        Assert.AreEqual(0, chatClient.StreamCalls);
        Assert.AreEqual(0, skillTool.ExecuteCalls);
        Assert.AreEqual(0, navigateTool.ExecuteCalls);
        Assert.AreEqual("move", result.Target);
        Assert.AreEqual("blocked", result.Stage);
        Assert.AreEqual("local_executor_write_action_disabled", result.Result);
        Assert.AreEqual("local_executor_write_action_disabled", result.Error);
        Assert.AreEqual("blocked", result.ExecutorMode);
        Assert.AreEqual("local_executor_blocked:local_executor_write_action_disabled", result.DecisionResponse);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithMoveIntentAndMechanicalTarget_BlocksWithoutExposingTargetToModel()
    {
        var chatClient = new RecordingChatClient([]);
        var navigateTool = new RecordingTool(
            "stardew_navigate_to_tile",
            typeof(NavigateToTileParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-nav-1","status":"queued"}"""));
        var runner = new NpcLocalExecutorRunner(chatClient, [navigateTool]);
        var intent = new NpcLocalActionIntent(
            NpcLocalActionKind.Move,
            "go to the beach",
            DestinationText: "beach",
            Target: new NpcLocalMoveTargetIntent(
                "Beach",
                32,
                34,
                "fixture"));

        var result = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            intent,
            [CreateObservationFact("Haley has a disclosed beach target.")],
            "trace-mechanical-move-disabled",
            CancellationToken.None);

        Assert.AreEqual(0, chatClient.StreamCalls);
        Assert.AreEqual(0, navigateTool.ExecuteCalls);
        Assert.AreEqual("move", result.Target);
        Assert.AreEqual("local_executor_blocked:local_executor_write_action_disabled", result.DecisionResponse);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithTaskStatusIntent_ExposesOnlyTaskStatusTool()
    {
        var chatClient = new RecordingChatClient(
            [
                new StreamEvent.ToolUseComplete(
                    "call-status",
                    "stardew_task_status",
                    Json("""{"commandId":"cmd-move-1"}"""))
            ]);
        var moveTool = new RecordingTool(
            "stardew_move",
            typeof(MoveParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-move-1","status":"queued"}"""));
        var taskTool = new RecordingTool(
            "stardew_task_status",
            typeof(TaskStatusParameters),
            ToolResult.Ok("""{"commandId":"cmd-move-1","status":"completed"}"""));
        var runner = new NpcLocalExecutorRunner(chatClient, [moveTool, taskTool]);

        var result = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(NpcLocalActionKind.TaskStatus, "check progress", CommandId: "cmd-move-1"),
            [CreateObservationFact("Haley has a move command in progress.")],
            "trace-task-status",
            CancellationToken.None);

        Assert.AreEqual(1, chatClient.StreamCalls);
        Assert.AreEqual("stardew_task_status", chatClient.LastTools.Single().Name);
        Assert.AreEqual(0, moveTool.ExecuteCalls);
        Assert.AreEqual(1, taskTool.ExecuteCalls);
        Assert.AreEqual("model_called", result.ExecutorMode);
        Assert.AreEqual("local_executor_completed:stardew_task_status", result.DecisionResponse);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithObserveIntent_UsesReadOnlyStatusTool()
    {
        var chatClient = new RecordingChatClient(
            [
                new StreamEvent.ToolUseComplete(
                    "call-observe",
                    "stardew_status",
                    Json("{}"))
            ]);
        var statusTool = new RecordingTool(
            "stardew_status",
            typeof(NoParameters),
            ToolResult.Ok("""{"status":"completed","summary":"Haley is in Town."}"""));
        var moveTool = new RecordingTool(
            "stardew_move",
            typeof(MoveParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-move-1","status":"queued"}"""));
        var runner = new NpcLocalExecutorRunner(chatClient, [statusTool, moveTool]);

        var result = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(NpcLocalActionKind.Observe, "recheck current state", ObserveTarget: "current location"),
            [CreateObservationFact("Haley should observe current state.")],
            "trace-observe",
            CancellationToken.None);

        Assert.AreEqual(1, chatClient.StreamCalls);
        Assert.AreEqual("stardew_status", chatClient.LastTools.Single().Name);
        Assert.AreEqual(1, statusTool.ExecuteCalls);
        Assert.AreEqual(0, moveTool.ExecuteCalls);
        Assert.AreEqual("model_called", result.ExecutorMode);
        Assert.AreEqual("local_executor_completed:stardew_status", result.DecisionResponse);
        Assert.IsTrue(chatClient.LastMessages.Single().Content?.Contains("observeTarget", StringComparison.Ordinal) ?? false);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithIdleMicroAction_BlocksWithoutCallingModelOrTool()
    {
        var chatClient = new RecordingChatClient(
            [
                new StreamEvent.ToolUseComplete(
                    "call-idle",
                    "stardew_idle_micro_action",
                    Json("""{"kind":"look_around","intensity":"light","ttlSeconds":4}"""))
            ]);
        var idleTool = new RecordingTool(
            "stardew_idle_micro_action",
            typeof(IdleMicroActionParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-idle-1","status":"completed","result":"displayed"}"""));
        var moveTool = new RecordingTool(
            "stardew_move",
            typeof(MoveParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-move-1","status":"queued"}"""));
        var speakTool = new RecordingTool(
            "stardew_speak",
            typeof(SpeakParameters),
            ToolResult.Ok("""{"accepted":true,"status":"completed"}"""));
        var runner = new NpcLocalExecutorRunner(chatClient, [idleTool, moveTool, speakTool]);

        var result = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(
                NpcLocalActionKind.IdleMicroAction,
                "thinking about the next errand",
                IdleMicroAction: new NpcLocalIdleMicroActionIntent("look_around", null, "light", 4)),
            [CreateObservationFact("Haley is idle and visible.")],
            "trace-idle",
            CancellationToken.None);

        Assert.AreEqual(0, chatClient.StreamCalls);
        Assert.AreEqual(0, idleTool.ExecuteCalls);
        Assert.AreEqual(0, moveTool.ExecuteCalls);
        Assert.AreEqual(0, speakTool.ExecuteCalls);
        Assert.AreEqual("idle_micro_action", result.Target);
        Assert.AreEqual("blocked", result.Stage);
        Assert.AreEqual("local_executor_write_action_disabled", result.Result);
        Assert.AreEqual("local_executor_write_action_disabled", result.Error);
        Assert.AreEqual("blocked", result.ExecutorMode);
        Assert.AreEqual("local_executor_blocked:local_executor_write_action_disabled", result.DecisionResponse);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithIdleMicroAction_WithoutDelegation_Blocks()
    {
        var runner = new NpcUnavailableLocalExecutorRunner();

        var result = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(
                NpcLocalActionKind.IdleMicroAction,
                "thinking about the next errand",
                IdleMicroAction: new NpcLocalIdleMicroActionIntent("look_around", null, "light", 4)),
            [CreateObservationFact("Haley is idle and visible.")],
            "trace-idle-unavailable",
            CancellationToken.None);

        Assert.AreEqual("blocked", result.Stage);
        Assert.AreEqual("blocked", result.ExecutorMode);
        Assert.AreEqual("local_executor_blocked:local_executor_write_action_disabled", result.DecisionResponse);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithWaitIntent_CompletesHostInterpretedWithoutModelCall()
    {
        var chatClient = new RecordingChatClient([]);
        var runner = new NpcLocalExecutorRunner(
            chatClient,
            [new RecordingTool("stardew_move", typeof(MoveParameters), ToolResult.Ok("{}"))]);

        var result = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(NpcLocalActionKind.Wait, "wait for morning", WaitReason: "night"),
            [CreateObservationFact("It is late.")],
            "trace-wait",
            CancellationToken.None);

        Assert.AreEqual(0, chatClient.StreamCalls);
        Assert.AreEqual("host_interpreted", result.ExecutorMode);
        Assert.AreEqual("local_executor_completed:wait", result.DecisionResponse);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithObserveIntentAndUnavailableDelegation_Blocks()
    {
        var runner = new NpcUnavailableLocalExecutorRunner();

        var observe = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(NpcLocalActionKind.Observe, "recheck current state", ObserveTarget: "current location"),
            [CreateObservationFact("Haley should observe current state.")],
            "trace-observe-unavailable",
            CancellationToken.None);
        var wait = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(NpcLocalActionKind.Wait, "wait", WaitReason: "night"),
            [CreateObservationFact("Haley should wait.")],
            "trace-wait-unavailable",
            CancellationToken.None);

        Assert.AreEqual("blocked", observe.Stage);
        Assert.AreEqual("blocked", observe.ExecutorMode);
        Assert.AreEqual("local_executor_blocked:local_executor_unavailable", observe.DecisionResponse);
        Assert.AreEqual("host_interpreted", wait.ExecutorMode);
        Assert.AreEqual("local_executor_completed:wait", wait.DecisionResponse);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithMoveIntentAndNoToolCall_DoesNotRetryModel()
    {
        var chatClient = new RecordingChatClient([]);
        var skillTool = new RecordingTool(
            "skill_view",
            typeof(SkillViewParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-move-1","status":"queued"}"""));
        var navigateTool = new RecordingTool(
            "stardew_navigate_to_tile",
            typeof(NavigateToTileParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-nav-1","status":"queued"}"""));
        var runner = new NpcLocalExecutorRunner(chatClient, [skillTool, navigateTool]);

        var result = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(NpcLocalActionKind.Move, "meet player", DestinationText: "beach"),
            [CreateObservationFact("Haley should meet the player.")],
            "trace-no-tool",
            CancellationToken.None);

        Assert.AreEqual(0, chatClient.StreamCalls);
        Assert.AreEqual(0, skillTool.ExecuteCalls);
        Assert.AreEqual(0, navigateTool.ExecuteCalls);
        Assert.AreEqual("blocked", result.ExecutorMode);
        Assert.AreEqual("local_executor_blocked:local_executor_write_action_disabled", result.DecisionResponse);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithMoveIntentAndRetrySuccessEvents_DoesNotCallModel()
    {
        var chatClient = RecordingChatClient.WithEventsByCall(
            [
                [],
                [
                    new StreamEvent.ToolUseComplete(
                        "call-skill-beach",
                        "skill_view",
                        Json("""{"name":"stardew-navigation","file_path":"references/poi/beach-shoreline.md"}"""))
                ],
                [
                    new StreamEvent.ToolUseComplete(
                        "call-nav",
                        "stardew_navigate_to_tile",
                        Json("""{"locationName":"Beach","x":32,"y":34,"source":"map-skill:stardew.navigation.poi.beach-shoreline","reason":"meet player"}"""))
                ]
            ]);
        var skillTool = new RecordingTool(
            "skill_view",
            typeof(SkillViewParameters),
            ToolResult.Ok("""{"success":true,"content":"`target(locationName=Beach,x=32,y=34,source=map-skill:stardew.navigation.poi.beach-shoreline)`"}"""));
        var navigateTool = new RecordingTool(
            "stardew_navigate_to_tile",
            typeof(NavigateToTileParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-nav-1","status":"queued"}"""));
        var runner = new NpcLocalExecutorRunner(chatClient, [skillTool, navigateTool]);

        var result = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(NpcLocalActionKind.Move, "meet player", DestinationText: "beach"),
            [CreateObservationFact("Haley should meet the player.")],
            "trace-retry-success",
            CancellationToken.None);

        Assert.AreEqual(0, chatClient.StreamCalls);
        Assert.AreEqual(0, skillTool.ExecuteCalls);
        Assert.AreEqual(0, navigateTool.ExecuteCalls);
        Assert.AreEqual("blocked", result.ExecutorMode);
        Assert.AreEqual("local_executor_blocked:local_executor_write_action_disabled", result.DecisionResponse);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithNavigateBeforeLoadedSkillTarget_DoesNotCallModelOrNavigate()
    {
        var chatClient = RecordingChatClient.WithEventsByCall(
            [
                [
                    new StreamEvent.ToolUseComplete(
                        "call-bad-nav",
                        "stardew_navigate_to_tile",
                        Json("""{"locationName":"图书馆","x":0,"y":0,"reason":"玩家说去图书馆"}"""))
                ],
                [
                    new StreamEvent.ToolUseComplete(
                        "call-skill-poi",
                        "skill_view",
                        Json("""{"name":"stardew-navigation","file_path":"references/poi/beach-shoreline.md"}"""))
                ],
                [
                    new StreamEvent.ToolUseComplete(
                        "call-good-nav",
                        "stardew_navigate_to_tile",
                        Json("""{"locationName":"Beach","x":32,"y":34,"source":"map-skill:stardew.navigation.poi.beach-shoreline","reason":"玩家说现在去海边"}"""))
                ]
            ]);
        var skillTool = new RecordingTool(
            "skill_view",
            typeof(SkillViewParameters),
            ToolResult.Ok("""{"success":true,"content":"`target(locationName=Beach,x=32,y=34,source=map-skill:stardew.navigation.poi.beach-shoreline)`"}"""));
        var navigateTool = new RecordingTool(
            "stardew_navigate_to_tile",
            typeof(NavigateToTileParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-nav-1","status":"queued"}"""));
        var runner = new NpcLocalExecutorRunner(chatClient, [skillTool, navigateTool]);

        var result = await runner.ExecuteAsync(
            CreateDescriptor("penny"),
            new NpcLocalActionIntent(NpcLocalActionKind.Move, "玩家说现在去海边", DestinationText: "海边"),
            [CreateObservationFact("Player asked Penny to go somewhere now.", "intentText=现在去海边")],
            "trace-provenance",
            CancellationToken.None);

        Assert.AreEqual(0, chatClient.StreamCalls);
        Assert.AreEqual(0, skillTool.ExecuteCalls);
        Assert.AreEqual(0, navigateTool.ExecuteCalls);
        Assert.AreEqual("blocked", result.Stage);
        Assert.AreEqual("move", result.Target);
        Assert.AreEqual("local_executor_blocked:local_executor_write_action_disabled", result.DecisionResponse);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithNavigateArgsDifferentFromLoadedSkillTarget_DoesNotCallModelOrNavigate()
    {
        var chatClient = RecordingChatClient.WithEventsByCall(
            [
                [
                    new StreamEvent.ToolUseComplete(
                        "call-skill-poi",
                        "skill_view",
                        Json("""{"name":"stardew-navigation","file_path":"references/poi/beach-shoreline.md"}"""))
                ],
                [
                    new StreamEvent.ToolUseComplete(
                        "call-bad-nav",
                        "stardew_navigate_to_tile",
                        Json("""{"locationName":"图书馆","x":32,"y":34,"source":"map-skill:stardew.navigation.poi.beach-shoreline","reason":"玩家说现在去海边"}"""))
                ],
                [
                    new StreamEvent.ToolUseComplete(
                        "call-good-nav",
                        "stardew_navigate_to_tile",
                        Json("""{"locationName":"Beach","x":32,"y":34,"source":"map-skill:stardew.navigation.poi.beach-shoreline","reason":"玩家说现在去海边"}"""))
                ]
            ]);
        var skillTool = new RecordingTool(
            "skill_view",
            typeof(SkillViewParameters),
            ToolResult.Ok("""{"success":true,"content":"`target(locationName=Beach,x=32,y=34,source=map-skill:stardew.navigation.poi.beach-shoreline)`"}"""));
        var navigateTool = new RecordingTool(
            "stardew_navigate_to_tile",
            typeof(NavigateToTileParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-nav-1","status":"queued"}"""));
        var runner = new NpcLocalExecutorRunner(chatClient, [skillTool, navigateTool]);

        var result = await runner.ExecuteAsync(
            CreateDescriptor("penny"),
            new NpcLocalActionIntent(NpcLocalActionKind.Move, "玩家说现在去海边", DestinationText: "海边"),
            [CreateObservationFact("Player asked Penny to go somewhere now.", "intentText=现在去海边")],
            "trace-provenance-mismatch",
            CancellationToken.None);

        Assert.AreEqual(0, chatClient.StreamCalls);
        Assert.AreEqual(0, skillTool.ExecuteCalls);
        Assert.AreEqual(0, navigateTool.ExecuteCalls);
        Assert.AreEqual("blocked", result.Stage);
        Assert.AreEqual("move", result.Target);
        Assert.AreEqual("local_executor_blocked:local_executor_write_action_disabled", result.DecisionResponse);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithMoveSkillTargetLoaded_DoesNotCallModelOrAddNavigationReminder()
    {
        var chatClient = RecordingChatClient.WithEventsByCall(
            [
                [
                    new StreamEvent.ToolUseComplete(
                        "call-skill-beach",
                        "skill_view",
                        Json("""{"name":"stardew-navigation","file_path":"references/poi/beach-shoreline.md"}"""))
                ],
                []
            ]);
        var skillTool = new RecordingTool(
            "skill_view",
            typeof(SkillViewParameters),
            ToolResult.Ok("""{"success":true,"content":"`target(locationName=Beach,x=32,y=34,source=map-skill:stardew.navigation.poi.beach-shoreline)`"}"""));
        var navigateTool = new RecordingTool(
            "stardew_navigate_to_tile",
            typeof(NavigateToTileParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-nav-1","status":"queued"}"""));
        var runner = new NpcLocalExecutorRunner(chatClient, [skillTool, navigateTool]);

        var result = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(NpcLocalActionKind.Move, "go to the beach now", DestinationText: "beach"),
            [CreateObservationFact("Player asked Haley to go to the beach now.")],
            "trace-target-reminder",
            CancellationToken.None);

        Assert.AreEqual("blocked", result.Stage);
        Assert.AreEqual(0, chatClient.StreamCalls);
        Assert.AreEqual(0, skillTool.ExecuteCalls);
        Assert.AreEqual(0, navigateTool.ExecuteCalls);
        Assert.AreEqual("local_executor_blocked:local_executor_write_action_disabled", result.DecisionResponse);
    }

    [TestMethod]
    public async Task BuildUserMessage_OmitsIrrelevantOptionalIntentFieldsByAction()
    {
        var chatClient = new RecordingChatClient([]);
        var runner = new NpcLocalExecutorRunner(
            chatClient,
            [
                new RecordingTool("skill_view", typeof(SkillViewParameters), ToolResult.Ok("{}")),
                new RecordingTool("stardew_navigate_to_tile", typeof(NavigateToTileParameters), ToolResult.Ok("{}"))
            ]);

        var moveResult = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(NpcLocalActionKind.Move, "move", DestinationText: "beach"),
            [CreateObservationFact("Move.")],
            "trace-move-fields",
            CancellationToken.None);
        Assert.AreEqual("blocked", moveResult.ExecutorMode);
        Assert.AreEqual(0, chatClient.StreamCalls);

        await new NpcLocalExecutorRunner(
                chatClient,
                [new RecordingTool("stardew_status", typeof(NoParameters), ToolResult.Ok("{}"))])
            .ExecuteAsync(
                CreateDescriptor("haley"),
                new NpcLocalActionIntent(NpcLocalActionKind.Observe, "observe", ObserveTarget: "current"),
                [CreateObservationFact("Observe.")],
                "trace-observe-fields",
                CancellationToken.None);
        AssertIntentOmits(chatClient.LastMessages.Last().Content!, "destinationId", "commandId", "waitReason");

        var waitResult = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(NpcLocalActionKind.Wait, "wait", WaitReason: "night"),
            [CreateObservationFact("Wait.")],
            "trace-wait-fields",
            CancellationToken.None);
        Assert.AreEqual("host_interpreted", waitResult.ExecutorMode);

        await new NpcLocalExecutorRunner(
                chatClient,
                [new RecordingTool("stardew_task_status", typeof(TaskStatusParameters), ToolResult.Ok("{}"))])
            .ExecuteAsync(
                CreateDescriptor("haley"),
                new NpcLocalActionIntent(NpcLocalActionKind.TaskStatus, "status", CommandId: "cmd-1"),
                [CreateObservationFact("Status.")],
                "trace-task-fields",
                CancellationToken.None);
        AssertIntentOmits(chatClient.LastMessages.Last().Content!, "destinationId", "observeTarget", "waitReason", "\"escalate\":false");

        var escalate = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(NpcLocalActionKind.Escalate, "need parent", Escalate: true),
            [CreateObservationFact("Escalate.")],
            "trace-escalate-fields",
            CancellationToken.None);
        Assert.AreEqual("host_interpreted", escalate.ExecutorMode);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithMoveIntentAndUnknownToolCall_DoesNotCallModel()
    {
        var chatClient = new RecordingChatClient(
            [
                new StreamEvent.ToolUseComplete(
                    "call-gift",
                    "stardew_gift",
                    Json("""{"item":"diamond"}"""))
            ]);
        var navigateTool = new RecordingTool(
            "stardew_navigate_to_tile",
            typeof(NavigateToTileParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-move-1","status":"queued"}"""));
        var runner = new NpcLocalExecutorRunner(
            chatClient,
            [
                new RecordingTool("skill_view", typeof(SkillViewParameters), ToolResult.Ok("{}")),
                navigateTool
            ]);

        var result = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(NpcLocalActionKind.Move, "meet player", DestinationText: "beach"),
            [CreateObservationFact("Haley should meet player.")],
            "trace-unknown-tool",
            CancellationToken.None);

        Assert.AreEqual(0, chatClient.StreamCalls);
        Assert.AreEqual(0, navigateTool.ExecuteCalls);
        Assert.AreEqual("move", result.Target);
        Assert.AreEqual("blocked", result.Stage);
        Assert.AreEqual("local_executor_write_action_disabled", result.Result);
        Assert.AreEqual("local_executor_write_action_disabled", result.Error);
        Assert.AreEqual("blocked", result.ExecutorMode);
        Assert.AreEqual("local_executor_blocked:local_executor_write_action_disabled", result.DecisionResponse);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithMoveIntentAndSkillSearchWithoutTarget_DoesNotCallModel()
    {
        var chatClient = RecordingChatClient.WithEventsByCall(
            [
                [
                    new StreamEvent.ToolUseComplete(
                        "call-skill-main",
                        "skill_view",
                        Json("""{"name":"stardew-navigation"}"""))
                ],
                [
                    new StreamEvent.ToolUseComplete(
                        "call-skill-index",
                        "skill_view",
                        Json("""{"name":"stardew-navigation","file_path":"references/index.md"}"""))
                ],
                []
            ]);
        var skillTool = new RecordingTool(
            "skill_view",
            typeof(SkillViewParameters),
            ToolResult.Ok("""{"success":true,"content":"No matching target is listed here."}"""));
        var navigateTool = new RecordingTool(
            "stardew_navigate_to_tile",
            typeof(NavigateToTileParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-nav-1","status":"queued"}"""));
        var runner = new NpcLocalExecutorRunner(chatClient, [skillTool, navigateTool]);

        var result = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(NpcLocalActionKind.Move, "go to the museum now", DestinationText: "museum"),
            [CreateObservationFact("Player asked Haley to go to the museum.")],
            "trace-unresolved-target",
            CancellationToken.None);

        Assert.AreEqual(0, chatClient.StreamCalls);
        Assert.AreEqual(0, skillTool.ExecuteCalls);
        Assert.AreEqual(0, navigateTool.ExecuteCalls);
        Assert.AreEqual("blocked", result.Stage);
        Assert.AreEqual("local_executor_write_action_disabled", result.Error);
        Assert.AreEqual("local_executor_blocked:local_executor_write_action_disabled", result.DecisionResponse);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithEscalateIntent_CompletesWithoutCallingModelOrTool()
    {
        var chatClient = new RecordingChatClient([]);
        var moveTool = new RecordingTool(
            "stardew_move",
            typeof(MoveParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-move-1","status":"queued"}"""));
        var runner = new NpcLocalExecutorRunner(chatClient, [moveTool]);

        var result = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(NpcLocalActionKind.Escalate, "needs private conversation", Escalate: true),
            [CreateObservationFact("Haley needs help from the higher-level lane.")],
            "trace-escalate",
            CancellationToken.None);

        Assert.AreEqual(0, chatClient.StreamCalls);
        Assert.AreEqual(0, moveTool.ExecuteCalls);
        Assert.AreEqual("escalate", result.Target);
        Assert.AreEqual("completed", result.Stage);
        Assert.AreEqual("needs private conversation", result.Result);
        Assert.AreEqual("host_interpreted", result.ExecutorMode);
        Assert.AreEqual("local_executor_completed:escalate", result.DecisionResponse);
    }

    private static void AssertIntentOmits(string message, params string[] forbidden)
    {
        var intentLine = message.Split('\n').Single(line => line.StartsWith("intent: ", StringComparison.Ordinal));
        foreach (var value in forbidden)
            Assert.IsFalse(intentLine.Contains(value, StringComparison.Ordinal), $"Intent line should omit {value}: {intentLine}");
    }

    private static void AssertToolNames(IReadOnlyList<string> actual, params string[] expected)
    {
        CollectionAssert.AreEqual(expected, actual.ToArray());
    }

    private static NpcRuntimeDescriptor CreateDescriptor(string npcId)
        => new(
            npcId,
            npcId,
            "stardew-valley",
            "save-1",
            "default",
            "stardew",
            "pack-root",
            $"sdv_save-1_{npcId}_default");

    private static NpcObservationFact CreateObservationFact(string summary, params string[] facts)
        => new(
            "haley",
            "stardew-valley",
            "save-1",
            "default",
            "sdv_save-1_haley_default",
            "observation",
            null,
            DateTime.UtcNow,
            summary,
            facts);

    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private sealed class RecordingChatClient : IChatClient
    {
        private readonly IReadOnlyList<IReadOnlyList<StreamEvent>> _eventsByCall;

        public RecordingChatClient(IReadOnlyList<StreamEvent> events)
        {
            _eventsByCall = [events];
        }

        public int StreamCalls { get; private set; }
        public List<Message> LastMessages { get; } = [];
        public List<IReadOnlyList<Message>> MessagesByCall { get; } = [];
        public List<IReadOnlyList<string>> ToolNamesByCall { get; } = [];
        public IReadOnlyList<ToolDefinition> LastTools { get; private set; } = [];

        public static RecordingChatClient WithEventsByCall(IReadOnlyList<IReadOnlyList<StreamEvent>> eventsByCall)
            => new(eventsByCall.Count == 0 ? [[]] : eventsByCall);

        private RecordingChatClient(IReadOnlyList<IReadOnlyList<StreamEvent>> eventsByCall)
        {
            _eventsByCall = eventsByCall;
        }

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("unused");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
            => Task.FromResult(new ChatResponse { Content = "unused", FinishReason = "stop" });

        public async IAsyncEnumerable<string> StreamAsync(
            IEnumerable<Message> messages,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<StreamEvent> StreamAsync(
            string? systemPrompt,
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition>? tools = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            StreamCalls++;
            var callMessages = messages.ToArray();
            LastMessages.AddRange(callMessages);
            MessagesByCall.Add(callMessages);
            LastTools = (tools ?? []).ToArray();
            ToolNamesByCall.Add(LastTools.Select(tool => tool.Name).ToArray());
            var callEvents = _eventsByCall[Math.Min(StreamCalls - 1, _eventsByCall.Count - 1)];
            foreach (var streamEvent in callEvents)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return streamEvent;
            }
        }
    }

    private sealed class RecordingTool : ITool, IToolSchemaProvider
    {
        private readonly Func<object, ToolResult> _execute;

        public RecordingTool(string name, Type parametersType, ToolResult result)
            : this(name, parametersType, _ => result)
        {
        }

        public RecordingTool(string name, Type parametersType, Func<object, ToolResult> execute)
        {
            Name = name;
            ParametersType = parametersType;
            _execute = execute;
        }

        public string Name { get; }
        public string Description => "test tool";
        public Type ParametersType { get; }
        public int ExecuteCalls { get; private set; }
        public object? LastParameters { get; private set; }

        public JsonElement GetParameterSchema()
            => JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new
                {
                    destination = new { type = "string" },
                    reason = new { type = "string" }
                },
                required = new[] { "destination" }
            });

        public Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
        {
            ExecuteCalls++;
            LastParameters = parameters;
            return Task.FromResult(_execute(parameters));
        }
    }

    private sealed class MoveParameters
    {
        public required string Destination { get; init; }
        public string? Reason { get; init; }
    }

    private sealed class TaskStatusParameters
    {
        public required string CommandId { get; init; }
    }

    private sealed class IdleMicroActionParameters
    {
        public required string Kind { get; init; }
        public string? AnimationAlias { get; init; }
        public string? Intensity { get; init; }
        public int? TtlSeconds { get; init; }
    }

    private sealed class NavigateToTileParameters
    {
        public required string LocationName { get; init; }

        public required int X { get; init; }

        public required int Y { get; init; }

        public string? Source { get; init; }

        public int? FacingDirection { get; init; }

        public string? Reason { get; init; }

        public string? Thought { get; init; }
    }

    private sealed class NoParameters
    {
    }

    private sealed class SpeakParameters
    {
        public required string Text { get; init; }
    }
}
