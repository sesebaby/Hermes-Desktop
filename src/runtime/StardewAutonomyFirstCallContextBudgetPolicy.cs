using Hermes.Agent.Core;
using Microsoft.Extensions.Logging;

namespace Hermes.Agent.Runtime;

public sealed class StardewAutonomyFirstCallContextBudgetPolicy : IFirstCallContextBudgetPolicy
{
    public const int DefaultBudgetChars = 5000;

    private readonly ILogger<StardewAutonomyFirstCallContextBudgetPolicy> _logger;
    private readonly int _budgetChars;

    public StardewAutonomyFirstCallContextBudgetPolicy(
        ILogger<StardewAutonomyFirstCallContextBudgetPolicy> logger,
        int budgetChars = DefaultBudgetChars)
    {
        _logger = logger;
        _budgetChars = budgetChars;
    }

    public FirstCallContextBudgetResult Apply(FirstCallContextBudgetRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!StardewAutonomySessionKeys.IsAutonomyTurnSession(request.Session))
            return new FirstCallContextBudgetResult(request.Messages, Applied: false, BudgetMet: true);

        var messagesBefore = request.Messages.Count;
        var charsBefore = CountCharacters(request.Messages);
        var toolResultCharsBefore = request.Messages
            .Where(message => IsRole(message, "tool"))
            .Sum(message => message.Content.Length);
        var recentTurnCharsBefore = request.Messages.TakeLast(Math.Min(6, request.Messages.Count)).Sum(CountCharacters);
        var traceId = GetStateString(request.Session, "traceId");
        var npcId = GetStateString(request.Session, "npcId");

        _logger.LogInformation(
            "autonomy_context_budget_started; sessionId={SessionId}; traceId={TraceId}; npcId={NpcId}; messagesBefore={MessagesBefore}; charsBefore={CharsBefore}; toolResultCharsBefore={ToolResultCharsBefore}; recentTurnCharsBefore={RecentTurnCharsBefore}; budgetChars={BudgetChars}",
            request.Session.Id,
            traceId,
            npcId,
            messagesBefore,
            charsBefore,
            toolResultCharsBefore,
            recentTurnCharsBefore,
            _budgetChars);

        var decisions = BuildProtectionDecisions(request.Messages, request.CurrentUserMessage);
        var output = request.Messages.Select((message, index) => new MessageSlot(message, index, decisions[index])).ToList();
        var prunedToolResults = 0;
        var prunedDuplicateStatusResults = 0;
        var truncatedToolCallArgs = 0;
        var replacedWithPlaceholders = 0;

        for (var i = output.Count - 2; i >= 0 && CountCharacters(output.Select(slot => slot.Message)) > _budgetChars; i--)
        {
            var slot = output[i];
            if (slot.Decision.Protected)
                continue;

            if (IsRole(slot.Message, "tool"))
            {
                var removed = slot.Message.Content.Length;
                output[i] = slot with
                {
                    Message = CopyMessage(
                        slot.Message,
                        $"[trimmed old tool result: {slot.Message.ToolName ?? "unknown"}, {removed} chars removed]")
                };
                prunedToolResults++;
                replacedWithPlaceholders++;
                continue;
            }

            if (slot.Message.ToolCalls is { Count: > 0 })
            {
                output[i] = slot with
                {
                    Message = CopyMessage(
                        slot.Message,
                        string.IsNullOrWhiteSpace(slot.Message.Content)
                            ? "[trimmed old assistant tool request]"
                            : Truncate(slot.Message.Content, 240),
                        TruncateToolCalls(slot.Message.ToolCalls))
                };
                truncatedToolCallArgs += slot.Message.ToolCalls.Count;
                continue;
            }

            output.RemoveAt(i);
        }

