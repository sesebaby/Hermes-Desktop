using System.Text.Json;
using Hermes.Agent.Core;
using Hermes.Agent.LLM;
using Hermes.Agent.Tasks;
using Hermes.Agent.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Services;

[TestClass]
public sealed class AgentToolSchemaTests
{
    [TestMethod]
    public async Task ExecuteAsync_SubagentTodoTool_ReceivesReferenceSchema()
    {
        var client = new CapturingChatClient();
        var registry = new ToolRegistry();
        registry.RegisterTool(new TodoTool(new SessionTodoStore()));
        var agentTool = new AgentTool(client, registry);

        await agentTool.ExecuteAsync(new AgentParameters
        {
            AgentType = "planner",
            Task = "Plan with todos"
        }, CancellationToken.None);

        var todoDefinition = client.LastTools.Single(t => t.Name == "todo");
        var properties = todoDefinition.Parameters.GetProperty("properties");
        Assert.IsTrue(properties.TryGetProperty("todos", out var todos));
        Assert.AreEqual(JsonValueKind.Array, todos.GetProperty("items").GetProperty("required").ValueKind);
        var status = todos.GetProperty("items").GetProperty("properties").GetProperty("status");
        CollectionAssert.AreEqual(
            new[] { "pending", "in_progress", "completed", "cancelled", "blocked", "failed" },
            status.GetProperty("enum").EnumerateArray().Select(v => v.GetString()).ToArray());
        Assert.IsTrue(todos.GetProperty("items").GetProperty("properties").TryGetProperty("reason", out _));
    }

    private sealed class CapturingChatClient : IChatClient
    {
        public IReadOnlyList<ToolDefinition> LastTools { get; private set; } = Array.Empty<ToolDefinition>();

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
            => Task.FromResult(new ChatResponse { Content = "", FinishReason = "stop" });

        public async IAsyncEnumerable<string> StreamAsync(
            IEnumerable<Message> messages,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<StreamEvent> StreamAsync(
            string? systemPrompt,
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition>? tools = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            LastTools = tools is null ? Array.Empty<ToolDefinition>() : tools.ToList();
            await Task.CompletedTask;
            yield break;
        }
    }
}
