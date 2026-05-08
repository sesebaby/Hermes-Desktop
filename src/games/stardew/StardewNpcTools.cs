namespace Hermes.Agent.Games.Stardew;

using System.Text.Json;
using System.Text.Json.Nodes;
using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.Runtime;
using Microsoft.Extensions.Logging;

public static class StardewNpcToolFactory
{
    private static readonly HashSet<string> LocalExecutorToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "stardew_status",
        "stardew_move",
        "stardew_navigate_to_tile",
        "stardew_idle_micro_action",
        "stardew_task_status"
    };

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
        TimeSpan? actionTimeout = null)
    {
        traceIdFactory ??= () => $"trace_{descriptor.NpcId}_{Guid.NewGuid():N}";
        idempotencyKeyFactory ??= () => $"idem_{descriptor.NpcId}_{Guid.NewGuid():N}";
        var runtimeActions = new StardewRuntimeActionController(
            runtimeDriver,
            worldCoordination,
            nowUtc,
            actionTimeout);

        return
        [
            new StardewStatusTool(adapter.Queries, descriptor),
            new StardewPlayerStatusTool(adapter.Queries, descriptor),
            new StardewProgressStatusTool(adapter.Queries, descriptor),
            new StardewSocialStatusTool(adapter.Queries, descriptor),
            new StardewQuestStatusTool(adapter.Queries, descriptor),
            new StardewFarmStatusTool(adapter.Queries, descriptor),
            new StardewRecentActivityTool(recentActivityProvider, descriptor, logger),
            new StardewMoveTool(adapter.Commands, descriptor, traceIdFactory, idempotencyKeyFactory, maxStatusPolls, runtimeActions),
            new StardewSpeakTool(adapter.Commands, descriptor, traceIdFactory, idempotencyKeyFactory, runtimeActions),
            new StardewOpenPrivateChatTool(adapter.Commands, descriptor, traceIdFactory, idempotencyKeyFactory, runtimeActions),
            new StardewTaskStatusTool(adapter.Commands)
        ];
    }

    public static IReadOnlyList<ITool> CreateLocalExecutorTools(
        IGameAdapter adapter,
        NpcRuntimeDescriptor descriptor,
        Func<string>? traceIdFactory = null,
        Func<string>? idempotencyKeyFactory = null,
        int maxStatusPolls = 3,
        NpcRuntimeDriver? runtimeDriver = null,
        WorldCoordinationService? worldCoordination = null,
        ILogger? logger = null,
        Func<DateTime>? nowUtc = null,
        TimeSpan? actionTimeout = null)
    {
        traceIdFactory ??= () => $"trace_{descriptor.NpcId}_{Guid.NewGuid():N}";
        idempotencyKeyFactory ??= () => $"idem_{descriptor.NpcId}_{Guid.NewGuid():N}";
        var runtimeActions = new StardewRuntimeActionController(
            runtimeDriver,
            worldCoordination,
            nowUtc,
            actionTimeout);

        return
        [
            new StardewStatusTool(adapter.Queries, descriptor),
            new StardewMoveTool(adapter.Commands, descriptor, traceIdFactory, idempotencyKeyFactory, maxStatusPolls, runtimeActions),
            new StardewNavigateToTileTool(adapter.Commands, descriptor, traceIdFactory, idempotencyKeyFactory, maxStatusPolls, runtimeActions),
            new StardewIdleMicroActionTool(adapter.Commands, descriptor, traceIdFactory, idempotencyKeyFactory, runtimeActions),
            new StardewTaskStatusTool(adapter.Commands)
        ];
    }

    public static string LocalExecutorToolFingerprint()
        => NpcToolSurface.FromTools(
            [
                new ToolFingerprintProbe("stardew_status"),
                new ToolFingerprintProbe("stardew_move"),
                new ToolFingerprintProbe("stardew_navigate_to_tile"),
                new ToolFingerprintProbe("stardew_idle_micro_action"),
                new ToolFingerprintProbe("stardew_task_status")
            ])
            .Fingerprint;

    private static bool IsLocalExecutorTool(ITool tool)
        => LocalExecutorToolNames.Contains(tool.Name);

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
            facts.Add($"lastAction={last.Action}:{last.Status}:{last.ErrorCode ?? last.BlockedReason ?? "none"}");

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

    public string Description => "Read the current Stardew facts for this NPC. This is a passive observation tool.";

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

    public override string Description => "Read a detailed natural-language player status report: player location, held item, money, basic clothing/equipment, stamina/health, spouse, and a short inventory summary. Use at most one extra status tool in a normal autonomy turn.";

    public override async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
        => Serialize(await RequireStardewQueries().GetPlayerStatusAsync(Descriptor.EffectiveBodyBinding, ct));
}

