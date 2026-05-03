namespace Hermes.Agent.Games.Stardew;

using System.Text.Json;
using System.Text.Json.Nodes;
using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.Runtime;

public static class StardewNpcToolFactory
{
    public static IReadOnlyList<ITool> CreateDefault(
        IGameAdapter adapter,
        NpcRuntimeDescriptor descriptor,
        Func<string>? traceIdFactory = null,
        Func<string>? idempotencyKeyFactory = null,
        int maxStatusPolls = 3,
        NpcRuntimeDriver? runtimeDriver = null,
        WorldCoordinationService? worldCoordination = null,
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
            new StardewSpeakTool(adapter.Commands, descriptor, traceIdFactory, idempotencyKeyFactory, runtimeActions),
            new StardewOpenPrivateChatTool(adapter.Commands, descriptor, traceIdFactory, idempotencyKeyFactory, runtimeActions),
            new StardewTaskStatusTool(adapter.Commands)
        ];
    }
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

    public string Description => "Ask this NPC to move to a Stardew tile. The runtime binds npcId, saveId, traceId, and idempotency internally.";

    public Type ParametersType => typeof(StardewMoveToolParameters);

    public JsonElement GetParameterSchema() => StardewNpcToolSchemas.Move();

    public async Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
    {
        var p = (StardewMoveToolParameters)parameters;
        if (string.IsNullOrWhiteSpace(p.LocationName))
            return ToolResult.Fail("locationName is required.");

        var action = new GameAction(
            _descriptor.NpcId,
            _descriptor.GameId,
            GameActionType.Move,
            _traceIdFactory(),
            _idempotencyKeyFactory(),
            new GameActionTarget("tile", p.LocationName, new GameTile(p.X, p.Y)),
            p.Reason,
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

    public string Description => "Ask this NPC to say a short line through the Stardew bridge. The runtime binds npcId, saveId, traceId, and idempotency internally.";

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
            var claimResult = _worldCoordination.TryClaimMove(
                claimId,
                action.NpcId,
                action.TraceId,
                new ClaimedTile(action.Target.LocationName!, action.Target.Tile!.X, action.Target.Tile.Y),
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

        var claimId = snapshot.ActionSlot?.WorkItemId ?? snapshot.PendingWorkItem?.WorkItemId ?? status.CommandId;
        await ClearAsync(claimId, GetCooldownAtUtc(status.Status, status.RetryAfterUtc), ct);
    }

    public Task ClearAsync(DateTime? nextWakeAtUtc, CancellationToken ct)
    {
        var snapshot = _runtimeDriver?.Snapshot();
        var claimId = snapshot?.ActionSlot?.WorkItemId ?? snapshot?.PendingWorkItem?.WorkItemId;
        return ClearAsync(claimId, nextWakeAtUtc, ct);
    }

    public static bool IsTerminalStatus(string? status)
        => string.Equals(status, StardewCommandStatuses.Completed, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, StardewCommandStatuses.Failed, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, StardewCommandStatuses.Cancelled, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, StardewCommandStatuses.Blocked, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, StardewCommandStatuses.Expired, StringComparison.OrdinalIgnoreCase);

    public static bool IsInFlightStatus(string? status)
        => string.Equals(status, StardewCommandStatuses.Queued, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, StardewCommandStatuses.Running, StringComparison.OrdinalIgnoreCase);

    private async Task ClearAsync(string? claimId, DateTime? nextWakeAtUtc, CancellationToken ct)
    {
        if (_runtimeDriver is null)
            return;

        if (!string.IsNullOrWhiteSpace(claimId) && _worldCoordination is not null)
            _worldCoordination.ReleaseClaim(claimId);

        await _runtimeDriver.SetActionSlotAsync(null, ct);
        await _runtimeDriver.SetPendingWorkItemAsync(null, ct);
        await _runtimeDriver.SetNextWakeAtUtcAsync(nextWakeAtUtc, ct);
    }

    private static bool RequiresMoveClaim(GameAction action)
        => action.Type == GameActionType.Move &&
           action.Target.Tile is not null &&
           !string.IsNullOrWhiteSpace(action.Target.LocationName);

    private static string ToWorkType(GameActionType actionType)
        => actionType switch
        {
            GameActionType.Move => "move",
            GameActionType.Speak => "speak",
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

public sealed class StardewMoveToolParameters
{
    public required string LocationName { get; init; }

    public int X { get; init; }

    public int Y { get; init; }

    public string? Reason { get; init; }
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
                ["locationName"] = new { type = "string", description = "Stardew location name, for example Town." },
                ["x"] = new { type = "integer", description = "Target tile X coordinate." },
                ["y"] = new { type = "integer", description = "Target tile Y coordinate." },
                ["reason"] = new { type = "string", description = "Short reason for the move." }
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

    private static JsonElement Schema(Dictionary<string, object> properties, string[] required)
        => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties,
            required
        }, JsonOptions);
}
