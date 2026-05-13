namespace Hermes.Agent.Games.Stardew;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.Runtime;
using Microsoft.Extensions.Logging;

public static class StardewNpcToolFactory
{
    public static string DefaultParentToolFingerprint
        => StardewNpcToolSurfacePolicy.Default.ParentToolFingerprint;

    public static IReadOnlyList<ITool> CreateDefault(
        IGameAdapter adapter,
        NpcRuntimeDescriptor descriptor,
        Func<string>? traceIdFactory = null,
        Func<string>? idempotencyKeyFactory = null,
        int maxStatusPolls = 3,
        NpcRuntimeDriver? runtimeDriver = null,
        WorldCoordinationService? worldCoordination = null,
        IStardewRecentActivityProvider? recentActivityProvider = null,
        ILogger? logger = null,
        Func<DateTime>? nowUtc = null,
        TimeSpan? actionTimeout = null,
        StardewNpcToolSurfacePolicy? toolSurfacePolicy = null,
        NpcActionChainGuardOptions? actionChainGuardOptions = null)
    {
        traceIdFactory ??= () => $"trace_{descriptor.NpcId}_{Guid.NewGuid():N}";
        idempotencyKeyFactory ??= () => $"idem_{descriptor.NpcId}_{Guid.NewGuid():N}";
        var runtimeActions = new StardewRuntimeActionController(
            runtimeDriver,
            worldCoordination,
            nowUtc,
            actionTimeout,
            actionChainGuardOptions);

        return (toolSurfacePolicy ?? StardewNpcToolSurfacePolicy.Default).ApplyToParent(
        [
            new StardewStatusTool(adapter.Queries, descriptor),
            new StardewPlayerStatusTool(adapter.Queries, descriptor),
            new StardewProgressStatusTool(adapter.Queries, descriptor),
            new StardewSocialStatusTool(adapter.Queries, descriptor),
            new StardewQuestStatusTool(adapter.Queries, descriptor),
            new StardewFarmStatusTool(adapter.Queries, descriptor),
            new StardewRecentActivityTool(recentActivityProvider, descriptor, logger),
            new StardewNavigateToTileTool(adapter.Commands, descriptor, traceIdFactory, idempotencyKeyFactory, maxStatusPolls, runtimeActions),
            new StardewSpeakTool(adapter.Commands, descriptor, traceIdFactory, idempotencyKeyFactory, maxStatusPolls, runtimeActions),
            new StardewOpenPrivateChatTool(adapter.Commands, descriptor, traceIdFactory, idempotencyKeyFactory, maxStatusPolls, runtimeActions),
            new StardewIdleMicroActionTool(adapter.Commands, descriptor, traceIdFactory, idempotencyKeyFactory, maxStatusPolls, runtimeActions),
            new StardewTaskStatusTool(adapter.Commands)
        ]);
    }

    private sealed class ToolFingerprintProbe(string name) : ITool
    {
        public string Name { get; } = name;
        public string Description => "fingerprint probe";
        public Type ParametersType => typeof(NoopParameters);
        public Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
            => Task.FromResult(ToolResult.Fail("fingerprint probe is not executable"));
    }

    private sealed class NoopParameters
    {
    }
}

public sealed class StardewSubmitHostTaskTool : ITool, IToolSchemaProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "move",
        "craft",
        "trade",
        "quest",
        "gather"
    };

    private readonly NpcRuntimeDescriptor _descriptor;
    private readonly NpcRuntimeDriver _runtimeDriver;
    private readonly ILogger? _logger;

    public StardewSubmitHostTaskTool(
        NpcRuntimeDescriptor descriptor,
        NpcRuntimeDriver runtimeDriver,
        ILogger? logger = null)
    {
        _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _runtimeDriver = runtimeDriver ?? throw new ArgumentNullException(nameof(runtimeDriver));
        _logger = logger;
    }

    public string Name => "stardew_submit_host_task";

    public string Description => "仅限私聊父 agent 使用。玩家要求现在就做现实世界动作且你决定答应时，先调用本工具提交 host task，让宿主后续按同一 host task lifecycle 执行，再自然回复玩家。只口头答应不会发生动作。action=move 时必须先用 skill_view 读取 stardew-navigation 分层资料，并把已加载 POI 给出的 target(locationName,x,y,source) 原样传入 target；不要使用 destinationId 或编造坐标。";

    public Type ParametersType => typeof(StardewSubmitHostTaskToolParameters);

    public JsonElement GetParameterSchema()
        => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    @enum = new[] { "move", "craft", "trade", "quest", "gather" },
                    description = "要提交给 host task runner 的行动类型。move 是当前支持的真实动作；craft/trade/quest/gather 是未来窗口任务骨架，当前会进入 host task lifecycle 并返回 unsupported/blocked fact，不会执行游戏写操作。"
                },
                reason = new
                {
                    type = "string",
                    description = "你为什么接受这个立即行动 host task 的简短原因。"
                },
                intentText = new
                {
                    type = "string",
                    description = "可选字段。玩家原话或一句自然语言意图；action=move 的执行目标以 target 为准。"
                },
                target = new
                {
                    type = "object",
                    description = "action=move 必填。必须来自已加载 stardew-navigation POI/reference 的机械目标；不要编造。",
                    properties = new
                    {
                        locationName = new { type = "string", description = "已加载 POI/reference 给出的地图名。" },
                        x = new { type = "integer", description = "已加载 POI/reference 给出的 tile X。" },
                        y = new { type = "integer", description = "已加载 POI/reference 给出的 tile Y。" },
                        source = new { type = "string", description = "披露该坐标的已加载 skill reference。" },
                        facingDirection = new { type = "integer", description = "可选朝向。" }
                    },
                    required = new[] { "locationName", "x", "y", "source" }
                },
                conversationId = new
                {
                    type = "string",
                    description = "可选。已知时填写当前私聊 conversation id。"
                }
            },
            required = new[] { "action", "reason" }
        });

    public async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (StardewSubmitHostTaskToolParameters)parameters;
        if (string.IsNullOrWhiteSpace(p.Action) ||
            !AllowedActions.Contains(p.Action))
        {
            return ToolResult.Fail("unsupported action");
        }

        if (string.IsNullOrWhiteSpace(p.Reason))
            return ToolResult.Fail("reason is required");

        var traceId = $"trace_host_task_{_descriptor.NpcId}_{Guid.NewGuid():N}";
        var workItemId = $"ingress_host_task_{_descriptor.NpcId}_{Guid.NewGuid():N}";
        var action = p.Action.Trim();
        if (string.Equals(action, "move", StringComparison.OrdinalIgnoreCase) &&
            !TryValidateMoveTarget(p.Target, out var targetError))
        {
            return ToolResult.Fail(targetError);
        }

        var conversationId = string.IsNullOrWhiteSpace(p.ConversationId)
            ? InferConversationId(p.CurrentSessionId)
            : p.ConversationId.Trim();
        var rootTodoId = InferRootTodoId();
        var payload = new JsonObject
        {
            ["action"] = action,
            ["reason"] = p.Reason.Trim()
        };
        if (p.Target is not null)
        {
            payload["target"] = new JsonObject
            {
                ["locationName"] = p.Target.LocationName.Trim(),
                ["x"] = p.Target.X,
                ["y"] = p.Target.Y,
                ["source"] = p.Target.Source.Trim()
            };
            if (p.Target.FacingDirection is not null)
                ((JsonObject)payload["target"]!)["facingDirection"] = p.Target.FacingDirection.Value;
        }
        if (!string.IsNullOrWhiteSpace(p.IntentText))
            payload["intentText"] = p.IntentText.Trim();
        if (!string.IsNullOrWhiteSpace(conversationId))
            payload["conversationId"] = conversationId;
        if (!string.IsNullOrWhiteSpace(rootTodoId))
            payload["rootTodoId"] = rootTodoId;

        await _runtimeDriver.EnqueueIngressWorkItemAsync(
            new NpcRuntimeIngressWorkItemSnapshot(
                workItemId,
                "stardew_host_task_submission",
                "queued",
                DateTime.UtcNow,
                $"idem_host_task_{_descriptor.NpcId}_{Guid.NewGuid():N}",
                traceId,
                payload),
            ct);
        _runtimeDriver.Instance.SetInboxDepth(_runtimeDriver.Snapshot().IngressWorkItems.Count);

        await new NpcRuntimeLogWriter(Path.Combine(_runtimeDriver.Instance.Namespace.ActivityPath, "runtime.jsonl")).WriteAsync(
            new NpcRuntimeLogRecord(
                DateTime.UtcNow,
                traceId,
                _descriptor.NpcId,
                _descriptor.GameId,
                _descriptor.SessionId,
                "host_task_submission",
                "stardew_submit_host_task",
                "queued",
                $"workItemId={workItemId};action={action};conversationId={conversationId ?? "-"}"),
            ct);

        _logger?.LogInformation(
            "Queued Stardew host task submission ingress; npc={NpcId}; trace={TraceId}; workItemId={WorkItemId}; action={Action}; conversationId={ConversationId}",
            _descriptor.NpcId,
            traceId,
            workItemId,
            action,
            conversationId ?? "-");

        return ToolResult.Ok(JsonSerializer.Serialize(new
        {
            queued = true,
            workItemId,
            traceId,
            action,
            conversationId,
            rootTodoId
        }, JsonOptions));
    }

    private static bool TryValidateMoveTarget(StardewSubmitHostTaskMoveTargetParameters? target, out string error)
    {
        if (target is null)
        {
            error = "target is required for move";
            return false;
        }

        if (string.IsNullOrWhiteSpace(target.LocationName))
        {
            error = "target.locationName is required for move";
            return false;
        }

        if (string.IsNullOrWhiteSpace(target.Source))
        {
            error = "target.source is required for move";
            return false;
        }

        error = "";
        return true;
    }

    private static string? InferConversationId(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return null;

        const string marker = ":private_chat:";
        var index = sessionId.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? null : sessionId[(index + marker.Length)..];
    }

    private string? InferRootTodoId()
    {
        if (!_runtimeDriver.Instance.TryGetTaskView(_descriptor.SessionId, out var taskView) ||
            taskView is null)
        {
            return null;
        }

        var todo = taskView.ActiveSnapshot.Todos.FirstOrDefault(item =>
                       string.Equals(item.Status, "in_progress", StringComparison.OrdinalIgnoreCase)) ??
                   taskView.ActiveSnapshot.Todos.FirstOrDefault(item =>
                       string.Equals(item.Status, "pending", StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(todo?.Id) ? null : todo.Id;
    }
}

public sealed class StardewSubmitHostTaskToolParameters : ISessionAwareToolParameters
{
    public required string Action { get; init; }

    public required string Reason { get; init; }

    public string? IntentText { get; init; }

    public StardewSubmitHostTaskMoveTargetParameters? Target { get; init; }

    public string? DestinationText { get; init; }

    public string? ConversationId { get; init; }

    [JsonIgnore]
    public string? CurrentSessionId { get; set; }
}

public sealed class StardewSubmitHostTaskMoveTargetParameters
{
    public required string LocationName { get; init; }

    public required int X { get; init; }

    public required int Y { get; init; }

    public required string Source { get; init; }

    public int? FacingDirection { get; init; }
}

public sealed class NpcNoWorldActionTool : ITool, IToolSchemaProvider
{
    private readonly ILogger? _logger;

    public NpcNoWorldActionTool(ILogger? logger = null)
    {
        _logger = logger;
    }

    public string Name => "npc_no_world_action";

    public string Description => "仅限私聊父 agent 使用。当前私聊轮次不需要立即改变游戏世界时，调用本工具声明无世界动作，然后自然回复玩家。不要用纯文本省略这个声明。";

    public Type ParametersType => typeof(NpcNoWorldActionToolParameters);

    public JsonElement GetParameterSchema()
        => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                reason = new
                {
                    type = "string",
                    description = "为什么这轮不需要立即改变游戏世界的简短原因。"
                }
            },
            required = new[] { "reason" }
        });

    public Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (NpcNoWorldActionToolParameters)parameters;
        if (string.IsNullOrWhiteSpace(p.Reason))
            return Task.FromResult(ToolResult.Fail("reason is required"));

        _logger?.LogInformation("NPC private-chat no-world action declared; reason={Reason}", p.Reason.Trim());

        return Task.FromResult(ToolResult.Ok(JsonSerializer.Serialize(new
        {
            noWorldAction = true,
            reason = p.Reason.Trim()
        })));
    }
}