public sealed class StardewProgressStatusTool : StardewFactStatusToolBase
{
    public StardewProgressStatusTool(IGameQueryService queries, NpcRuntimeDescriptor descriptor) : base(queries, descriptor) { }

    public override string Name => "stardew_progress_status";

    public override string Description => "Read a natural-language game progress report: year, season, day, time, farm name, money, skill levels, mine depth, and house upgrade. Use only when game-stage context matters.";

    public override async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
        => Serialize(await RequireStardewQueries().GetProgressStatusAsync(Descriptor.EffectiveBodyBinding, ct));
}

public sealed class StardewSocialStatusTool : StardewFactStatusToolBase
{
    public StardewSocialStatusTool(IGameQueryService queries, NpcRuntimeDescriptor descriptor) : base(queries, descriptor) { }

    public override string Name => "stardew_social_status";

    public override string Description => "Read a natural-language social report for this NPC or a named NPC: friendship hearts, talked-today, gift counts, and marriage/spouse status. Use only when relationship context matters.";

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

    public override string Description => "Read a natural-language quest report with up to five visible player quests. Use only when player tasks or quest context matters.";

    public override async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
        => Serialize(await RequireStardewQueries().GetQuestStatusAsync(Descriptor.EffectiveBodyBinding, ct));
}

public sealed class StardewFarmStatusTool : StardewFactStatusToolBase
{
    public StardewFarmStatusTool(IGameQueryService queries, NpcRuntimeDescriptor descriptor) : base(queries, descriptor) { }

    public override string Name => "stardew_farm_status";

    public override string Description => "Read a natural-language farm report: farm name, date, weather, money, and degraded placeholders for expensive crop/animal scans. Use only when farm context matters.";

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

    public string Description => "Read this NPC runtime's recent continuity report: recent observations/events, last action status, and active todos. This is not a world-state query; use only to avoid repeating or to resume a prior task.";

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

public sealed class StardewMoveTool : ITool, IToolSchemaProvider
{
    private readonly IGameCommandService _commands;
    private readonly NpcRuntimeDescriptor _descriptor;
    private readonly Func<string> _traceIdFactory;
    private readonly Func<string> _idempotencyKeyFactory;
    private readonly int _maxStatusPolls;
    private readonly StardewRuntimeActionController _runtimeActions;

    internal StardewMoveTool(
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
        _maxStatusPolls = Math.Max(0, maxStatusPolls);
        _runtimeActions = runtimeActions;
    }

    public string Name => "stardew_move";

    public string Description => "Ask this NPC to move to a semantic destination from the latest observation. Copy the exact destination id from a destination[n].destinationId fact. Never invent destinations. Optional thought is a short private-feeling movement thought shown as a non-blocking overhead bubble when the move starts. If a move ends with path_blocked, path_unreachable, invalid_destination_id, or interrupted, observe again or choose a different destinationId instead of retrying the same destination.";

    public Type ParametersType => typeof(StardewMoveToolParameters);

    public JsonElement GetParameterSchema() => StardewNpcToolSchemas.Move();

    public async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (StardewMoveToolParameters)parameters;
        if (string.IsNullOrWhiteSpace(p.Destination))
            return ToolResult.Fail("destination is required — copy the exact destinationId from a destination[n].destinationId fact in the latest observation.");

        var traceId = _traceIdFactory();
        var payload = new JsonObject
        {
            ["destinationId"] = p.Destination
        };
        if (!string.IsNullOrWhiteSpace(p.Thought))
            payload["thought"] = p.Thought;

        var action = new GameAction(
            _descriptor.NpcId,
            _descriptor.GameId,
            GameActionType.Move,
            traceId,
            _idempotencyKeyFactory(),
            new GameActionTarget("destination"),
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
        var statusPolls = await PollUntilTerminalAsync(commandResult, ct);
        var finalStatus = statusPolls.Count > 0 ? statusPolls[^1] : null;

        return ToolResult.Ok(StardewNpcToolJson.Serialize(new StardewNpcActionToolResult(
            commandResult.Accepted,
            commandResult.CommandId,
            commandResult.Status,
            commandResult.FailureReason,
            commandResult.TraceId,
            finalStatus,
            statusPolls)));
    }

    private async Task<IReadOnlyList<GameCommandStatus>> PollUntilTerminalAsync(GameCommandResult commandResult, CancellationToken ct)
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
}

public sealed class StardewNavigateToTileTool : ITool, IToolSchemaProvider
{
    private readonly IGameCommandService _commands;
    private readonly NpcRuntimeDescriptor _descriptor;
    private readonly Func<string> _traceIdFactory;
    private readonly Func<string> _idempotencyKeyFactory;
    private readonly int _maxStatusPolls;
    private readonly StardewRuntimeActionController _runtimeActions;

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
        _maxStatusPolls = Math.Max(0, maxStatusPolls);
        _runtimeActions = runtimeActions;
    }

