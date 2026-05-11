using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Hermes.Agent.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hermes.Agent.Runtime;

public sealed class NpcAutonomyLoop
{
    private const int FallbackSpeakMaxLength = 80;
    private readonly IGameAdapter _adapter;
    private readonly NpcObservationFactStore _factStore;
    private readonly Hermes.Agent.Core.IAgent? _agent;
    private readonly NpcRuntimeLogWriter? _logWriter;
    private readonly INpcLocalExecutorRunner? _localExecutorRunner;
    private readonly ILogger<NpcAutonomyLoop>? _logger;
    private readonly Func<string> _traceIdFactory;

    public NpcAutonomyLoop(
        IGameAdapter adapter,
        NpcObservationFactStore factStore,
        Hermes.Agent.Core.IAgent? agent = null,
        NpcRuntimeLogWriter? logWriter = null,
        ILogger<NpcAutonomyLoop>? logger = null,
        Func<string>? traceIdFactory = null,
        INpcLocalExecutorRunner? localExecutorRunner = null)
    {
        _adapter = adapter;
        _factStore = factStore;
        _agent = agent;
        _logWriter = logWriter;
        _localExecutorRunner = localExecutorRunner;
        _logger = logger;
        _traceIdFactory = traceIdFactory ?? (() => $"trace_{Guid.NewGuid():N}");
    }

    public async Task<NpcAutonomyTickResult> RunOneTickAsync(
        NpcRuntimeInstance instance,
        GameEventCursor eventCursor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var result = await RunOneTickForInstanceAsync(instance, eventCursor, ct);
        instance.RecordTrace(result.TraceId);
        await WriteInstanceTaskContinuityEvidenceAsync(instance, result.TraceId, ct);
        return result;
    }

    public async Task<NpcAutonomyTickResult> RunOneTickAsync(
        NpcRuntimeInstance instance,
        GameEventBatch eventBatch,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var result = await RunOneTickForInstanceAsync(instance, eventBatch, ct);
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

        var result = await RunOneTickForInstanceAsync(instance, observation, eventBatch, ct);
        instance.RecordTrace(result.TraceId);
        await WriteInstanceTaskContinuityEvidenceAsync(instance, result.TraceId, ct);
        return result;
    }

