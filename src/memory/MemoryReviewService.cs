namespace Hermes.Agent.Memory;

using System.Text.Json;
using Hermes.Agent.Core;
using Hermes.Agent.LLM;
using Hermes.Agent.Plugins;
using Hermes.Agent.Skills;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging;

public static class MemoryReviewDefaults
{
    public const int NudgeInterval = 5;
    public const int SkillCreationNudgeInterval = 5;
}

public sealed record BackgroundReviewNotification(
    string SessionId,
    bool ReviewMemory,
    bool ReviewSkills,
    bool Success,
    bool HasActions,
    string Summary);

/// <summary>
/// Periodic post-response memory review.
/// Mirrors Python's background memory nudge: after every configured N user
/// turns, a detached review pass inspects the conversation and may call the
/// same built-in memory tool. User responses are never delayed by this pass.
/// </summary>
public sealed class MemoryReviewService
{
    private const string MemoryReviewPrompt =
        "Review the conversation above and consider saving to memory if appropriate.\n\n" +
        "Focus on:\n" +
        "1. Has the user revealed things about themselves -- their persona, desires, preferences, or personal details worth remembering?\n" +
        "2. Has the user expressed expectations about how you should behave, their work style, or ways they want you to operate?\n\n" +
        "If something stands out, save it using the memory tool. If nothing is worth saving, just say 'Nothing to save.' and stop.";

    private const string SkillReviewPrompt =
        "Review the conversation above and consider whether a skill should be saved or updated.\n\n" +
        "Work in this order -- do not skip steps:\n\n" +
        "1. SURVEY the existing skill landscape first. Call skills_list to see what you have. " +
        "If anything looks potentially relevant, skill_view it before deciding. You are looking for the CLASS of task that just happened, not the exact task. " +
        "Example: a successful Tauri build is in the class \"desktop app build troubleshooting\", not \"fix my specific Tauri error today\".\n\n" +
        "2. THINK CLASS-FIRST. What general pattern of task did the user just complete? What conditions will trigger this pattern again? " +
        "Describe the class in one sentence before looking at what to save.\n\n" +
        "3. PREFER GENERALIZING AN EXISTING SKILL over creating a new one. If a skill already covers the class -- even partially -- " +
        "update it (skill_manage patch) with the new insight. Broaden its \"when to use\" trigger if needed.\n\n" +
        "4. ONLY CREATE A NEW SKILL when no existing skill reasonably covers the class. When you create one, name and scope it at the class level " +
        "(\"react-i18n-setup\", not \"add-i18n-to-my-dashboard-app\"). The trigger section must describe the class of situations, not this one session.\n\n" +
        "5. If you notice two existing skills that overlap, note it in your response so a future review can consolidate them. " +
        "Do not consolidate now unless the overlap is obvious and low-risk.\n\n" +
        "Only act when something is genuinely worth saving. If nothing stands out, just say 'Nothing to save.' and stop.";

    private const string CombinedReviewPrompt =
        "Review the conversation above and consider two things:\n\n" +
        "**Memory**: Has the user revealed things about themselves -- their persona, desires, preferences, or personal details? " +
        "Has the user expressed expectations about how you should behave, their work style, or ways they want you to operate? " +
        "If so, save using the memory tool.\n\n" +
        "**Skills**: Was a non-trivial approach used to complete a task that required trial and error, changing course due to experiential findings, " +
        "or a different method or outcome than the user expected? If so, work in this order:\n" +
        "  a. SURVEY existing skills first (skills_list, then skill_view on candidates).\n" +
        "  b. Identify the CLASS of task, not the specific task (\"desktop app build troubleshooting\", not \"fix my Tauri error\").\n" +
        "  c. PREFER UPDATING/GENERALIZING an existing skill that covers the class.\n" +
        "  d. ONLY CREATE A NEW SKILL if no existing one covers the class. Scope at the class level, not this one session.\n" +
        "  e. If you notice overlapping skills during the survey, note it so a future review can consolidate them.\n\n" +
        "Only act if there's something genuinely worth saving. If nothing stands out, just say 'Nothing to save.' and stop.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IChatClient _chatClient;
    private readonly MemoryManager _memoryManager;
    private readonly PluginManager? _pluginManager;
    private readonly SkillManager? _skillManager;
    private readonly ILogger<MemoryReviewService> _logger;
    private readonly int _nudgeInterval;
    private readonly int _skillNudgeInterval;
    private int _turnsSinceReview;
    private int _toolIterationsSinceSkillReview;