public sealed class NpcNoWorldActionToolParameters
{
    public required string Reason { get; init; }
}

public sealed record StardewNpcToolSurfacePolicy(
    IReadOnlyList<string> ParentToolNames)
{
    private static readonly string[] ParentCatalog =
    [
        "stardew_status",
        "stardew_player_status",
        "stardew_progress_status",
        "stardew_social_status",
        "stardew_quest_status",
        "stardew_farm_status",
        "stardew_recent_activity",
        "stardew_navigate_to_tile",
        "stardew_speak",
        "stardew_open_private_chat",
        "stardew_idle_micro_action",
        "stardew_task_status"
    ];

    public static StardewNpcToolSurfacePolicy Default { get; } = new(ParentCatalog);

    public static StardewNpcToolSurfacePolicy Create(
        IEnumerable<string>? parentToolNames = null)
        => new(Normalize(parentToolNames ?? ParentCatalog));

    public IReadOnlyList<ITool> ApplyToParent(IEnumerable<ITool> tools)
        => Apply("parent", tools, ParentToolNames, ParentCatalog);

    public string ParentToolFingerprint
        => string.Join("|", ParentToolNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase));

    private static IReadOnlyList<ITool> Apply(
        string surfaceName,
        IEnumerable<ITool> tools,
        IReadOnlyList<string> allowedToolNames,
        IReadOnlyCollection<string> knownToolNames)
    {
        var toolList = tools.ToArray();
        ValidateKnown(surfaceName, allowedToolNames, knownToolNames);
        var allowed = new HashSet<string>(allowedToolNames, StringComparer.OrdinalIgnoreCase);
        return toolList.Where(tool => allowed.Contains(tool.Name)).ToArray();
    }

    private static void ValidateKnown(
        string surfaceName,
        IReadOnlyList<string> allowedToolNames,
        IReadOnlyCollection<string> knownToolNames)
    {
        var known = new HashSet<string>(knownToolNames, StringComparer.OrdinalIgnoreCase);
        var unknown = allowedToolNames
            .Where(name => !known.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (unknown.Length > 0)
            throw new ArgumentException($"Unknown Stardew {surfaceName} tool(s): {string.Join(", ", unknown)}.");
    }

    private static IReadOnlyList<string> Normalize(IEnumerable<string> toolNames)
        => toolNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

public interface IStardewRecentActivityProvider
{
    Task<StardewStatusFactResponseData> ReadRecentActivityAsync(NpcRuntimeDescriptor descriptor, CancellationToken ct);
}

public sealed class StardewRecentActivityProvider : IStardewRecentActivityProvider
{
    private readonly NpcObservationFactStore _factStore;
    private readonly NpcRuntimeDriver _runtimeDriver;

    public StardewRecentActivityProvider(NpcObservationFactStore factStore, NpcRuntimeDriver runtimeDriver)
    {
        _factStore = factStore;
        _runtimeDriver = runtimeDriver;
    }

    public Task<StardewStatusFactResponseData> ReadRecentActivityAsync(NpcRuntimeDescriptor descriptor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var snapshot = _runtimeDriver.Snapshot();
        var facts = _factStore.Snapshot(descriptor)
            .TakeLast(5)
            .Select((fact, index) =>
            {
                var details = fact.Facts.Count == 0
                    ? "-"
                    : string.Join("|", fact.Facts.Select(SanitizeFact));
                return $"recent[{index}]={fact.SourceKind}:{SanitizeFact(fact.Summary)};facts={details}";
            })
            .ToList();

        if (snapshot.LastTerminalCommandStatus is { } last)
        {
            if (IsInteractionLifecycleTerminal(last))
            {
                facts.Add($"lastInteraction={last.Action}:{last.Status}:{last.ErrorCode ?? last.BlockedReason ?? "none"}");
            }
            else
            {
                if (FormatTaskLifecycleFact(last, snapshot.ActionChainGuard) is { } lifecycleFact)
                    facts.Add(lifecycleFact);

                var correlation = FormatActionCorrelation(snapshot.ActionChainGuard);
                facts.Add(string.IsNullOrWhiteSpace(correlation)
                    ? $"lastAction={last.Action}:{last.Status}:{last.ErrorCode ?? last.BlockedReason ?? "none"}"
                    : $"lastAction={last.Action}:{last.Status}:{last.ErrorCode ?? last.BlockedReason ?? "none"};{correlation}");
            }
        }

        if (snapshot.ActionChainGuard is { } chain)
        {
            facts.Add(FormatActionChainFact(chain));
            if (FormatTaskStuckFact(chain) is { } stuckFact)
                facts.Add(stuckFact);
            if (FormatActionLoopFact(chain) is { } loopFact)
                facts.Add(loopFact);
        }

        if (_runtimeDriver.Instance.TryGetTaskView(descriptor.SessionId, out var taskView) && taskView is not null)
        {
            foreach (var (todo, index) in taskView.ActiveSnapshot.Todos
                         .Where(todo => todo.Status is "pending" or "in_progress")
                         .Take(3)
                         .Select((todo, index) => (todo, index)))
            {
                facts.Add($"todo[{index}]={todo.Status}:{SanitizeFact(todo.Content)}");
            }
        }

        var unknownFields = facts.Count == 0 ? new[] { "recentFacts" } : Array.Empty<string>();
        var summary = facts.Count == 0
            ? "最近行动暂时没有可用记录。"
            : $"最近有 {facts.Count} 条连续性记录；需要恢复任务或避免重复时参考这些记录。";
        return Task.FromResult(new StardewStatusFactResponseData(
            summary,
            facts.Take(12).ToArray(),
            unknownFields.Length > 0 ? "degraded" : "completed",
            unknownFields));
    }

    private static string SanitizeFact(string value)
        => value.Replace('\r', ' ').Replace('\n', ' ').Trim();

    private static string FormatActionChainFact(NpcRuntimeActionChainGuardSnapshot chain)
    {
        var reason = chain.BlockedReasonCode ?? chain.LastReasonCode ?? "none";
        var status = string.IsNullOrWhiteSpace(chain.LastTerminalStatus)
            ? chain.GuardStatus
            : chain.LastTerminalStatus;
        var correlation = FormatActionCorrelation(chain);
        return string.IsNullOrWhiteSpace(correlation)
            ? $"action_chain: chainId={chain.ChainId}; status={status}; guard={chain.GuardStatus}; actions={chain.ConsecutiveActions}; failures={chain.ConsecutiveFailures}; sameActionFailures={chain.ConsecutiveSameActionFailures}; closureMissing={chain.ClosureMissingCount}; deferredIngress={chain.DeferredIngressAttempts}; reason={reason}"
            : $"action_chain: chainId={chain.ChainId}; status={status}; guard={chain.GuardStatus}; actions={chain.ConsecutiveActions}; failures={chain.ConsecutiveFailures}; sameActionFailures={chain.ConsecutiveSameActionFailures}; closureMissing={chain.ClosureMissingCount}; deferredIngress={chain.DeferredIngressAttempts}; reason={reason}; {correlation}";
    }

    private static string FormatActionCorrelation(NpcRuntimeActionChainGuardSnapshot? chain)
    {
        if (chain is null)
            return string.Empty;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(chain.RootTodoId))
            parts.Add($"rootTodoId={SanitizeFact(chain.RootTodoId)}");
        if (!string.IsNullOrWhiteSpace(chain.RootTraceId))
            parts.Add($"rootTraceId={SanitizeFact(chain.RootTraceId)}");
        if (!string.IsNullOrWhiteSpace(chain.ConversationId))
            parts.Add($"conversationId={SanitizeFact(chain.ConversationId)}");
        if (!string.IsNullOrWhiteSpace(chain.LastTargetKey))
            parts.Add($"lastTarget={SanitizeFact(chain.LastTargetKey)}");
        return string.Join("; ", parts);
    }

    private static string? FormatActionLoopFact(NpcRuntimeActionChainGuardSnapshot chain)
    {
        if (!IsActionLoop(chain))
            return null;

        var action = string.IsNullOrWhiteSpace(chain.LastAction) ? "-" : chain.LastAction;
        var targetKey = string.IsNullOrWhiteSpace(chain.LastTargetKey) ? "-" : chain.LastTargetKey;
        var reason = chain.BlockedReasonCode ?? chain.LastReasonCode ?? "none";
        return $"action_loop: chainId={chain.ChainId}; action={action}; targetKey={targetKey}; sameActionFailures={chain.ConsecutiveSameActionFailures}; failures={chain.ConsecutiveFailures}; reason={reason}";
    }

    private static string? FormatTaskStuckFact(NpcRuntimeActionChainGuardSnapshot chain)
    {
        if (!IsActionLoop(chain))
            return null;

        var action = string.IsNullOrWhiteSpace(chain.LastAction) ? "-" : SanitizeFact(chain.LastAction);
        var status = string.IsNullOrWhiteSpace(chain.LastTerminalStatus) ? chain.GuardStatus : chain.LastTerminalStatus;
        var reason = chain.BlockedReasonCode ?? chain.LastReasonCode ?? "none";
        var targetKey = string.IsNullOrWhiteSpace(chain.LastTargetKey) ? null : SanitizeFact(chain.LastTargetKey);
        var rootTodoId = string.IsNullOrWhiteSpace(chain.RootTodoId) ? null : SanitizeFact(chain.RootTodoId);
        var rootTraceId = string.IsNullOrWhiteSpace(chain.RootTraceId) ? null : SanitizeFact(chain.RootTraceId);
        var conversationId = string.IsNullOrWhiteSpace(chain.ConversationId) ? null : SanitizeFact(chain.ConversationId);
        return BuildTaskLifecycleFact("task_stuck", action, "-", status, reason, targetKey, rootTodoId, rootTraceId, conversationId, chain);
    }

    private static bool IsActionLoop(NpcRuntimeActionChainGuardSnapshot chain)
        => chain.ConsecutiveSameActionFailures >= 2 ||
           string.Equals(chain.BlockedReasonCode, StardewBridgeErrorCodes.ActionLoop, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(chain.LastReasonCode, StardewBridgeErrorCodes.ActionLoop, StringComparison.OrdinalIgnoreCase);

    private static string? FormatTaskLifecycleFact(
        GameCommandStatus status,
        NpcRuntimeActionChainGuardSnapshot? chain)
    {
        var action = string.IsNullOrWhiteSpace(status.Action) ? "-" : status.Action;
        var commandId = string.IsNullOrWhiteSpace(status.CommandId) ? "-" : status.CommandId;
        var reason = status.ErrorCode ?? status.BlockedReason ?? "none";
        var targetKey = string.IsNullOrWhiteSpace(chain?.LastTargetKey) ? null : SanitizeFact(chain.LastTargetKey);
        var rootTodoId = string.IsNullOrWhiteSpace(chain?.RootTodoId) ? null : SanitizeFact(chain.RootTodoId);
        var rootTraceId = string.IsNullOrWhiteSpace(chain?.RootTraceId) ? null : SanitizeFact(chain.RootTraceId);
        var conversationId = string.IsNullOrWhiteSpace(chain?.ConversationId) ? null : SanitizeFact(chain.ConversationId);

        if (string.Equals(status.Status, StardewCommandStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            return BuildTaskLifecycleFact("task_done", action, commandId, status.Status, reason, targetKey, rootTodoId, rootTraceId, conversationId, chain);

        if (string.Equals(status.Status, StardewCommandStatuses.Blocked, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status.Status, StardewCommandStatuses.Failed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status.Status, StardewCommandStatuses.Cancelled, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status.Status, StardewCommandStatuses.Interrupted, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status.Status, StardewCommandStatuses.Expired, StringComparison.OrdinalIgnoreCase))
        {
            return BuildTaskLifecycleFact("task_error", action, commandId, status.Status, reason, targetKey, rootTodoId, rootTraceId, conversationId, chain);
        }

        return null;
    }

    private static string BuildTaskLifecycleFact(
        string factKind,
        string action,
        string commandId,
        string status,
        string reason,
        string? targetKey,
        string? rootTodoId,
        string? rootTraceId,
        string? conversationId,
        NpcRuntimeActionChainGuardSnapshot? chain)
    {
        var parts = new List<string>
        {
            $"action={action}",
            $"commandId={commandId}",
            $"status={status}",
            $"reason={reason}"
        };

        if (!string.IsNullOrWhiteSpace(targetKey))
            parts.Add($"target={targetKey}");
        if (!string.IsNullOrWhiteSpace(rootTodoId))
            parts.Add($"rootTodoId={rootTodoId}");
        if (!string.IsNullOrWhiteSpace(rootTraceId))
            parts.Add($"rootTraceId={rootTraceId}");
        if (!string.IsNullOrWhiteSpace(conversationId))
            parts.Add($"conversationId={conversationId}");
        if (factKind == "task_stuck" && chain is not null)
            parts.Add($"sameActionFailures={chain.ConsecutiveSameActionFailures}");

        return $"{factKind}: {string.Join("; ", parts)}";
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
}

public sealed class StardewStatusTool : ITool, IToolSchemaProvider
{
    private readonly IGameQueryService _queries;
    private readonly NpcRuntimeDescriptor _descriptor;

    public StardewStatusTool(IGameQueryService queries, NpcRuntimeDescriptor descriptor)
    {
        _queries = queries;
        _descriptor = descriptor;
    }

    public string Name => "stardew_status";

    public string Description => "读取当前 NPC 的星露谷现场事实。这是被动观察工具，不会改变世界。";

    public Type ParametersType => typeof(StardewStatusToolParameters);

    public JsonElement GetParameterSchema() => StardewNpcToolSchemas.Empty();

    public async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
        => ToolResult.Ok(StardewNpcToolJson.Serialize(await _queries.ObserveAsync(_descriptor.EffectiveBodyBinding, ct)));
}

public abstract class StardewFactStatusToolBase : ITool, IToolSchemaProvider
{
    protected readonly IGameQueryService Queries;
    protected readonly NpcRuntimeDescriptor Descriptor;

    protected StardewFactStatusToolBase(IGameQueryService queries, NpcRuntimeDescriptor descriptor)
    {
        Queries = queries;
        Descriptor = descriptor;
    }

    public abstract string Name { get; }

    public abstract string Description { get; }

    public virtual Type ParametersType => typeof(StardewStatusToolParameters);

    public virtual JsonElement GetParameterSchema() => StardewNpcToolSchemas.Empty();

    public abstract Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct);

    protected static ToolResult Serialize(StardewStatusFactResponseData data)
        => ToolResult.Ok(StardewNpcToolJson.Serialize(data));

    protected StardewQueryService RequireStardewQueries()
        => Queries as StardewQueryService
           ?? throw new InvalidOperationException("This Stardew status tool requires StardewQueryService.");
}

public sealed class StardewPlayerStatusTool : StardewFactStatusToolBase
{
    public StardewPlayerStatusTool(IGameQueryService queries, NpcRuntimeDescriptor descriptor) : base(queries, descriptor) { }

    public override string Name => "stardew_player_status";

    public override string Description => "读取玩家状态报告：玩家位置、手持物、金钱、基础穿着装备、体力生命、配偶和简短背包摘要。普通自主回合最多额外查一个状态工具。";

    public override async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
        => Serialize(await RequireStardewQueries().GetPlayerStatusAsync(Descriptor.EffectiveBodyBinding, ct));
}

public sealed class StardewProgressStatusTool : StardewFactStatusToolBase
{
    public StardewProgressStatusTool(IGameQueryService queries, NpcRuntimeDescriptor descriptor) : base(queries, descriptor) { }

    public override string Name => "stardew_progress_status";

    public override string Description => "读取游戏进度报告：年份、季节、日期、时间、农场名、金钱、技能等级、矿井深度和房屋升级。只在需要游戏阶段语境时使用。";

    public override async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
        => Serialize(await RequireStardewQueries().GetProgressStatusAsync(Descriptor.EffectiveBodyBinding, ct));
}

public sealed class StardewSocialStatusTool : StardewFactStatusToolBase
{
    public StardewSocialStatusTool(IGameQueryService queries, NpcRuntimeDescriptor descriptor) : base(queries, descriptor) { }

    public override string Name => "stardew_social_status";

    public override string Description => "读取当前 NPC 或指定 NPC 的社交报告：好感心数、今日是否交谈、送礼次数、婚姻和配偶状态。只在需要关系语境时使用。";

    public override Type ParametersType => typeof(StardewSocialStatusToolParameters);

    public override JsonElement GetParameterSchema() => StardewNpcToolSchemas.SocialStatus();

    public override async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (StardewSocialStatusToolParameters)parameters;
        return Serialize(await RequireStardewQueries().GetSocialStatusAsync(Descriptor.EffectiveBodyBinding, p.TargetNpcId, ct));
    }
}

public sealed class StardewQuestStatusTool : StardewFactStatusToolBase
{
    public StardewQuestStatusTool(IGameQueryService queries, NpcRuntimeDescriptor descriptor) : base(queries, descriptor) { }

    public override string Name => "stardew_quest_status";

    public override string Description => "读取玩家任务报告，最多包含五个可见任务。只在需要玩家任务或 quest 语境时使用。";

    public override async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
        => Serialize(await RequireStardewQueries().GetQuestStatusAsync(Descriptor.EffectiveBodyBinding, ct));
}

public sealed class StardewFarmStatusTool : StardewFactStatusToolBase
{
    public StardewFarmStatusTool(IGameQueryService queries, NpcRuntimeDescriptor descriptor) : base(queries, descriptor) { }

    public override string Name => "stardew_farm_status";

    public override string Description => "读取农场报告：农场名、日期、天气、金钱，以及高成本作物/动物扫描的 degraded 占位信息。只在需要农场语境时使用。";

    public override async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
        => Serialize(await RequireStardewQueries().GetFarmStatusAsync(Descriptor.EffectiveBodyBinding, ct));
}

public sealed class StardewRecentActivityTool : ITool, IToolSchemaProvider
{
    private readonly IStardewRecentActivityProvider? _provider;
    private readonly NpcRuntimeDescriptor _descriptor;
    private readonly ILogger? _logger;

    public StardewRecentActivityTool(IStardewRecentActivityProvider? provider, NpcRuntimeDescriptor descriptor, ILogger? logger = null)
    {
        _provider = provider;
        _descriptor = descriptor;
        _logger = logger;
    }

    public string Name => "stardew_recent_activity";

    public string Description => "读取当前 NPC runtime 的最近连续性报告：最近观察/事件、上一行动状态和 active todo。这不是世界状态查询，只用于避免重复或恢复旧任务。";

    public Type ParametersType => typeof(StardewStatusToolParameters);

    public JsonElement GetParameterSchema() => StardewNpcToolSchemas.Empty();

    public async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        if (_provider is null)
        {
            var degraded = new StardewStatusFactResponseData(
                "最近行动暂时不可用：当前运行时没有注入 recent activity provider。",
                [
                    "status=degraded",
                    "unknownFields=recentObservation|lastAction|activeTodos"
                ],
                "degraded",
                ["recentActivityProvider"]);
            stopwatch.Stop();
            LogRecentActivityCompleted(degraded, stopwatch.ElapsedMilliseconds);
            return ToolResult.Ok(StardewNpcToolJson.Serialize(degraded));
        }

        var data = await _provider.ReadRecentActivityAsync(_descriptor, ct);
        stopwatch.Stop();
        LogRecentActivityCompleted(data, stopwatch.ElapsedMilliseconds);
        return ToolResult.Ok(StardewNpcToolJson.Serialize(data));
    }

    private void LogRecentActivityCompleted(StardewStatusFactResponseData data, long durationMs)
    {
        if (_logger is null)
            return;

        var factCount = data.Facts.Count(fact => fact.StartsWith("recent[", StringComparison.OrdinalIgnoreCase));
        var todoCount = data.Facts.Count(fact => fact.StartsWith("todo[", StringComparison.OrdinalIgnoreCase));
        var lastActionStatus = data.Facts.FirstOrDefault(fact => fact.StartsWith("lastAction=", StringComparison.OrdinalIgnoreCase)) ?? "none";
        _logger.LogInformation(
            "recent_activity_query_completed; npc={NpcId}; trace={TraceId}; durationMs={DurationMs}; payloadChars={PayloadChars}; factCount={FactCount}; todoCount={TodoCount}; lastActionStatus={LastActionStatus}; saveId={SaveId}; profileId={ProfileId}; sessionId={SessionId}; status={Status}; unknownFields={UnknownFields}",
            _descriptor.NpcId,
            "runtime",
            durationMs,
            StardewNpcToolJson.Serialize(data).Length,
            factCount,
            todoCount,
            lastActionStatus,
            _descriptor.SaveId,
            _descriptor.ProfileId,
            _descriptor.SessionId,
            data.Status,
            string.Join("|", data.UnknownFields ?? []));
    }
}

internal sealed class StardewActionStatusPoller
{
    private readonly IGameCommandService _commands;
    private readonly int _maxStatusPolls;
    private readonly StardewRuntimeActionController _runtimeActions;

    public StardewActionStatusPoller(
        IGameCommandService commands,
        int maxStatusPolls,
        StardewRuntimeActionController runtimeActions)
    {
        _commands = commands;
        _maxStatusPolls = Math.Max(0, maxStatusPolls);
        _runtimeActions = runtimeActions;
    }

    public async Task<IReadOnlyList<GameCommandStatus>> PollUntilTerminalAsync(GameCommandResult commandResult, CancellationToken ct)
    {
        if (!commandResult.Accepted ||
            string.IsNullOrWhiteSpace(commandResult.CommandId) ||
            StardewRuntimeActionController.IsTerminalStatus(commandResult.Status))
        {
            return [];
        }

        var statuses = new List<GameCommandStatus>();
        for (var i = 0; i < _maxStatusPolls; i++)
        {
            var status = await _commands.GetStatusAsync(commandResult.CommandId, ct);
            statuses.Add(status);
            await _runtimeActions.RecordStatusAsync(status, ct);

            if (StardewRuntimeActionController.IsTerminalStatus(status.Status))
                break;
        }

        return statuses;
    }

    public async Task<StardewNpcActionToolResult> ToActionToolResultAsync(GameCommandResult commandResult, CancellationToken ct)
    {
        var statusPolls = await PollUntilTerminalAsync(commandResult, ct);
        var finalStatus = statusPolls.Count > 0 ? statusPolls[^1] : null;
        return new StardewNpcActionToolResult(
            commandResult.Accepted,
            commandResult.CommandId,
            commandResult.Status,
            commandResult.FailureReason,
            commandResult.TraceId,
            finalStatus,
            statusPolls);
    }
}

public sealed class StardewNavigateToTileTool : ITool, IToolSchemaProvider
{
    private readonly IGameCommandService _commands;
    private readonly NpcRuntimeDescriptor _descriptor;
    private readonly Func<string> _traceIdFactory;
    private readonly Func<string> _idempotencyKeyFactory;
    private readonly StardewRuntimeActionController _runtimeActions;
    private readonly StardewActionStatusPoller _statusPoller;

    internal StardewNavigateToTileTool(
        IGameCommandService commands,
        NpcRuntimeDescriptor descriptor,
        Func<string> traceIdFactory,
        Func<string> idempotencyKeyFactory,
        int maxStatusPolls,
        StardewRuntimeActionController runtimeActions)
    {
        _commands = commands;
        _descriptor = descriptor;
        _traceIdFactory = traceIdFactory;
        _idempotencyKeyFactory = idempotencyKeyFactory;
        _runtimeActions = runtimeActions;
        _statusPoller = new StardewActionStatusPoller(commands, maxStatusPolls, runtimeActions);
    }

    public string Name => "stardew_navigate_to_tile";

    public string Description => "移动到已经由 stardew-navigation skill 资料披露的具体星露谷地图 tile。父层 autonomy 负责解析 target 并调用本工具；真实移动、跨地图行走和失败结果由宿主与 Stardew bridge 执行并返回。";

    public Type ParametersType => typeof(StardewNavigateToTileToolParameters);

    public JsonElement GetParameterSchema() => StardewNpcToolSchemas.NavigateToTile();

    public async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (StardewNavigateToTileToolParameters)parameters;
        if (string.IsNullOrWhiteSpace(p.LocationName))
            return ToolResult.Fail("locationName is required.");

        var traceId = _traceIdFactory();
        var payload = new JsonObject();
        if (!string.IsNullOrWhiteSpace(p.Source))
            payload["targetSource"] = p.Source;
        if (p.FacingDirection is not null)
            payload["facingDirection"] = p.FacingDirection.Value;
        if (!string.IsNullOrWhiteSpace(p.Thought))
            payload["thought"] = p.Thought;

        var action = new GameAction(
            _descriptor.NpcId,
            _descriptor.GameId,
            GameActionType.Move,
            traceId,
            _idempotencyKeyFactory(),
            new GameActionTarget(
                "tile",
                p.LocationName.Trim(),
                new GameTile(p.X, p.Y)),
            p.Reason,
            payload,
            BodyBinding: _descriptor.EffectiveBodyBinding);

        var preparedAction = await _runtimeActions.TryBeginAsync(action, ct);
        if (preparedAction?.BlockedResult is not null)
        {
            return ToolResult.Ok(StardewNpcToolJson.Serialize(new StardewNpcActionToolResult(
                preparedAction.BlockedResult.Accepted,
                preparedAction.BlockedResult.CommandId,
                preparedAction.BlockedResult.Status,
                preparedAction.BlockedResult.FailureReason,
                preparedAction.BlockedResult.TraceId,
                FinalStatus: null,
                StatusPolls: [])));
        }

        var commandResult = await _commands.SubmitAsync(action, ct);
        await _runtimeActions.RecordSubmitResultAsync(preparedAction, commandResult, ct);
        return ToolResult.Ok(StardewNpcToolJson.Serialize(await _statusPoller.ToActionToolResultAsync(commandResult, ct)));
    }
}