    public async Task<NpcAutonomyTickResult> RunDelegatedIntentAsync(
        NpcRuntimeInstance instance,
        string traceId,
        string intentJson,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(traceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(intentJson);

        var route = await RunLocalExecutorAsync(instance, instance.Descriptor, [], traceId, intentJson, ct);
        await WriteActivityAsync(instance.Descriptor, traceId, 0, route.DecisionResponse, ct);
        instance.RecordTrace(traceId);
        await WriteInstanceTaskContinuityEvidenceAsync(instance, traceId, ct);
        return new NpcAutonomyTickResult(instance.Descriptor.NpcId, traceId, 0, 0, route.DecisionResponse, instance.Snapshot().Controller.EventCursor);
    }

    private async Task<NpcAutonomyTickResult> RunOneTickForInstanceAsync(
        NpcRuntimeInstance instance,
        GameEventCursor eventCursor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(eventCursor);

        var descriptor = instance.Descriptor;
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
        return await RunOneTickForInstanceAsync(instance, eventBatch, ct);
    }

    private Task<NpcAutonomyTickResult> RunOneTickForInstanceAsync(
        NpcRuntimeInstance instance,
        GameEventBatch eventBatch,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return RunOneTickCoreAsync(instance, instance.Descriptor, null, eventBatch, ct);
    }

    private Task<NpcAutonomyTickResult> RunOneTickForInstanceAsync(
        NpcRuntimeInstance instance,
        GameObservation observation,
        GameEventBatch eventBatch,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return RunOneTickCoreAsync(instance, instance.Descriptor, observation, eventBatch, ct);
    }

    public async Task<NpcAutonomyTickResult> RunOneTickAsync(
        NpcRuntimeDescriptor descriptor,
        GameEventCursor eventCursor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(eventCursor);

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
        return await RunOneTickCoreAsync(null, descriptor, null, eventBatch, ct);
    }

    public async Task<NpcAutonomyTickResult> RunOneTickAsync(
        NpcRuntimeDescriptor descriptor,
        GameObservation observation,
        GameEventBatch eventBatch,
        CancellationToken ct)
        => await RunOneTickCoreAsync(null, descriptor, observation, eventBatch, ct);

    private async Task<NpcAutonomyTickResult> RunOneTickCoreAsync(
        NpcRuntimeInstance? instance,
        NpcRuntimeDescriptor descriptor,
        GameObservation? observation,
        GameEventBatch eventBatch,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(eventBatch);

        var traceId = _traceIdFactory();
        if (observation is not null)
            _factStore.RecordObservation(descriptor, observation);

        var eventFacts = 0;
        foreach (var record in eventBatch.Records)
        {
            if (!BelongsToRuntimeNpc(descriptor, record))
                continue;

            _factStore.RecordEvent(descriptor, record);
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
            decisionSession.State[StardewAutonomySessionKeys.IsAutonomyTurn] = true;
            var decisionMessage = BuildDecisionMessage(
                descriptor,
                instance?.Snapshot().Controller.LastTerminalCommandStatus,
                GetActiveTodos(instance, descriptor.SessionId));
            _logger?.LogInformation(
                "NPC autonomy decision request prepared; npc={NpcId}; trace={TraceId}; injectedFacts={FactCount}; messageChars={MessageChars}",
                descriptor.NpcId,
                traceId,
                0,
                decisionMessage.Length);
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

        if (_localExecutorRunner is not null &&
            ShouldRouteToLocalExecutor(decisionResponse, decisionSession) &&
            decisionResponse is not null)
        {
            var route = await RunLocalExecutorAsync(
                instance,
                descriptor,
                [],
                traceId,
                decisionResponse,
                ct);
            decisionResponse = route.DecisionResponse;
        }

        await WriteActivityAsync(descriptor, traceId, eventFacts, decisionResponse, ct);
        await WriteToolBudgetDiagnosticAsync(descriptor, traceId, rawDecisionResponse, ct);
        if (_localExecutorRunner is null)
        {
            await WriteNarrativeMovementDiagnosticAsync(descriptor, traceId, decisionResponse, decisionSession, ct);
            await WriteNoToolActionDiagnosticAsync(instance, descriptor, traceId, decisionResponse, decisionSession, ct);
        }

        await WriteSessionTaskContinuityEvidenceAsync(descriptor, traceId, decisionSession, null, ct);

        return new NpcAutonomyTickResult(descriptor.NpcId, traceId, 0, eventFacts, decisionResponse, eventBatch.NextCursor);
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

    private static string BuildDecisionMessage(
        NpcRuntimeDescriptor descriptor,
        GameCommandStatus? lastTerminalCommandStatus = null,
        IReadOnlyList<SessionTodoItem>? activeTodos = null)
    {
        var message =
            $"NPC: {descriptor.DisplayName} ({descriptor.NpcId})\n" +
            "你被唤醒了一轮。你是生活在星露谷里的人，不是宿主替你操控的脚本。\n" +
            "宿主这一次只负责唤醒你；它没有替你观察世界，没有替你选择目标，也没有要求你必须先观察。\n" +
            "你要像生活在星露谷里的人一样，自己决定下一步该做什么；宿主不会替你规定是否观察、等待、移动、说话、推进任务或做闲置动作。\n" +
            "不要声称已经看见、到达、完成或知道当前世界状态，除非那来自你自己已有的上下文或后续工具执行结果。\n" +
            "如果决定移动，先用 skill_view 读取 stardew-navigation 及其 references/index.md、相关 region/POI 文件；只有资料明确给出 target(locationName,x,y,source) 后，才调用 stardew_navigate_to_tile，并原样填写这些字段。\n" +
            "不要把玩家自然语言地点直接当 locationName，不要编造坐标，不要使用 destinationId。\n" +
            "stardew_navigate_to_tile 只是提交真实动作；真实移动、跨地图行走、等待切图和失败码都由宿主与 Stardew bridge 执行，并通过工具结果返回给你。\n" +
            "如果只是查长动作进度，调用 stardew_task_status 且 commandId 必须来自已有命令。\n" +
            "如果你只是想在原地做一个短暂可见的小动作，用 action=idle_micro_action，并提供 idleMicroAction.kind；可选 kind 只有 emote_happy、emote_question、emote_sleepy、emote_music、look_left、look_right、look_up、look_down、look_around、tiny_hop、tiny_shake、idle_pose、idle_animation_once。\n" +
            "idle_micro_action 只能表达原地短动作；不要同时附带 speech 或 target，也不要把它改写成 move 或 speak。\n" +
            "如果任务真的被外部条件阻断，用 taskUpdate 把已有 todo 标成 blocked；如果确定做不成，标成 failed；blocked 或 failed 都要写短 reason。\n" +
            "wait 只表示你现在选择暂不推进，不是普通世界动作。";
        var lastActionFact = BuildLastActionResultFact(lastTerminalCommandStatus);
        message += "\nMOVE TOOL CONTRACT: parent autonomy owns target resolution. Use skill_view to load stardew-navigation and the smallest relevant reference files, then call stardew_navigate_to_tile with the exact target(locationName,x,y,source). The host executes the real action and the tool result is the feedback channel.";
        var activeTodoFact = BuildActiveTodoClosureFact(lastTerminalCommandStatus, activeTodos);
        if (activeTodoFact is not null)
            message += "\n" + activeTodoFact;
        return lastActionFact is null
            ? message
            : message + "\n" + lastActionFact;
    }

    private static string? BuildActiveTodoClosureFact(
        GameCommandStatus? lastTerminalCommandStatus,
        IReadOnlyList<SessionTodoItem>? activeTodos)
    {
        if (lastTerminalCommandStatus is null ||
            !TerminalStatusNames.Contains(lastTerminalCommandStatus.Status) ||
            activeTodos is null ||
            activeTodos.Count == 0)
        {
            return null;
        }

        var todoText = string.Join("; ", activeTodos.Take(3).Select(todo => $"{todo.Id}:{todo.Status}:{todo.Content}"));
        return "active todo closure required: 你有 active todo 与刚结束的 last_action_result 同时存在；" +
               $"active todo={todoText}。你必须显式收口：调用 todo 标 completed/blocked/failed，或提交新的世界动作，或明确 wait/no-action 并写短 reason。";
    }

    private static string? BuildLastActionResultFact(GameCommandStatus? status)
    {
        if (status is null)
            return null;

        var commandId = string.IsNullOrWhiteSpace(status.CommandId) ? "-" : status.CommandId;
        if (IsActionSlotTimeout(status))
            return $"action_slot_timeout: 上一轮行动槽在完成前超时；commandId={commandId}; status={status.Status}。你自己决定下一步是观察、等待、换目标，还是用不同方式继续。";

        var action = string.IsNullOrWhiteSpace(status.Action) ? "-" : status.Action;
        var reason = status.ErrorCode ?? status.BlockedReason;
        var result = $"last_action_result: 上一轮真实动作已结束；commandId={commandId}; action={action}; status={status.Status}";
        return string.IsNullOrWhiteSpace(reason)
            ? result + "。这是宿主执行结果事实，不是下一步指令。"
            : result + $"; reason={reason}。这是宿主执行结果事实，不是下一步指令。";
    }

    private static bool IsActionSlotTimeout(GameCommandStatus status)
        => string.Equals(status.ErrorCode, StardewBridgeErrorCodes.ActionSlotTimeout, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status.BlockedReason, StardewBridgeErrorCodes.ActionSlotTimeout, StringComparison.OrdinalIgnoreCase);

    private static bool IsToolIterationLimitFallback(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           value.StartsWith("I've reached the maximum number of tool call iterations.", StringComparison.Ordinal);

    private static bool ShouldRouteToLocalExecutor(string? decisionResponse, Session? decisionSession)
    {
        if (string.IsNullOrWhiteSpace(decisionResponse))
            return false;

        if (decisionResponse.TrimStart().StartsWith("{", StringComparison.Ordinal))
            return true;

        return !SessionHasAnyToolCalls(decisionSession);
    }

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
            decisionResponse ?? $"woken:eventFacts={eventFacts}"), ct);
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

    private async Task<LocalExecutorRouteResult> RunLocalExecutorAsync(
        NpcRuntimeInstance? instance,
        NpcRuntimeDescriptor descriptor,
        IReadOnlyList<NpcObservationFact> facts,
        string traceId,
        string decisionResponse,
        CancellationToken ct)
    {
        if (!NpcLocalActionIntent.TryParse(decisionResponse, out var intent, out var error) ||
            intent is null)
        {
            await WriteDiagnosticAsync(
                descriptor,
                traceId,
                "intent_contract",
                "rejected",
                error,
                "intent_contract_invalid",
                ct);

            return new LocalExecutorRouteResult(
                $"local_executor_escalated:{error}");
        }

        var action = FormatLocalAction(intent.Action);
        await WriteDiagnosticAsync(
            descriptor,
            traceId,
            "intent_contract",
            "accepted",
            $"action={action};reason={intent.Reason}",
            null,
            ct);
        await WriteDiagnosticAsync(
            descriptor,
            traceId,
            "parent_tool_surface",
            "verified",
            "registered_tools=0;stardew_navigate_to_tile=0;stardew_task_status=0;stardew_speak=0;todo=0;agent=0",
            null,
            ct);
        await WriteDiagnosticAsync(
            descriptor,
            traceId,
            "local_executor",
            "selected",
            $"action={action};lane=delegation",
            null,
            ct);

        if (IsLocalExecutorDisabledWriteAction(intent.Action))
        {
            var blockedResult = NpcLocalExecutorRunner.BlockDisabledWriteAction(intent);
            if (instance is not null)
            {
                await ApplyTaskUpdateContractAsync(instance, descriptor, traceId, intent.TaskUpdate, ct);
                await SubmitSpeechContractAsync(descriptor, traceId, intent.Speech, ct);
            }

            await WriteLocalExecutorResultAsync(descriptor, traceId, blockedResult, ct);
            return new LocalExecutorRouteResult(blockedResult.DecisionResponse);
        }

        var result = await _localExecutorRunner!.ExecuteAsync(descriptor, intent, facts, traceId, ct);
        if (instance is not null)
        {
            await ApplyTaskUpdateContractAsync(instance, descriptor, traceId, intent.TaskUpdate, ct);
            await SubmitSpeechContractAsync(descriptor, traceId, intent.Speech, ct);
        }

        await WriteLocalExecutorDiagnosticsAsync(descriptor, traceId, result.Diagnostics, ct);
        await WriteLocalExecutorResultAsync(descriptor, traceId, result, ct);
        return new LocalExecutorRouteResult(result.DecisionResponse);
    }

    private async Task ApplyTaskUpdateContractAsync(
        NpcRuntimeInstance instance,
        NpcRuntimeDescriptor descriptor,
        string traceId,
        NpcLocalTaskUpdateIntent? taskUpdate,
        CancellationToken ct)
    {
        if (taskUpdate is null)
            return;

        var snapshot = instance.TodoStore.Read(descriptor.SessionId);
        var existing = snapshot.Todos.FirstOrDefault(todo =>
            string.Equals(todo.Id, taskUpdate.TaskId, StringComparison.Ordinal));
        if (existing is null)
        {
            await WriteDiagnosticAsync(
                descriptor,
                traceId,
                "task_update_contract",
                "skipped",
                $"task_not_found:{taskUpdate.TaskId}",
                null,
                ct);
            return;
        }

        instance.TodoStore.Write(
            descriptor.SessionId,
            [new SessionTodoInput(taskUpdate.TaskId, null, taskUpdate.Status, taskUpdate.Reason)],
            merge: true);
        await WriteTaskContinuityAsync(
            descriptor,
            traceId,
            "task_update_contract",
            "task_written",
            taskUpdate.Status,
            null,
            taskUpdate.Reason,
            ct);
    }

    private async Task SubmitSpeechContractAsync(
        NpcRuntimeDescriptor descriptor,
        string traceId,
        NpcLocalSpeechIntent? speech,
        CancellationToken ct)
    {
        if (speech?.ShouldSpeak is not true ||
            string.IsNullOrWhiteSpace(speech.Text))
        {
            return;
        }

        var channel = string.IsNullOrWhiteSpace(speech.Channel)
            ? "player"
            : speech.Channel.Trim();
        var action = new GameAction(
            descriptor.NpcId,
            descriptor.GameId,
            GameActionType.Speak,
            traceId,
            $"idem_{descriptor.NpcId}_{traceId}_speech",
            new GameActionTarget("player"),
            Payload: new JsonObject
            {
                ["text"] = speech.Text.Trim(),
                ["channel"] = channel
            },
            BodyBinding: descriptor.EffectiveBodyBinding);

        var result = await _adapter.Commands.SubmitAsync(action, ct);
        await WriteHostActionResultAsync(descriptor, traceId, "stardew_speak", result, ct);
    }

    private async Task WriteHostActionResultAsync(
        NpcRuntimeDescriptor descriptor,
        string traceId,
        string target,
        GameCommandResult result,
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
            "host_action",
            target,
            result.Accepted ? "submitted" : "rejected",
            string.IsNullOrWhiteSpace(result.Status) ? "unknown" : result.Status,
            CommandId: result.CommandId,
            Error: result.FailureReason), ct);
    }

