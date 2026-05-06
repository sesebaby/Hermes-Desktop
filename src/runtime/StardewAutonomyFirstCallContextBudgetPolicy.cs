using Hermes.Agent.Core;
using Microsoft.Extensions.Logging;

namespace Hermes.Agent.Runtime;

public sealed class StardewAutonomyFirstCallContextBudgetPolicy : IFirstCallContextBudgetPolicy, IOutboundContextCompactionPolicy
{
    public const int DefaultBudgetChars = 5000;
    private const int DynamicRecallCapChars = 1000;
    private const string DynamicRecallTrimDiagnostic = "dynamic_recall_trimmed";
    private const string MemoryContextOpenTag = "<memory-context>";
    private const string MemoryContextCloseTag = "</memory-context>";
    private const string ActiveTaskContextHeader = "[Your active task list was preserved across context compression]";

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
        var promptTokensEstimatedBefore = EstimateTokensFromChars(charsBefore);
        var estimatedDeepSeekInputRmbBefore = EstimateDeepSeekFlashInputRmb(promptTokensEstimatedBefore);
        var toolResultCharsBefore = request.Messages
            .Where(message => IsRole(message, "tool"))
            .Sum(message => message.Content.Length);
        var recentTurnCharsBefore = request.Messages.TakeLast(Math.Min(6, request.Messages.Count)).Sum(CountCharacters);
        var dynamicRecallTrim = ApplyDynamicRecallCap(request.Messages);
        var traceId = GetStateString(request.Session, "traceId");
        var npcId = GetStateString(request.Session, "npcId");

        _logger.LogInformation(
            "autonomy_context_budget_started; sessionId={SessionId}; traceId={TraceId}; npcId={NpcId}; messagesBefore={MessagesBefore}; charsBefore={CharsBefore}; toolResultCharsBefore={ToolResultCharsBefore}; recentTurnCharsBefore={RecentTurnCharsBefore}; dynamicRecallCharsBefore={DynamicRecallCharsBefore}; budgetChars={BudgetChars}",
            request.Session.Id,
            traceId,
            npcId,
            messagesBefore,
            charsBefore,
            toolResultCharsBefore,
            recentTurnCharsBefore,
            dynamicRecallTrim.DynamicRecallCharsBefore,
            _budgetChars);