public sealed class StardewSpeakTool : ITool, IToolSchemaProvider
{
    private readonly IGameCommandService _commands;
    private readonly NpcRuntimeDescriptor _descriptor;
    private readonly Func<string> _traceIdFactory;
    private readonly Func<string> _idempotencyKeyFactory;
    private readonly StardewRuntimeActionController _runtimeActions;
    private readonly StardewActionStatusPoller _statusPoller;

    internal StardewSpeakTool(
        IGameCommandService commands,
        NpcRuntimeDescriptor descriptor,
        Func<string> traceIdFactory,
        Func<string> idempotencyKeyFactory,
        int maxStatusPolls,
        StardewRuntimeActionController runtimeActions)
    {
        _commands = commands;
        _descriptor = descriptor;
        _traceIdFactory = traceIdFactory;
        _idempotencyKeyFactory = idempotencyKeyFactory;
        _runtimeActions = runtimeActions;
        _statusPoller = new StardewActionStatusPoller(commands, maxStatusPolls, runtimeActions);
    }

    public string Name => "stardew_speak";

    public string Description => "让当前 NPC 通过 Stardew bridge 说一句短的非阻塞可见话。附近玩家看到头顶气泡，远处或跨地图玩家收到手机消息。用于让玩家知道你在做什么，不要连续多轮移动/查状态却完全沉默。runtime 会内部绑定 npcId、saveId、traceId 和幂等键。";