    public MemoryReviewService(
        IChatClient chatClient,
        MemoryManager memoryManager,
        ILogger<MemoryReviewService> logger,
        PluginManager? pluginManager = null,
        int nudgeInterval = MemoryReviewDefaults.NudgeInterval,
        SkillManager? skillManager = null,
        int skillNudgeInterval = MemoryReviewDefaults.SkillCreationNudgeInterval)
    {
        _chatClient = chatClient;
        _memoryManager = memoryManager;
        _logger = logger;
        _pluginManager = pluginManager;
        _nudgeInterval = nudgeInterval;
        _skillManager = skillManager;
        _skillNudgeInterval = skillNudgeInterval;
    }

    public event Action<BackgroundReviewNotification>? BackgroundReviewCompleted;

    public bool QueueAfterTurn(
        string sessionId,
        IReadOnlyList<Message> messagesSnapshot,
        string finalResponse,
        bool interrupted,
        int toolIterations = 0,
        bool skillManageUsed = false)
    {
        var due = EvaluateReviewDue(finalResponse, interrupted, toolIterations, skillManageUsed);
        if (!due.ReviewMemory && !due.ReviewSkills)
            return false;

        _logger.LogInformation(
            "Queued background review for session {SessionId} (memory={ReviewMemory}, skills={ReviewSkills})",
            sessionId,
            due.ReviewMemory,
            due.ReviewSkills);

        var snapshot = messagesSnapshot.Select(CloneMessage).ToList();
        _ = Task.Run(async () =>
        {
            try
            {
                var results = await ReviewConversationAsync(snapshot, due.ReviewMemory, due.ReviewSkills, CancellationToken.None);
                var summary = BuildReviewSummary(due.ReviewMemory, due.ReviewSkills, results, out var hasActions);
                _logger.LogInformation(
                    "Background review completed for session {SessionId} (memory={ReviewMemory}, skills={ReviewSkills}, actions={HasActions}): {Summary}",
                    sessionId,
                    due.ReviewMemory,
                    due.ReviewSkills,
                    hasActions,
                    summary);
                BackgroundReviewCompleted?.Invoke(new BackgroundReviewNotification(
                    sessionId,
                    due.ReviewMemory,
                    due.ReviewSkills,
                    Success: true,
                    HasActions: hasActions,
                    Summary: summary));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Background memory review failed non-fatally for session {SessionId}", sessionId);
                BackgroundReviewCompleted?.Invoke(new BackgroundReviewNotification(
                    sessionId,
                    due.ReviewMemory,
                    due.ReviewSkills,
                    Success: false,
                    HasActions: false,
                    Summary: $"Background review failed: {ex.Message}"));
            }
        });

        return true;
    }

    public bool MarkTurnAndCheckDue(string finalResponse, bool interrupted)
        => EvaluateReviewDue(finalResponse, interrupted, toolIterations: 0, skillManageUsed: false).ReviewMemory;

    public bool MarkTurnAndCheckDue(
        string finalResponse,
        bool interrupted,
        int toolIterations,
        bool skillManageUsed)
    {
        var due = EvaluateReviewDue(finalResponse, interrupted, toolIterations, skillManageUsed);
        return due.ReviewMemory || due.ReviewSkills;
    }

    private ReviewDue EvaluateReviewDue(
        string finalResponse,
        bool interrupted,
        int toolIterations,
        bool skillManageUsed)
    {
        if (interrupted || string.IsNullOrWhiteSpace(finalResponse))
            return new ReviewDue(false, false);

        var reviewMemory = false;
        if (_nudgeInterval > 0)
        {
            var turns = Interlocked.Increment(ref _turnsSinceReview);
            if (turns >= _nudgeInterval)
            {
                Interlocked.Exchange(ref _turnsSinceReview, 0);
                reviewMemory = true;
            }
        }

        var reviewSkills = false;
        if (_skillManager is not null && _skillNudgeInterval > 0)
        {
            if (skillManageUsed)
            {
                Interlocked.Exchange(ref _toolIterationsSinceSkillReview, 0);
            }
            else if (toolIterations > 0)
            {
                var iterations = Interlocked.Add(ref _toolIterationsSinceSkillReview, toolIterations);
                if (iterations >= _skillNudgeInterval)
                {
                    Interlocked.Exchange(ref _toolIterationsSinceSkillReview, 0);
                    reviewSkills = true;
                }
            }
        }

        return new ReviewDue(reviewMemory, reviewSkills);
    }

    public async Task<IReadOnlyList<ToolResult>> ReviewConversationAsync(
        IReadOnlyList<Message> messagesSnapshot,
        CancellationToken ct)
        => await ReviewConversationAsync(messagesSnapshot, reviewMemory: true, reviewSkills: false, maxIterations: 1, ct);