        for (var i = output.Count - 2; i >= 0 && CountCharacters(output.Select(slot => slot.Message)) > _budgetChars; i--)
        {
            var slot = output[i];
            if (slot.Decision.Protected)
                continue;

            if (IsRole(slot.Message, "tool") && IsBroadStatusTool(slot.Message.ToolName))
            {
                output.RemoveAt(i);
                prunedDuplicateStatusResults++;
                continue;
            }

            if (!IsRole(slot.Message, "system") && !IsRole(slot.Message, "user"))
                output.RemoveAt(i);
        }

        var finalMessages = output.Select(slot => slot.Message).ToList();
        var charsAfter = CountCharacters(finalMessages);
        var budgetMet = charsAfter <= _budgetChars;
        var unmetReason = budgetMet
            ? "none"
            : DetermineBudgetUnmetReason(output, _budgetChars);

        _logger.LogInformation(
            "autonomy_context_budget_completed; sessionId={SessionId}; traceId={TraceId}; npcId={NpcId}; messagesAfter={MessagesAfter}; charsAfter={CharsAfter}; charsSaved={CharsSaved}; budgetMet={BudgetMet}; budgetUnmetReason={BudgetUnmetReason}; protectedTailMessages={ProtectedTailMessages}; prunedToolResults={PrunedToolResults}; prunedDuplicateStatusResults={PrunedDuplicateStatusResults}; truncatedToolCallArgs={TruncatedToolCallArgs}; replacedWithPlaceholders={ReplacedWithPlaceholders}",
            request.Session.Id,
            traceId,
            npcId,
            finalMessages.Count,
            charsAfter,
            Math.Max(0, charsBefore - charsAfter),
            budgetMet,
            unmetReason,
            output.Count(slot => slot.Decision.IsLatestContinuationGroup),
            prunedToolResults,
            prunedDuplicateStatusResults,
            truncatedToolCallArgs,
            replacedWithPlaceholders);