    public Type ParametersType => typeof(StardewSpeakToolParameters);

    public JsonElement GetParameterSchema() => StardewNpcToolSchemas.Speak();

    public async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (StardewSpeakToolParameters)parameters;
        if (string.IsNullOrWhiteSpace(p.Text))
            return ToolResult.Fail("text is required.");

        var channel = string.IsNullOrWhiteSpace(p.Channel) ? "player" : p.Channel;
        var payload = new JsonObject
        {
            ["text"] = p.Text,
            ["channel"] = channel
        };
        var action = new GameAction(
            _descriptor.NpcId,
            _descriptor.GameId,
            GameActionType.Speak,
            _traceIdFactory(),
            _idempotencyKeyFactory(),
            new GameActionTarget("player"),
            Payload: payload,
            BodyBinding: _descriptor.EffectiveBodyBinding);

        var preparedAction = await _runtimeActions.TryBeginAsync(action, ct);
        if (preparedAction?.BlockedResult is not null)
            return ToolResult.Ok(StardewNpcToolJson.Serialize(preparedAction.BlockedResult));

        var commandResult = await _commands.SubmitAsync(action, ct);
        await _runtimeActions.RecordSubmitResultAsync(preparedAction, commandResult, ct);
        return ToolResult.Ok(StardewNpcToolJson.Serialize(await _statusPoller.ToActionToolResultAsync(commandResult, ct)));
    }
}

