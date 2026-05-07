using System.Runtime.CompilerServices;
using System.Text.Json;
using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.LLM;
using Hermes.Agent.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Runtime;

[TestClass]
public sealed class NpcLocalExecutorRunnerTests
{
    [TestMethod]
    public async Task ExecuteAsync_WithMoveIntent_StreamsDelegationToolCallAndExecutesRestrictedTool()
    {
        var chatClient = new RecordingChatClient(
            [
                new StreamEvent.ToolUseComplete(
                    "call-move",
                    "stardew_move",
                    Json("""{"destination":"PierreShop","reason":"meet player"}""")),
                new StreamEvent.MessageComplete("stop")
            ]);
        var moveTool = new RecordingTool(
            "stardew_move",
            typeof(MoveParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-move-1","status":"queued"}"""));
        var taskTool = new RecordingTool(
            "stardew_task_status",
            typeof(TaskStatusParameters),
            ToolResult.Ok("""{"commandId":"cmd-status-1","status":"completed"}"""));
        var runner = new NpcLocalExecutorRunner(chatClient, [moveTool, taskTool]);
        var intent = new NpcLocalActionIntent(
            NpcLocalActionKind.Move,
            "meet player",
            DestinationId: "PierreShop");

        var result = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            intent,
            [CreateObservationFact("Haley can move to Pierre.", "destination[0].destinationId=PierreShop")],
            "trace-local-runner",
            CancellationToken.None);

        Assert.AreEqual(1, chatClient.StreamCalls);
        Assert.AreEqual("stardew_move", chatClient.LastTools.Single().Name);
        Assert.AreEqual("model_called", result.ExecutorMode);
        Assert.IsTrue(chatClient.LastMessages.Single().Content?.Contains("PierreShop", StringComparison.Ordinal) ?? false);
        Assert.AreEqual(1, moveTool.ExecuteCalls);
        Assert.AreEqual(0, taskTool.ExecuteCalls);
        var parameters = (MoveParameters)moveTool.LastParameters!;
        Assert.AreEqual("PierreShop", parameters.Destination);
        Assert.AreEqual("meet player", parameters.Reason);
        Assert.AreEqual("stardew_move", result.Target);
        Assert.AreEqual("completed", result.Stage);
        Assert.AreEqual("queued", result.Result);
        Assert.AreEqual("cmd-move-1", result.CommandId);
        Assert.AreEqual("local_executor_completed:stardew_move", result.DecisionResponse);
        StringAssert.Contains(result.MemorySummary!, "stardew_move");
        Assert.IsFalse(result.MemorySummary!.Contains("\"commandId\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ExecuteAsync_WithMechanicalTargetMove_HostDeterministicExecutesNavigateToTile()
    {
        var chatClient = new RecordingChatClient(
            [
                new StreamEvent.ToolUseComplete(
                    "call-move",
                    "stardew_navigate_to_tile",
                    Json("""{"locationName":"Town","x":1,"y":1,"reason":"rewritten"}"""))
            ]);
        var moveTool = new RecordingTool(
            "stardew_move",
            typeof(MoveParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-move-legacy","status":"queued"}"""));
        var navigateTool = new RecordingTool(
            "stardew_navigate_to_tile",
            typeof(NavigateToTileParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-nav-1","status":"queued"}"""));
        var runner = new NpcLocalExecutorRunner(chatClient, [moveTool, navigateTool]);
        var intent = new NpcLocalActionIntent(
            NpcLocalActionKind.Move,
            "go to the beach",
            Target: new NpcLocalMoveTargetIntent(
                "Beach",
                20,
                35,
                "map-skill:stardew.navigation.poi.beach.shoreline",
                FacingDirection: 2));

        var result = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            intent,
            [CreateObservationFact("Haley can navigate to the beach.")],
            "trace-mechanical-move",
            CancellationToken.None);

        Assert.AreEqual(0, chatClient.StreamCalls);
        Assert.AreEqual(0, moveTool.ExecuteCalls);
        Assert.AreEqual(1, navigateTool.ExecuteCalls);
        var parameters = (NavigateToTileParameters)navigateTool.LastParameters!;
        Assert.AreEqual("Beach", parameters.LocationName);
        Assert.AreEqual(20, parameters.X);
        Assert.AreEqual(35, parameters.Y);
        Assert.AreEqual(2, parameters.FacingDirection);
        Assert.AreEqual("go to the beach", parameters.Reason);
        Assert.AreEqual("stardew_navigate_to_tile", result.Target);
        Assert.AreEqual("completed", result.Stage);
        Assert.AreEqual("queued", result.Result);
        Assert.AreEqual("cmd-nav-1", result.CommandId);
        Assert.AreEqual("host_deterministic", result.ExecutorMode);
        Assert.AreEqual("local_executor_completed:stardew_navigate_to_tile", result.DecisionResponse);
        Assert.AreEqual("map-skill:stardew.navigation.poi.beach.shoreline", result.TargetSource);
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
        var moveTool = new RecordingTool(
            "stardew_move",
            typeof(MoveParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-move-1","status":"queued"}"""));
        var runner = new NpcLocalExecutorRunner(chatClient, [moveTool]);

        var result = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(NpcLocalActionKind.Move, "meet player", DestinationId: "PierreShop"),
            [CreateObservationFact("Haley can move to Pierre.", "destination[0].destinationId=PierreShop")],
            "trace-no-tool",
            CancellationToken.None);

        Assert.AreEqual(2, chatClient.StreamCalls);
        Assert.AreEqual(0, moveTool.ExecuteCalls);
        Assert.AreEqual("blocked", result.ExecutorMode);
        Assert.AreEqual("local_executor_blocked:no_tool_call", result.DecisionResponse);
        CollectionAssert.Contains(result.Diagnostics.ToArray(), "target=local_executor stage=attempt result=no_tool_call;attempt=1");
        CollectionAssert.Contains(result.Diagnostics.ToArray(), "target=local_executor stage=retry result=no_tool_call;attempt=2");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithNoToolCallThenRetrySuccess_RecordsOnlyFirstFailedAttempt()
    {
        var chatClient = RecordingChatClient.WithEventsByCall(
            [
                [],
                [
                    new StreamEvent.ToolUseComplete(
                        "call-move",
                        "stardew_move",
                        Json("""{"destination":"PierreShop","reason":"meet player"}"""))
                ]
            ]);
        var moveTool = new RecordingTool(
            "stardew_move",
            typeof(MoveParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-move-1","status":"queued"}"""));
        var runner = new NpcLocalExecutorRunner(chatClient, [moveTool]);

        var result = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(NpcLocalActionKind.Move, "meet player", DestinationId: "PierreShop"),
            [CreateObservationFact("Haley can move to Pierre.", "destination[0].destinationId=PierreShop")],
            "trace-retry-success",
            CancellationToken.None);

        Assert.AreEqual(2, chatClient.StreamCalls);
        Assert.AreEqual(1, moveTool.ExecuteCalls);
        Assert.AreEqual("model_called", result.ExecutorMode);
        Assert.AreEqual("local_executor_completed:stardew_move", result.DecisionResponse);
        CollectionAssert.Contains(result.Diagnostics.ToArray(), "target=local_executor stage=attempt result=no_tool_call;attempt=1");
        CollectionAssert.DoesNotContain(result.Diagnostics.ToArray(), "target=local_executor stage=retry result=no_tool_call;attempt=2");
    }