    public string Name => "stardew_navigate_to_tile";

    public string Description => "Executor-only mechanical navigation to a concrete Stardew location tile selected by the parent model from disclosed map skill facts. Do not expose this tool to the parent autonomy lane.";

    public Type ParametersType => typeof(StardewNavigateToTileToolParameters);

    public JsonElement GetParameterSchema() => StardewNpcToolSchemas.NavigateToTile();

    public async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (StardewNavigateToTileToolParameters)parameters;
        if (string.IsNullOrWhiteSpace(p.LocationName))
            return ToolResult.Fail("locationName is required.");

        var traceId = _traceIdFactory();
        var payload = new JsonObject();
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
        var statusPolls = await PollUntilTerminalAsync(commandResult, ct);
        var finalStatus = statusPolls.Count > 0 ? statusPolls[^1] : null;

        return ToolResult.Ok(StardewNpcToolJson.Serialize(new StardewNpcActionToolResult(
            commandResult.Accepted,
            commandResult.CommandId,
            commandResult.Status,
            commandResult.FailureReason,
            commandResult.TraceId,
            finalStatus,
            statusPolls)));
    }

    private async Task<IReadOnlyList<GameCommandStatus>> PollUntilTerminalAsync(GameCommandResult commandResult, CancellationToken ct)
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
}

public sealed class StardewSpeakTool : ITool, IToolSchemaProvider
{
    private readonly IGameCommandService _commands;
    private readonly NpcRuntimeDescriptor _descriptor;
    private readonly Func<string> _traceIdFactory;
    private readonly Func<string> _idempotencyKeyFactory;
    private readonly StardewRuntimeActionController _runtimeActions;

    internal StardewSpeakTool(
        IGameCommandService commands,
        NpcRuntimeDescriptor descriptor,
        Func<string> traceIdFactory,
        Func<string> idempotencyKeyFactory,
        StardewRuntimeActionController runtimeActions)
    {
        _commands = commands;
        _descriptor = descriptor;
        _traceIdFactory = traceIdFactory;
        _idempotencyKeyFactory = idempotencyKeyFactory;
        _runtimeActions = runtimeActions;
    }

    public string Name => "stardew_speak";

    public string Description => "Ask this NPC to say a short non-blocking visible line through the Stardew bridge. Nearby players see an overhead bubble; far or cross-map players receive a phone message. Use this to keep the player informed instead of silently doing many move/status turns. The runtime binds npcId, saveId, traceId, and idempotency internally.";

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
        return ToolResult.Ok(StardewNpcToolJson.Serialize(commandResult));
    }
}

public sealed class StardewIdleMicroActionTool : ITool, IToolSchemaProvider
{
    private readonly IGameCommandService _commands;
    private readonly NpcRuntimeDescriptor _descriptor;
    private readonly Func<string> _traceIdFactory;
    private readonly Func<string> _idempotencyKeyFactory;
    private readonly StardewRuntimeActionController _runtimeActions;

    internal StardewIdleMicroActionTool(
        IGameCommandService commands,
        NpcRuntimeDescriptor descriptor,
        Func<string> traceIdFactory,
        Func<string> idempotencyKeyFactory,
        StardewRuntimeActionController runtimeActions)
    {
        _commands = commands;
        _descriptor = descriptor;
        _traceIdFactory = traceIdFactory;
        _idempotencyKeyFactory = idempotencyKeyFactory;
        _runtimeActions = runtimeActions;
    }

    public string Name => "stardew_idle_micro_action";

    public string Description => "Executor-only idle micro action tool. Choose only from the approved idle micro action contract already selected by the parent autonomy lane.";

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
        return ToolResult.Ok(StardewNpcToolJson.Serialize(commandResult));
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

    public string Description => "Read the status of a Stardew command previously returned by an NPC action tool.";

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

    internal StardewOpenPrivateChatTool(
        IGameCommandService commands,
        NpcRuntimeDescriptor descriptor,
        Func<string> traceIdFactory,
        Func<string> idempotencyKeyFactory,
        StardewRuntimeActionController runtimeActions)
    {
        _commands = commands;
        _descriptor = descriptor;
        _traceIdFactory = traceIdFactory;
        _idempotencyKeyFactory = idempotencyKeyFactory;
        _runtimeActions = runtimeActions;
    }

    public string Name => "stardew_open_private_chat";