        return new FirstCallContextBudgetResult(finalMessages, Applied: true, BudgetMet: budgetMet, BudgetUnmetReason: unmetReason);
    }

    public static int CountCharacters(IEnumerable<Message> messages)
        => messages.Sum(CountCharacters);

    private static int CountCharacters(Message message)
    {
        var count = message.Role.Length + message.Content.Length;
        if (!string.IsNullOrWhiteSpace(message.ToolCallId))
            count += message.ToolCallId.Length;
        if (!string.IsNullOrWhiteSpace(message.ToolName))
            count += message.ToolName.Length;
        if (!string.IsNullOrWhiteSpace(message.TaskSessionId))
            count += message.TaskSessionId.Length;
        if (message.ToolCalls is not null)
            count += message.ToolCalls.Sum(call => call.Id.Length + call.Name.Length + call.Arguments.Length);
        return count;
    }

    private static ProtectionDecision[] BuildProtectionDecisions(IReadOnlyList<Message> messages, string currentUserMessage)
    {
        var decisions = new ProtectionDecision[messages.Count];
        for (var i = 0; i < decisions.Length; i++)
            decisions[i] = new ProtectionDecision();

        var latestAssistantToolIndex = -1;
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            if (IsRole(messages[i], "assistant") && messages[i].ToolCalls is { Count: > 0 })
            {
                latestAssistantToolIndex = i;
                break;
            }
        }

        var latestToolCallIds = latestAssistantToolIndex >= 0
            ? messages[latestAssistantToolIndex].ToolCalls!
                .Select(call => call.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            if (LooksLikeRecallOrMemoryBlock(message))
                decisions[i] = decisions[i] with { Protected = true, Reason = "recall_block" };
            else if (LooksLikeActiveTaskContext(message))
                decisions[i] = decisions[i] with { Protected = true, Reason = "active_task_context" };
            else if (IsRole(message, "system"))
                decisions[i] = decisions[i] with { Protected = true, Reason = "system" };
            else if (IsRole(message, "user") && (i == messages.Count - 1 || message.Content == currentUserMessage))
                decisions[i] = decisions[i] with { Protected = true, Reason = "current_user" };
        }

        if (latestAssistantToolIndex >= 0)
        {
            decisions[latestAssistantToolIndex] = decisions[latestAssistantToolIndex] with
            {
                Protected = true,
                Reason = "protected_tail",
                IsLatestContinuationGroup = true
            };

            for (var i = latestAssistantToolIndex + 1; i < messages.Count; i++)
            {
                if (IsRole(messages[i], "tool") &&
                    messages[i].ToolCallId is { } toolCallId &&
                    latestToolCallIds.Contains(toolCallId))
                {
                    decisions[i] = decisions[i] with
                    {
                        Protected = true,
                        Reason = "protected_tail",
                        IsLatestContinuationGroup = true
                    };
                }
            }
        }

        return decisions;
    }

    private static string DetermineBudgetUnmetReason(IReadOnlyList<MessageSlot> output, int budgetChars)
    {
        var protectedSlots = output.Where(slot => slot.Decision.Protected).ToList();
        if (protectedSlots.Sum(slot => CountCharacters(slot.Message)) > budgetChars)
        {
            if (protectedSlots.Any(slot => slot.Decision.Reason == "recall_block"))
                return "recall_block";
            if (protectedSlots.Any(slot => slot.Decision.Reason == "active_task_context"))
                return "active_task_context";
            if (protectedSlots.Any(slot => slot.Decision.Reason == "protected_tail"))
                return "protected_tail";
            return "protected_content_over_budget";
        }

        return "unknown";
    }

    private static bool LooksLikeRecallOrMemoryBlock(Message message)
        => IsRole(message, "system") &&
           (message.Content.Contains("Relevant Memories", StringComparison.OrdinalIgnoreCase) ||
            message.Content.Contains("USER PROFILE", StringComparison.OrdinalIgnoreCase) ||
            message.Content.Contains("session_search", StringComparison.OrdinalIgnoreCase) ||
            message.Content.Contains("recall", StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeActiveTaskContext(Message message)
        => message.Content.Contains("active todo", StringComparison.OrdinalIgnoreCase) ||
           message.Content.Contains("active task", StringComparison.OrdinalIgnoreCase) ||
           message.Content.Contains("Task Context", StringComparison.OrdinalIgnoreCase);

    private static Message CopyMessage(Message message, string content, List<ToolCall>? toolCalls = null)
        => new()
        {
            Role = message.Role,
            Content = content,
            Timestamp = message.Timestamp,
            ToolCallId = message.ToolCallId,
            ToolName = message.ToolName,
            TaskSessionId = message.TaskSessionId,
            ToolCalls = toolCalls ?? message.ToolCalls,
            Reasoning = message.Reasoning,
            ReasoningContent = message.ReasoningContent,
            ReasoningDetails = message.ReasoningDetails,
            CodexReasoningItems = message.CodexReasoningItems
        };

    private static List<ToolCall> TruncateToolCalls(IEnumerable<ToolCall> toolCalls)
        => toolCalls.Select(call => new ToolCall
        {
            Id = call.Id,
            Name = call.Name,
            Arguments = call.Arguments.Length <= 180
                ? call.Arguments
                : call.Arguments[..180] + "...[trimmed]"
        }).ToList();

    private static string Truncate(string value, int maxChars)
        => value.Length <= maxChars ? value : value[..maxChars] + "...[trimmed]";

    private static bool IsRole(Message message, string role)
        => string.Equals(message.Role, role, StringComparison.OrdinalIgnoreCase);

    private static bool IsBroadStatusTool(string? toolName)
        => toolName is "stardew_status" or
           "stardew_player_status" or
           "stardew_progress_status" or
           "stardew_social_status" or
           "stardew_quest_status" or
           "stardew_farm_status" or
           "stardew_recent_activity";

    private static string GetStateString(Session session, string key)
        => session.State.TryGetValue(key, out var value) && value is not null
            ? value.ToString() ?? "-"
            : "-";

    private sealed record MessageSlot(Message Message, int OriginalIndex, ProtectionDecision Decision);

    private sealed record ProtectionDecision(
        bool Protected = false,
        string Reason = "",
        bool IsLatestContinuationGroup = false);
}
