namespace StardewHermesBridge.Bridge;

using System.Collections.Concurrent;
using StardewHermesBridge.Logging;
using StardewValley;

public sealed class BridgeCommandQueue
{
    private readonly ConcurrentQueue<BridgeMoveCommand> _pending = new();
    private readonly ConcurrentDictionary<string, BridgeMoveCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _idempotency = new(StringComparer.OrdinalIgnoreCase);
    private readonly SmapiBridgeLogger _logger;

    public BridgeCommandQueue(SmapiBridgeLogger logger)
    {
        _logger = logger;
    }

    public BridgeResponse<MoveAcceptedData> EnqueueMove(BridgeEnvelope<MovePayload> envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope.NpcId))
            return Error<MoveAcceptedData>(envelope, "invalid_target", "npcId is required.", retryable: false);

        if (!string.IsNullOrWhiteSpace(envelope.IdempotencyKey) &&
            _idempotency.TryGetValue(envelope.IdempotencyKey, out var existingCommandId) &&
            _commands.TryGetValue(existingCommandId, out var existing))
        {
            return Accepted(envelope, existing);
        }

        var commandId = $"cmd_move_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}"[..38];
        var command = new BridgeMoveCommand(
            commandId,
            envelope.TraceId,
            envelope.NpcId,
            envelope.Payload.Target.LocationName,
            envelope.Payload.Target.Tile,
            envelope.IdempotencyKey);
        _commands[commandId] = command;
        if (!string.IsNullOrWhiteSpace(envelope.IdempotencyKey))
            _idempotency[envelope.IdempotencyKey] = commandId;

        _pending.Enqueue(command);
        _logger.Write("task_move_enqueued", command.NpcId, "move", command.TraceId, command.CommandId, "queued", null);
        return Accepted(envelope, command);
    }

    public BridgeResponse<TaskStatusData> GetStatus(BridgeEnvelope<TaskStatusRequest> envelope)
    {
        if (!_commands.TryGetValue(envelope.Payload.CommandId, out var command))
            return Error<TaskStatusData>(envelope, "command_not_found", "Command was not found.", retryable: false);

        return new BridgeResponse<TaskStatusData>(
            true,
            command.TraceId,
            envelope.RequestId,
            command.CommandId,
            command.Status,
            command.ToStatusData(),
            null,
            new { });
    }

    public BridgeResponse<TaskStatusData> Cancel(BridgeEnvelope<TaskCancelRequest> envelope)
    {
        if (!_commands.TryGetValue(envelope.Payload.CommandId, out var command))
            return Error<TaskStatusData>(envelope, "command_not_found", "Command was not found.", retryable: false);

        command.Cancel(envelope.Payload.Reason);
        _logger.Write("task_cancelled", command.NpcId, "move", command.TraceId, command.CommandId, "cancelled", envelope.Payload.Reason);
        return new BridgeResponse<TaskStatusData>(
            true,
            command.TraceId,
            envelope.RequestId,
            command.CommandId,
            command.Status,
            command.ToStatusData(),
            null,
            new { });
    }

    public TaskStatusData? PumpOneTick()
    {
        if (!_pending.TryDequeue(out var command))
            return null;

        if (!Context.IsWorldReady || Game1.player is null)
        {
            command.Fail("world_not_ready");
            _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", "world_not_ready");
            return command.ToStatusData();
        }

        // Phase 1 scaffold: command reaches the game loop and records completion.
        // Real pathing/NPC displacement will replace this checkpoint without changing the bridge contract.
        command.Start();
        command.Complete();
        _logger.Write("task_completed", command.NpcId, "move", command.TraceId, command.CommandId, "completed", null);
        return command.ToStatusData();
    }

    public void Drain(string reason)
    {
        foreach (var command in _commands.Values.Where(command => command.Status is "queued" or "running"))
            command.Fail(reason);
    }

    public void Clear()
    {
        _commands.Clear();
        _idempotency.Clear();
        while (_pending.TryDequeue(out _))
        {
        }
    }

    private static BridgeResponse<MoveAcceptedData> Accepted(BridgeEnvelope<MovePayload> envelope, BridgeMoveCommand command)
        => new(
            true,
            command.TraceId,
            envelope.RequestId,
            command.CommandId,
            command.Status,
            new MoveAcceptedData(true, new MoveClaim(command.NpcId, command.TargetTile, command.TargetTile)),
            null,
            new { });

    private static BridgeResponse<TData> Error<TData>(BridgeEnvelope<object> envelope, string code, string message, bool retryable)
        => new(false, envelope.TraceId, envelope.RequestId, null, "failed", default, new BridgeError(code, message, retryable), new { });

    private static BridgeResponse<TData> Error<TData, TPayload>(BridgeEnvelope<TPayload> envelope, string code, string message, bool retryable)
        => new(false, envelope.TraceId, envelope.RequestId, null, "failed", default, new BridgeError(code, message, retryable), new { });
}

public sealed class BridgeMoveCommand
{
    private readonly DateTime _createdAtUtc = DateTime.UtcNow;
    private DateTime? _startedAtUtc;

    public BridgeMoveCommand(string commandId, string traceId, string npcId, string locationName, TileDto targetTile, string? idempotencyKey)
    {
        CommandId = commandId;
        TraceId = traceId;
        NpcId = npcId;
        LocationName = locationName;
        TargetTile = targetTile;
        IdempotencyKey = idempotencyKey;
    }

    public string CommandId { get; }
    public string TraceId { get; }
    public string NpcId { get; }
    public string LocationName { get; }
    public TileDto TargetTile { get; }
    public string? IdempotencyKey { get; }
    public string Status { get; private set; } = "queued";
    public string? BlockedReason { get; private set; }
    public string? ErrorCode { get; private set; }

    public void Start()
    {
        _startedAtUtc = DateTime.UtcNow;
        Status = "running";
    }

    public void Complete()
    {
        Status = "completed";
    }

    public void Cancel(string reason)
    {
        Status = "cancelled";
        BlockedReason = reason;
    }

    public void Fail(string errorCode)
    {
        Status = "failed";
        ErrorCode = errorCode;
        BlockedReason = errorCode;
    }

    public TaskStatusData ToStatusData()
        => new(
            CommandId,
            NpcId,
            "move",
            Status,
            _startedAtUtc,
            (long)(DateTime.UtcNow - (_startedAtUtc ?? _createdAtUtc)).TotalMilliseconds,
            Status == "completed" ? 1.0 : Status == "running" ? 0.5 : 0,
            BlockedReason,
            ErrorCode);
}
