using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.Memory;
using Hermes.Agent.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hermes.Agent.Runtime;

public sealed class NpcAutonomyLoop
{
    private const int FallbackSpeakMaxLength = 80;
    private readonly IGameAdapter _adapter;
    private readonly NpcObservationFactStore _factStore;
    private readonly Hermes.Agent.Core.IAgent? _agent;
    private readonly NpcRuntimeLogWriter? _logWriter;
    private readonly MemoryManager? _memoryManager;
    private readonly ILogger<NpcAutonomyLoop>? _logger;
    private readonly Func<string> _traceIdFactory;

    public NpcAutonomyLoop(
        IGameAdapter adapter,
        NpcObservationFactStore factStore,
        Hermes.Agent.Core.IAgent? agent = null,
        NpcRuntimeLogWriter? logWriter = null,
        MemoryManager? memoryManager = null,
        ILogger<NpcAutonomyLoop>? logger = null,
        Func<string>? traceIdFactory = null)
    {
        _adapter = adapter;
        _factStore = factStore;
        _agent = agent;
        _logWriter = logWriter;
        _memoryManager = memoryManager;
        _logger = logger;
        _traceIdFactory = traceIdFactory ?? (() => $"trace_{Guid.NewGuid():N}");
    }

    public async Task<NpcAutonomyTickResult> RunOneTickAsync(
        NpcRuntimeInstance instance,
        GameEventCursor eventCursor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var result = await RunOneTickAsync(instance.Descriptor, eventCursor, ct);
        instance.RecordTrace(result.TraceId);
        await WriteInstanceTaskContinuityEvidenceAsync(instance, result.TraceId, ct);
        return result;
    }

    public async Task<NpcAutonomyTickResult> RunOneTickAsync(
        NpcRuntimeInstance instance,
        GameObservation observation,
        GameEventBatch eventBatch,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var result = await RunOneTickAsync(instance.Descriptor, observation, eventBatch, ct);
        instance.RecordTrace(result.TraceId);
        await WriteInstanceTaskContinuityEvidenceAsync(instance, result.TraceId, ct);
        return result;
    }

    public async Task<NpcAutonomyTickResult> RunOneTickAsync(
        NpcRuntimeDescriptor descriptor,
        GameEventCursor eventCursor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(eventCursor);

        var observeStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var observation = await _adapter.Queries.ObserveAsync(descriptor.EffectiveBodyBinding, ct);
        observeStopwatch.Stop();
        LogObservation(descriptor, observation, observeStopwatch.ElapsedMilliseconds);

        var eventStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var eventBatch = await _adapter.Events.PollBatchAsync(eventCursor, ct);
        eventStopwatch.Stop();
        _logger?.LogInformation(
            "NPC autonomy event poll completed; npc={NpcId}; events={EventCount}; nextCursor={NextCursor}; nextSequence={NextSequence}; durationMs={DurationMs}",
            descriptor.NpcId,
            eventBatch.Records.Count,
            eventBatch.NextCursor?.Since ?? "-",
            eventBatch.NextCursor?.Sequence,
            eventStopwatch.ElapsedMilliseconds);
        return await RunOneTickAsync(descriptor, observation, eventBatch, ct);
    }