        var decisions = BuildProtectionDecisions(
            dynamicRecallTrim.Messages,
            request.CurrentUserMessage,
            dynamicRecallTrim.DynamicRecallMessageIndexes);
        var output = dynamicRecallTrim.Messages.Select((message, index) => new MessageSlot(message, index, decisions[index])).ToList();
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
                        toolCalls: null)
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

        output = SanitizeToolPairs(output);

        var finalMessages = output.Select(slot => slot.Message).ToList();
        var charsAfter = CountCharacters(finalMessages);
        var promptTokensEstimatedAfter = EstimateTokensFromChars(charsAfter);
        var estimatedDeepSeekInputRmbAfter = EstimateDeepSeekFlashInputRmb(promptTokensEstimatedAfter);
        var budgetMet = charsAfter <= _budgetChars;
        var categoryCounts = CountCategories(output, dynamicRecallTrim.DynamicRecallCharsBefore);
        var unmetReason = budgetMet
            ? "none"
            : DetermineBudgetUnmetReason(categoryCounts, _budgetChars);
        var trimDiagnostics = BuildTrimDiagnostics(
            dynamicRecallTrim.Trimmed,
            prunedToolResults,
            prunedDuplicateStatusResults,
            truncatedToolCallArgs);

        _logger.LogInformation(
            "autonomy_context_budget_completed; sessionId={SessionId}; traceId={TraceId}; npcId={NpcId}; messagesAfter={MessagesAfter}; charsBefore={CharsBefore}; charsAfter={CharsAfter}; charsSaved={CharsSaved}; promptTokensEstimatedBefore={PromptTokensEstimatedBefore}; promptTokensEstimatedAfter={PromptTokensEstimatedAfter}; usageSource={UsageSource}; estimatedDeepSeekInputRmbBefore={EstimatedDeepSeekInputRmbBefore}; estimatedDeepSeekInputRmbAfter={EstimatedDeepSeekInputRmbAfter}; budgetMet={BudgetMet}; budgetUnmetReason={BudgetUnmetReason}; systemChars={SystemChars}; builtinMemoryChars={BuiltinMemoryChars}; dynamicRecallCharsBefore={DynamicRecallCharsBefore}; dynamicRecallCharsAfter={DynamicRecallCharsAfter}; activeTaskChars={ActiveTaskChars}; protectedTailChars={ProtectedTailChars}; currentUserChars={CurrentUserChars}; trimDiagnostics={TrimDiagnostics}; protectedTailMessages={ProtectedTailMessages}; prunedToolResults={PrunedToolResults}; prunedDuplicateStatusResults={PrunedDuplicateStatusResults}; truncatedToolCallArgs={TruncatedToolCallArgs}; replacedWithPlaceholders={ReplacedWithPlaceholders}",
            request.Session.Id,
            traceId,
            npcId,
            finalMessages.Count,
            charsBefore,
            charsAfter,
            Math.Max(0, charsBefore - charsAfter),
            promptTokensEstimatedBefore,
            promptTokensEstimatedAfter,
            "estimated",
            estimatedDeepSeekInputRmbBefore,
            estimatedDeepSeekInputRmbAfter,
            budgetMet,
            unmetReason,
            categoryCounts.SystemChars,
            categoryCounts.BuiltinMemoryChars,
            categoryCounts.DynamicRecallCharsBefore,
            categoryCounts.DynamicRecallCharsAfter,
            categoryCounts.ActiveTaskChars,
            categoryCounts.ProtectedTailChars,
            categoryCounts.CurrentUserChars,
            trimDiagnostics,
            output.Count(slot => slot.Decision.IsLatestContinuationGroup),
            prunedToolResults,
            prunedDuplicateStatusResults,
            truncatedToolCallArgs,
            replacedWithPlaceholders);

        return new FirstCallContextBudgetResult(finalMessages, Applied: true, BudgetMet: budgetMet, BudgetUnmetReason: unmetReason);
    }

    public ContextCompactionResult Apply(ContextCompactionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = Apply(new FirstCallContextBudgetRequest(
            request.Session,
            request.Messages,
            request.CurrentUserMessage,
            request.Iteration));
        return new ContextCompactionResult(result.Messages, result.Applied, result.BudgetMet, result.BudgetUnmetReason);
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

    private static int EstimateTokensFromChars(int chars)
        => Math.Max(1, (int)Math.Ceiling(chars / 4.0));

    private static decimal EstimateDeepSeekFlashInputRmb(int inputTokens)
        => Math.Round(inputTokens / 1_000_000m, 8);

    private static ProtectionDecision[] BuildProtectionDecisions(
        IReadOnlyList<Message> messages,
        string currentUserMessage,
        IReadOnlySet<int> dynamicRecallMessageIndexes)
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
            if (LooksLikeActiveTaskContext(message))
                decisions[i] = decisions[i] with { Protected = true, Reason = "active_task_context" };
            else if (LooksLikeBuiltinMemoryBlock(message))
                decisions[i] = decisions[i] with { Protected = true, Reason = "builtin_memory" };
            else if (IsRole(message, "user") && (i == messages.Count - 1 || message.Content == currentUserMessage))
                decisions[i] = decisions[i] with { Protected = true, Reason = "current_user" };
            else if (dynamicRecallMessageIndexes.Contains(i))
                decisions[i] = decisions[i] with { Protected = false, Reason = "dynamic_recall" };
            else if (IsRole(message, "system"))
                decisions[i] = decisions[i] with { Protected = true, Reason = "system" };
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

    private static List<MessageSlot> SanitizeToolPairs(IReadOnlyList<MessageSlot> slots)
    {
        var survivingToolCallIds = slots
            .Where(slot => IsRole(slot.Message, "assistant") && slot.Message.ToolCalls is { Count: > 0 })
            .SelectMany(slot => slot.Message.ToolCalls!)
            .Select(call => call.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        var withoutOrphans = slots
            .Where(slot =>
                !IsRole(slot.Message, "tool") ||
                (slot.Message.ToolCallId is { } toolCallId && survivingToolCallIds.Contains(toolCallId)))
            .ToList();
        var survivingToolResultIds = withoutOrphans
            .Where(slot => IsRole(slot.Message, "tool") && !string.IsNullOrWhiteSpace(slot.Message.ToolCallId))
            .Select(slot => slot.Message.ToolCallId!)
            .ToHashSet(StringComparer.Ordinal);
        var output = new List<MessageSlot>(withoutOrphans.Count);

        foreach (var slot in withoutOrphans)
        {
            output.Add(slot);
            if (!IsRole(slot.Message, "assistant") || slot.Message.ToolCalls is not { Count: > 0 })
                continue;

            foreach (var toolCall in slot.Message.ToolCalls)
            {
                if (string.IsNullOrWhiteSpace(toolCall.Id) || survivingToolResultIds.Contains(toolCall.Id))
                    continue;

                output.Add(slot with
                {
                    Message = new Message
                    {
                        Role = "tool",
                        ToolCallId = toolCall.Id,
                        ToolName = toolCall.Name,
                        Content = $"[missing tool result after context compression: {toolCall.Name}]"
                    }
                });
                survivingToolResultIds.Add(toolCall.Id);
            }
        }

        return output;
    }

    private static CategoryCounts CountCategories(
        IReadOnlyList<MessageSlot> output,
        int dynamicRecallCharsBefore)
    {
        var counts = new CategoryCounts(
            SystemChars: 0,
            BuiltinMemoryChars: 0,
            DynamicRecallCharsBefore: dynamicRecallCharsBefore,
            DynamicRecallCharsAfter: 0,
            ActiveTaskChars: 0,
            ProtectedTailChars: 0,
            CurrentUserChars: 0);

        foreach (var slot in output)
        {
            var chars = CountCharacters(slot.Message);
            counts = slot.Decision.Reason switch
            {
                "system" => counts with { SystemChars = counts.SystemChars + chars },
                "builtin_memory" => counts with { BuiltinMemoryChars = counts.BuiltinMemoryChars + chars },
                "active_task_context" => counts with { ActiveTaskChars = counts.ActiveTaskChars + chars },
                "protected_tail" => counts with { ProtectedTailChars = counts.ProtectedTailChars + chars },
                "current_user" => counts with
                {
                    CurrentUserChars = counts.CurrentUserChars + chars,
                    DynamicRecallCharsAfter = counts.DynamicRecallCharsAfter + CountMemoryContextSurfaceChars(slot.Message.Content)
                },
                "dynamic_recall" => counts with
                {
                    DynamicRecallCharsAfter = counts.DynamicRecallCharsAfter + slot.Message.Content.Length
                },
                _ => counts
            };
        }

        return counts;
    }

    private static string DetermineBudgetUnmetReason(CategoryCounts counts, int budgetChars)
    {
        if (counts.SystemChars > budgetChars)
            return "core_system_over_budget";
        if (counts.BuiltinMemoryChars > 0 && counts.SystemChars + counts.BuiltinMemoryChars > budgetChars)
            return "builtin_memory_over_budget";
        if (counts.SystemChars + counts.BuiltinMemoryChars + counts.ProtectedTailChars > budgetChars)
            return "protected_tail_over_budget";
        if (counts.SystemChars + counts.BuiltinMemoryChars + counts.ProtectedTailChars + counts.ActiveTaskChars > budgetChars)
            return "active_task_context_over_budget";

        var protectedChars = counts.SystemChars +
                             counts.BuiltinMemoryChars +
                             counts.ProtectedTailChars +
                             counts.ActiveTaskChars +
                             counts.CurrentUserChars;
        return protectedChars > budgetChars
            ? "protected_content_over_budget"
            : "unknown";
    }

    private static DynamicRecallTrimResult ApplyDynamicRecallCap(IReadOnlyList<Message> messages)
    {
        var output = messages.ToArray();
        var remaining = DynamicRecallCapChars;
        var before = 0;
        var trimmed = false;
        var dynamicRecallMessageIndexes = new HashSet<int>();

        for (var i = output.Length - 1; i >= 0; i--)
        {
            var result = TrimDynamicRecall(output[i], remaining);
            output[i] = result.Message;
            if (result.DynamicRecallCharsBefore > 0)
                dynamicRecallMessageIndexes.Add(i);
            before += result.DynamicRecallCharsBefore;
            trimmed |= result.Trimmed;
            remaining = Math.Max(0, remaining - result.DynamicRecallCharsAfter);
        }

        return new DynamicRecallTrimResult(output, dynamicRecallMessageIndexes, before, trimmed);
    }

    private static MessageDynamicRecallTrimResult TrimDynamicRecall(Message message, int remainingBudget)
    {
        if (ContainsMemoryContextOpenTag(message.Content))
        {
            var memoryContextResult = TrimMemoryContextBlocks(message.Content, remainingBudget);
            return new MessageDynamicRecallTrimResult(
                memoryContextResult.Trimmed
                    ? CopyMessage(message, memoryContextResult.Content)
                    : message,
                memoryContextResult.DynamicRecallCharsBefore,
                memoryContextResult.DynamicRecallCharsAfter,
                memoryContextResult.Trimmed);
        }

        if (LooksLikeDynamicRecallSystemBlock(message))
        {
            var recallResult = TrimRecallText(message.Content, remainingBudget);
            return new MessageDynamicRecallTrimResult(
                recallResult.Trimmed
                    ? CopyMessage(message, recallResult.Content)
                    : message,
                recallResult.DynamicRecallCharsBefore,
                recallResult.DynamicRecallCharsAfter,
                recallResult.Trimmed);
        }

        return new MessageDynamicRecallTrimResult(message, 0, 0, Trimmed: false);
    }

    private static RecallTextTrimResult TrimMemoryContextBlocks(string content, int remainingBudget)
    {
        var cursor = 0;
        var output = new System.Text.StringBuilder(content.Length);
        var before = 0;
        var after = 0;
        var trimmed = false;
        var budget = remainingBudget;

        while (cursor < content.Length)
        {
            var openIndex = content.IndexOf(MemoryContextOpenTag, cursor, StringComparison.OrdinalIgnoreCase);
            if (openIndex < 0)
            {
                output.Append(content, cursor, content.Length - cursor);
                break;
            }

            output.Append(content, cursor, openIndex - cursor);

            var innerStart = openIndex + MemoryContextOpenTag.Length;
            var closeIndex = content.IndexOf(MemoryContextCloseTag, innerStart, StringComparison.OrdinalIgnoreCase);
            var innerEnd = closeIndex >= 0 ? closeIndex : content.Length;
            var inner = content[innerStart..innerEnd];
            var tagChars = MemoryContextOpenTag.Length + (closeIndex >= 0 ? MemoryContextCloseTag.Length : 0);
            var surfaceBefore = tagChars + inner.Length;

            if (budget <= tagChars)
            {
                before += surfaceBefore;
                trimmed = true;
                budget = 0;

                if (closeIndex < 0)
                    break;

                cursor = closeIndex + MemoryContextCloseTag.Length;
                continue;
            }

            var recallResult = TrimRecallText(inner, budget - tagChars);

            output.Append(content, openIndex, MemoryContextOpenTag.Length);
            output.Append(recallResult.Content);
            before += surfaceBefore;
            after += tagChars + recallResult.DynamicRecallCharsAfter;
            trimmed |= recallResult.Trimmed;
            budget = Math.Max(0, budget - tagChars - recallResult.DynamicRecallCharsAfter);

            if (closeIndex < 0)
                break;

            output.Append(content, closeIndex, MemoryContextCloseTag.Length);
            cursor = closeIndex + MemoryContextCloseTag.Length;
        }

        return new RecallTextTrimResult(
            trimmed ? output.ToString() : content,
            before,
            after,
            trimmed);
    }

    private static RecallTextTrimResult TrimRecallText(string content, int remainingBudget)
    {
        var before = content.Length;
        if (before <= remainingBudget)
            return new RecallTextTrimResult(content, before, before, Trimmed: false);

        var keptChars = Math.Min(Math.Max(remainingBudget, 0), before);
        while (keptChars > 0)
        {
            var candidateMarker = BuildDynamicRecallTrimMarker(keptChars, before);
            var candidateLength = candidateMarker.Length + 1 + keptChars;
            if (candidateLength <= remainingBudget)
            {
                var trimmed = candidateMarker + "\n" + content[..keptChars];
                return new RecallTextTrimResult(trimmed, before, trimmed.Length, Trimmed: true);
            }

            keptChars -= candidateLength - remainingBudget;
        }

        var marker = BuildDynamicRecallTrimMarker(0, before);
        return marker.Length <= remainingBudget
            ? new RecallTextTrimResult(marker, before, marker.Length, Trimmed: true)
            : new RecallTextTrimResult("", before, 0, Trimmed: true);
    }

    private static string BuildDynamicRecallTrimMarker(int keptChars, int originalChars)
        => $"[trimmed dynamic recall: kept {keptChars} of {originalChars} chars; use session_search for more]";

    private static string BuildTrimDiagnostics(
        bool dynamicRecallTrimmed,
        int prunedToolResults,
        int prunedDuplicateStatusResults,
        int truncatedToolCallArgs)
    {
        var diagnostics = new List<string>();
        if (dynamicRecallTrimmed)
            diagnostics.Add(DynamicRecallTrimDiagnostic);
        if (prunedToolResults > 0)
            diagnostics.Add("old_tool_results_trimmed");
        if (prunedDuplicateStatusResults > 0)
            diagnostics.Add("old_status_results_deduped");
        if (truncatedToolCallArgs > 0)
            diagnostics.Add("assistant_tool_args_trimmed");

        return diagnostics.Count == 0
            ? "none"
            : string.Join(",", diagnostics);
    }

    private static bool LooksLikeBuiltinMemoryBlock(Message message)
        => IsRole(message, "system") &&
           (message.Content.Contains("MEMORY (your personal notes)", StringComparison.Ordinal) ||
            message.Content.Contains("USER PROFILE (who the user is)", StringComparison.Ordinal));

    private static bool LooksLikeDynamicRecallSystemBlock(Message message)
    {
        if (!IsRole(message, "system"))
            return false;

        var content = message.Content.TrimStart();
        return content.StartsWith("[Relevant Memories]", StringComparison.OrdinalIgnoreCase) ||
               content.StartsWith("Relevant Memories", StringComparison.OrdinalIgnoreCase) ||
               content.StartsWith("[System note: The following is recalled memory context", StringComparison.OrdinalIgnoreCase) ||
               content.StartsWith("The following is recalled memory context", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsMemoryContextOpenTag(string content)
        => content.Contains(MemoryContextOpenTag, StringComparison.OrdinalIgnoreCase);

    private static int CountMemoryContextSurfaceChars(string content)
    {
        var total = 0;
        var cursor = 0;

        while (cursor < content.Length)
        {
            var open = content.IndexOf(MemoryContextOpenTag, cursor, StringComparison.OrdinalIgnoreCase);
            if (open < 0)
                break;

            var close = content.IndexOf(MemoryContextCloseTag, open + MemoryContextOpenTag.Length, StringComparison.OrdinalIgnoreCase);
            if (close < 0)
            {
                total += content.Length - open;
                break;
            }

            var end = close + MemoryContextCloseTag.Length;
            total += end - open;
            cursor = end;
        }

        return total;
    }

    private static bool LooksLikeActiveTaskContext(Message message)
        => IsRole(message, "system") &&
           message.Content.StartsWith(ActiveTaskContextHeader, StringComparison.Ordinal);

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

    private sealed record DynamicRecallTrimResult(
        IReadOnlyList<Message> Messages,
        IReadOnlySet<int> DynamicRecallMessageIndexes,
        int DynamicRecallCharsBefore,
        bool Trimmed);

    private sealed record MessageDynamicRecallTrimResult(
        Message Message,
        int DynamicRecallCharsBefore,
        int DynamicRecallCharsAfter,
        bool Trimmed);

    private sealed record RecallTextTrimResult(
        string Content,
        int DynamicRecallCharsBefore,
        int DynamicRecallCharsAfter,
        bool Trimmed);

    private sealed record CategoryCounts(
        int SystemChars,
        int BuiltinMemoryChars,
        int DynamicRecallCharsBefore,
        int DynamicRecallCharsAfter,
        int ActiveTaskChars,
        int ProtectedTailChars,
        int CurrentUserChars);
}
