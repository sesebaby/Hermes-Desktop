using Hermes.Agent.Core;
using Hermes.Agent.Game;
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
        return await RunOneTickForInstanceAsync(instance, observation, eventBatch, ct);
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
        return await RunOneTickCoreAsync(null, descriptor, observation, eventBatch, ct);
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
            decisionSession.State[StardewAutonomySessionKeys.IsAutonomyTurn] = true;
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

        if (_localExecutorRunner is not null && !string.IsNullOrWhiteSpace(decisionResponse))
        {
            var route = await RunLocalExecutorAsync(
                instance,
                descriptor,
                currentFacts,
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
            await WriteNoToolActionDiagnosticAsync(descriptor, traceId, decisionResponse, decisionSession, ct);
        }

        await WriteSessionTaskContinuityEvidenceAsync(descriptor, traceId, decisionSession, null, ct);

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
            "你现在要决定下一步自主行动。先看当前观察事实和 active todo，再决定是推进任务、观察当前状态，还是回应玩家。\n" +
            "如果玩家给过的约定还没完成，要优先考虑怎么继续；被玩家打断时先回应玩家，再恢复原来的任务。\n" +
            "如果只是需要稍后继续，不要把 todo 标成 blocked，也不要反复输出 wait；保持任务 pending/in_progress，并用 schedule_cron 工具预约下一次继续。\n" +
            "低风险动作只输出一个 JSON object 交给本地执行层，不要直接写工具参数或假装已经做完。\n" +
            "必须只输出 raw JSON object；不要 Markdown code fence，不要解释文字，不要在 JSON 前后添加任何自然语言。\n" +
            "JSON schema 固定为 {\"action\":\"move|observe|wait|task_status|idle_micro_action|escalate\",\"reason\":\"short reason\",\"destinationId\":\"optional semantic move\",\"target\":{\"locationName\":\"required for mechanical move\",\"x\":0,\"y\":0,\"facingDirection\":\"optional\",\"source\":\"required disclosed map skill id\"},\"commandId\":\"optional for task_status\",\"observeTarget\":\"optional for observe\",\"waitReason\":\"optional for wait\",\"idleMicroAction\":{\"kind\":\"required for idle_micro_action\",\"animationAlias\":\"optional only when kind=idle_animation_once\",\"intensity\":\"optional\",\"ttlSeconds\":0},\"speech\":{\"shouldSpeak\":false,\"channel\":\"player|overhead|private\",\"text\":\"optional short line\"},\"taskUpdate\":{\"taskId\":\"optional existing todo id\",\"status\":\"pending|in_progress|blocked|completed|failed|cancelled\",\"reason\":\"optional short reason\"},\"escalate\":false}。\n" +
            "只输出所选 action 需要的字段；不要输出 null、空字符串或无关字段，尤其不要在非 escalate 动作里输出 escalate=false。\n" +
            "如果需要移动，二选一：语义移动用 destinationId，必须复制当前事实里的 destinationId；机械坐标移动用完整 target(locationName,x,y,source)，必须来自已披露地图 skill，不要猜坐标。\n" +
            "机械 target 只表达父层决策；本地 executor 会用 executor-only stardew_navigate_to_tile 执行，父层不要写工具参数或调用该工具。\n" +
            "如果只是查长动作进度，action=task_status 且 commandId 必须来自已有命令。\n" +
            "如果 NPC 暂时不该移动、也不需要说话，只是在原地做一个短暂可见的小动作，优先用 action=idle_micro_action，并提供 idleMicroAction.kind；可选 kind 只有 emote_happy、emote_question、emote_sleepy、emote_music、look_left、look_right、look_up、look_down、look_around、tiny_hop、tiny_shake、idle_pose、idle_animation_once。\n" +
            "idle_micro_action 只能表达原地短动作；不要同时附带 speech、destinationId 或 target，也不要把它改写成 move 或 speak。\n" +
            "如果任务真的被外部条件阻断，用 taskUpdate 把已有 todo 标成 blocked；如果确定做不成，标成 failed；blocked 或 failed 都要写短 reason。\n" +
            "wait 只作为没有可推进行动、没有可查询命令、也没有必要预约时的兜底调度意图；不要把 wait 当普通世界动作。\n" +
            "如果这是答应玩家的事，能说话时用 speech 字段告诉玩家卡在哪里；不要调用或编写工具参数。\n" +
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
            "registered_tools=0;stardew_move=0;stardew_task_status=0;stardew_speak=0;todo=0;agent=0",
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