public sealed class StardewIdleMicroActionTool : ITool, IToolSchemaProvider
{
    private readonly IGameCommandService _commands;
    private readonly NpcRuntimeDescriptor _descriptor;
    private readonly Func<string> _traceIdFactory;
    private readonly Func<string> _idempotencyKeyFactory;
    private readonly StardewRuntimeActionController _runtimeActions;
    private readonly StardewActionStatusPoller _statusPoller;

    internal StardewIdleMicroActionTool(
        IGameCommandService commands,
        NpcRuntimeDescriptor descriptor,
        Func<string> traceIdFactory,
        Func<string> idempotencyKeyFactory,
        int maxStatusPolls,
        StardewRuntimeActionController runtimeActions)
    {
        _commands = commands;
        _descriptor = descriptor;
        _traceIdFactory = traceIdFactory;
        _idempotencyKeyFactory = idempotencyKeyFactory;
        _runtimeActions = runtimeActions;
        _statusPoller = new StardewActionStatusPoller(commands, maxStatusPolls, runtimeActions);
    }

    public string Name => "stardew_idle_micro_action";

    public string Description => "让当前 NPC 在原地做一个短暂可见微动作。父层 autonomy 直接调用本工具；真实动作、失败结果和 lifecycle fact 由宿主与 Stardew bridge 执行并返回。";

    public Type ParametersType => typeof(StardewIdleMicroActionToolParameters);

    public JsonElement GetParameterSchema() => StardewNpcToolSchemas.IdleMicroAction();

    public async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (StardewIdleMicroActionToolParameters)parameters;
        if (string.IsNullOrWhiteSpace(p.Kind))
            return ToolResult.Fail("kind is required.");

        var payload = new JsonObject
        {
            ["kind"] = p.Kind
        };
        if (!string.IsNullOrWhiteSpace(p.AnimationAlias))
            payload["animationAlias"] = p.AnimationAlias;
        if (!string.IsNullOrWhiteSpace(p.Intensity))
            payload["intensity"] = p.Intensity;
        if (p.TtlSeconds is not null)
            payload["ttlSeconds"] = p.TtlSeconds.Value;

        var action = new GameAction(
            _descriptor.NpcId,
            _descriptor.GameId,
            GameActionType.IdleMicroAction,
            _traceIdFactory(),
            _idempotencyKeyFactory(),
            new GameActionTarget("self"),
            "idle_micro_action",
            payload,
            BodyBinding: _descriptor.EffectiveBodyBinding);

        var preparedAction = await _runtimeActions.TryBeginAsync(action, ct);
        if (preparedAction?.BlockedResult is not null)
            return ToolResult.Ok(StardewNpcToolJson.Serialize(preparedAction.BlockedResult));