    [TestMethod]
    public async Task BuildUserMessage_OmitsIrrelevantOptionalIntentFieldsByAction()
    {
        var chatClient = new RecordingChatClient([]);
        var runner = new NpcLocalExecutorRunner(
            chatClient,
            [new RecordingTool("stardew_move", typeof(MoveParameters), ToolResult.Ok("{}"))]);

        await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(NpcLocalActionKind.Move, "move", DestinationId: "PierreShop"),
            [CreateObservationFact("Move.")],
            "trace-move-fields",
            CancellationToken.None);
        AssertIntentOmits(chatClient.LastMessages.Last().Content!, "commandId", "observeTarget", "waitReason", "\"escalate\":false");

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
        var moveTool = new RecordingTool(
            "stardew_move",
            typeof(MoveParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-move-1","status":"queued"}"""));
        var runner = new NpcLocalExecutorRunner(chatClient, [moveTool]);

        var result = await runner.ExecuteAsync(
            CreateDescriptor("haley"),
            new NpcLocalActionIntent(NpcLocalActionKind.Move, "meet player", DestinationId: "PierreShop"),
            [CreateObservationFact("Haley can move to Pierre.", "destination[0].destinationId=PierreShop")],
            "trace-unknown-tool",
            CancellationToken.None);

        Assert.AreEqual(0, moveTool.ExecuteCalls);
        Assert.AreEqual("stardew_gift", result.Target);
        Assert.AreEqual("blocked", result.Stage);
        Assert.AreEqual("unknown_tool:stardew_gift", result.Result);
        Assert.AreEqual("unknown_tool", result.Error);
        Assert.AreEqual("blocked", result.ExecutorMode);
        Assert.AreEqual("local_executor_blocked:unknown_tool", result.DecisionResponse);
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
            LastMessages.AddRange(messages);
            LastTools = (tools ?? []).ToArray();
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
        private readonly ToolResult _result;

        public RecordingTool(string name, Type parametersType, ToolResult result)
        {
            Name = name;
            ParametersType = parametersType;
            _result = result;
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
            return Task.FromResult(_result);
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

    private sealed class NavigateToTileParameters
    {
        public required string LocationName { get; init; }

        public required int X { get; init; }

        public required int Y { get; init; }

        public int? FacingDirection { get; init; }

        public string? Reason { get; init; }

        public string? Thought { get; init; }
    }

    private sealed class NoParameters
    {
    }
}
