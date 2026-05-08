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
    public async Task AgentTool_UsesDefaultAgentDefinitionsWhenNoConfigProvided()
    {
        var client = new CapturingChatClient();
        var registry = CreateRegistry(AgentToolConfig.BuiltInAgentDefinitions.SelectMany(definition => definition.AllowedTools));
        var agentTool = new AgentTool(client, registry);

        foreach (var definition in AgentToolConfig.BuiltInAgentDefinitions)
        {
            await agentTool.ExecuteAsync(new AgentParameters
            {
                AgentType = definition.Name.ToLowerInvariant(),
                Task = $"Run {definition.Name}"
            }, CancellationToken.None);

            Assert.AreEqual(definition.SystemPrompt, client.LastSystemPrompt, definition.Name);
            CollectionAssert.AreEqual(
                definition.AllowedTools.ToArray(),
                client.LastTools.Select(tool => tool.Name).ToArray(),
                definition.Name);
        }
    }

    [TestMethod]
    public async Task AgentTool_UsesConfiguredAgentDefinition()
    {
        var client = new CapturingChatClient();
        var registry = CreateRegistry(["session_search", "todo", "memory"]);
        var agentTool = new AgentTool(
            client,
            registry,
            new AgentToolConfig
            {
                AgentDefinitions =
                [
                    new AgentDefinition(
                        "qa",
                        "You are a focused QA specialist.",
                        ["todo", "session_search"])
                ]
            });

        await agentTool.ExecuteAsync(new AgentParameters
        {
            AgentType = "qa",
            Task = "Verify the change"
        }, CancellationToken.None);

        Assert.AreEqual("You are a focused QA specialist.", client.LastSystemPrompt);
        CollectionAssert.AreEqual(new[] { "todo", "session_search" }, client.LastTools.Select(tool => tool.Name).ToArray());
    }

    [TestMethod]
    public async Task AgentTool_InvalidConfiguredToolNamesAreIgnoredOrRejectedDeterministically()
    {
        var client = new CapturingChatClient();
        var registry = CreateRegistry(["session_search", "todo", "memory"]);
        var agentTool = new AgentTool(
            client,
            registry,
            new AgentToolConfig
            {
                AgentDefinitions =
                [
                    new AgentDefinition(
                        "qa",
                        "You are a focused QA specialist.",
                        ["session_search", "", "not_registered", "todo", "todo"])
                ]
            });

        await agentTool.ExecuteAsync(new AgentParameters
        {
            AgentType = "qa",
            Task = "Verify the change"
        }, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "session_search", "todo" }, client.LastTools.Select(tool => tool.Name).ToArray());
        CollectionAssert.DoesNotContain(client.LastTools.Select(tool => tool.Name).ToArray(), "memory");
        CollectionAssert.DoesNotContain(client.LastTools.Select(tool => tool.Name).ToArray(), "not_registered");
    }

    [TestMethod]
    public async Task AgentTool_UnknownTypeFallsBackToGeneralDefinition()
    {
        var client = new CapturingChatClient();
        var general = AgentToolConfig.BuiltInAgentDefinitions.Single(definition => definition.Name == "General");
        var registry = CreateRegistry(general.AllowedTools.Append("stardew_move"));
        var agentTool = new AgentTool(
            client,
            registry,
            new AgentToolConfig
            {
                AgentDefinitions =
                [
                    new AgentDefinition(
                        "qa",
                        "You are a focused QA specialist.",
                        ["stardew_move"])
                ]
            });

        await agentTool.ExecuteAsync(new AgentParameters
        {
            AgentType = "unknown-specialist",
            Task = "Verify fallback"
        }, CancellationToken.None);

        Assert.AreEqual(general.SystemPrompt, client.LastSystemPrompt);
        CollectionAssert.AreEqual(general.AllowedTools.ToArray(), client.LastTools.Select(tool => tool.Name).ToArray());
        CollectionAssert.DoesNotContain(client.LastTools.Select(tool => tool.Name).ToArray(), "stardew_move");
    }

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

    private static ToolRegistry CreateRegistry(IEnumerable<string> toolNames)
    {
        var registry = new ToolRegistry();
        foreach (var toolName in toolNames.Distinct(StringComparer.OrdinalIgnoreCase))
            registry.RegisterTool(new FakeTool(toolName));

        return registry;
    }

    private sealed class FakeTool(string name) : ITool
    {
        public string Name { get; } = name;
        public string Description => "Fake tool for AgentTool tests.";
        public Type ParametersType => typeof(FakeToolParameters);

        public Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
            => Task.FromResult(ToolResult.Ok("{}"));
    }

    private sealed class FakeToolParameters
    {
    }

    private sealed class CapturingChatClient : IChatClient
    {
        public IReadOnlyList<ToolDefinition> LastTools { get; private set; } = Array.Empty<ToolDefinition>();
        public string? LastSystemPrompt { get; private set; }

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
            LastSystemPrompt = systemPrompt;
            LastTools = tools is null ? Array.Empty<ToolDefinition>() : tools.ToList();
            await Task.CompletedTask;
            yield break;
        }
    }
}