        var commandResult = await _commands.SubmitAsync(action, ct);
        await _runtimeActions.RecordSubmitResultAsync(preparedAction, commandResult, ct);
        return ToolResult.Ok(StardewNpcToolJson.Serialize(await _statusPoller.ToActionToolResultAsync(commandResult, ct)));
    }
}

public sealed class StardewTaskStatusTool : ITool, IToolSchemaProvider
{
    private readonly IGameCommandService _commands;

    public StardewTaskStatusTool(IGameCommandService commands)
    {
        _commands = commands;
    }

    public string Name => "stardew_task_status";

    public string Description => "读取此前 NPC 行动工具返回的 Stardew command 状态。";

    public Type ParametersType => typeof(StardewTaskStatusToolParameters);

    public JsonElement GetParameterSchema() => StardewNpcToolSchemas.TaskStatus();

    public async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (StardewTaskStatusToolParameters)parameters;
        if (string.IsNullOrWhiteSpace(p.CommandId))
            return ToolResult.Fail("commandId is required.");

        return ToolResult.Ok(StardewNpcToolJson.Serialize(await _commands.GetStatusAsync(p.CommandId, ct)));
    }
}

public sealed class StardewOpenPrivateChatTool : ITool, IToolSchemaProvider
{
    private readonly IGameCommandService _commands;
    private readonly NpcRuntimeDescriptor _descriptor;
    private readonly Func<string> _traceIdFactory;
    private readonly Func<string> _idempotencyKeyFactory;
    private readonly StardewRuntimeActionController _runtimeActions;
    private readonly StardewActionStatusPoller _statusPoller;

    internal StardewOpenPrivateChatTool(
        IGameCommandService commands,
        NpcRuntimeDescriptor descriptor,
        Func<string> traceIdFactory,
        Func<string> idempotencyKeyFactory,
        int maxStatusPolls,
        StardewRuntimeActionController runtimeActions)
    {
        _commands = commands;
        _descriptor = descriptor;
        _traceIdFactory = traceIdFactory;
        _idempotencyKeyFactory = idempotencyKeyFactory;
        _runtimeActions = runtimeActions;
        _statusPoller = new StardewActionStatusPoller(commands, maxStatusPolls, runtimeActions);
    }

    public string Name => "stardew_open_private_chat";

    public string Description => "为当前 NPC 打开游戏内私聊输入，让玩家可以输入消息。只在已观察事实表明适合私聊时使用。";

    public Type ParametersType => typeof(StardewOpenPrivateChatToolParameters);

    public JsonElement GetParameterSchema() => StardewNpcToolSchemas.OpenPrivateChat();

    public async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (StardewOpenPrivateChatToolParameters)parameters;
        var payload = new JsonObject
        {
            ["prompt"] = p.Prompt
        };
        var action = new GameAction(
            _descriptor.NpcId,
            _descriptor.GameId,
            GameActionType.OpenPrivateChat,
            _traceIdFactory(),
            _idempotencyKeyFactory(),
            new GameActionTarget("player"),
            Payload: payload,
            BodyBinding: _descriptor.EffectiveBodyBinding);

        var preparedAction = await _runtimeActions.TryBeginAsync(action, ct);
        if (preparedAction?.BlockedResult is not null)
            return ToolResult.Ok(StardewNpcToolJson.Serialize(preparedAction.BlockedResult));

        var commandResult = await _commands.SubmitAsync(action, ct);
        await _runtimeActions.RecordSubmitResultAsync(preparedAction, commandResult, ct);
        return ToolResult.Ok(StardewNpcToolJson.Serialize(await _statusPoller.ToActionToolResultAsync(commandResult, ct)));
    }
}

internal sealed class StardewRuntimeActionController
{
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DefaultActionTimeout = TimeSpan.FromMinutes(1);

    private readonly NpcRuntimeDriver? _runtimeDriver;
    private readonly WorldCoordinationService? _worldCoordination;
    private readonly Func<DateTime> _nowUtc;
    private readonly TimeSpan _actionTimeout;
    private readonly NpcActionChainGuardOptions _actionChainGuardOptions;

    public StardewRuntimeActionController(
        NpcRuntimeDriver? runtimeDriver,
        WorldCoordinationService? worldCoordination,
        Func<DateTime>? nowUtc,
        TimeSpan? actionTimeout,
        NpcActionChainGuardOptions? actionChainGuardOptions = null)
    {
        _runtimeDriver = runtimeDriver;
        _worldCoordination = worldCoordination;
        _nowUtc = nowUtc ?? (() => DateTime.UtcNow);
        _actionTimeout = actionTimeout ?? DefaultActionTimeout;
        _actionChainGuardOptions = actionChainGuardOptions ?? new NpcActionChainGuardOptions();
    }

    public async Task<StardewPreparedActionState?> TryBeginAsync(GameAction action, CancellationToken ct)
    {
        if (_runtimeDriver is null)
            return null;

        var snapshot = _runtimeDriver.Snapshot();
        if (snapshot.ActionSlot is not null || snapshot.PendingWorkItem is not null)
        {
            return StardewPreparedActionState.Blocked(new GameCommandResult(
                Accepted: false,
                CommandId: snapshot.ActionSlot?.CommandId ?? snapshot.PendingWorkItem?.CommandId ?? string.Empty,
                Status: StardewCommandStatuses.Blocked,
                FailureReason: StardewBridgeErrorCodes.ActionSlotBusy,
                TraceId: action.TraceId,
                Retryable: true));
        }

        var startedAtUtc = _nowUtc();
        var chainGuard = PrepareActionChainGuard(snapshot.ActionChainGuard, action, startedAtUtc);

        var workItemId = $"work_{action.IdempotencyKey}";
        var claimId = RequiresMoveClaim(action) ? workItemId : null;
        if (claimId is not null && _worldCoordination is not null)
        {
            var claimTargetTile = action.Target.Tile is not null && !string.IsNullOrWhiteSpace(action.Target.LocationName)
                ? new ClaimedTile(action.Target.LocationName, action.Target.Tile.X, action.Target.Tile.Y)
                : null;
            var claimResult = _worldCoordination.TryClaimMove(
                claimId,
                action.NpcId,
                action.TraceId,
                claimTargetTile,
                interactionTile: null,
                action.IdempotencyKey);
            if (!claimResult.Accepted)
            {
                await _runtimeDriver.SetNextWakeAtUtcAsync(startedAtUtc + DefaultRetryDelay, ct);
                return StardewPreparedActionState.Blocked(new GameCommandResult(
                    Accepted: false,
                    CommandId: string.Empty,
                    Status: StardewCommandStatuses.Blocked,
                    FailureReason: claimResult.ErrorCode ?? StardewBridgeErrorCodes.CommandConflict,
                    TraceId: action.TraceId,
                    Retryable: true));
            }
        }

        try
        {
            await _runtimeDriver.SetPendingWorkItemAsync(
                new NpcRuntimePendingWorkItemSnapshot(
                    workItemId,
                    ToWorkType(action),
                    CommandId: null,
                    Status: "submitting",
                    CreatedAtUtc: startedAtUtc,
                    IdempotencyKey: action.IdempotencyKey),
                ct);
            await _runtimeDriver.SetActionSlotAsync(
                new NpcRuntimeActionSlotSnapshot(
                    "action",
                    workItemId,
                    CommandId: null,
                    action.TraceId,
                    startedAtUtc,
                    startedAtUtc + _actionTimeout),
                ct);
            await _runtimeDriver.SetNextWakeAtUtcAsync(null, ct);
            await _runtimeDriver.SetActionChainGuardAsync(chainGuard.Snapshot, ct);
            return new StardewPreparedActionState(
                workItemId,
                claimId,
                startedAtUtc,
                action.TraceId,
                null,
                IsWorldActionContinuityAction(action));
        }
        catch
        {
            if (claimId is not null && _worldCoordination is not null)
                _worldCoordination.ReleaseClaim(claimId);

            throw;
        }
    }

