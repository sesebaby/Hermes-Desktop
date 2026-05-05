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
    public void BudgetPolicy_LargeRelevantMemoriesSystemBlock_TrimsDynamicRecallToMeetBudget()
    {
        var logger = new CapturingLogger<StardewAutonomyFirstCallContextBudgetPolicy>();
        var policy = new StardewAutonomyFirstCallContextBudgetPolicy(logger);
        var session = CreateAutonomySession();
        var currentUser = "CURRENT AUTONOMY DECISION";
        var messages = new List<Message>
        {
            new() { Role = "system", Content = "CORE SYSTEM: use session_search when more history is needed." },
            new() { Role = "system", Content = "[Relevant Memories]\n" + new string('r', 12000) },
            new() { Role = "assistant", Content = new string('a', 3400) },
            new() { Role = "tool", ToolName = "stardew_status", ToolCallId = "old-status", Content = new string('s', 3400) },
            new() { Role = "user", Content = currentUser }
        };

        var result = policy.Apply(new FirstCallContextBudgetRequest(session, messages, currentUser, 1));
        var output = result.Messages.ToList();
        var completed = CompletedLog(logger);

        Assert.IsTrue(result.BudgetMet, $"Expected dynamic recall to be capped; reason={result.BudgetUnmetReason}.");
        Assert.AreNotEqual("recall_block", result.BudgetUnmetReason);
        Assert.IsTrue(StardewAutonomyFirstCallContextBudgetPolicy.CountCharacters(output) <= 5000);
        Assert.IsTrue(output.Any(message => message.Content.Contains("[trimmed dynamic recall", StringComparison.Ordinal)));
        Assert.IsTrue(ExtractIntField(completed, "dynamicRecallCharsBefore") > ExtractIntField(completed, "dynamicRecallCharsAfter"));
        Assert.IsTrue(ExtractIntField(completed, "dynamicRecallCharsAfter") <= 1000, completed);
        Assert.IsTrue(CountDynamicRecallSurfaceChars(output) <= 1000);
        Assert.IsTrue(ExtractIntField(completed, "systemChars") < 200, completed);
        StringAssert.Contains(completed, "dynamic_recall_trimmed");
    }

    [TestMethod]
    public void BudgetPolicy_LargeMemoryContextInCurrentUser_TrimsRecallButKeepsDecisionText()
    {
        var logger = new CapturingLogger<StardewAutonomyFirstCallContextBudgetPolicy>();
        var policy = new StardewAutonomyFirstCallContextBudgetPolicy(logger);
        var session = CreateAutonomySession();
        const string decision = "DECISION: continue walking to Haley's photo spot.";
        var currentUser = decision + "\n<memory-context>\n" + new string('m', 12000) + "\n</memory-context>";
        var messages = new List<Message>
        {
            new() { Role = "system", Content = "CORE SYSTEM" },
            new() { Role = "assistant", Content = new string('a', 3200) },
            new() { Role = "tool", ToolName = "stardew_status", ToolCallId = "old-status", Content = new string('s', 3200) },
            new() { Role = "user", Content = currentUser }
        };

        var result = policy.Apply(new FirstCallContextBudgetRequest(session, messages, currentUser, 1));
        var outputUser = result.Messages.Last(message => message.Role == "user").Content;

        Assert.IsTrue(result.BudgetMet, $"Expected current-user recall segment to be capped; reason={result.BudgetUnmetReason}.");
        Assert.IsTrue(outputUser.StartsWith(decision, StringComparison.Ordinal));
        Assert.IsTrue(outputUser.Contains("<memory-context>", StringComparison.Ordinal));
        Assert.IsTrue(outputUser.Contains("[trimmed dynamic recall", StringComparison.Ordinal));
        Assert.IsFalse(outputUser.Contains(new string('m', 2000), StringComparison.Ordinal));
        Assert.IsTrue(StardewAutonomyFirstCallContextBudgetPolicy.CountCharacters(result.Messages) <= 5000);
    }

    [TestMethod]
    public void BudgetPolicy_MalformedMemoryContext_TrimsFromOpeningTagAndKeepsDecisionPrefix()
    {
        var logger = new CapturingLogger<StardewAutonomyFirstCallContextBudgetPolicy>();
        var policy = new StardewAutonomyFirstCallContextBudgetPolicy(logger);
        var session = CreateAutonomySession();
        const string decision = "DECISION PREFIX MUST SURVIVE";
        var currentUser = decision + "\n<memory-context>\n" + new string('u', 9000);
        var messages = new List<Message>
        {
            new() { Role = "system", Content = "CORE SYSTEM" },
            new() { Role = "user", Content = currentUser }
        };

        var result = policy.Apply(new FirstCallContextBudgetRequest(session, messages, currentUser, 1));
        var outputUser = result.Messages.Last(message => message.Role == "user").Content;

        Assert.IsTrue(outputUser.StartsWith(decision, StringComparison.Ordinal));
        Assert.IsTrue(outputUser.Contains("[trimmed dynamic recall", StringComparison.Ordinal));
        Assert.IsFalse(outputUser.Contains(new string('u', 2000), StringComparison.Ordinal));
        Assert.AreNotEqual("recall_block", result.BudgetUnmetReason);
    }

    [TestMethod]
    public void BudgetPolicy_DynamicRecallAcrossSystemAndUser_UsesSharedCap()
    {
        var logger = new CapturingLogger<StardewAutonomyFirstCallContextBudgetPolicy>();
        var policy = new StardewAutonomyFirstCallContextBudgetPolicy(logger);
        var session = CreateAutonomySession();
        var currentUser = "DECISION\n<memory-context>\n" + new string('u', 6000) + "\n</memory-context>";
        var messages = new List<Message>
        {
            new() { Role = "system", Content = "CORE SYSTEM" },
            new() { Role = "system", Content = "[Relevant Memories]\n" + new string('s', 6000) },
            new() { Role = "user", Content = currentUser }
        };

        var result = policy.Apply(new FirstCallContextBudgetRequest(session, messages, currentUser, 1));
        var completed = CompletedLog(logger);

        Assert.IsTrue(result.BudgetMet, $"Expected shared dynamic recall cap to meet budget; reason={result.BudgetUnmetReason}.");
        Assert.IsTrue(ExtractIntField(completed, "dynamicRecallCharsAfter") <= 1000, completed);
        Assert.IsTrue(CountDynamicRecallSurfaceChars(result.Messages) <= 1000);
        Assert.IsTrue(result.Messages.Count(message => message.Content.Contains("[trimmed dynamic recall", StringComparison.Ordinal)) >= 1);
    }

    [TestMethod]
    public void BudgetPolicy_RecalledMemorySystemNote_TrimsAsDynamicRecallNotCoreSystem()
    {
        var logger = new CapturingLogger<StardewAutonomyFirstCallContextBudgetPolicy>();
        var policy = new StardewAutonomyFirstCallContextBudgetPolicy(logger);
        var session = CreateAutonomySession();
        var currentUser = "CURRENT";
        var messages = new List<Message>
        {
            new() { Role = "system", Content = "CORE SYSTEM" },
            new()
            {
                Role = "system",
                Content = "[System note: The following is recalled memory context, NOT new user input.]\n" + new string('n', 9000)
            },
            new() { Role = "user", Content = currentUser }
        };

        var result = policy.Apply(new FirstCallContextBudgetRequest(session, messages, currentUser, 1));
        var completed = CompletedLog(logger);

        Assert.IsTrue(result.BudgetMet, $"Expected recalled system note to be capped; reason={result.BudgetUnmetReason}.");
        Assert.IsTrue(CountDynamicRecallSurfaceChars(result.Messages) <= 1000);
        Assert.IsTrue(ExtractIntField(completed, "systemChars") < 50, completed);
        Assert.AreNotEqual("core_system_over_budget", result.BudgetUnmetReason);
    }

    [TestMethod]
    public void BudgetPolicy_LogsCompleteCategoryFieldSet()
    {
        var logger = new CapturingLogger<StardewAutonomyFirstCallContextBudgetPolicy>();
        var policy = new StardewAutonomyFirstCallContextBudgetPolicy(logger);
        var session = CreateAutonomySession();
        var currentUser = "CURRENT";
        var messages = new List<Message>
        {
            new() { Role = "system", Content = "CORE SYSTEM" },
            new() { Role = "system", Content = "MEMORY (your personal notes)\nsmall memory" },
            new() { Role = "system", Content = "[Relevant Memories]\nsmall recall" },
            new() { Role = "system", Content = "[Your active task list was preserved across context compression]\n- finish walk" },
            new()
            {
                Role = "assistant",
                Content = "latest request",
                ToolCalls = [new ToolCall { Id = "latest", Name = "stardew_task_status", Arguments = "{}" }]
            },
            new() { Role = "tool", ToolName = "stardew_task_status", ToolCallId = "latest", Content = "latest tool result" },
            new() { Role = "user", Content = currentUser }
        };

        policy.Apply(new FirstCallContextBudgetRequest(session, messages, currentUser, 1));
        var completed = CompletedLog(logger);

        foreach (var field in new[]
                 {
                     "systemChars", "builtinMemoryChars", "dynamicRecallCharsBefore", "dynamicRecallCharsAfter",
                     "activeTaskChars", "protectedTailChars", "currentUserChars", "charsBefore", "charsAfter",
                     "budgetMet", "budgetUnmetReason"
                 })
        {
            StringAssert.Contains(completed, field + "=");
        }
    }

    [TestMethod]
    public void BudgetPolicy_RecallTrimmed_DoesNotEmitRecallBlockMissReason()
    {
        var logger = new CapturingLogger<StardewAutonomyFirstCallContextBudgetPolicy>();
        var policy = new StardewAutonomyFirstCallContextBudgetPolicy(logger);
        var session = CreateAutonomySession();
        var currentUser = "CURRENT";
        var messages = new List<Message>
        {
            new() { Role = "system", Content = "CORE SYSTEM" },
            new() { Role = "system", Content = "[Relevant Memories]\n" + new string('r', 15000) },
            new() { Role = "user", Content = currentUser }
        };

        var result = policy.Apply(new FirstCallContextBudgetRequest(session, messages, currentUser, 1));

        Assert.AreNotEqual("recall_block", result.BudgetUnmetReason);
        Assert.IsFalse(CompletedLog(logger).Contains("budgetUnmetReason=recall_block", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BudgetPolicy_CoreSystemOverBudget_LogsCoreSystemReason()
    {
        var logger = new CapturingLogger<StardewAutonomyFirstCallContextBudgetPolicy>();
        var policy = new StardewAutonomyFirstCallContextBudgetPolicy(logger);
        var session = CreateAutonomySession();
        var currentUser = "CURRENT";
        var coreSystem = "CORE SYSTEM: session_search is available for on-demand history.\n" + new string('c', 5600);
        var messages = new List<Message>
        {
            new() { Role = "system", Content = coreSystem },
            new() { Role = "user", Content = currentUser }
        };

        var result = policy.Apply(new FirstCallContextBudgetRequest(session, messages, currentUser, 1));

        Assert.IsFalse(result.BudgetMet);
        Assert.AreEqual("core_system_over_budget", result.BudgetUnmetReason);
        StringAssert.Contains(CompletedLog(logger), "budgetUnmetReason=core_system_over_budget");
        Assert.IsTrue(result.Messages.Any(message => message.Content == coreSystem));
    }

    [TestMethod]
    public void BudgetPolicy_BuiltinMemoryOverBudget_LogsBuiltinMemoryReason()
    {
        var logger = new CapturingLogger<StardewAutonomyFirstCallContextBudgetPolicy>();
        var policy = new StardewAutonomyFirstCallContextBudgetPolicy(logger);
        var session = CreateAutonomySession();
        var currentUser = "CURRENT";
        var messages = new List<Message>
        {
            new() { Role = "system", Content = "CORE SYSTEM" },
            new()
            {
                Role = "system",
                Content = "MEMORY (your personal notes)\n" + new string('m', 3400) +
                          "\nUSER PROFILE (who the user is)\n" + new string('u', 1800)
            },
            new() { Role = "user", Content = currentUser }
        };

        var result = policy.Apply(new FirstCallContextBudgetRequest(session, messages, currentUser, 1));

        Assert.IsFalse(result.BudgetMet);
        Assert.AreEqual("builtin_memory_over_budget", result.BudgetUnmetReason);
        StringAssert.Contains(CompletedLog(logger), "builtinMemoryChars=");
    }

    [TestMethod]
    public void BudgetPolicy_ProtectedTailOverBudget_LogsProtectedTailReason()
    {
        var logger = new CapturingLogger<StardewAutonomyFirstCallContextBudgetPolicy>();
        var policy = new StardewAutonomyFirstCallContextBudgetPolicy(logger);
        var session = CreateAutonomySession();
        var currentUser = "CURRENT";
        var messages = new List<Message>
        {
            new() { Role = "system", Content = "CORE SYSTEM" },
            new()
            {
                Role = "assistant",
                Content = new string('a', 2600),
                ToolCalls = [new ToolCall { Id = "latest", Name = "stardew_move", Arguments = new string('x', 2600) }]
            },
            new() { Role = "tool", ToolName = "stardew_move", ToolCallId = "latest", Content = new string('t', 2600) },
            new() { Role = "user", Content = currentUser }
        };

        var result = policy.Apply(new FirstCallContextBudgetRequest(session, messages, currentUser, 1));

        Assert.IsFalse(result.BudgetMet);
        Assert.AreEqual("protected_tail_over_budget", result.BudgetUnmetReason);
        StringAssert.Contains(CompletedLog(logger), "protectedTailChars=");
    }

    [TestMethod]
    public void BudgetPolicy_ActiveTaskOverBudget_LogsActiveTaskReason()
    {
        var logger = new CapturingLogger<StardewAutonomyFirstCallContextBudgetPolicy>();
        var policy = new StardewAutonomyFirstCallContextBudgetPolicy(logger);
        var session = CreateAutonomySession();
        var currentUser = "CURRENT";
        var messages = new List<Message>
        {
            new() { Role = "system", Content = "CORE SYSTEM" },
            new()
            {
                Role = "system",
                Content = "[Your active task list was preserved across context compression]\n" + new string('t', 5400)
            },
            new() { Role = "user", Content = currentUser }
        };

        var result = policy.Apply(new FirstCallContextBudgetRequest(session, messages, currentUser, 1));

        Assert.IsFalse(result.BudgetMet);
        Assert.AreEqual("active_task_context_over_budget", result.BudgetUnmetReason);
        StringAssert.Contains(CompletedLog(logger), "activeTaskChars=");
    }

    [TestMethod]
    public void BudgetPolicy_UserPromptWithActiveTodo_RemainsCurrentUser()
    {
        var logger = new CapturingLogger<StardewAutonomyFirstCallContextBudgetPolicy>();
        var policy = new StardewAutonomyFirstCallContextBudgetPolicy(logger);
        var session = CreateAutonomySession();
        var currentUser = """
                          one turn purpose: continue Haley follow-up
                          Active todo:
                          - greet Haley
                          - continue task context if still valid
                          """;
        var messages = new List<Message>
        {
            new() { Role = "system", Content = "CORE SYSTEM" },
            new() { Role = "user", Content = currentUser }
        };

        policy.Apply(new FirstCallContextBudgetRequest(session, messages, currentUser, 1));
        var completed = CompletedLog(logger);

        Assert.AreEqual(0, ExtractIntField(completed, "activeTaskChars"), completed);
        Assert.IsTrue(ExtractIntField(completed, "currentUserChars") >= currentUser.Length, completed);
    }

    [TestMethod]
    public void BudgetPolicy_SystemHeaderBlock_RemainsActiveTaskContext()
    {
        var logger = new CapturingLogger<StardewAutonomyFirstCallContextBudgetPolicy>();
        var policy = new StardewAutonomyFirstCallContextBudgetPolicy(logger);
        var session = CreateAutonomySession();
        const string currentUser = "CURRENT";
        const string activeTaskHeader = "[Your active task list was preserved across context compression]";
        var messages = new List<Message>
        {
            new() { Role = "system", Content = "CORE SYSTEM" },
            new() { Role = "system", Content = activeTaskHeader + "\n- finish Haley follow-up" },
            new() { Role = "user", Content = currentUser }
        };

        policy.Apply(new FirstCallContextBudgetRequest(session, messages, currentUser, 1));
        var completed = CompletedLog(logger);

        Assert.IsTrue(ExtractIntField(completed, "activeTaskChars") > 0, completed);
        Assert.IsTrue(ExtractIntField(completed, "currentUserChars") > 0, completed);
    }

    [TestMethod]
    public void BudgetPolicy_UserRoleWithActiveTaskHeader_IsNotClassifiedAsActiveTaskContext()
    {
        var logger = new CapturingLogger<StardewAutonomyFirstCallContextBudgetPolicy>();
        var policy = new StardewAutonomyFirstCallContextBudgetPolicy(logger);
        var session = CreateAutonomySession();
        var currentUser = """
                          [Your active task list was preserved across context compression]
                          This is still the user prompt, not preserved system task context.
                          """;
        var messages = new List<Message>
        {
            new() { Role = "system", Content = "CORE SYSTEM" },
            new() { Role = "user", Content = currentUser }
        };

        policy.Apply(new FirstCallContextBudgetRequest(session, messages, currentUser, 1));
        var completed = CompletedLog(logger);

        Assert.AreEqual(0, ExtractIntField(completed, "activeTaskChars"), completed);
        Assert.IsTrue(ExtractIntField(completed, "currentUserChars") >= currentUser.Length, completed);
    }

    [TestMethod]
    public void BudgetPolicy_ReproductionShape_OnlySystemHeaderCountsAsActiveTaskContext()
    {
        var logger = new CapturingLogger<StardewAutonomyFirstCallContextBudgetPolicy>();
        var policy = new StardewAutonomyFirstCallContextBudgetPolicy(logger);
        var session = CreateAutonomySession();
        var currentUser = """
                          active todo
                          - walk to town
                          - check whether task context is still relevant
                          - do not restart broad scans
                          """ + new string('u', 1800);
        const string activeTaskHeader = "[Your active task list was preserved across context compression]";
        var activeTaskBlock = activeTaskHeader + "\n- preserved move follow-up";
        var messages = new List<Message>
        {
            new() { Role = "system", Content = "CORE SYSTEM" },
            new() { Role = "system", Content = activeTaskBlock },
            new() { Role = "user", Content = currentUser }
        };

        policy.Apply(new FirstCallContextBudgetRequest(session, messages, currentUser, 1));
        var completed = CompletedLog(logger);

        Assert.AreEqual(activeTaskBlock.Length + "system".Length, ExtractIntField(completed, "activeTaskChars"));
        Assert.IsTrue(ExtractIntField(completed, "currentUserChars") >= currentUser.Length, completed);
    }

    [TestMethod]
    public void BudgetPolicy_MixedOverflow_UsesDocumentedReasonPriority()
    {
        var logger = new CapturingLogger<StardewAutonomyFirstCallContextBudgetPolicy>();
        var policy = new StardewAutonomyFirstCallContextBudgetPolicy(logger);
        var session = CreateAutonomySession();
        var currentUser = "CURRENT";
        var messages = new List<Message>
        {
            new() { Role = "system", Content = "CORE SYSTEM\n" + new string('c', 5200) },
            new() { Role = "system", Content = "USER PROFILE (who the user is)\n" + new string('u', 1200) },
            new()
            {
                Role = "assistant",
                Content = new string('a', 1600),
                ToolCalls = [new ToolCall { Id = "latest", Name = "stardew_move", Arguments = new string('x', 1600) }]
            },
            new() { Role = "tool", ToolName = "stardew_move", ToolCallId = "latest", Content = new string('t', 1600) },
            new() { Role = "user", Content = currentUser }
        };

        var result = policy.Apply(new FirstCallContextBudgetRequest(session, messages, currentUser, 1));

        Assert.AreEqual("core_system_over_budget", result.BudgetUnmetReason);
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
            File.ReadAllText(Path.Combine(root, "skills", "gaming", "stardew-core", "SKILL.md")),
            File.ReadAllText(Path.Combine(root, "skills", "gaming", "stardew-social", "SKILL.md")),
            File.ReadAllText(Path.Combine(root, "skills", "gaming", "stardew-navigation", "SKILL.md")));

        StringAssert.Contains(combined, "本轮目标");
        StringAssert.Contains(combined, "session_search");
        StringAssert.Contains(combined, "memory");
        StringAssert.Contains(combined, "避免重复广泛状态扫描");
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

    private static string CompletedLog(CapturingLogger<StardewAutonomyFirstCallContextBudgetPolicy> logger)
        => logger.Messages.LastOrDefault(message => message.Contains("autonomy_context_budget_completed", StringComparison.Ordinal))
           ?? throw new AssertFailedException("Missing autonomy_context_budget_completed log.");

    private static int ExtractIntField(string log, string fieldName)
    {
        var marker = fieldName + "=";
        var start = log.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            throw new AssertFailedException($"Missing field {fieldName} in log: {log}");

        start += marker.Length;
        var end = log.IndexOf(';', start);
        if (end < 0)
            end = log.Length;

        var value = log[start..end].Trim();
        return int.TryParse(value, out var parsed)
            ? parsed
            : throw new AssertFailedException($"Field {fieldName} was not an int in log: {log}");
    }

    private static int CountDynamicRecallSurfaceChars(IEnumerable<Message> messages)
        => messages.Sum(message =>
        {
            if (!message.Content.Contains("[trimmed dynamic recall", StringComparison.Ordinal))
                return 0;

            var memoryContextLength = CountMemoryContextSurfaceChars(message.Content);
            if (memoryContextLength > 0)
                return memoryContextLength;

            return string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase)
                ? message.Content.Length
                : 0;
        });

    private static int CountMemoryContextSurfaceChars(string content)
    {
        const string openTag = "<memory-context>";
        const string closeTag = "</memory-context>";
        var total = 0;
        var cursor = 0;

        while (cursor < content.Length)
        {
            var open = content.IndexOf(openTag, cursor, StringComparison.OrdinalIgnoreCase);
            if (open < 0)
                break;

            var close = content.IndexOf(closeTag, open + openTag.Length, StringComparison.OrdinalIgnoreCase);
            if (close < 0)
            {
                total += content.Length - open;
                break;
            }

            var end = close + closeTag.Length;
            total += end - open;
            cursor = end;
        }

        return total;
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
