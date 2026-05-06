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
        var runner = new NpcLocalExecutorRunner(chatClient, [moveTool]);
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
        Assert.IsTrue(chatClient.LastMessages.Single().Content?.Contains("PierreShop", StringComparison.Ordinal) ?? false);
        Assert.AreEqual(1, moveTool.ExecuteCalls);
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
        Assert.AreEqual("local_executor_completed:escalate", result.DecisionResponse);
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
        private readonly IReadOnlyList<StreamEvent> _events;

        public RecordingChatClient(IReadOnlyList<StreamEvent> events)
        {
            _events = events;
        }

        public int StreamCalls { get; private set; }
        public IReadOnlyList<Message> LastMessages { get; private set; } = [];
        public IReadOnlyList<ToolDefinition> LastTools { get; private set; } = [];

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
            LastMessages = messages.ToArray();
            LastTools = (tools ?? []).ToArray();
            foreach (var streamEvent in _events)
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
}