    public async Task RecordSubmitResultAsync(
        StardewPreparedActionState? preparedAction,
        GameCommandResult commandResult,
        CancellationToken ct)
    {
        if (_runtimeDriver is null || preparedAction is null || preparedAction.BlockedResult is not null)
            return;

        if (!commandResult.Accepted)
        {
            await ClearAsync(
                preparedAction.ClaimId ?? preparedAction.WorkItemId,
                commandResult.Retryable ? _nowUtc() + DefaultRetryDelay : null,
                ct);
            return;
        }

        var snapshot = _runtimeDriver.Snapshot();
        if (!string.IsNullOrWhiteSpace(commandResult.CommandId) &&
            !string.IsNullOrWhiteSpace(preparedAction.ClaimId) &&
            _worldCoordination is not null)
        {
            _worldCoordination.RekeyClaim(preparedAction.ClaimId, commandResult.CommandId);
        }

        if (snapshot.PendingWorkItem is not null)
        {
            await _runtimeDriver.SetPendingWorkItemAsync(
                snapshot.PendingWorkItem with
                {
                    CommandId = string.IsNullOrWhiteSpace(commandResult.CommandId) ? snapshot.PendingWorkItem.CommandId : commandResult.CommandId,
                    Status = commandResult.Status
                },
                ct);
        }

        if (snapshot.ActionSlot is not null)
        {
            await _runtimeDriver.SetActionSlotAsync(
                snapshot.ActionSlot with
                {
                    CommandId = string.IsNullOrWhiteSpace(commandResult.CommandId) ? snapshot.ActionSlot.CommandId : commandResult.CommandId
                },
                ct);
        }

        if (IsTerminalStatus(commandResult.Status))
        {
            var terminalSnapshot = _runtimeDriver.Snapshot();
            var commandId = string.IsNullOrWhiteSpace(commandResult.CommandId)
                ? terminalSnapshot.ActionSlot?.CommandId ?? terminalSnapshot.PendingWorkItem?.CommandId ?? terminalSnapshot.ActionSlot?.WorkItemId ?? terminalSnapshot.PendingWorkItem?.WorkItemId ?? preparedAction.WorkItemId
                : commandResult.CommandId;
            var action = terminalSnapshot.PendingWorkItem?.WorkType ?? "action";
            await _runtimeDriver.SetLastTerminalCommandStatusAsync(
                new GameCommandStatus(
                    commandId,
                    string.Empty,
                    action,
                    commandResult.Status,
                    1,
                    commandResult.FailureReason,
                    commandResult.FailureReason,
                    UpdatedAtUtc: _nowUtc()),
                ct);
            if (preparedAction.UpdatesWorldActionContinuity)
            {
                await RecordTerminalActionChainStatusAsync(
                    terminalSnapshot.ActionChainGuard,
                    action,
                    commandResult.Status,
                    commandResult.FailureReason,
                    _nowUtc(),
                    ct);
            }

            await ClearAsync(
                preparedAction.ClaimId ?? preparedAction.WorkItemId,
                commandResult.Retryable ? _nowUtc() + DefaultRetryDelay : null,
                ct);
        }
    }

    public async Task RecordStatusAsync(GameCommandStatus status, CancellationToken ct)
    {
        if (_runtimeDriver is null)
            return;

        var snapshot = _runtimeDriver.Snapshot();
        if (snapshot.PendingWorkItem is not null && !IsTerminalStatus(status.Status))
        {
            await _runtimeDriver.SetPendingWorkItemAsync(
                snapshot.PendingWorkItem with
                {
                    CommandId = string.IsNullOrWhiteSpace(status.CommandId) ? snapshot.PendingWorkItem.CommandId : status.CommandId,
                    Status = status.Status
                },
                ct);
        }

        if (snapshot.ActionSlot is not null &&
            !string.IsNullOrWhiteSpace(status.CommandId) &&
            !string.Equals(snapshot.ActionSlot.CommandId, status.CommandId, StringComparison.OrdinalIgnoreCase))
        {
            await _runtimeDriver.SetActionSlotAsync(snapshot.ActionSlot with { CommandId = status.CommandId }, ct);
        }

        if (!IsTerminalStatus(status.Status))
            return;

        var terminalSnapshot = _runtimeDriver.Snapshot();
        await _runtimeDriver.SetLastTerminalCommandStatusAsync(status, ct);
        if (IsWorldActionContinuityTerminal(status))
        {
            await RecordTerminalActionChainStatusAsync(
                terminalSnapshot.ActionChainGuard,
                status.Action,
                status.Status,
                status.ErrorCode ?? status.BlockedReason,
                _nowUtc(),
                ct);
        }

        var primaryClaimId = !string.IsNullOrWhiteSpace(status.CommandId)
            ? status.CommandId
            : snapshot.ActionSlot?.CommandId ?? snapshot.PendingWorkItem?.CommandId ?? snapshot.ActionSlot?.WorkItemId ?? snapshot.PendingWorkItem?.WorkItemId;
        await ClearAsync(primaryClaimId, GetCooldownAtUtc(status.Status, status.RetryAfterUtc), ct, snapshot.ActionSlot?.WorkItemId, snapshot.PendingWorkItem?.WorkItemId);
    }

    public Task ClearAsync(DateTime? nextWakeAtUtc, CancellationToken ct)
    {
        var snapshot = _runtimeDriver?.Snapshot();
        var primaryClaimId = snapshot?.ActionSlot?.CommandId
            ?? snapshot?.PendingWorkItem?.CommandId
            ?? snapshot?.ActionSlot?.WorkItemId
            ?? snapshot?.PendingWorkItem?.WorkItemId;
        return ClearAsync(primaryClaimId, nextWakeAtUtc, ct, snapshot?.ActionSlot?.WorkItemId, snapshot?.PendingWorkItem?.WorkItemId);
    }

    public static bool IsTerminalStatus(string? status)
        => string.Equals(status, StardewCommandStatuses.Completed, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, StardewCommandStatuses.Failed, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, StardewCommandStatuses.Cancelled, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, StardewCommandStatuses.Interrupted, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, StardewCommandStatuses.Blocked, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, StardewCommandStatuses.Expired, StringComparison.OrdinalIgnoreCase);

    public static bool IsInFlightStatus(string? status)
        => string.Equals(status, StardewCommandStatuses.Queued, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, StardewCommandStatuses.Running, StringComparison.OrdinalIgnoreCase);

