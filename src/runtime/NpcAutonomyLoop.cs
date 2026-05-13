using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
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
    private readonly ILogger<NpcAutonomyLoop>? _logger;
    private readonly Func<string> _traceIdFactory;

    public NpcAutonomyLoop(
        IGameAdapter adapter,
        NpcObservationFactStore factStore,
        Hermes.Agent.Core.IAgent? agent = null,
        NpcRuntimeLogWriter? logWriter = null,
        ILogger<NpcAutonomyLoop>? logger = null,
        Func<string>? traceIdFactory = null)
    {
        _adapter = adapter;
        _factStore = factStore;
        _agent = agent;
        _logWriter = logWriter;
        _logger = logger;
        _traceIdFactory = traceIdFactory ?? (() => $"trace_{Guid.NewGuid():N}");
    }

    public async Task<NpcAutonomyTickResult> RunOneTickAsync(
        NpcRuntimeDriver runtimeDriver,
        GameEventCursor eventCursor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(runtimeDriver);

        var result = await RunOneTickForDriverAsync(runtimeDriver, eventCursor, ct);
        runtimeDriver.Instance.RecordTrace(result.TraceId);
        await WriteInstanceTaskContinuityEvidenceAsync(runtimeDriver.Instance, result.TraceId, ct);
        return result;
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
        NpcRuntimeDriver runtimeDriver,
        GameEventBatch eventBatch,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(runtimeDriver);

        var result = await RunOneTickForDriverAsync(runtimeDriver, eventBatch, ct);
        runtimeDriver.Instance.RecordTrace(result.TraceId);
        await WriteInstanceTaskContinuityEvidenceAsync(runtimeDriver.Instance, result.TraceId, ct);
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

    private async Task<NpcAutonomyTickResult> RunOneTickForDriverAsync(
        NpcRuntimeDriver runtimeDriver,
        GameEventCursor eventCursor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(runtimeDriver);
        ArgumentNullException.ThrowIfNull(eventCursor);

        var descriptor = runtimeDriver.Instance.Descriptor;
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
        return await RunOneTickCoreAsync(runtimeDriver, runtimeDriver.Instance, descriptor, null, eventBatch, ct);
    }

    private Task<NpcAutonomyTickResult> RunOneTickForDriverAsync(
        NpcRuntimeDriver runtimeDriver,
        GameEventBatch eventBatch,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(runtimeDriver);
        return RunOneTickCoreAsync(runtimeDriver, runtimeDriver.Instance, runtimeDriver.Instance.Descriptor, null, eventBatch, ct);
    }

    private Task<NpcAutonomyTickResult> RunOneTickForInstanceAsync(
        NpcRuntimeInstance instance,
        GameEventBatch eventBatch,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return RunOneTickCoreAsync(null, instance, instance.Descriptor, null, eventBatch, ct);
    }

    private Task<NpcAutonomyTickResult> RunOneTickForInstanceAsync(
        NpcRuntimeInstance instance,
        GameObservation observation,
        GameEventBatch eventBatch,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return RunOneTickCoreAsync(null, instance, instance.Descriptor, observation, eventBatch, ct);
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
        return await RunOneTickCoreAsync(null, null, descriptor, null, eventBatch, ct);
    }

    public async Task<NpcAutonomyTickResult> RunOneTickAsync(
        NpcRuntimeDescriptor descriptor,
        GameObservation observation,
        GameEventBatch eventBatch,
        CancellationToken ct)
        => await RunOneTickCoreAsync(null, null, descriptor, observation, eventBatch, ct);

    private async Task<NpcAutonomyTickResult> RunOneTickCoreAsync(
        NpcRuntimeDriver? runtimeDriver,
        NpcRuntimeInstance? instance,
        NpcRuntimeDescriptor descriptor,
        GameObservation? observation,
        GameEventBatch eventBatch,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(eventBatch);
        instance ??= runtimeDriver?.Instance;

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
        string? rawDecisionResponse = null;
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
                instance?.Snapshot().Controller.ActionChainGuard,
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
            var rawFirstDecisionResponse = decisionResponse;
            rawDecisionResponse = rawFirstDecisionResponse;
            if (IsToolIterationLimitFallback(decisionResponse))
                decisionResponse = null;
            if (!IsToolIterationLimitFallback(rawFirstDecisionResponse) &&
                ShouldRunAutonomySelfCheck(instance, descriptor.SessionId, decisionResponse, decisionSession))
            {
                var selfCheckMessage = BuildAutonomySelfCheckMessage(
                    descriptor,
                    instance?.Snapshot().Controller.LastTerminalCommandStatus,
                    GetActiveTodos(instance, descriptor.SessionId),
                    decisionResponse);
                _logger?.LogInformation(
                    "NPC autonomy self-check request prepared; npc={NpcId}; trace={TraceId}; messageChars={MessageChars}",
                    descriptor.NpcId,
                    traceId,
                    selfCheckMessage.Length);
                var selfCheckStopwatch = System.Diagnostics.Stopwatch.StartNew();
                decisionResponse = await _agent.ChatAsync(
                    selfCheckMessage,
                    decisionSession,
                    ct);
                selfCheckStopwatch.Stop();
                _logger?.LogInformation(
                    "NPC autonomy self-check response received; npc={NpcId}; trace={TraceId}; responseChars={ResponseChars}; durationMs={DurationMs}",
                    descriptor.NpcId,
                    traceId,
                    decisionResponse?.Length ?? 0,
                    selfCheckStopwatch.ElapsedMilliseconds);
                rawDecisionResponse = decisionResponse;
            }
        }
        if (IsToolIterationLimitFallback(decisionResponse))
            decisionResponse = null;

        await WriteActivityAsync(descriptor, traceId, eventFacts, decisionResponse, ct);
        await WriteToolBudgetDiagnosticAsync(descriptor, traceId, rawDecisionResponse, ct);
        await WriteNarrativeMovementDiagnosticAsync(descriptor, traceId, decisionResponse, decisionSession, ct);

        await WriteNoToolActionDiagnosticAsync(instance, descriptor, traceId, decisionResponse, decisionSession, ct);

        await WriteSessionTaskContinuityEvidenceAsync(runtimeDriver, instance, descriptor, traceId, decisionSession, null, ct);

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
        NpcRuntimeActionChainGuardSnapshot? actionChainGuard = null,
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
            "如果只是查长动作进度，调用 stardew_task_status；默认不要填写 commandId，宿主会查询当前任务。\n" +
            "如果你只是想在原地做一个短暂可见的小动作，用 action=idle_micro_action，并提供 idleMicroAction.kind；可选 kind 只有 emote_happy、emote_question、emote_sleepy、emote_music、look_left、look_right、look_up、look_down、look_around、tiny_hop、tiny_shake、idle_pose、idle_animation_once。\n" +
            "idle_micro_action 只能表达原地短动作；不要同时附带 speech 或 target，也不要把它改写成 move 或 speak。\n" +
            "如果任务真的被外部条件阻断，用 taskUpdate 把已有 todo 标成 blocked；如果确定做不成，标成 failed；blocked 或 failed 都要写短 reason。\n" +
            "wait 只表示你现在选择暂不推进，不是普通世界动作。";
        var lastActionFact = BuildLastActionResultFact(lastTerminalCommandStatus);
        message += "\nMOVE TOOL CONTRACT: parent autonomy owns target resolution. Use skill_view to load stardew-navigation and the smallest relevant reference files, then call stardew_navigate_to_tile with the exact target(locationName,x,y,source). The host executes the real action and the tool result is the feedback channel.";
        var activeTodoFact = BuildActiveTodoContinuityFact(lastTerminalCommandStatus, activeTodos);
        if (activeTodoFact is not null)
            message += "\n" + activeTodoFact;
        var actionChainFact = BuildActionChainFact(actionChainGuard);
        if (actionChainFact is not null)
            message += "\n" + actionChainFact;
        var actionLoopFact = BuildActionLoopFact(actionChainGuard);
        if (actionLoopFact is not null)
            message += "\n" + actionLoopFact;
        return lastActionFact is null
            ? message
            : message + "\n" + lastActionFact;
    }

    private static string BuildAutonomySelfCheckMessage(
        NpcRuntimeDescriptor descriptor,
        GameCommandStatus? lastTerminalCommandStatus,
        IReadOnlyList<SessionTodoItem> activeTodos,
        string? previousResponse)
    {
        var commandId = string.IsNullOrWhiteSpace(lastTerminalCommandStatus?.CommandId)
            ? "-"
            : lastTerminalCommandStatus.CommandId;
        var action = string.IsNullOrWhiteSpace(lastTerminalCommandStatus?.Action)
            ? "-"
            : lastTerminalCommandStatus.Action;
        var todoText = string.Join("; ", activeTodos.Take(3).Select(todo => $"{todo.Id}:{todo.Status}:{todo.Content}"));
        var previousText = string.IsNullOrWhiteSpace(previousResponse) ? "-" : Truncate(previousResponse.Trim(), 300);

        return
            $"NPC: {descriptor.DisplayName} ({descriptor.NpcId})\n" +
            "自检：上一轮没有真实工具调用，所以宿主没有执行观察、移动、说话或任务更新。\n" +
            "JSON 文本不会执行；只有 assistant tool call 才会调用工具。\n" +
            $"上一轮真实动作结果：commandId={commandId}; action={action}; status={lastTerminalCommandStatus?.Status ?? "-"}。\n" +
            $"active todo={todoText}。\n" +
            $"上一轮文本={previousText}\n" +
            "现在请收口这个连续性状态：调用可见工具，例如 todo/todo_write、stardew_status、stardew_speak、stardew_idle_micro_action 或无参 stardew_task_status；" +
            "如果你决定暂不行动，必须回复 `no-action: <原因>` 或 `wait: <原因>`。不要再把 JSON 文本当工具调用。";
    }

    private static string? BuildActiveTodoContinuityFact(
        GameCommandStatus? lastTerminalCommandStatus,
        IReadOnlyList<SessionTodoItem>? activeTodos)
    {
        if (lastTerminalCommandStatus is null ||
            !TerminalStatusNames.Contains(lastTerminalCommandStatus.Status) ||
            !IsTaskContinuityTerminal(lastTerminalCommandStatus) ||
            activeTodos is null ||
            activeTodos.Count == 0)
        {
            return null;
        }

        var todoText = string.Join("; ", activeTodos.Take(3).Select(todo => $"{todo.Id}:{todo.Status}:{todo.Content}"));
        return "active todo continuity: 你有 active todo 与刚结束的 last_action_result 同时存在；" +
               $"active todo={todoText}。这是连续性事实，不是执行锁；请进行下一步行动：你需要自己决定标 completed/blocked/failed、继续新动作，或暂时等待。";
    }

    private static string? BuildLastActionResultFact(GameCommandStatus? status)
    {
        if (status is null)
            return null;

        var commandId = string.IsNullOrWhiteSpace(status.CommandId) ? "-" : status.CommandId;
        if (IsInteractionLifecycleTerminal(status))
        {
            var interactionAction = string.IsNullOrWhiteSpace(status.Action) ? "-" : status.Action;
            var interactionReason = status.ErrorCode ?? status.BlockedReason;
            var interactionFact = $"interaction_session: 上一轮交互窗口/会话状态已结束；commandId={commandId}; action={interactionAction}; status={status.Status}";
            return string.IsNullOrWhiteSpace(interactionReason)
                ? interactionFact + "。这是窗口/会话事实，不是真实世界动作结果，也不是下一步指令。"
                : interactionFact + $"; reason={interactionReason}。这是窗口/会话事实，不是真实世界动作结果，也不是下一步指令。";
        }

        if (IsActionSlotTimeout(status))
            return $"action_slot_timeout: 上一轮行动槽在完成前超时；commandId={commandId}; status={status.Status}。你自己决定下一步是观察、等待、换目标，还是用不同方式继续。";

        var action = string.IsNullOrWhiteSpace(status.Action) ? "-" : status.Action;
        var reason = status.ErrorCode ?? status.BlockedReason;
        var result = $"last_action_result: 上一轮真实动作已结束；commandId={commandId}; action={action}; status={status.Status}";
        return string.IsNullOrWhiteSpace(reason)
            ? result + "。这是宿主执行结果事实，不是下一步指令；请进行下一步行动。"
            : result + $"; reason={reason}。这是宿主执行结果事实，不是下一步指令；请进行下一步行动。";
    }

    private static string? BuildActionChainFact(NpcRuntimeActionChainGuardSnapshot? chain)
    {
        if (chain is null)
            return null;

        var reason = chain.BlockedReasonCode ?? chain.LastReasonCode ?? "none";
        var status = string.IsNullOrWhiteSpace(chain.LastTerminalStatus)
            ? chain.GuardStatus
            : chain.LastTerminalStatus;
        var correlation = FormatActionCorrelation(chain);
        return string.IsNullOrWhiteSpace(correlation)
            ? $"action_chain: chainId={chain.ChainId}; status={status}; guard={chain.GuardStatus}; actions={chain.ConsecutiveActions}; failures={chain.ConsecutiveFailures}; sameActionFailures={chain.ConsecutiveSameActionFailures}; closureMissing={chain.ClosureMissingCount}; deferredIngress={chain.DeferredIngressAttempts}; reason={reason}。这是历史诊断事实，不是执行锁，也不是下一步指令。"
            : $"action_chain: chainId={chain.ChainId}; status={status}; guard={chain.GuardStatus}; actions={chain.ConsecutiveActions}; failures={chain.ConsecutiveFailures}; sameActionFailures={chain.ConsecutiveSameActionFailures}; closureMissing={chain.ClosureMissingCount}; deferredIngress={chain.DeferredIngressAttempts}; reason={reason}; {correlation}。这是历史诊断事实，不是执行锁，也不是下一步指令。";
    }

    private static string FormatActionCorrelation(NpcRuntimeActionChainGuardSnapshot? chain)
    {
        if (chain is null)
            return string.Empty;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(chain.RootTodoId))
            parts.Add($"rootTodoId={SanitizePromptFact(chain.RootTodoId)}");
        if (!string.IsNullOrWhiteSpace(chain.RootTraceId))
            parts.Add($"rootTraceId={SanitizePromptFact(chain.RootTraceId)}");
        if (!string.IsNullOrWhiteSpace(chain.ConversationId))
            parts.Add($"conversationId={SanitizePromptFact(chain.ConversationId)}");
        if (!string.IsNullOrWhiteSpace(chain.LastTargetKey))
            parts.Add($"lastTarget={SanitizePromptFact(chain.LastTargetKey)}");
        return string.Join("; ", parts);
    }

    private static string SanitizePromptFact(string value)
        => value.Replace('\r', ' ').Replace('\n', ' ').Trim();

    private static string? BuildActionLoopFact(NpcRuntimeActionChainGuardSnapshot? chain)
    {
        if (chain is null || !IsActionLoop(chain))
            return null;

        var action = string.IsNullOrWhiteSpace(chain.LastAction) ? "-" : chain.LastAction;
        var targetKey = string.IsNullOrWhiteSpace(chain.LastTargetKey) ? "-" : chain.LastTargetKey;
        var reason = chain.BlockedReasonCode ?? chain.LastReasonCode ?? "none";
        return $"action_loop: chainId={chain.ChainId}; action={action}; targetKey={targetKey}; sameActionFailures={chain.ConsecutiveSameActionFailures}; failures={chain.ConsecutiveFailures}; reason={reason}。这是重复失败事实，不是下一步指令；不要继续同动作同目标硬重试。";
    }

    private static bool IsActionLoop(NpcRuntimeActionChainGuardSnapshot chain)
        => chain.ConsecutiveSameActionFailures >= 2 ||
           string.Equals(chain.BlockedReasonCode, StardewBridgeErrorCodes.ActionLoop, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(chain.LastReasonCode, StardewBridgeErrorCodes.ActionLoop, StringComparison.OrdinalIgnoreCase);

    private static bool IsActionSlotTimeout(GameCommandStatus status)
        => string.Equals(status.ErrorCode, StardewBridgeErrorCodes.ActionSlotTimeout, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status.BlockedReason, StardewBridgeErrorCodes.ActionSlotTimeout, StringComparison.OrdinalIgnoreCase);

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

        var requiresTaskContinuityDecision = RequiresTaskContinuityDecision(instance, descriptor.SessionId);
        if (requiresTaskContinuityDecision)
        {
            if (!string.IsNullOrWhiteSpace(decisionResponse) &&
                TryExtractExplicitNoActionReason(decisionResponse, out var reason))
            {
                await WriteTaskContinuityAsync(
                    descriptor,
                    traceId,
                    "task_continuity_no_action",
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
                "task_continuity_unresolved",
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
            null,
            instance,
            descriptor,
            traceId,
            null,
            instance.Snapshot().Controller.LastTerminalCommandStatus,
            ct);
    }

    private async Task WriteSessionTaskContinuityEvidenceAsync(
        NpcRuntimeDriver? runtimeDriver,
        NpcRuntimeInstance? instance,
        NpcRuntimeDescriptor descriptor,
        string traceId,
        Session? decisionSession,
        GameCommandStatus? controllerTerminalStatus,
        CancellationToken ct)
    {
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
            if (!evidence.HasCommandIdentity)
            {
                await WriteTaskContinuityAsync(
                    descriptor,
                    traceId,
                    "action_result_unresolved",
                    "diagnostic",
                    "missing_command_id",
                    null,
                    call.Name,
                    ct);
                continue;
            }

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
                !TryReadTodoWriteResult(todoMessage.Content, out var todoId, out var status, out var reason))
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

    private static bool TryReadTodoWriteResult(string content, out string? todoId, out string status, out string? reason)
    {
        todoId = null;
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

                todoId = ReadString(todo, "id");
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

    private static bool ShouldRunAutonomySelfCheck(
        NpcRuntimeInstance? instance,
        string sessionId,
        string? decisionResponse,
        Session? decisionSession)
        => RequiresTaskContinuityDecision(instance, sessionId) &&
           !HasAnyStardewActionToolCall(decisionSession) &&
           !HasAnyTodoToolCall(decisionSession) &&
           (string.IsNullOrWhiteSpace(decisionResponse) ||
            !TryExtractExplicitNoActionReason(decisionResponse, out _));

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

    private sealed record ToolResultEvidence(string? CommandId, GameCommandStatus? TerminalStatus)
    {
        public bool HasCommandIdentity =>
            !string.IsNullOrWhiteSpace(CommandId) ||
            !string.IsNullOrWhiteSpace(TerminalStatus?.CommandId);
    }

    private static bool HasToolCall(Session? session, string toolName)
    {
        if (session is null)
            return false;

        return session.Messages.Any(message =>
            string.Equals(message.ToolName, toolName, StringComparison.OrdinalIgnoreCase) ||
            (message.ToolCalls?.Any(toolCall => string.Equals(toolCall.Name, toolName, StringComparison.OrdinalIgnoreCase)) ?? false));
    }

    private static bool RequiresTaskContinuityDecision(NpcRuntimeInstance? instance, string sessionId)
        => instance?.Snapshot().Controller.LastTerminalCommandStatus is { } status &&
           TerminalStatusNames.Contains(status.Status) &&
           IsTaskContinuityTerminal(status) &&
           GetActiveTodos(instance, sessionId).Count > 0;

    private static bool IsTaskContinuityTerminal(GameCommandStatus status)
    {
        if (IsInteractionLifecycleTerminal(status))
            return false;

        return status.Action switch
        {
            var action when string.Equals(action, "move", StringComparison.OrdinalIgnoreCase) => true,
            var action when string.Equals(action, "speak", StringComparison.OrdinalIgnoreCase) => true,
            var action when string.Equals(action, "idle_micro_action", StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        };
    }

    private static bool IsInteractionLifecycleTerminal(GameCommandStatus status)
        => StartsWithOrdinalIgnoreCase(status.CommandId, "work_private_chat:") ||
           StartsWithOrdinalIgnoreCase(status.CommandId, "work_private_chat_reply:") ||
           StartsWithOrdinalIgnoreCase(status.CommandId, "private_chat:") ||
           StartsWithOrdinalIgnoreCase(status.CommandId, "private_chat_reply:") ||
           string.Equals(status.Action, "open_private_chat", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status.Action, "private_chat", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status.Action, "private_chat_reply", StringComparison.OrdinalIgnoreCase);

    private static bool StartsWithOrdinalIgnoreCase(string? value, string prefix)
        => !string.IsNullOrWhiteSpace(value) &&
           value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

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

    private static bool HasAnyStardewActionToolCall(Session? session)
        => HasToolCall(session, "stardew_navigate_to_tile") ||
           HasToolCall(session, "stardew_speak") ||
           HasToolCall(session, "stardew_open_private_chat") ||
           HasToolCall(session, "stardew_idle_micro_action");

    private static bool HasAnyTodoToolCall(Session? session)
        => HasToolCall(session, "todo") ||
           HasToolCall(session, "todo_write");

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