    private async Task WriteDiagnosticAsync(
        NpcRuntimeDescriptor descriptor,
        string traceId,
        string target,
        string stage,
        string result,
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
            "diagnostic",
            target,
            stage,
            Truncate(result, 300),
            Error: string.IsNullOrWhiteSpace(error) ? null : error), ct);
    }

    private async Task WriteLocalExecutorResultAsync(
        NpcRuntimeDescriptor descriptor,
        string traceId,
        NpcLocalExecutorResult result,
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
            "local_executor",
            result.Target,
            result.Stage,
            Truncate(result.Result, 300),
            CommandId: result.CommandId,
            Error: string.IsNullOrWhiteSpace(result.Error) ? null : result.Error,
            ExecutorMode: result.ExecutorMode,
            TargetSource: result.TargetSource), ct);
    }

    private async Task WriteLocalExecutorDiagnosticsAsync(
        NpcRuntimeDescriptor descriptor,
        string traceId,
        IReadOnlyList<string> diagnostics,
        CancellationToken ct)
    {
        if (_logWriter is null)
            return;

        foreach (var diagnostic in diagnostics)
        {
            var stage = ExtractDiagnosticValue(diagnostic, "stage") ?? "diagnostic";
            var result = ExtractDiagnosticValue(diagnostic, "result") ?? diagnostic;
            await _logWriter.WriteAsync(new NpcRuntimeLogRecord(
                DateTime.UtcNow,
                traceId,
                descriptor.NpcId,
                descriptor.GameId,
                descriptor.SessionId,
                "diagnostic",
                "local_executor",
                stage,
                Truncate(result, 300)), ct);
        }
    }

    private static string? ExtractDiagnosticValue(string value, string key)
    {
        var prefix = key + "=";
        var start = value.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;

        start += prefix.Length;
        var end = value.IndexOf(' ', start);
        return end < 0 ? value[start..] : value[start..end];
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
            HasToolCall(decisionSession, "stardew_navigate_to_tile"))
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
            "stardew_navigate_to_tile",
            "warning",
            "narrative_move_without_navigation_tool",
            Error: Truncate(decisionResponse, 300)), ct);
    }

    private async Task WriteNoToolActionDiagnosticAsync(
        NpcRuntimeInstance? instance,
        NpcRuntimeDescriptor descriptor,
        string traceId,
        string? decisionResponse,
        Session? decisionSession,
        CancellationToken ct)
    {
        if (HasAnyStardewActionToolCall(decisionSession))
        {
            return;
        }

        var requiresClosureChoice = RequiresClosureChoice(instance, descriptor.SessionId);
        if (requiresClosureChoice)
        {
            if (!string.IsNullOrWhiteSpace(decisionResponse) &&
                TryExtractExplicitNoActionReason(decisionResponse, out var reason))
            {
                await WriteTaskContinuityAsync(
                    descriptor,
                    traceId,
                    "closure_no_action",
                    "diagnostic",
                    "recorded",
                    null,
                    reason,
                    ct);
                return;
            }

            await WriteTaskContinuityAsync(
                descriptor,
                traceId,
                "closure_missing",
                "diagnostic",
                "missing",
                null,
                "terminal_action_with_active_todo_without_explicit_closure",
                ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(decisionResponse))
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
        => string.Equals(call.Name, "stardew_navigate_to_tile", StringComparison.OrdinalIgnoreCase) ||
           IsFeedbackTool(call);

    private static bool IsFeedbackTool(ToolCall call)
        => string.Equals(call.Name, "stardew_speak", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(call.Name, "stardew_open_private_chat", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(call.Name, "stardew_idle_micro_action", StringComparison.OrdinalIgnoreCase);

    private static bool IsTodoTool(ToolCall call)
        => string.Equals(call.Name, "todo", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(call.Name, "todo_write", StringComparison.OrdinalIgnoreCase);

    private static bool IsBlockedOrFailed(string? status)
        => string.Equals(status, "blocked", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase);

    private static string FormatLocalAction(NpcLocalActionKind action)
        => action switch
        {
            NpcLocalActionKind.Move => "move",
            NpcLocalActionKind.Observe => "observe",
            NpcLocalActionKind.Wait => "wait",
            NpcLocalActionKind.TaskStatus => "task_status",
            NpcLocalActionKind.IdleMicroAction => "idle_micro_action",
            NpcLocalActionKind.Escalate => "escalate",
            _ => action.ToString()
        };

    private static bool IsLocalExecutorDisabledWriteAction(NpcLocalActionKind action)
        => action is NpcLocalActionKind.Move or NpcLocalActionKind.IdleMicroAction;

    private sealed record LocalExecutorRouteResult(string DecisionResponse);

    private sealed record ToolResultEvidence(string? CommandId, GameCommandStatus? TerminalStatus);

    private static bool HasToolCall(Session? session, string toolName)
    {
        if (session is null)
            return false;

        return session.Messages.Any(message =>
            string.Equals(message.ToolName, toolName, StringComparison.OrdinalIgnoreCase) ||
            (message.ToolCalls?.Any(toolCall => string.Equals(toolCall.Name, toolName, StringComparison.OrdinalIgnoreCase)) ?? false));
    }

    private static bool RequiresClosureChoice(NpcRuntimeInstance? instance, string sessionId)
        => instance?.Snapshot().Controller.LastTerminalCommandStatus is { } status &&
           TerminalStatusNames.Contains(status.Status) &&
           GetActiveTodos(instance, sessionId).Count > 0;

    private static IReadOnlyList<SessionTodoItem> GetActiveTodos(NpcRuntimeInstance? instance, string sessionId)
    {
        if (instance is null ||
            !instance.TryGetTaskView(sessionId, out var taskView) ||
            taskView is null)
        {
            return [];
        }

        return taskView.ActiveSnapshot.Todos.Where(IsActiveTodo).ToArray();
    }

    private static bool TryExtractExplicitNoActionReason(string value, out string reason)
    {
        reason = "";
        var match = Regex.Match(
            value.Trim(),
            @"^(?:wait|no-action|no_action)\s*[:：]\s*(?<reason>\S.{0,180})$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
            return false;

        reason = Truncate(match.Groups["reason"].Value.Trim(), 180);
        return !string.IsNullOrWhiteSpace(reason);
    }

    private static bool SessionHasAnyToolCalls(Session? session)
    {
        if (session is null)
            return false;

        return session.Messages.Any(message =>
            !string.IsNullOrWhiteSpace(message.ToolName) ||
            message.ToolCalls is { Count: > 0 });
    }

    private static bool HasAnyStardewActionToolCall(Session? session)
        => HasToolCall(session, "stardew_navigate_to_tile") ||
           HasToolCall(session, "stardew_speak") ||
           HasToolCall(session, "stardew_open_private_chat") ||
           HasToolCall(session, "stardew_idle_micro_action");

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