    private async Task ClearAsync(string? claimId, DateTime? nextWakeAtUtc, CancellationToken ct, params string?[] fallbackClaimIds)
    {
        if (_runtimeDriver is null)
            return;

        if (_worldCoordination is not null)
        {
            var released = false;
            if (!string.IsNullOrWhiteSpace(claimId))
                released = _worldCoordination.ReleaseClaim(claimId);

            if (!released)
            {
                foreach (var fallbackClaimId in fallbackClaimIds.Where(candidate => !string.IsNullOrWhiteSpace(candidate)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (string.Equals(fallbackClaimId, claimId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (_worldCoordination.ReleaseClaim(fallbackClaimId!))
                        break;
                }
            }
        }

        await _runtimeDriver.SetActionSlotAsync(null, ct);
        await _runtimeDriver.SetPendingWorkItemAsync(null, ct);
        await _runtimeDriver.SetNextWakeAtUtcAsync(nextWakeAtUtc, ct);
    }

    private static bool RequiresMoveClaim(GameAction action)
        => action.Type == GameActionType.Move &&
           (!string.IsNullOrWhiteSpace(ReadPayloadString(action.Payload, "destinationId")) ||
            (action.Target.Tile is not null && !string.IsNullOrWhiteSpace(action.Target.LocationName)));

    private static string? ReadPayloadString(JsonObject? payload, string propertyName)
    {
        if (payload is null || !payload.TryGetPropertyValue(propertyName, out var node) || node is null)
            return null;

        var value = node.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string ToWorkType(GameAction action)
        => action.Type switch
        {
            GameActionType.Move => "move",
            GameActionType.Speak when string.Equals(ReadPayloadString(action.Payload, "channel"), "private_chat", StringComparison.OrdinalIgnoreCase) => "private_chat_reply",
            GameActionType.Speak => "speak",
            GameActionType.IdleMicroAction => "idle_micro_action",
            GameActionType.OpenPrivateChat => "open_private_chat",
            _ => "action"
        };

    private static string ToWorkType(GameActionType actionType)
        => actionType switch
        {
            GameActionType.Move => "move",
            GameActionType.Speak => "speak",
            GameActionType.IdleMicroAction => "idle_micro_action",
            GameActionType.OpenPrivateChat => "open_private_chat",
            _ => "action"
        };

    private static bool IsWorldActionContinuityAction(GameAction action)
        => action.Type is GameActionType.Move or GameActionType.IdleMicroAction ||
           (action.Type == GameActionType.Speak &&
            !string.Equals(ReadPayloadString(action.Payload, "channel"), "private_chat", StringComparison.OrdinalIgnoreCase));

    private static bool IsWorldActionContinuityTerminal(GameCommandStatus status)
        => string.Equals(status.Action, "move", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status.Action, "idle_micro_action", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status.Action, "speak", StringComparison.OrdinalIgnoreCase);

    private ActionChainGuardPreparation PrepareActionChainGuard(
        NpcRuntimeActionChainGuardSnapshot? current,
        GameAction action,
        DateTime nowUtc)
    {
        if (!IsWorldActionContinuityAction(action))
            return new ActionChainGuardPreparation(current);

        var guard = ShouldStartNewChain(current, action, nowUtc)
            ? CreateActionChainGuard(action, nowUtc)
            : current!;

        var accepted = guard with
        {
            GuardStatus = "open",
            BlockedReasonCode = null,
            BlockedUntilClosure = false,
            RootTodoId = ReadPayloadString(action.Payload, "rootTodoId") ?? guard.RootTodoId,
            ConversationId = ReadPayloadString(action.Payload, "conversationId") ?? guard.ConversationId,
            UpdatedAtUtc = nowUtc,
            LastAction = ToWorkType(action),
            LastTargetKey = BuildTargetKey(action),
            ConsecutiveActions = guard.ConsecutiveActions + 1
        };
        return new ActionChainGuardPreparation(accepted);
    }

    private bool ShouldStartNewChain(NpcRuntimeActionChainGuardSnapshot? current, GameAction action, DateTime nowUtc)
    {
        if (current is null)
            return true;

        if (string.Equals(current.GuardStatus, "closed", StringComparison.OrdinalIgnoreCase))
            return true;

        if (current.BlockedUntilClosure ||
            string.Equals(current.GuardStatus, "blocked_until_closure", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (nowUtc - current.UpdatedAtUtc > _actionChainGuardOptions.EffectiveChainTtl)
            return true;

        return false;
    }

    private static NpcRuntimeActionChainGuardSnapshot CreateActionChainGuard(GameAction action, DateTime nowUtc)
        => new(
            $"chain_{Guid.NewGuid():N}",
            "open",
            null,
            false,
            ReadPayloadString(action.Payload, "rootTodoId"),
            action.TraceId,
            nowUtc,
            nowUtc,
            null,
            null,
            0,
            0,
            0,
            null,
            null,
            0,
            0,
            ReadPayloadString(action.Payload, "conversationId"));

    private async Task RecordTerminalActionChainStatusAsync(
        NpcRuntimeActionChainGuardSnapshot? current,
        string? action,
        string status,
        string? reasonCode,
        DateTime nowUtc,
        CancellationToken ct)
    {
        if (_runtimeDriver is null || current is null)
            return;

        var failure = IsFailureStatus(status);
        var sameAction = string.Equals(current.LastAction, action, StringComparison.OrdinalIgnoreCase);
        var updated = current with
        {
            UpdatedAtUtc = nowUtc,
            LastTerminalStatus = status,
            LastReasonCode = string.IsNullOrWhiteSpace(reasonCode) ? null : reasonCode,
            ConsecutiveFailures = failure ? current.ConsecutiveFailures + 1 : 0,
            ConsecutiveSameActionFailures = failure && sameAction ? current.ConsecutiveSameActionFailures + 1 : 0
        };

        await _runtimeDriver.SetActionChainGuardAsync(updated, ct);
    }

    private static bool IsFailureStatus(string? status)
        => string.Equals(status, StardewCommandStatuses.Failed, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, StardewCommandStatuses.Cancelled, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, StardewCommandStatuses.Interrupted, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, StardewCommandStatuses.Blocked, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, StardewCommandStatuses.Expired, StringComparison.OrdinalIgnoreCase);

    private static string BuildTargetKey(GameAction action)
        => action.Type switch
        {
            GameActionType.Move when action.Target.Tile is not null && !string.IsNullOrWhiteSpace(action.Target.LocationName)
                => $"move:{action.Target.LocationName}:{action.Target.Tile.X}:{action.Target.Tile.Y}",
            GameActionType.Move when !string.IsNullOrWhiteSpace(ReadPayloadString(action.Payload, "destinationId"))
                => $"move:destination:{ReadPayloadString(action.Payload, "destinationId")}",
            GameActionType.Speak => $"speak:{action.Target.Kind}:{action.Target.EntityId ?? "player"}",
            GameActionType.OpenPrivateChat => $"open_private_chat:{action.Target.Kind}:{action.Target.EntityId ?? "player"}",
            GameActionType.IdleMicroAction => $"idle_micro_action:{ReadPayloadString(action.Payload, "kind") ?? action.Target.Kind}",
            _ => $"{ToWorkType(action.Type)}:{action.Target.Kind}:{action.Target.EntityId ?? action.Target.ObjectId ?? action.Target.LocationName ?? "-"}"
        };

    private DateTime? GetCooldownAtUtc(string? status, DateTime? retryAfterUtc)
    {
        if (retryAfterUtc.HasValue)
            return retryAfterUtc;

        if (string.Equals(status, StardewCommandStatuses.Blocked, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, StardewCommandStatuses.Expired, StringComparison.OrdinalIgnoreCase))
        {
            return _nowUtc() + DefaultRetryDelay;
        }

        return null;
    }
}

internal sealed record ActionChainGuardPreparation(NpcRuntimeActionChainGuardSnapshot? Snapshot);

internal sealed record StardewPreparedActionState(
    string WorkItemId,
    string? ClaimId,
    DateTime StartedAtUtc,
    string TraceId,
    GameCommandResult? BlockedResult,
    bool UpdatesWorldActionContinuity = false)
{
    public static StardewPreparedActionState Blocked(GameCommandResult result)
        => new(string.Empty, null, DateTime.MinValue, result.TraceId, result);
}

public sealed class StardewStatusToolParameters
{
}

public sealed class StardewSocialStatusToolParameters
{
    public string? TargetNpcId { get; init; }
}

public sealed class StardewNavigateToTileToolParameters
{
    public required string LocationName { get; init; }

    public required int X { get; init; }

    public required int Y { get; init; }

    public string? Source { get; init; }

    public int? FacingDirection { get; init; }

    public string? Reason { get; init; }

    public string? Thought { get; init; }
}

public sealed class StardewSpeakToolParameters
{
    public required string Text { get; init; }

    public string? Channel { get; init; }
}

public sealed class StardewTaskStatusToolParameters
{
    public required string CommandId { get; init; }
}

public sealed class StardewIdleMicroActionToolParameters
{
    public required string Kind { get; init; }

    public string? AnimationAlias { get; init; }

    public string? Intensity { get; init; }

    public int? TtlSeconds { get; init; }
}

public sealed class StardewOpenPrivateChatToolParameters
{
    public string? Prompt { get; init; }
}

internal sealed record StardewNpcActionToolResult(
    bool Accepted,
    string CommandId,
    string Status,
    string? FailureReason,
    string TraceId,
    GameCommandStatus? FinalStatus,
    IReadOnlyList<GameCommandStatus> StatusPolls);

internal static class StardewNpcToolJson
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
}

internal static class StardewNpcToolSchemas
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static JsonElement Empty()
        => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        }, JsonOptions);

    public static JsonElement NavigateToTile()
        => Schema(
            new Dictionary<string, object>
            {
                ["locationName"] = new { type = "string", description = "从已加载地图 skill 参考资料中选出的星露谷地图名。" },
                ["x"] = new { type = "integer", description = "从已加载地图 skill 参考资料中逐字复制的目标 tile X 坐标。" },
                ["y"] = new { type = "integer", description = "从已加载地图 skill 参考资料中逐字复制的目标 tile Y 坐标。" },
                ["source"] = new { type = "string", description = "披露该坐标的已加载地图 skill reference，例如 map-skill:stardew.navigation.poi.beach-shoreline。" },
                ["facingDirection"] = new { type = "integer", description = "可选。参考资料或上游意图给出时使用的朝向。" },
                ["reason"] = new { type = "string", description = "来自上游 intent 的简短原因。" },
                ["thought"] = new { type = "string", description = "可选。移动开始时显示的简短头顶气泡。" }
            },
            ["locationName", "x", "y", "source"]);

    public static JsonElement Speak()
        => Schema(
            new Dictionary<string, object>
            {
                ["text"] = new { type = "string", description = "NPC 要说的一句短话。" },
                ["channel"] = new { type = "string", description = "发送渠道；默认发给玩家。" }
            },
            ["text"]);

    public static JsonElement IdleMicroAction()
        => Schema(
            new Dictionary<string, object>
            {
                ["kind"] = new { type = "string", description = "从固定白名单中选择的原地微动作类型。" },
                ["animationAlias"] = new { type = "string", description = "可选。kind 为 idle_animation_once 时使用的白名单动画别名。" },
                ["intensity"] = new { type = "string", description = "可选。来自上游意图的轻量强度标签。" },
                ["ttlSeconds"] = new { type = "integer", description = "可选。原地微动作的存活秒数。" }
            },
            ["kind"]);

    public static JsonElement TaskStatus()
        => Schema(
            new Dictionary<string, object>
            {
                ["commandId"] = new { type = "string", description = "长动作工具返回的 command id。" }
            },
            ["commandId"]);

    public static JsonElement OpenPrivateChat()
        => Schema(
            new Dictionary<string, object>
            {
                ["prompt"] = new { type = "string", description = "可选。打开私聊时显示或记录的简短提示。" }
            },
            []);

    public static JsonElement SocialStatus()
        => Schema(
            new Dictionary<string, object>
            {
                ["targetNpcId"] = new { type = "string", description = "可选。要查看的 NPC id 或名字；省略时查看当前 NPC。" }
            },
            []);

    private static JsonElement Schema(Dictionary<string, object> properties, string[] required)
        => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties,
            required
        }, JsonOptions);
}