    public async Task<NpcAutonomyTickResult> RunOneTickAsync(
        NpcRuntimeDescriptor descriptor,
        GameObservation observation,
        GameEventBatch eventBatch,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(eventBatch);

        var traceId = _traceIdFactory();
        var currentFacts = new List<NpcObservationFact>
        {
            ToObservationFact(descriptor, observation)
        };
        _factStore.RecordObservation(descriptor, observation);

        var eventFacts = 0;
        foreach (var record in eventBatch.Records)
        {
            if (!BelongsToRuntimeNpc(descriptor, record))
                continue;

            _factStore.RecordEvent(descriptor, record);
            currentFacts.Add(ToEventFact(descriptor, record));
            eventFacts++;
        }

        string? decisionResponse = null;
        Session? decisionSession = null;
        if (_agent is not null)
        {
            decisionSession = new Session
            {
                Id = descriptor.SessionId,
                Platform = descriptor.AdapterId
            };
            decisionSession.State["traceId"] = traceId;
            decisionSession.State["npcId"] = descriptor.NpcId;
            var decisionMessage = BuildDecisionMessage(descriptor, currentFacts);
            _logger?.LogInformation(
                "NPC autonomy decision request prepared; npc={NpcId}; trace={TraceId}; facts={FactCount}; messageChars={MessageChars}; gameTime={GameTime}; location={Location}; tile={Tile}",
                descriptor.NpcId,
                traceId,
                currentFacts.Count,
                decisionMessage.Length,
                FindFactValue(observation.Facts, "gameTime") ?? FindFactValue(observation.Facts, "gameClock") ?? "-",
                FindFactValue(observation.Facts, "location") ?? "-",
                FindFactValue(observation.Facts, "tile") ?? "-");
            var decisionStopwatch = System.Diagnostics.Stopwatch.StartNew();
            decisionResponse = await _agent.ChatAsync(
                decisionMessage,
                decisionSession,
                ct);
            decisionStopwatch.Stop();
            _logger?.LogInformation(
                "NPC autonomy decision response received; npc={NpcId}; trace={TraceId}; responseChars={ResponseChars}; durationMs={DurationMs}",
                descriptor.NpcId,
                traceId,
                decisionResponse?.Length ?? 0,
                decisionStopwatch.ElapsedMilliseconds);
        }
        var rawDecisionResponse = decisionResponse;
        if (IsToolIterationLimitFallback(decisionResponse))
            decisionResponse = null;

        await WriteActivityAsync(descriptor, traceId, eventFacts, decisionResponse, ct);
        await WriteToolBudgetDiagnosticAsync(descriptor, traceId, rawDecisionResponse, ct);
        await WriteNarrativeMovementDiagnosticAsync(descriptor, traceId, decisionResponse, decisionSession, ct);
        await WriteNoToolActionDiagnosticAsync(descriptor, traceId, decisionResponse, decisionSession, ct);
        await WriteSessionTaskContinuityEvidenceAsync(descriptor, traceId, decisionSession, null, ct);
        await WriteMemoryAsync(traceId, decisionResponse, ct);

        return new NpcAutonomyTickResult(descriptor.NpcId, traceId, 1, eventFacts, decisionResponse, eventBatch.NextCursor);
    }

