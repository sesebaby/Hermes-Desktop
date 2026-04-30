namespace StardewHermesBridge.Bridge;

using System.Collections.Concurrent;
using StardewHermesBridge.Dialogue;
using StardewHermesBridge.Logging;
using StardewModdingAPI;
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
            return Error<MoveAcceptedData>(envelope.TraceId, envelope.RequestId, "invalid_target", "npcId is required.", retryable: false);

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
            return Error<TaskStatusData>(envelope.TraceId, envelope.RequestId, "command_not_found", "Command was not found.", retryable: false);

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
            return Error<TaskStatusData>(envelope.TraceId, envelope.RequestId, "command_not_found", "Command was not found.", retryable: false);

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

    public BridgeResponse<SpeakData> Speak(BridgeEnvelope<SpeakPayload> envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope.NpcId))
            return Error<SpeakData>(envelope.TraceId, envelope.RequestId, "invalid_target", "npcId is required.", retryable: false);

        if (string.IsNullOrWhiteSpace(envelope.Payload.Text))
            return Error<SpeakData>(envelope.TraceId, envelope.RequestId, "invalid_target", "text is required.", retryable: false);

        if (!Context.IsWorldReady || Game1.player is null)
        {
            _logger.Write("action_speak_failed", envelope.NpcId, "speak", envelope.TraceId, null, "failed", "world_not_ready");
            return Error<SpeakData>(envelope.TraceId, envelope.RequestId, "world_not_ready", "The Stardew world is not ready.", retryable: true);
        }

        var npc = Game1.getCharacterFromName(envelope.NpcId, mustBeVillager: false, includeEventActors: false);
        if (npc is null)
        {
            _logger.Write("action_speak_failed", envelope.NpcId, "speak", envelope.TraceId, null, "failed", "invalid_target");
            return Error<SpeakData>(envelope.TraceId, envelope.RequestId, "invalid_target", "NPC was not found.", retryable: false);
        }

        var channel = string.IsNullOrWhiteSpace(envelope.Payload.Channel) ? "player" : envelope.Payload.Channel;
        NpcRawDialogueRenderer.Display(npc, envelope.Payload.Text);
        _logger.Write("action_speak_completed", envelope.NpcId, "speak", envelope.TraceId, null, "completed", null);
        return new BridgeResponse<SpeakData>(
            true,
            envelope.TraceId,
            envelope.RequestId,
            null,
            "completed",
            new SpeakData(envelope.NpcId, envelope.Payload.Text, channel, true),
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

        var npc = Game1.getCharacterFromName(command.NpcId, mustBeVillager: false, includeEventActors: false);
        if (npc is null)
        {
            command.Fail("invalid_target");
            _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", "invalid_target");
            return command.ToStatusData();
        }

        var targetLocation = Game1.getLocationFromName(command.LocationName);
        if (targetLocation is null)
        {
            command.Fail("invalid_target");
            _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", $"location_not_found:{command.LocationName}");
            return command.ToStatusData();
        }

        var targetTile = new Microsoft.Xna.Framework.Vector2(command.TargetTile.X, command.TargetTile.Y);
        if (!targetLocation.isTileLocationOpen(targetTile) || !targetLocation.CanSpawnCharacterHere(targetTile))
        {
            command.Fail("invalid_target");
            _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", $"tile_blocked:{command.LocationName}:{command.TargetTile.X},{command.TargetTile.Y}");
            return command.ToStatusData();
        }

        command.Start();
        npc.currentLocation?.characters.Remove(npc);
        targetLocation.characters.Remove(npc);
        npc.currentLocation = targetLocation;
        npc.Halt();
        npc.setTilePosition(command.TargetTile.X, command.TargetTile.Y);
        targetLocation.addCharacter(npc);
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

    private static BridgeResponse<TData> Error<TData>(string traceId, string requestId, string code, string message, bool retryable)
        => new(false, traceId, requestId, null, "failed", default, new BridgeError(code, message, retryable), new { });
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
            TraceId,
            NpcId,
            "move",
            Status,
            _startedAtUtc,
            (long)(DateTime.UtcNow - (_startedAtUtc ?? _createdAtUtc)).TotalMilliseconds,
            Status == "completed" ? 1.0 : Status == "running" ? 0.5 : 0,
            BlockedReason,
            ErrorCode);
}
