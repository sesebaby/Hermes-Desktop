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
    public async Task ExecuteAsync_WithMoveIntent_ResolvesTargetThroughSkillViewThenNavigatesToTile()
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
                        Json("""{"locationName":"Beach","x":32,"y":34,"source":"map-skill:stardew.navigation.poi.beach-shoreline","reason":"meet player at the beach now"}"""))
                ]
            ]);
        var skillViewTool = new RecordingTool(
            "skill_view",
            typeof(SkillViewParameters),
            ToolResult.Ok("""{"success":true,"name":"stardew-navigation","file":"references/poi/beach-shoreline.md","content":"`target(locationName=Beach,x=32,y=34,source=map-skill:stardew.navigation.poi.beach-shoreline)`"}"""));
        var navigateTool = new RecordingTool(
            "stardew_navigate_to_tile",
            typeof(NavigateToTileParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-nav-1","status":"queued"}"""));
        var taskTool = new RecordingTool(
            "stardew_task_status",
            typeof(TaskStatusParameters),
            ToolResult.Ok("""{"commandId":"cmd-status-1","status":"completed"}"""));
        var legacyMoveTool = new RecordingTool(
            "stardew_move",
            typeof(MoveParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-move-legacy","status":"queued"}"""));
        var runner = new NpcLocalExecutorRunner(chatClient, [skillViewTool, navigateTool, taskTool, legacyMoveTool]);
        var intent = new NpcLocalActionIntent(
            NpcLocalActionKind.Move,
            "meet player at the beach now",
            DestinationText: "beach");

        var result = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            intent,
            [CreateObservationFact("Player asked Haley to go to the beach now.", "private_chat_request=go_to_beach_now")],
            "trace-local-runner",
            CancellationToken.None);

        Assert.AreEqual(4, chatClient.StreamCalls);
        Assert.IsTrue(
            chatClient.ToolNamesByCall.Take(3).All(names => names.SequenceEqual(["skill_view"]) || names.SequenceEqual(["stardew_navigate_to_tile"])),
            $"Move executor should expose one tool per turn before completion: {string.Join(" | ", chatClient.ToolNamesByCall.Select(names => string.Join(",", names)))}");
        Assert.IsFalse(
            chatClient.ToolNamesByCall.Take(3).Any(names => names.Contains("skill_view") && names.Contains("stardew_navigate_to_tile")),
            $"Move executor should not expose skill_view and navigate together: {string.Join(" | ", chatClient.ToolNamesByCall.Select(names => string.Join(",", names)))}");
        AssertToolNames(chatClient.ToolNamesByCall[3], "stardew_navigate_to_tile");
        CollectionAssert.Contains(chatClient.LastTools.Select(tool => tool.Name).ToArray(), "stardew_navigate_to_tile");
        CollectionAssert.DoesNotContain(chatClient.LastTools.Select(tool => tool.Name).ToArray(), "stardew_move");
        Assert.AreEqual("model_called", result.ExecutorMode);
        Assert.IsTrue(chatClient.LastMessages.First().Content?.Contains("meet player at the beach now", StringComparison.Ordinal) ?? false);
        Assert.AreEqual(3, skillViewTool.ExecuteCalls);
        Assert.AreEqual(1, navigateTool.ExecuteCalls);
        Assert.AreEqual(0, legacyMoveTool.ExecuteCalls);
        Assert.AreEqual(0, taskTool.ExecuteCalls);
        var parameters = (NavigateToTileParameters)navigateTool.LastParameters!;
        Assert.AreEqual("Beach", parameters.LocationName);
        Assert.AreEqual(32, parameters.X);
        Assert.AreEqual(34, parameters.Y);
        Assert.AreEqual("meet player at the beach now", parameters.Reason);
        Assert.AreEqual("stardew_navigate_to_tile", result.Target);
        Assert.AreEqual("completed", result.Stage);
        Assert.AreEqual("queued", result.Result);
        Assert.AreEqual("cmd-nav-1", result.CommandId);
        Assert.AreEqual("local_executor_completed:stardew_navigate_to_tile", result.DecisionResponse);
        StringAssert.Contains(result.MemorySummary!, "stardew_navigate_to_tile");
        Assert.IsFalse(result.MemorySummary!.Contains("\"commandId\"", StringComparison.Ordinal));
        CollectionAssert.Contains(result.Diagnostics.ToArray(), "target=skill_view stage=completed result=skill_source:stardew-navigation");
        CollectionAssert.Contains(result.Diagnostics.ToArray(), "target=skill_view stage=completed result=skill_source:stardew-navigation/references/index.md");
        CollectionAssert.Contains(result.Diagnostics.ToArray(), "target=skill_view stage=completed result=skill_source:stardew-navigation/references/poi/beach-shoreline.md");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithStaleParentMechanicalTarget_DoesNotExposeTargetToModel()
    {
        var chatClient = RecordingChatClient.WithEventsByCall(
            [
                [
                    new StreamEvent.ToolUseComplete(
                        "call-skill",
                        "skill_view",
                        Json("""{"name":"stardew-navigation","file_path":"references/poi/beach-shoreline.md"}"""))
                ],
                [
                    new StreamEvent.ToolUseComplete(
                        "call-nav",
                        "stardew_navigate_to_tile",
                        Json("""{"locationName":"Beach","x":32,"y":34,"source":"map-skill:stardew.navigation.poi.beach-shoreline","reason":"go to the beach"}"""))
                ]
            ]);
        var skillViewTool = new RecordingTool(
            "skill_view",
            typeof(SkillViewParameters),
            ToolResult.Ok("""{"success":true,"content":"`target(locationName=Beach,x=32,y=34,source=map-skill:stardew.navigation.poi.beach-shoreline)`"}"""));
        var moveTool = new RecordingTool(
            "stardew_move",
            typeof(MoveParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-move-legacy","status":"queued"}"""));
        var navigateTool = new RecordingTool(
            "stardew_navigate_to_tile",
            typeof(NavigateToTileParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-nav-1","status":"queued"}"""));
        var runner = new NpcLocalExecutorRunner(chatClient, [skillViewTool, moveTool, navigateTool]);
        var intent = new NpcLocalActionIntent(
            NpcLocalActionKind.Move,
            "go to the beach",
            DestinationText: "beach",
            Target: new NpcLocalMoveTargetIntent(
                "图书馆",
                0,
                0,
                "stardew-navigation",
                FacingDirection: 2));

        var result = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            intent,
            [CreateObservationFact("Haley can navigate to the beach.")],
            "trace-mechanical-move",
            CancellationToken.None);

        Assert.AreEqual(2, chatClient.StreamCalls);
        var firstUserMessage = chatClient.MessagesByCall[0].Single(message => message.Role == "user").Content!;
        StringAssert.Contains(firstUserMessage, "\"destinationText\":\"beach\"");
        Assert.IsFalse(firstUserMessage.Contains("\"target\"", StringComparison.Ordinal), firstUserMessage);
        Assert.IsFalse(firstUserMessage.Contains("图书馆", StringComparison.Ordinal), firstUserMessage);
        Assert.IsFalse(firstUserMessage.Contains("locationName", StringComparison.Ordinal), firstUserMessage);
        Assert.AreEqual(0, moveTool.ExecuteCalls);
        Assert.AreEqual(1, skillViewTool.ExecuteCalls);
        Assert.AreEqual(1, navigateTool.ExecuteCalls);
        var parameters = (NavigateToTileParameters)navigateTool.LastParameters!;
        Assert.AreEqual("Beach", parameters.LocationName);
        Assert.AreEqual(32, parameters.X);
        Assert.AreEqual(34, parameters.Y);
        Assert.AreEqual("go to the beach", parameters.Reason);
        Assert.AreEqual("stardew_navigate_to_tile", result.Target);
        Assert.AreEqual("completed", result.Stage);
        Assert.AreEqual("queued", result.Result);
        Assert.AreEqual("cmd-nav-1", result.CommandId);
        Assert.AreEqual("model_called", result.ExecutorMode);
        Assert.AreEqual("local_executor_completed:stardew_navigate_to_tile", result.DecisionResponse);
        Assert.AreEqual("map-skill:stardew.navigation.poi.beach-shoreline", result.TargetSource);
        CollectionAssert.Contains(
            result.Diagnostics.ToArray(),
            "target=skill_view stage=completed result=navigation_target_loaded;locationName=Beach;x=32;y=34;source=map-skill:stardew.navigation.poi.beach-shoreline");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithMoveIntent_DoesNotPreloadNavigationSkillContextBeforeModelCall()
    {
        var chatClient = RecordingChatClient.WithEventsByCall(
            [
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
                        Json("""{"locationName":"Beach","x":32,"y":34,"source":"map-skill:stardew.navigation.poi.beach-shoreline","reason":"go to the beach now"}"""))
                ]
            ]);
        var skillTool = new RecordingTool(
            "skill_view",
            typeof(SkillViewParameters),
            parameters =>
            {
                var p = (SkillViewParameters)parameters;
                return p.FilePath switch
                {
                    null => ToolResult.Ok("""{"success":true,"name":"stardew-navigation","content":"Navigation skill overview."}"""),
                    "references/index.md" => ToolResult.Ok("""{"success":true,"name":"stardew-navigation","file":"references/index.md","content":"Beach destination: references/poi/beach-shoreline.md"}"""),
                    "references/poi/beach-shoreline.md" => ToolResult.Ok("""{"success":true,"name":"stardew-navigation","file":"references/poi/beach-shoreline.md","content":"`target(locationName=Beach,x=32,y=34,source=map-skill:stardew.navigation.poi.beach-shoreline)`"}"""),
                    _ => ToolResult.Ok("""{"success":true,"content":""}""")
                };
            });
        var navigateTool = new RecordingTool(
            "stardew_navigate_to_tile",
            typeof(NavigateToTileParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-nav-1","status":"queued"}"""));
        var runner = new NpcLocalExecutorRunner(chatClient, [skillTool, navigateTool]);

        var result = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(NpcLocalActionKind.Move, "go to the beach now", DestinationText: "beach"),
            [CreateObservationFact("Player asked Haley to go to the beach now.")],
            "trace-preload-navigation",
            CancellationToken.None);

        Assert.AreEqual(2, chatClient.StreamCalls);
        Assert.AreEqual(1, skillTool.ExecuteCalls);
        Assert.AreEqual(1, navigateTool.ExecuteCalls);
        Assert.AreEqual("local_executor_completed:stardew_navigate_to_tile", result.DecisionResponse);
        var firstUserMessage = chatClient.MessagesByCall[0].Single(message => message.Role == "user").Content!;
        Assert.IsFalse(firstUserMessage.Contains("Navigation skill overview.", StringComparison.Ordinal), firstUserMessage);
        Assert.IsFalse(firstUserMessage.Contains("Beach destination: references/poi/beach-shoreline.md", StringComparison.Ordinal), firstUserMessage);
        Assert.IsFalse(firstUserMessage.Contains("preloaded_navigation_context", StringComparison.Ordinal), firstUserMessage);
        CollectionAssert.DoesNotContain(result.Diagnostics.ToArray(), "target=skill_view stage=completed result=skill_source:stardew-navigation;preloaded=true");
        CollectionAssert.DoesNotContain(result.Diagnostics.ToArray(), "target=skill_view stage=completed result=skill_source:stardew-navigation/references/index.md;preloaded=true");
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
    public async Task ExecuteAsync_WithIdleMicroAction_ExposesOnlyIdleMicroActionTool()
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

        Assert.AreEqual(1, chatClient.StreamCalls);
        Assert.AreEqual(1, chatClient.LastTools.Count);
        Assert.AreEqual("stardew_idle_micro_action", chatClient.LastTools.Single().Name);
        Assert.AreEqual(1, idleTool.ExecuteCalls);
        Assert.AreEqual(0, moveTool.ExecuteCalls);
        Assert.AreEqual(0, speakTool.ExecuteCalls);
        Assert.AreEqual("stardew_idle_micro_action", result.Target);
        Assert.AreEqual("displayed", result.Result);
        Assert.AreEqual("local_executor_completed:stardew_idle_micro_action", result.DecisionResponse);
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
        Assert.AreEqual("local_executor_blocked:local_executor_unavailable", result.DecisionResponse);
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
    public async Task ExecuteAsync_WithNoToolCall_RetriesOnceThenBlocks()
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

        Assert.AreEqual(2, chatClient.StreamCalls);
        Assert.AreEqual(0, skillTool.ExecuteCalls);
        Assert.AreEqual(0, navigateTool.ExecuteCalls);
        Assert.AreEqual("blocked", result.ExecutorMode);
        Assert.AreEqual("local_executor_blocked:executor_protocol_violation", result.DecisionResponse);
        CollectionAssert.Contains(result.Diagnostics.ToArray(), "target=local_executor stage=attempt result=executor_protocol_violation;attempt=1");
        CollectionAssert.Contains(result.Diagnostics.ToArray(), "target=local_executor stage=retry result=executor_protocol_violation;attempt=2");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithNoToolCallThenRetrySuccess_RecordsOnlyFirstFailedAttempt()
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

        Assert.AreEqual(3, chatClient.StreamCalls);
        Assert.AreEqual(1, skillTool.ExecuteCalls);
        Assert.AreEqual(1, navigateTool.ExecuteCalls);
        Assert.AreEqual("model_called", result.ExecutorMode);
        Assert.AreEqual("local_executor_completed:stardew_navigate_to_tile", result.DecisionResponse);
        CollectionAssert.Contains(result.Diagnostics.ToArray(), "target=local_executor stage=attempt result=executor_protocol_violation;attempt=1");
        CollectionAssert.DoesNotContain(result.Diagnostics.ToArray(), "target=local_executor stage=retry result=executor_protocol_violation;attempt=2");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithNavigateBeforeLoadedSkillTarget_ReturnsToolErrorAndSelfCorrects()
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

        Assert.AreEqual(3, chatClient.StreamCalls);
        Assert.AreEqual(1, navigateTool.ExecuteCalls, "第一次没有 skill target 证据的导航调用不能执行。");
        var parameters = (NavigateToTileParameters)navigateTool.LastParameters!;
        Assert.AreEqual("Beach", parameters.LocationName);
        Assert.AreEqual(32, parameters.X);
        Assert.AreEqual(34, parameters.Y);
        Assert.AreEqual("completed", result.Stage);
        Assert.AreEqual("stardew_navigate_to_tile", result.Target);
        Assert.IsTrue(
            chatClient.MessagesByCall.Skip(1).Any(messages => messages.Any(message =>
                message.Role == "tool" &&
                message.ToolName == "stardew_navigate_to_tile" &&
                (message.Content?.Contains("navigation_target_not_loaded", StringComparison.Ordinal) ?? false))),
            "模型过早导航时，应收到工具结果式错误并自我纠正。");
        CollectionAssert.Contains(
            result.Diagnostics.ToArray(),
            "target=stardew_navigate_to_tile stage=blocked result=navigation_target_not_loaded");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithNavigateArgsDifferentFromLoadedSkillTarget_ReturnsToolErrorAndSelfCorrects()
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

        Assert.AreEqual(3, chatClient.StreamCalls);
        Assert.AreEqual(1, navigateTool.ExecuteCalls, "与已加载 skill target 不一致的导航调用不能执行。");
        var parameters = (NavigateToTileParameters)navigateTool.LastParameters!;
        Assert.AreEqual("Beach", parameters.LocationName);
        Assert.AreEqual("completed", result.Stage);
        Assert.IsTrue(
            chatClient.MessagesByCall.Skip(2).Any(messages => messages.Any(message =>
                message.Role == "tool" &&
                message.ToolName == "stardew_navigate_to_tile" &&
                (message.Content?.Contains("navigation_target_mismatch", StringComparison.Ordinal) ?? false))),
            "模型填错导航参数时，应收到包含已加载 target 的工具错误。");
        CollectionAssert.Contains(
            result.Diagnostics.ToArray(),
            "target=stardew_navigate_to_tile stage=blocked result=navigation_target_mismatch");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithMoveIntentAndMismatchedSkillTarget_BlocksWithoutSubmittingNavigation()
    {
        var chatClient = RecordingChatClient.WithEventsByCall(
            [
                [
                    new StreamEvent.ToolUseComplete(
                        "call-skill-town",
                        "skill_view",
                        Json("""{"name":"stardew-navigation","file_path":"references/poi/town-square.md"}"""))
                ]
            ]);
        var skillTool = new RecordingTool(
            "skill_view",
            typeof(SkillViewParameters),
            ToolResult.Ok("""{"success":true,"content":"`target(locationName=Town,x=42,y=17,source=map-skill:stardew.navigation.poi.town-square)`"}"""));
        var navigateTool = new RecordingTool(
            "stardew_navigate_to_tile",
            typeof(NavigateToTileParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-town","status":"queued"}"""));
        var runner = new NpcLocalExecutorRunner(chatClient, [skillTool, navigateTool]);

        var result = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(NpcLocalActionKind.Move, "玩家邀请海莉现在去海边", DestinationText: "海边"),
            [CreateObservationFact("Player asked Haley to go to the beach now.", "intentText=海莉，我们现在去海边吧")],
            "trace-mismatched-target",
            CancellationToken.None);

        Assert.AreEqual(1, skillTool.ExecuteCalls);
        Assert.AreEqual(0, navigateTool.ExecuteCalls);
        Assert.AreEqual("blocked", result.Stage);
        Assert.AreEqual("navigation_target_mismatch", result.Error);
        Assert.AreEqual("local_executor_blocked:navigation_target_mismatch", result.DecisionResponse);
        CollectionAssert.Contains(
            result.Diagnostics.ToArray(),
            "target=skill_view stage=completed result=navigation_target_loaded;locationName=Town;x=42;y=17;source=map-skill:stardew.navigation.poi.town-square");
        CollectionAssert.Contains(
            result.Diagnostics.ToArray(),
            "target=local_executor stage=blocked result=navigation_target_mismatch;destinationText=海边;source=map-skill:stardew.navigation.poi.town-square");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithMoveSkillTargetLoaded_AddsNavigationTargetReminder()
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
        Assert.IsTrue(
            result.Diagnostics.Contains("target=skill_view stage=completed result=navigation_target_loaded;locationName=Beach;x=32;y=34;source=map-skill:stardew.navigation.poi.beach-shoreline"),
            $"应记录已从 skill 内容读取到完整 target。diagnostics={string.Join(" | ", result.Diagnostics)}");
        Assert.IsTrue(
            chatClient.MessagesByCall.Skip(1).Any(messages => messages.Any(message =>
                message.Role == "user" &&
                (message.Content?.Contains("下一步必须调用 stardew_navigate_to_tile", StringComparison.Ordinal) ?? false) &&
                message.Content.Contains("locationName=Beach,x=32,y=34,source=map-skill:stardew.navigation.poi.beach-shoreline", StringComparison.Ordinal))),
            "读取到完整 target 后，下一轮模型调用前应收到直接执行导航工具的中文提醒。");
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

        await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(NpcLocalActionKind.Move, "move", DestinationText: "beach"),
            [CreateObservationFact("Move.")],
            "trace-move-fields",
            CancellationToken.None);
        var moveMessage = chatClient.LastMessages.Last().Content!;
        StringAssert.Contains(moveMessage, "\"destinationText\":\"beach\"");
        AssertIntentOmits(moveMessage, "destinationId", "commandId", "observeTarget", "waitReason", "\"escalate\":false", "\"target\"");
        Assert.IsFalse(moveMessage.Contains("facts:", StringComparison.Ordinal), moveMessage);

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
    public async Task ExecuteAsync_WithUnknownToolCall_BlocksWithoutExecutingAnyTool()
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

        Assert.AreEqual(0, navigateTool.ExecuteCalls);
        Assert.AreEqual("stardew_gift", result.Target);
        Assert.AreEqual("blocked", result.Stage);
        Assert.AreEqual("unknown_tool:stardew_gift", result.Result);
        Assert.AreEqual("unknown_tool", result.Error);
        Assert.AreEqual("blocked", result.ExecutorMode);
        Assert.AreEqual("local_executor_blocked:unknown_tool", result.DecisionResponse);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithMoveIntentAndSkillSearchWithoutTarget_ReturnsUnresolvedNavigationTarget()
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

        Assert.AreEqual(3, chatClient.StreamCalls);
        Assert.AreEqual(2, skillTool.ExecuteCalls);
        Assert.AreEqual(0, navigateTool.ExecuteCalls);
        Assert.AreEqual("blocked", result.Stage);
        Assert.AreEqual("unresolved_navigation_target", result.Error);
        Assert.AreEqual("local_executor_blocked:unresolved_navigation_target", result.DecisionResponse);
        CollectionAssert.Contains(result.Diagnostics.ToArray(), "target=skill_view stage=completed result=skill_source:stardew-navigation");
        CollectionAssert.Contains(result.Diagnostics.ToArray(), "target=skill_view stage=completed result=skill_source:stardew-navigation/references/index.md");
        CollectionAssert.Contains(result.Diagnostics.ToArray(), "target=local_executor stage=attempt result=unresolved_navigation_target;attempt=1");
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