    public async Task<IReadOnlyList<ToolResult>> ReviewConversationAsync(
        IReadOnlyList<Message> messagesSnapshot,
        bool reviewMemory,
        bool reviewSkills,
        CancellationToken ct)
        => await ReviewConversationAsync(messagesSnapshot, reviewMemory, reviewSkills, maxIterations: 8, ct);

    private async Task<IReadOnlyList<ToolResult>> ReviewConversationAsync(
        IReadOnlyList<Message> messagesSnapshot,
        bool reviewMemory,
        bool reviewSkills,
        int maxIterations,
        CancellationToken ct)
    {
        if (!reviewMemory && !reviewSkills)
            return Array.Empty<ToolResult>();

        var reviewMessages = new List<Message>
        {
            new()
            {
                Role = "system",
                Content = BuildReviewSystemPrompt(reviewMemory, reviewSkills)
            }
        };
        reviewMessages.AddRange(messagesSnapshot.Select(CloneMessage));
        reviewMessages.Add(new Message { Role = "user", Content = SelectReviewPrompt(reviewMemory, reviewSkills) });

        var tools = BuildReviewToolDefinitions(reviewMemory, reviewSkills).ToList();

        var results = new List<ToolResult>();
        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var response = await _chatClient.CompleteWithToolsAsync(reviewMessages, tools, ct);
            if (!response.HasToolCalls)
                break;

            var toolCalls = response.ToolCalls ?? new List<ToolCall>();
            reviewMessages.Add(new Message
            {
                Role = "assistant",
                Content = response.Content ?? "",
                ToolCalls = toolCalls.Select(CloneToolCall).ToList(),
                Reasoning = response.Reasoning,
                ReasoningContent = response.ReasoningContent,
                ReasoningDetails = response.ReasoningDetails,
                CodexReasoningItems = response.CodexReasoningItems
            });

            foreach (var toolCall in toolCalls)
            {
                try
                {
                    var result = await ExecuteReviewToolCallAsync(toolCall, reviewMemory, reviewSkills, ct);
                    results.Add(result);
                    reviewMessages.Add(new Message
                    {
                        Role = "tool",
                        Content = result.Content,
                        ToolCallId = toolCall.Id,
                        ToolName = toolCall.Name
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Background memory/skill review tool call failed");
                }
            }
        }

        return results;
    }

    private async Task<ToolResult> ExecuteReviewToolCallAsync(
        ToolCall toolCall,
        bool reviewMemory,
        bool reviewSkills,
        CancellationToken ct)
    {
        if (reviewMemory && string.Equals(toolCall.Name, "memory", StringComparison.OrdinalIgnoreCase))
        {
            var parameters = JsonSerializer.Deserialize<MemoryToolParameters>(toolCall.Arguments, JsonOptions);
            if (parameters is null)
                return ToolResult.Fail("Invalid memory tool arguments.");

            return await new MemoryTool(_memoryManager, _pluginManager).ExecuteAsync(parameters, ct);
        }

        if (reviewSkills && _skillManager is not null)
        {
            if (string.Equals(toolCall.Name, "skills_list", StringComparison.OrdinalIgnoreCase))
            {
                var parameters = JsonSerializer.Deserialize<SkillsListParameters>(toolCall.Arguments, JsonOptions)
                    ?? new SkillsListParameters();
                return await new SkillsListTool(_skillManager).ExecuteAsync(parameters, ct);
            }

            if (string.Equals(toolCall.Name, "skill_view", StringComparison.OrdinalIgnoreCase))
            {
                var parameters = JsonSerializer.Deserialize<SkillViewParameters>(toolCall.Arguments, JsonOptions);
                if (parameters is null)
                    return ToolResult.Fail("Invalid skill_view tool arguments.");

                return await new SkillViewTool(_skillManager).ExecuteAsync(parameters, ct);
            }

            if (string.Equals(toolCall.Name, "skill_manage", StringComparison.OrdinalIgnoreCase))
            {
                var parameters = JsonSerializer.Deserialize<SkillManageParameters>(toolCall.Arguments, JsonOptions);
                if (parameters is null)
                    return ToolResult.Fail("Invalid skill_manage tool arguments.");

                return await new SkillManageTool(_skillManager).ExecuteAsync(parameters, ct);
            }
        }

        return ToolResult.Fail($"Unknown review tool: {toolCall.Name}");
    }

    private static ToolDefinition BuildMemoryToolDefinition()
        => new()
        {
            Name = "memory",
            Description = MemoryReferenceText.MemoryToolDescription,
            Parameters = MemoryReferenceText.BuildMemoryToolParameterSchema()
        };

    private IEnumerable<ToolDefinition> BuildReviewToolDefinitions(bool reviewMemory, bool reviewSkills)
    {
        if (reviewMemory)
            yield return BuildMemoryToolDefinition();

        if (reviewSkills && _skillManager is not null)
        {
            yield return BuildToolDefinition(new SkillsListTool(_skillManager));
            yield return BuildToolDefinition(new SkillViewTool(_skillManager));
            yield return BuildToolDefinition(new SkillManageTool(_skillManager));
        }
    }

    private static ToolDefinition BuildToolDefinition(ITool tool)
        => new()
        {
            Name = tool.Name,
            Description = tool.Description,
            Parameters = ((IToolSchemaProvider)tool).GetParameterSchema()
        };

    private static string BuildReviewSystemPrompt(bool reviewMemory, bool reviewSkills)
    {
        var parts = new List<string>();
        if (reviewMemory)
            parts.Add(MemoryReferenceText.MemoryGuidance);
        if (reviewSkills)
            parts.Add(MemoryReferenceText.SkillsGuidance);
        return string.Join("\n\n", parts);
    }

    private static string SelectReviewPrompt(bool reviewMemory, bool reviewSkills)
        => (reviewMemory, reviewSkills) switch
        {
            (true, true) => CombinedReviewPrompt,
            (true, false) => MemoryReviewPrompt,
            (false, true) => SkillReviewPrompt,
            _ => "Nothing to save."
        };

    private static string BuildReviewSummary(
        bool reviewMemory,
        bool reviewSkills,
        IEnumerable<ToolResult> results,
        out bool hasActions)
    {
        var actions = SummarizeReviewActions(results);
        if (!string.IsNullOrWhiteSpace(actions))
        {
            hasActions = true;
            return actions;
        }

        hasActions = false;
        return $"Checked {DescribeReviewScope(reviewMemory, reviewSkills)}; nothing to save.";
    }

    private static string DescribeReviewScope(bool reviewMemory, bool reviewSkills)
        => (reviewMemory, reviewSkills) switch
        {
            (true, true) => "memory and skills",
            (true, false) => "memory",
            (false, true) => "skills",
            _ => "review state"
        };

    private static string? SummarizeReviewActions(IEnumerable<ToolResult> results)
    {
        var actions = new List<string>();
        foreach (var result in results.Where(result => result.Success))
        {
            try
            {
                using var doc = JsonDocument.Parse(result.Content);
                var root = doc.RootElement;
                var message = root.TryGetProperty("message", out var messageElement)
                    ? messageElement.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(message))
                    continue;

                var target = root.TryGetProperty("target", out var targetElement)
                    ? targetElement.GetString()
                    : null;
                var lower = message.ToLowerInvariant();
                if (lower.Contains("created", StringComparison.Ordinal) ||
                    lower.Contains("updated", StringComparison.Ordinal) ||
                    lower.Contains("deleted", StringComparison.Ordinal) ||
                    lower.Contains("patched", StringComparison.Ordinal) ||
                    lower.Contains("written", StringComparison.Ordinal))
                {
                    actions.Add(message);
                }
                else if (lower.Contains("added", StringComparison.Ordinal) ||
                         lower.Contains("replaced", StringComparison.Ordinal) ||
                         lower.Contains("removed", StringComparison.Ordinal))
                {
                    actions.Add(target == "user" ? "User profile updated" : "Memory updated");
                }
            }
            catch (JsonException)
            {
                // Ignore non-JSON tool output in the optional review summary.
            }
        }

        return actions.Count == 0 ? null : string.Join(" · ", actions.Distinct(StringComparer.Ordinal));
    }

    private static Message CloneMessage(Message message)
        => new()
        {
            Role = message.Role,
            Content = message.Content,
            Timestamp = message.Timestamp,
            ToolCallId = message.ToolCallId,
            ToolName = message.ToolName,
            ToolCalls = message.ToolCalls?.Select(CloneToolCall).ToList(),
            Reasoning = message.Reasoning,
            ReasoningContent = message.ReasoningContent,
            ReasoningDetails = message.ReasoningDetails,
            CodexReasoningItems = message.CodexReasoningItems
        };

    private static ToolCall CloneToolCall(ToolCall toolCall)
        => new()
        {
            Id = toolCall.Id,
            Name = toolCall.Name,
            Arguments = toolCall.Arguments
        };

    private sealed record ReviewDue(bool ReviewMemory, bool ReviewSkills);
}