    public string Description => "Open an in-game private chat input for this NPC so the player can type a message. Use this only when the observed facts make private chat appropriate.";

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
        return ToolResult.Ok(StardewNpcToolJson.Serialize(commandResult));
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

    public StardewRuntimeActionController(
        NpcRuntimeDriver? runtimeDriver,
        WorldCoordinationService? worldCoordination,
        Func<DateTime>? nowUtc,
        TimeSpan? actionTimeout)
    {
        _runtimeDriver = runtimeDriver;
        _worldCoordination = worldCoordination;
        _nowUtc = nowUtc ?? (() => DateTime.UtcNow);
        _actionTimeout = actionTimeout ?? DefaultActionTimeout;
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
                    ToWorkType(action.Type),
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
            return new StardewPreparedActionState(workItemId, claimId, startedAtUtc, action.TraceId, null);
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

        await _runtimeDriver.SetLastTerminalCommandStatusAsync(status, ct);
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

    private static string ToWorkType(GameActionType actionType)
        => actionType switch
        {
            GameActionType.Move => "move",
            GameActionType.Speak => "speak",
            GameActionType.IdleMicroAction => "idle_micro_action",
            GameActionType.OpenPrivateChat => "open_private_chat",
            _ => "action"
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

internal sealed record StardewPreparedActionState(
    string WorkItemId,
    string? ClaimId,
    DateTime StartedAtUtc,
    string TraceId,
    GameCommandResult? BlockedResult)
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

public sealed class StardewMoveToolParameters
{
    public required string Destination { get; init; }

    public string? Reason { get; init; }

    public string? Thought { get; init; }
}

public sealed class StardewNavigateToTileToolParameters
{
    public required string LocationName { get; init; }

    public required int X { get; init; }

    public required int Y { get; init; }

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

    public static JsonElement Move()
        => Schema(
            new Dictionary<string, object>
            {
                ["destination"] = new { type = "string", description = "Exact destination identifier from the latest observation's destination[n].destinationId stable key. Never invent destinations." },
                ["reason"] = new { type = "string", description = "Short reason copied from the destination[n] fact's reason field, or a brief NPC intent aligned with the destination." },
                ["thought"] = new { type = "string", description = "Optional short inner thought shown as a non-blocking overhead bubble when movement starts. Keep it immersive and under one sentence." }
            },
            ["destination"]);

    public static JsonElement NavigateToTile()
        => Schema(
            new Dictionary<string, object>
            {
                ["locationName"] = new { type = "string", description = "Exact Stardew location/map name selected by the parent model from disclosed map skill facts." },
                ["x"] = new { type = "integer", description = "Target tile X coordinate copied exactly from the parent intent." },
                ["y"] = new { type = "integer", description = "Target tile Y coordinate copied exactly from the parent intent." },
                ["facingDirection"] = new { type = "integer", description = "Optional Stardew facing direction copied from the parent intent when supplied." },
                ["reason"] = new { type = "string", description = "Short reason from the parent intent." },
                ["thought"] = new { type = "string", description = "Optional short inner thought shown as a non-blocking overhead bubble when movement starts." }
            },
            ["locationName", "x", "y"]);

    public static JsonElement Speak()
        => Schema(
            new Dictionary<string, object>
            {
                ["text"] = new { type = "string", description = "Short line for the NPC to say." },
                ["channel"] = new { type = "string", description = "Delivery channel; defaults to player." }
            },
            ["text"]);

    public static JsonElement IdleMicroAction()
        => Schema(
            new Dictionary<string, object>
            {
                ["kind"] = new { type = "string", description = "Approved idle micro action kind selected from the fixed whitelist." },
                ["animationAlias"] = new { type = "string", description = "Optional allowlisted animation alias when kind is idle_animation_once." },
                ["intensity"] = new { type = "string", description = "Optional light intensity label from the parent intent." },
                ["ttlSeconds"] = new { type = "integer", description = "Optional TTL for the idle micro action." }
            },
            ["kind"]);

    public static JsonElement TaskStatus()
        => Schema(
            new Dictionary<string, object>
            {
                ["commandId"] = new { type = "string", description = "Command id returned by stardew_move or stardew_speak." }
            },
            ["commandId"]);

    public static JsonElement OpenPrivateChat()
        => Schema(
            new Dictionary<string, object>
            {
                ["prompt"] = new { type = "string", description = "Optional short prompt shown or logged with the private chat request." }
            },
            []);

    public static JsonElement SocialStatus()
        => Schema(
            new Dictionary<string, object>
            {
                ["targetNpcId"] = new { type = "string", description = "Optional NPC id/name to inspect. Omit to inspect this NPC." }
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