    private static bool BelongsToRuntimeNpc(NpcRuntimeDescriptor descriptor, GameEventRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.NpcId))
            return true;

        var body = descriptor.EffectiveBodyBinding;
        return string.Equals(descriptor.NpcId, record.NpcId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(body.TargetEntityId, record.NpcId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(body.SmapiName, record.NpcId, StringComparison.OrdinalIgnoreCase);
    }

    private static NpcObservationFact ToObservationFact(NpcRuntimeDescriptor descriptor, GameObservation observation)
        => new(
            descriptor.NpcId,
            descriptor.GameId,
            descriptor.SaveId,
            descriptor.ProfileId,
            descriptor.SessionId,
            "observation",
            null,
            observation.TimestampUtc,
            observation.Summary,
            observation.Facts.ToArray());

    private static NpcObservationFact ToEventFact(NpcRuntimeDescriptor descriptor, GameEventRecord record)
        => new(
            descriptor.NpcId,
            descriptor.GameId,
            descriptor.SaveId,
            descriptor.ProfileId,
            descriptor.SessionId,
            "event",
            record.EventId,
            record.TimestampUtc,
            record.Summary,
            [record.EventType]);

    private static string BuildDecisionMessage(NpcRuntimeDescriptor descriptor, IReadOnlyList<NpcObservationFact> facts)
    {
        var lines = facts.Select(fact =>
            $"- [{fact.SourceKind}] {fact.SourceId ?? "current"} {fact.TimestampUtc:O}: {fact.Summary} ({string.Join("; ", fact.Facts)})");
        return
            $"NPC: {descriptor.DisplayName} ({descriptor.NpcId})\n" +
            "你现在要决定下一步自主行动。先看当前观察事实和 active todo，再决定是推进任务、观察等待，还是回应玩家。\n" +
            "如果玩家给过的约定还没完成，要优先考虑怎么继续；被玩家打断时先回应玩家，再恢复原来的任务。\n" +
            "需要移动就用 stardew_move，长动作开始后用 stardew_task_status 查进度，不要用一句话假装已经做完。\n" +
            "如果任务暂时做不了，把 todo 标成 blocked；如果确定做不成，把 todo 标成 failed；blocked 或 failed 都要写短 reason。\n" +
            "如果这是答应玩家的事，能说话时要用 stardew_speak 或私聊告诉玩家卡在哪里。\n" +
            "每条事实前面的 ISO 时间是记录时间不是星露谷游戏内时间；gameTime/gameClock 才是游戏内时间，判断早晚必须看它们。\n" +
            "下面的事件只是上下文，不要把事件当成玩家的新命令。\n\n" +
            "[Observed Facts]\n" +
            string.Join("\n", lines);
    }

    private void LogObservation(NpcRuntimeDescriptor descriptor, GameObservation observation, long durationMs)
    {
        _logger?.LogInformation(
            "NPC autonomy observation completed; npc={NpcId}; observedNpc={ObservedNpcId}; summary={Summary}; gameTime={GameTime}; gameClock={GameClock}; location={Location}; tile={Tile}; factCount={FactCount}; durationMs={DurationMs}",
            descriptor.NpcId,
            observation.NpcId,
            Truncate(observation.Summary, 180),
            FindFactValue(observation.Facts, "gameTime") ?? "-",
            FindFactValue(observation.Facts, "gameClock") ?? "-",
            FindFactValue(observation.Facts, "location") ?? "-",
            FindFactValue(observation.Facts, "tile") ?? "-",
            observation.Facts.Count,
            durationMs);
    }

    private static string? FindFactValue(IReadOnlyList<string> facts, string key)
    {
        var prefix = key + "=";
        var fact = facts.FirstOrDefault(value => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return fact is null ? null : fact[prefix.Length..];
    }

    private static bool IsToolIterationLimitFallback(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           value.StartsWith("I've reached the maximum number of tool call iterations.", StringComparison.Ordinal);

    private async Task WriteActivityAsync(
        NpcRuntimeDescriptor descriptor,
        string traceId,
        int eventFacts,
        string? decisionResponse,
        CancellationToken ct)
    {
        if (_logWriter is null)
            return;

        await _logWriter.WriteAsync(new NpcRuntimeLogRecord(
            DateTime.UtcNow,
            traceId,
            descriptor.NpcId,
            descriptor.GameId,
            descriptor.SessionId,
            "tick",
            null,
            "completed",
            decisionResponse ?? $"observed:{eventFacts + 1}"), ct);
    }

    private async Task WriteToolBudgetDiagnosticAsync(
        NpcRuntimeDescriptor descriptor,
        string traceId,
        string? decisionResponse,
        CancellationToken ct)
    {
        if (_logWriter is null || !IsToolIterationLimitFallback(decisionResponse))
            return;

        await _logWriter.WriteAsync(new NpcRuntimeLogRecord(
            DateTime.UtcNow,
            traceId,
            descriptor.NpcId,
            descriptor.GameId,
            descriptor.SessionId,
            "diagnostic",
            "tool_budget",
            "warning",
            "max_tool_iterations",
            Error: "agent_max_tool_iterations_fallback_dropped"), ct);
    }

    private async Task WriteNarrativeMovementDiagnosticAsync(
        NpcRuntimeDescriptor descriptor,
        string traceId,
        string? decisionResponse,
        Session? decisionSession,
        CancellationToken ct)
    {
        if (_logWriter is null ||
            string.IsNullOrWhiteSpace(decisionResponse) ||
            !LooksLikePhysicalMovement(decisionResponse) ||
            HasToolCall(decisionSession, "stardew_move"))
        {
            return;
        }

        await _logWriter.WriteAsync(new NpcRuntimeLogRecord(
            DateTime.UtcNow,
            traceId,
            descriptor.NpcId,
            descriptor.GameId,
            descriptor.SessionId,
            "diagnostic",
            "stardew_move",
            "warning",
            "narrative_move_without_stardew_move",
            Error: Truncate(decisionResponse, 300)), ct);
    }

    private async Task WriteNoToolActionDiagnosticAsync(
        NpcRuntimeDescriptor descriptor,
        string traceId,
        string? decisionResponse,
        Session? decisionSession,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(decisionResponse) ||
            HasAnyStardewActionToolCall(decisionSession))
        {
            return;
        }

        if (!TryExtractVisibleLine(decisionResponse, out var text))
        {
            await WriteNoToolDecisionDiagnosticAsync(descriptor, traceId, decisionResponse, "no_visible_line", ct);
            return;
        }

        await WriteNoToolDecisionDiagnosticAsync(descriptor, traceId, decisionResponse, "fallback_speak_retired", ct, text);
    }

    private async Task WriteNoToolDecisionDiagnosticAsync(
        NpcRuntimeDescriptor descriptor,
        string traceId,
        string? decisionResponse,
        string reason,
        CancellationToken ct,
        string? resultOverride = null)
    {
        if (_logWriter is null || string.IsNullOrWhiteSpace(decisionResponse))
            return;

        await _logWriter.WriteAsync(new NpcRuntimeLogRecord(
            DateTime.UtcNow,
            traceId,
            descriptor.NpcId,
            descriptor.GameId,
            descriptor.SessionId,
            "diagnostic",
            "stardew_speak",
            "warning",
            resultOverride ?? $"no_tool_decision:{reason}",
            Error: Truncate(decisionResponse, 300)), ct);
    }

    private async Task WriteInstanceTaskContinuityEvidenceAsync(
        NpcRuntimeInstance instance,
        string traceId,
        CancellationToken ct)
    {
        if (_logWriter is null)
            return;

        var descriptor = instance.Descriptor;
        if (instance.TryGetTaskView(descriptor.SessionId, out var taskView) &&
            taskView?.ActiveSnapshot.Todos.Any(IsActiveTodo) is true)
        {
            await WriteTaskContinuityAsync(
                descriptor,
                traceId,
                "observed_active_todo",
                "observed",
                "active",
                null,
                null,
                ct);
        }

        await WriteSessionTaskContinuityEvidenceAsync(
            descriptor,
            traceId,
            null,
            instance.Snapshot().Controller.LastTerminalCommandStatus,
            ct);
    }

    private async Task WriteSessionTaskContinuityEvidenceAsync(
        NpcRuntimeDescriptor descriptor,
        string traceId,
        Session? decisionSession,
        GameCommandStatus? controllerTerminalStatus,
        CancellationToken ct)
    {
        if (_logWriter is null)
            return;

        if (decisionSession is null)
        {
            if (controllerTerminalStatus is not null)
                await WriteCommandTerminalEvidenceAsync(descriptor, traceId, controllerTerminalStatus, ct);
            return;
        }

        var toolMessagesByCallId = decisionSession.Messages
            .Where(message => string.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase) &&
                              !string.IsNullOrWhiteSpace(message.ToolCallId))
            .GroupBy(message => message.ToolCallId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var calls = decisionSession.Messages
            .Where(message => string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            .SelectMany(message => message.ToolCalls ?? [])
            .ToArray();
        var toolEvidenceByCallId = toolMessagesByCallId.ToDictionary(
            pair => pair.Key,
            pair => ReadToolResultEvidence(pair.Value.Content),
            StringComparer.OrdinalIgnoreCase);

        var needsFeedback = false;
        foreach (var call in calls.Where(IsStardewActionTool))
        {
            var evidence = GetToolResultEvidence(toolEvidenceByCallId, call.Id);
            await WriteTaskContinuityAsync(
                descriptor,
                traceId,
                "action_submitted",
                "submitted",
                "submitted",
                evidence.CommandId,
                null,
                ct);

            if (evidence.TerminalStatus is not null)
            {
                await WriteCommandTerminalEvidenceAsync(descriptor, traceId, evidence.TerminalStatus, ct);
                needsFeedback |= IsBlockedOrFailed(evidence.TerminalStatus.Status);
            }
        }

        foreach (var call in calls.Where(IsTodoTool))
        {
            if (!toolMessagesByCallId.TryGetValue(call.Id, out var todoMessage) ||
                !TryReadTodoWriteResult(todoMessage.Content, out var status, out var reason))
                continue;

            await WriteTaskContinuityAsync(
                descriptor,
                traceId,
                "todo_update_tool_result",
                "task_written",
                status,
                null,
                reason,
                ct);
            needsFeedback |= IsBlockedOrFailed(status);
        }

        if (!needsFeedback)
            return;

        var feedbackCalls = calls.Where(IsFeedbackTool).ToArray();
        foreach (var call in feedbackCalls)
        {
            var evidence = GetToolResultEvidence(toolEvidenceByCallId, call.Id);
            await WriteTaskContinuityAsync(
                descriptor,
                traceId,
                "feedback_attempted",
                "feedback",
                "attempted",
                evidence.CommandId,
                null,
                ct);
        }

        if (feedbackCalls.Length == 0)
            await WriteTaskContinuityAsync(
                descriptor,
                traceId,
                "feedback_missing",
                "diagnostic",
                "missing",
                null,
                "blocked_or_failed_task_without_player_feedback_tool",
                ct);
    }

    private async Task WriteCommandTerminalEvidenceAsync(
        NpcRuntimeDescriptor descriptor,
        string traceId,
        GameCommandStatus status,
        CancellationToken ct)
    {
        if (!TerminalStatusNames.Contains(status.Status))
            return;

        await WriteTaskContinuityAsync(
            descriptor,
            traceId,
            "command_terminal",
            "terminal",
            status.Status,
            status.CommandId,
            status.ErrorCode ?? status.BlockedReason,
            ct);
    }

    private async Task WriteTaskContinuityAsync(
        NpcRuntimeDescriptor descriptor,
        string traceId,
        string target,
        string stage,
        string result,
        string? commandId,
        string? error,
        CancellationToken ct)
    {
        if (_logWriter is null)
            return;

        await _logWriter.WriteAsync(new NpcRuntimeLogRecord(
            DateTime.UtcNow,
            traceId,
            descriptor.NpcId,
            descriptor.GameId,
            descriptor.SessionId,
            "task_continuity",
            target,
            stage,
            result,
            CommandId: commandId,
            Error: string.IsNullOrWhiteSpace(error) ? null : Truncate(error, 300)), ct);
    }

    private static bool IsActiveTodo(SessionTodoItem todo)
        => string.Equals(todo.Status, "pending", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(todo.Status, "in_progress", StringComparison.OrdinalIgnoreCase);

    private static readonly HashSet<string> TerminalStatusNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "completed",
        "failed",
        "cancelled",
        "interrupted",
        "blocked",
        "expired"
    };

    private static ToolResultEvidence ReadToolResultEvidence(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new ToolResultEvidence(null, null);

        try
        {
            using var doc = JsonDocument.Parse(content);
            var commandId = ReadString(doc.RootElement, "commandId");
            GameCommandStatus? terminalStatus = null;
            if (doc.RootElement.TryGetProperty("finalStatus", out var finalStatus) &&
                finalStatus.ValueKind == JsonValueKind.Object &&
                TryParseCommandStatus(finalStatus, out var finalStatusValue))
            {
                terminalStatus = finalStatusValue;
            }
            else if (TryParseInlineCommandStatus(doc.RootElement, out var inlineStatus))
            {
                terminalStatus = inlineStatus;
            }

            return new ToolResultEvidence(commandId ?? terminalStatus?.CommandId, terminalStatus);
        }
        catch (JsonException)
        {
            return new ToolResultEvidence(null, null);
        }
    }

    private static bool TryParseInlineCommandStatus(JsonElement element, out GameCommandStatus status)
    {
        status = new GameCommandStatus("", "", "", "", 0, null, null);
        var statusText = ReadString(element, "status");
        if (string.IsNullOrWhiteSpace(statusText) || !TerminalStatusNames.Contains(statusText))
            return false;

        status = new GameCommandStatus(
            ReadString(element, "commandId") ?? "",
            "",
            ReadString(element, "action") ?? "",
            statusText,
            1,
            ReadString(element, "failureReason"),
            ReadString(element, "failureReason"));
        return true;
    }

    private static bool TryParseCommandStatus(JsonElement element, out GameCommandStatus status)
    {
        status = new GameCommandStatus("", "", "", "", 0, null, null);
        var statusText = ReadString(element, "status");
        if (string.IsNullOrWhiteSpace(statusText))
            return false;

        var progress = element.TryGetProperty("progress", out var progressElement) &&
                       progressElement.TryGetDouble(out var progressValue)
            ? progressValue
            : 0;
        status = new GameCommandStatus(
            ReadString(element, "commandId") ?? "",
            ReadString(element, "npcId") ?? "",
            ReadString(element, "action") ?? "",
            statusText,
            progress,
            ReadString(element, "blockedReason"),
            ReadString(element, "errorCode"));
        return true;
    }

    private static bool TryReadTodoWriteResult(string content, out string status, out string? reason)
    {
        status = "";
        reason = null;
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("todos", out var todos) ||
                todos.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var todo in todos.EnumerateArray())
            {
                var parsedStatus = ReadString(todo, "status");
                if (string.IsNullOrWhiteSpace(parsedStatus))
                    continue;

                status = parsedStatus;
                reason = ReadString(todo, "reason");
                return true;
            }
        }
        catch (JsonException)
        {
        }

        return false;
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static ToolResultEvidence GetToolResultEvidence(
        IReadOnlyDictionary<string, ToolResultEvidence> evidenceByCallId,
        string callId)
        => evidenceByCallId.TryGetValue(callId, out var evidence)
            ? evidence
            : new ToolResultEvidence(null, null);

    private static bool IsStardewActionTool(ToolCall call)
        => string.Equals(call.Name, "stardew_move", StringComparison.OrdinalIgnoreCase) ||
           IsFeedbackTool(call);

    private static bool IsFeedbackTool(ToolCall call)
        => string.Equals(call.Name, "stardew_speak", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(call.Name, "stardew_open_private_chat", StringComparison.OrdinalIgnoreCase);

    private static bool IsTodoTool(ToolCall call)
        => string.Equals(call.Name, "todo", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(call.Name, "todo_write", StringComparison.OrdinalIgnoreCase);

    private static bool IsBlockedOrFailed(string? status)
        => string.Equals(status, "blocked", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase);

    private sealed record ToolResultEvidence(string? CommandId, GameCommandStatus? TerminalStatus);

    private async Task WriteMemoryAsync(string traceId, string? decisionResponse, CancellationToken ct)
    {
        if (_memoryManager is null || string.IsNullOrWhiteSpace(decisionResponse))
            return;

        await _memoryManager.AddAsync(
            "memory",
            $"Autonomy tick {traceId}: {Truncate(decisionResponse, 300)}",
            ct);
    }

    private static bool HasToolCall(Session? session, string toolName)
    {
        if (session is null)
            return false;

        return session.Messages.Any(message =>
            string.Equals(message.ToolName, toolName, StringComparison.OrdinalIgnoreCase) ||
            (message.ToolCalls?.Any(toolCall => string.Equals(toolCall.Name, toolName, StringComparison.OrdinalIgnoreCase)) ?? false));
    }

    private static bool HasAnyStardewActionToolCall(Session? session)
        => HasToolCall(session, "stardew_move") ||
           HasToolCall(session, "stardew_speak") ||
           HasToolCall(session, "stardew_open_private_chat");

    private static bool TryExtractVisibleLine(string value, out string text)
    {
        text = "";
        if (TryExtractQuotedVisibleLine(value, out text))
            return true;

        var cleaned = StripMarkdownAndStageDirections(value);
        var candidates = cleaned
            .Split(['\r', '\n', '。', '！', '？', '!', '?'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeVisibleSpeechText)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !line.StartsWith("系统", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.StartsWith("Haley", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.StartsWith("海莉状态", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.Contains("等待天", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.Contains("等待白天", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var selected = candidates.FirstOrDefault(candidate =>
            IsVisibleSpeechCandidate(candidate, quoted: false) &&
            !LooksLikePhoneMessageRecap(value, candidate));

        if (string.IsNullOrWhiteSpace(selected))
            return false;

        text = Truncate(selected, FallbackSpeakMaxLength);
        return !string.IsNullOrWhiteSpace(text);
    }

    private static bool TryExtractQuotedVisibleLine(string value, out string text)
    {
        text = "";
        foreach (Match match in Regex.Matches(value, "[\"“「](?<line>[^\"”」\\r\\n]{1,120})[\"”」]"))
        {
            var candidate = NormalizeVisibleSpeechText(match.Groups["line"].Value);
            if (!IsCurrentQuotedSpeech(value, match) ||
                !IsVisibleSpeechCandidate(candidate, quoted: true))
            {
                continue;
            }

            text = Truncate(candidate, FallbackSpeakMaxLength);
            return true;
        }

        return false;
    }

    private static bool IsCurrentQuotedSpeech(string value, Match match)
    {
        var beforeStart = Math.Max(0, match.Index - 80);
        var before = value.Substring(beforeStart, match.Index - beforeStart);
        var afterStart = match.Index + match.Length;
        var afterLength = Math.Min(80, value.Length - afterStart);
        var after = afterLength > 0 ? value.Substring(afterStart, afterLength) : "";
        var context = NormalizeVisibleSpeechText(before + after);

        if (LooksLikeHistoricalQuoteContext(context))
            return false;

        if (LooksLikeCurrentSpeechContext(context))
            return true;

        return string.IsNullOrWhiteSpace(StripMarkdownAndStageDirections(value).Replace(match.Value, "", StringComparison.Ordinal).Trim());
    }

    private static bool LooksLikeHistoricalQuoteContext(string value)
        => value.Contains("手机", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("消息", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("未读", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("收件箱", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("那些话", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("回忆", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("默念", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("又念", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("复述", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("记得", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("早几个小时前", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("下午", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("傍晚", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("昨", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikePhoneMessageRecap(string fullValue, string candidate)
    {
        var normalizedFull = NormalizeVisibleSpeechText(fullValue);
        var normalizedCandidate = NormalizeVisibleSpeechText(candidate);
        if (string.IsNullOrWhiteSpace(normalizedCandidate))
            return false;

        var hasPhoneMessageContext =
            normalizedFull.Contains("手机", StringComparison.OrdinalIgnoreCase) ||
            normalizedFull.Contains("消息", StringComparison.OrdinalIgnoreCase) ||
            normalizedFull.Contains("通知", StringComparison.OrdinalIgnoreCase) ||
            normalizedFull.Contains("送到", StringComparison.OrdinalIgnoreCase) ||
            normalizedFull.Contains("送达", StringComparison.OrdinalIgnoreCase);
        if (!hasPhoneMessageContext)
            return false;

        return normalizedCandidate.Contains("刚刚跟他说", StringComparison.OrdinalIgnoreCase) ||
               normalizedCandidate.Contains("刚刚对他说", StringComparison.OrdinalIgnoreCase) ||
               normalizedCandidate.Contains("消息", StringComparison.OrdinalIgnoreCase) ||
               normalizedCandidate.Contains("手机", StringComparison.OrdinalIgnoreCase) ||
               normalizedCandidate.Contains("送到", StringComparison.OrdinalIgnoreCase) ||
               normalizedCandidate.Contains("送达", StringComparison.OrdinalIgnoreCase) ||
               normalizedCandidate.Contains("不用再催", StringComparison.OrdinalIgnoreCase) ||
               normalizedCandidate.Contains("持续等待", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeCurrentSpeechContext(string value)
        => value.Contains("说", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("嘟囔", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("咕哝", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("梦话", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("自言自语", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("回答", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("回应", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("开口", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("声音", StringComparison.OrdinalIgnoreCase);

    private static string StripMarkdownAndStageDirections(string value)
    {
        var withoutCode = Regex.Replace(value, "```.*?```", "", RegexOptions.Singleline);
        var withoutStageDirections = Regex.Replace(withoutCode, @"\*[^*\r\n]{1,240}\*", "", RegexOptions.Singleline);
        var withoutBracketNotes = Regex.Replace(withoutStageDirections, @"---[\s\S]*$", "");
        return withoutBracketNotes
            .Replace(">", "", StringComparison.Ordinal)
            .Replace("💤", "", StringComparison.Ordinal)
            .Replace("🌙", "", StringComparison.Ordinal)
            .Replace("✨", "", StringComparison.Ordinal)
            .Trim();
    }

    private static string NormalizeVisibleSpeechText(string value)
        => value
            .Trim(' ', '\t', '\r', '\n', '*', '_', '"', '\'', '“', '”', '「', '」', '（', '）', '(', ')')
            .Trim();

    private static bool LooksLikeOnlyNarration(string value)
        => value.StartsWith("在", StringComparison.OrdinalIgnoreCase) ||
           value.StartsWith("被", StringComparison.OrdinalIgnoreCase) ||
           value.StartsWith("月光", StringComparison.OrdinalIgnoreCase) ||
           value.StartsWith("房间", StringComparison.OrdinalIgnoreCase) ||
           value.StartsWith("窗外", StringComparison.OrdinalIgnoreCase) ||
           value.StartsWith("凌", StringComparison.OrdinalIgnoreCase);

    private static bool IsVisibleSpeechCandidate(string value, bool quoted)
        => HasSpeechContent(value) &&
           HasCjkContent(value) &&
           !LooksLikeOnlyNarration(value) &&
           !LooksLikeStatusOrInstruction(value) &&
           !value.Contains("zzz", StringComparison.OrdinalIgnoreCase) &&
           !value.Contains("睡眠中", StringComparison.OrdinalIgnoreCase) &&
           !value.Contains("深度睡眠", StringComparison.OrdinalIgnoreCase) &&
           (quoted || LooksLikeDirectUnquotedSpeech(value));

    private static bool LooksLikeDirectUnquotedSpeech(string value)
    {
        value = NormalizeVisibleSpeechText(value);
        if (value.Length == 0)
            return false;

        if (value.StartsWith("我", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("咦", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("嗯", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("唔", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("啊", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("喂", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("嘿", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("好吧", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("等等", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("牛哥", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return value.Contains("你", StringComparison.OrdinalIgnoreCase) &&
               !value.StartsWith("海莉", StringComparison.OrdinalIgnoreCase) &&
               !value.StartsWith("潘妮", StringComparison.OrdinalIgnoreCase) &&
               !value.StartsWith("拖车", StringComparison.OrdinalIgnoreCase) &&
               !value.StartsWith("房间", StringComparison.OrdinalIgnoreCase) &&
               !value.StartsWith("凌晨", StringComparison.OrdinalIgnoreCase) &&
               !value.StartsWith("那些", StringComparison.OrdinalIgnoreCase) &&
               !value.StartsWith("天亮", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeStatusOrInstruction(string value)
        => value.StartsWith("当前状态", StringComparison.OrdinalIgnoreCase) ||
           value.StartsWith("状态", StringComparison.OrdinalIgnoreCase) ||
           value.StartsWith("目标", StringComparison.OrdinalIgnoreCase) ||
           value.StartsWith("等待", StringComparison.OrdinalIgnoreCase) ||
           value.StartsWith("继续", StringComparison.OrdinalIgnoreCase) ||
           value.StartsWith("让她", StringComparison.OrdinalIgnoreCase) ||
           value.StartsWith("让他", StringComparison.OrdinalIgnoreCase) ||
           value.StartsWith("没有新的行动", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("等待天亮", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("等天亮", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("等待游戏时间", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("没什么可做", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("当前状态", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("深夜休息中", StringComparison.OrdinalIgnoreCase);

    private static bool HasSpeechContent(string value)
    {
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
                return true;

            if (ch >= '\u4e00' && ch <= '\u9fff')
                return true;
        }

        return false;
    }

    private static bool HasCjkContent(string value)
    {
        foreach (var ch in value)
        {
            if (ch >= '\u4e00' && ch <= '\u9fff')
                return true;
        }

        return false;
    }

    private static bool LooksLikePhysicalMovement(string value)
    {
        string[] markers =
        [
            "走向",
            "走到",
            "走进",
            "走出",
            "走上",
            "走下",
            "回到",
            "回房",
            "出门",
            "靠近",
            "移动到",
            "上楼",
            "下楼",
            "离开",
            "前往",
            "walked to",
            "walks to",
            "walking to",
            "moved to",
            "moves to",
            "heads to",
            "headed to",
            "goes to",
            "went to"
        ];

        return markers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}

public sealed record NpcAutonomyTickResult(
    string NpcId,
    string TraceId,
    int ObservationFacts,
    int EventFacts,
    string? DecisionResponse = null,
    GameEventCursor? NextEventCursor = null);
