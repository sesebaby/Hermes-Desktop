using System.Runtime.CompilerServices;
using Hermes.Agent.Context;
using Hermes.Agent.Core;
using Hermes.Agent.LLM;
using Hermes.Agent.Memory;
using Hermes.Agent.Runtime;
using Hermes.Agent.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Stardew;

[TestClass]
public class StardewAutonomyContextBudgetTests
{
    [TestMethod]
    public void BudgetPolicy_MarkerMissing_NoOps()
    {
        var logger = new CapturingLogger<StardewAutonomyFirstCallContextBudgetPolicy>();
        var policy = new StardewAutonomyFirstCallContextBudgetPolicy(logger);
        var session = new Session { Id = "private-chat", Platform = "stardew" };
        session.State["traceId"] = "trace-private";
        session.State["npcId"] = "haley";
        var messages = new List<Message>
        {
            new() { Role = "system", Content = "system" },
            new() { Role = "user", Content = "private chat with trace and npc id" }
        };

        var result = policy.Apply(new FirstCallContextBudgetRequest(session, messages, "private chat", 1));

        Assert.IsFalse(result.Applied);
        CollectionAssert.AreEqual(messages, result.Messages.ToList());
        Assert.IsFalse(logger.Messages.Any(message => message.Contains("autonomy_context_budget_started", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void BudgetPolicy_MarkerTrue_ReducesUnprotectedPayloadToFiveThousandChars()
    {
        var logger = new CapturingLogger<StardewAutonomyFirstCallContextBudgetPolicy>();
        var policy = new StardewAutonomyFirstCallContextBudgetPolicy(logger);
        var session = CreateAutonomySession();
        var currentUser = "CURRENT AUTONOMY DECISION";
        var messages = new List<Message>
        {
            new() { Role = "system", Content = "SYSTEM SOUL AND STARDew SKILL SUPPLEMENT" },
            new() { Role = "assistant", Content = new string('a', 4200) },
            new() { Role = "tool", ToolName = "stardew_status", ToolCallId = "old-status", Content = new string('s', 4200) },
            new() { Role = "user", Content = currentUser }
        };

        var result = policy.Apply(new FirstCallContextBudgetRequest(session, messages, currentUser, 1));

        Assert.IsTrue(result.Applied);
        Assert.IsTrue(result.BudgetMet, $"Expected budget to be met but got {result.BudgetUnmetReason}.");
        Assert.IsTrue(StardewAutonomyFirstCallContextBudgetPolicy.CountCharacters(result.Messages) <= 5000);
        Assert.IsTrue(result.Messages.Any(message => message.Role == "system" && message.Content.Contains("SYSTEM SOUL", StringComparison.Ordinal)));
        Assert.IsTrue(result.Messages.Any(message => message.Role == "user" && message.Content == currentUser));
        Assert.IsTrue(logger.Messages.Any(message =>
            message.Contains("autonomy_context_budget_started", StringComparison.Ordinal) &&
            message.Contains("trace-budget", StringComparison.Ordinal) &&
            message.Contains("haley", StringComparison.Ordinal)));
        Assert.IsTrue(logger.Messages.Any(message =>
            message.Contains("autonomy_context_budget_completed", StringComparison.Ordinal) &&
            message.Contains("budgetMet=True", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void BudgetPolicy_ProtectsLatestAssistantToolRequestAndMatchingToolResults()
    {
        var policy = new StardewAutonomyFirstCallContextBudgetPolicy(NullLogger<StardewAutonomyFirstCallContextBudgetPolicy>.Instance);
        var session = CreateAutonomySession();
        var messages = new List<Message>
        {
            new() { Role = "system", Content = "system" },
            new()
            {
                Role = "assistant",
                Content = "old request",
                ToolCalls = [new ToolCall { Id = "old-call", Name = "stardew_status", Arguments = "{}" }]
            },
            new() { Role = "tool", ToolName = "stardew_status", ToolCallId = "old-call", Content = new string('o', 7000) },
            new()
            {
                Role = "assistant",
                Content = "latest request without continuation keywords",
                ToolCalls =
                [
                    new ToolCall { Id = "latest-move", Name = "stardew_move", Arguments = """{"destination":"Town"}""" },
                    new ToolCall { Id = "latest-task", Name = "stardew_task_status", Arguments = "{}" }
                ]
            },
            new() { Role = "tool", ToolName = "stardew_move", ToolCallId = "latest-move", Content = "move result survives" },
            new() { Role = "tool", ToolName = "stardew_task_status", ToolCallId = "latest-task", Content = "task result survives" },
            new() { Role = "user", Content = "current decision" }
        };

        var result = policy.Apply(new FirstCallContextBudgetRequest(session, messages, "current decision", 1));
        var output = result.Messages.ToList();

        Assert.IsTrue(output.Any(message => message.Role == "assistant" && message.ToolCalls?.Any(call => call.Id == "latest-move") == true));
        Assert.IsTrue(output.Any(message => message.Role == "tool" && message.ToolCallId == "latest-move" && message.Content.Contains("survives", StringComparison.Ordinal)));
        Assert.IsTrue(output.Any(message => message.Role == "tool" && message.ToolCallId == "latest-task" && message.Content.Contains("survives", StringComparison.Ordinal)));
        Assert.IsFalse(output.Any(message => message.ToolCallId == "old-call" && message.Content.Contains(new string('o', 100), StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task Agent_FirstToolIteration_AppliesPolicyBeforeClientCall()
    {
        var chatClient = new CapturingChatClient();
        var policy = new ReplacingBudgetPolicy([
            new Message { Role = "system", Content = "policy output" },
            new Message { Role = "user", Content = "trimmed user" }
        ]);
        var agent = new Agent(
            chatClient,
            NullLogger<Agent>.Instance,
            turnMemoryCoordinator: CreateNoopTurnMemoryCoordinator(),
            firstCallContextBudgetPolicy: policy);
        agent.RegisterTool(new NoopTool("noop"));
        var session = CreateAutonomySession();

        var result = await agent.ChatAsync("original user", session, CancellationToken.None);

        Assert.AreEqual("done", result);
        Assert.AreEqual(1, policy.Calls);
        Assert.IsNotNull(chatClient.FirstToolMessages);
        CollectionAssert.AreEqual(new[] { "policy output", "trimmed user" }, chatClient.FirstToolMessages.Select(message => message.Content).ToArray());
    }

    [TestMethod]
    public async Task StatusBudget_MarkerTrue_BlocksSecondBroadStatusAcrossDifferentToolNames()
    {
        var chatClient = new SequencedToolChatClient(
            new ChatResponse
            {
                FinishReason = "tool_calls",
                ToolCalls =
                [
                    new ToolCall { Id = "call-1", Name = "stardew_status", Arguments = "{}" },
                    new ToolCall { Id = "call-2", Name = "stardew_social_status", Arguments = "{}" }
                ]
            },
            new ChatResponse { Content = "done", FinishReason = "stop" });
        var agent = new Agent(chatClient, NullLogger<Agent>.Instance);
        var firstTool = new CountingTool("stardew_status");
        var secondTool = new CountingTool("stardew_social_status");
        agent.RegisterTool(firstTool);
        agent.RegisterTool(secondTool);
        var session = CreateAutonomySession();

        await agent.ChatAsync("check status", session, CancellationToken.None);

        Assert.AreEqual(1, firstTool.ExecuteCalls);
        Assert.AreEqual(0, secondTool.ExecuteCalls);
        var blocked = session.Messages.Single(message => message.Role == "tool" && message.ToolCallId == "call-2");
        StringAssert.Contains(blocked.Content, "status_tool_budget_exceeded");
        StringAssert.Contains(blocked.Content, "broad_status");
    }

    [TestMethod]
    public async Task StatusBudget_NoMarker_AllowsBroadStatusAcrossDifferentToolNames()
    {
        var chatClient = new SequencedToolChatClient(
            new ChatResponse
            {
                FinishReason = "tool_calls",
                ToolCalls =
                [
                    new ToolCall { Id = "call-1", Name = "stardew_status", Arguments = "{}" },
                    new ToolCall { Id = "call-2", Name = "stardew_social_status", Arguments = "{}" }
                ]
            },
            new ChatResponse { Content = "done", FinishReason = "stop" });
        var agent = new Agent(chatClient, NullLogger<Agent>.Instance);
        var firstTool = new CountingTool("stardew_status");
        var secondTool = new CountingTool("stardew_social_status");
        agent.RegisterTool(firstTool);
        agent.RegisterTool(secondTool);
        var session = new Session { Id = "private-chat", Platform = "stardew" };
        session.State["traceId"] = "trace-private";
        session.State["npcId"] = "haley";

        await agent.ChatAsync("check status", session, CancellationToken.None);

        Assert.AreEqual(1, firstTool.ExecuteCalls);
        Assert.AreEqual(1, secondTool.ExecuteCalls);
    }

    [TestMethod]
    public async Task StatusBudget_MarkerTrue_AllowsStardewTaskStatusAfterBroadStatus()
    {
        var chatClient = new SequencedToolChatClient(
            new ChatResponse
            {
                FinishReason = "tool_calls",
                ToolCalls =
                [
                    new ToolCall { Id = "call-1", Name = "stardew_status", Arguments = "{}" },
                    new ToolCall { Id = "call-2", Name = "stardew_task_status", Arguments = "{}" }
                ]
            },
            new ChatResponse { Content = "done", FinishReason = "stop" });
        var agent = new Agent(chatClient, NullLogger<Agent>.Instance);
        var broadTool = new CountingTool("stardew_status");
        var taskTool = new CountingTool("stardew_task_status");
        agent.RegisterTool(broadTool);
        agent.RegisterTool(taskTool);
        var session = CreateAutonomySession();

        await agent.ChatAsync("continue task", session, CancellationToken.None);

        Assert.AreEqual(1, broadTool.ExecuteCalls);
        Assert.AreEqual(1, taskTool.ExecuteCalls);
    }

    [TestMethod]
    public void StardewSkillAssets_ContainSinglePurposeAndOnDemandRecallGuidance()
    {
        var root = FindRepositoryRoot();
        var combined = string.Join(
            "\n",
            File.ReadAllText(Path.Combine(root, "skills", "gaming", "stardew-core.md")),
            File.ReadAllText(Path.Combine(root, "skills", "gaming", "stardew-social.md")),
            File.ReadAllText(Path.Combine(root, "skills", "gaming", "stardew-navigation.md")));

        StringAssert.Contains(combined, "one turn purpose");
        StringAssert.Contains(combined, "session_search");
        StringAssert.Contains(combined, "memory");
        StringAssert.Contains(combined, "avoid repeated broad status scans");
        StringAssert.Contains(combined, "stardew_task_status");
        Assert.IsFalse(combined.Contains("create NPC recap", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(combined.Contains("create persona summary", StringComparison.OrdinalIgnoreCase));
    }

    private static Session CreateAutonomySession()
    {
        var session = new Session { Id = "sdv_save-1_haley_default", Platform = "stardew" };
        session.State[StardewAutonomySessionKeys.IsAutonomyTurn] = true;
        session.State["traceId"] = "trace-budget";
        session.State["npcId"] = "haley";
        return session;
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "skills", "gaming")) &&
                File.Exists(Path.Combine(dir.FullName, "HermesDesktop.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from AppContext.BaseDirectory.");
    }

    private static TurnMemoryCoordinator CreateNoopTurnMemoryCoordinator()
        => new(
            contextManager: null,
            new HermesMemoryOrchestrator(Array.Empty<IMemoryProvider>(), NullLogger<HermesMemoryOrchestrator>.Instance),
            NullLogger<TurnMemoryCoordinator>.Instance);

    private sealed class ReplacingBudgetPolicy(IReadOnlyList<Message> replacement) : IFirstCallContextBudgetPolicy
    {
        public int Calls { get; private set; }

        public FirstCallContextBudgetResult Apply(FirstCallContextBudgetRequest request)
        {
            Calls++;
            return new FirstCallContextBudgetResult(replacement, Applied: true, BudgetMet: true);
        }
    }

    private sealed class CapturingChatClient : IChatClient
    {
        public List<Message>? FirstToolMessages { get; private set; }

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("done");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            FirstToolMessages ??= messages.ToList();
            return Task.FromResult(new ChatResponse { Content = "done", FinishReason = "stop" });
        }

        public async IAsyncEnumerable<string> StreamAsync(IEnumerable<Message> messages, [EnumeratorCancellation] CancellationToken ct)
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
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class SequencedToolChatClient(params ChatResponse[] responses) : IChatClient
    {
        private readonly Queue<ChatResponse> _responses = new(responses);

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("done");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
            => Task.FromResult(_responses.Dequeue());

        public async IAsyncEnumerable<string> StreamAsync(IEnumerable<Message> messages, [EnumeratorCancellation] CancellationToken ct)
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
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class CountingTool(string name) : NoopTool(name)
    {
        public int ExecuteCalls { get; private set; }

        public override Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
        {
            ExecuteCalls++;
            return Task.FromResult(ToolResult.Ok("{}"));
        }
    }

    private class NoopTool(string name) : ITool
    {
        public string Name { get; } = name;
        public string Description => $"Description of {Name}";
        public Type ParametersType => typeof(EmptyParams);
        public virtual Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct) => Task.FromResult(ToolResult.Ok("{}"));
    }

    private sealed class EmptyParams { }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();
            public void Dispose() { }
        }
    }
}
