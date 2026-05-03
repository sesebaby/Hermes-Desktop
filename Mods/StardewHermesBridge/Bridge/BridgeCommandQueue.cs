namespace StardewHermesBridge.Bridge;

using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Microsoft.Xna.Framework;
using StardewHermesBridge.Dialogue;
using StardewHermesBridge.Logging;
using StardewHermesBridge.Ui;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Pathfinding;

public sealed class BridgeCommandQueue
{
    private readonly ConcurrentQueue<BridgeMoveCommand> _pending = new();
    private readonly ConcurrentQueue<IBridgeUiCommand> _pendingUi = new();
    private readonly ConcurrentDictionary<string, BridgeMoveCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _idempotency = new(StringComparer.OrdinalIgnoreCase);
    private readonly SmapiBridgeLogger _logger;
    private readonly BridgeEventBuffer _events;
    private readonly Action<string>? _privateChatOpened;
    private readonly Action<string>? _privateChatSubmitted;
    private readonly Action<string, string>? _privateChatReplyDisplayed;
    private readonly object _privateChatInputGate = new();
    private BridgePrivateChatInput? _privateChatInput;
    private BridgeMoveCommand? _activeMove;

    public BridgeCommandQueue(
        SmapiBridgeLogger logger,
        BridgeEventBuffer? events = null,
        Action<string>? privateChatOpened = null,
        Action<string>? privateChatSubmitted = null,
        Action<string, string>? privateChatReplyDisplayed = null)
    {
        _logger = logger;
        _events = events ?? new BridgeEventBuffer();
        _privateChatOpened = privateChatOpened;
        _privateChatSubmitted = privateChatSubmitted;
        _privateChatReplyDisplayed = privateChatReplyDisplayed;
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
            envelope.Payload.FacingDirection,
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

    public BridgeResponse<TaskStatusData> LookupByIdempotency(BridgeEnvelope<TaskLookupRequest> envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope.Payload.IdempotencyKey))
            return Error<TaskStatusData>(envelope.TraceId, envelope.RequestId, "command_not_found", "Idempotency key was not found.", retryable: false);

        if (!_idempotency.TryGetValue(envelope.Payload.IdempotencyKey, out var commandId) ||
            !_commands.TryGetValue(commandId, out var command))
        {
            return Error<TaskStatusData>(envelope.TraceId, envelope.RequestId, "command_not_found", "Idempotency key was not found.", retryable: false);
        }

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

    public Task<BridgeResponse<SpeakData>> SpeakAsync(BridgeEnvelope<SpeakPayload> envelope, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(envelope.NpcId))
            return Task.FromResult(Error<SpeakData>(envelope.TraceId, envelope.RequestId, "invalid_target", "npcId is required.", retryable: false));

        if (string.IsNullOrWhiteSpace(envelope.Payload.Text))
            return Task.FromResult(Error<SpeakData>(envelope.TraceId, envelope.RequestId, "invalid_target", "text is required.", retryable: false));

        var completion = new TaskCompletionSource<BridgeResponse<SpeakData>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingUi.Enqueue(new BridgeSpeakUiCommand(envelope, completion));
        return completion.Task.WaitAsync(ct);
    }

    private BridgeResponse<SpeakData> ExecuteSpeak(BridgeEnvelope<SpeakPayload> envelope)
    {
        var npcId = envelope.NpcId;
        if (string.IsNullOrWhiteSpace(npcId))
            return Error<SpeakData>(envelope.TraceId, envelope.RequestId, "invalid_target", "npcId is required.", retryable: false);

        if (!Context.IsWorldReady || Game1.player is null)
        {
            _logger.Write("action_speak_failed", npcId, "speak", envelope.TraceId, null, "failed", "world_not_ready");
            return Error<SpeakData>(envelope.TraceId, envelope.RequestId, "world_not_ready", "The Stardew world is not ready.", retryable: true);
        }

        var npc = BridgeNpcResolver.Resolve(npcId);
        if (npc is null)
        {
            _logger.Write("action_speak_failed", npcId, "speak", envelope.TraceId, null, "failed", "invalid_target");
            return Error<SpeakData>(envelope.TraceId, envelope.RequestId, "invalid_target", "NPC was not found.", retryable: false);
        }

        var channel = string.IsNullOrWhiteSpace(envelope.Payload.Channel) ? "player" : envelope.Payload.Channel;
        NpcRawDialogueRenderer.Display(npc, envelope.Payload.Text);
        if (string.Equals(channel, "private_chat", StringComparison.OrdinalIgnoreCase))
        {
            var conversationId = string.IsNullOrWhiteSpace(envelope.Payload.ConversationId)
                ? envelope.RequestId
                : envelope.Payload.ConversationId;
            _events.Record(
                "private_chat_reply_displayed",
                npc.Name,
                $"{npc.Name} private chat reply displayed.",
                conversationId,
                new JsonObject { ["conversationId"] = conversationId });
            _privateChatReplyDisplayed?.Invoke(npc.Name, conversationId);
        }
        _logger.Write("action_speak_completed", npcId, "speak", envelope.TraceId, null, "completed", null);
        return new BridgeResponse<SpeakData>(
            true,
            envelope.TraceId,
            envelope.RequestId,
            null,
            "completed",
            new SpeakData(npcId, envelope.Payload.Text, channel, true),
            null,
            new { });
    }

    public Task<BridgeResponse<OpenPrivateChatData>> OpenPrivateChatAsync(BridgeEnvelope<OpenPrivateChatPayload> envelope, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(envelope.NpcId))
            return Task.FromResult(Error<OpenPrivateChatData>(envelope.TraceId, envelope.RequestId, "invalid_target", "npcId is required.", retryable: false));

        var completion = new TaskCompletionSource<BridgeResponse<OpenPrivateChatData>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingUi.Enqueue(new BridgeOpenPrivateChatUiCommand(envelope, completion));
        return completion.Task.WaitAsync(ct);
    }

    private BridgeResponse<OpenPrivateChatData> ExecuteOpenPrivateChat(BridgeEnvelope<OpenPrivateChatPayload> envelope)
    {
        var npcId = envelope.NpcId;
        if (string.IsNullOrWhiteSpace(npcId))
            return Error<OpenPrivateChatData>(envelope.TraceId, envelope.RequestId, "invalid_target", "npcId is required.", retryable: false);

        if (!Context.IsWorldReady || Game1.player is null)
        {
            _logger.Write("action_open_private_chat_failed", npcId, "open_private_chat", envelope.TraceId, null, "failed", "world_not_ready");
            return Error<OpenPrivateChatData>(envelope.TraceId, envelope.RequestId, "world_not_ready", "The Stardew world is not ready.", retryable: true);
        }

        if (Game1.activeClickableMenu is not null)
        {
            _logger.Write("action_open_private_chat_failed", npcId, "open_private_chat", envelope.TraceId, null, "failed", "menu_blocked");
            return Error<OpenPrivateChatData>(envelope.TraceId, envelope.RequestId, "menu_blocked", "A Stardew menu is already open.", retryable: true);
        }

        var npc = BridgeNpcResolver.Resolve(npcId);
        if (npc is null)
        {
            _logger.Write("action_open_private_chat_failed", npcId, "open_private_chat", envelope.TraceId, null, "failed", "invalid_target");
            return Error<OpenPrivateChatData>(envelope.TraceId, envelope.RequestId, "invalid_target", "NPC was not found.", retryable: false);
        }

        var conversationId = string.IsNullOrWhiteSpace(envelope.Payload.ConversationId)
            ? envelope.RequestId
            : envelope.Payload.ConversationId;
        MarkPrivateChatInputOpened(npc.Name, conversationId, envelope.TraceId);
        var inputMenu = new PrivateChatInputMenu(
            npc,
            envelope.Payload.Prompt,
            submittedText =>
            {
                var submitted = SanitizePrivateChatText(submittedText);
                ClearPrivateChatInput(conversationId);
                if (!string.IsNullOrWhiteSpace(submitted))
                {
                    _events.Record(
                        "player_private_message_submitted",
                        npc.Name,
                        "Player submitted a private chat message.",
                        conversationId,
                        new JsonObject
                        {
                            ["conversationId"] = conversationId,
                            ["text"] = submitted,
                            ["submittedAtUtc"] = DateTime.UtcNow
                        });
                    _privateChatSubmitted?.Invoke(npc.Name);
                    _logger.Write("private_chat_message_submitted", npc.Name, "private_chat", envelope.TraceId, null, "recorded", null);
                }
                else
                {
                    _events.Record(
                        "player_private_message_cancelled",
                        npc.Name,
                        "Player cancelled private chat.",
                        conversationId,
                        new JsonObject { ["conversationId"] = conversationId });
                    _logger.Write("private_chat_message_cancelled", npc.Name, "private_chat", envelope.TraceId, null, "recorded", null);
                }
            },
            () =>
            {
                RecordPrivateChatInputClosedWithoutSubmit();
            });

        _events.Record(
            "private_chat_opened",
            npc.Name,
            $"{npc.Name} private chat input opened.",
            conversationId,
            new JsonObject
            {
                ["conversationId"] = conversationId,
                ["prompt"] = envelope.Payload.Prompt
            });
        Game1.activeClickableMenu = inputMenu;
        _privateChatOpened?.Invoke(npc.Name);
        _logger.Write("action_open_private_chat_completed", npc.Name, "open_private_chat", envelope.TraceId, null, "completed", null);
        return new BridgeResponse<OpenPrivateChatData>(
            true,
            envelope.TraceId,
            envelope.RequestId,
            null,
            "completed",
            new OpenPrivateChatData(npc.Name, true),
            null,
            new { });
    }

    public void RecordPrivateChatInputClosedWithoutSubmit()
    {
        BridgePrivateChatInput? input;
        lock (_privateChatInputGate)
        {
            input = _privateChatInput;
            _privateChatInput = null;
        }

        if (input is null)
            return;

        _events.Record(
            "player_private_message_cancelled",
            input.NpcName,
            "Player closed private chat without submitting.",
            input.ConversationId,
            new JsonObject
            {
                ["conversationId"] = input.ConversationId,
                ["reason"] = "closed_without_submit"
            });
        _logger.Write("private_chat_input_closed_without_submit", input.NpcName, "private_chat", input.TraceId, null, "recorded", null);
    }

    public TaskStatusData? PumpOneTick()
    {
        var uiStatus = TryPumpUiCommand();
        if (uiStatus is not null)
            return uiStatus;

        var command = _activeMove;
        if (command is null && !_pending.TryDequeue(out command))
            return null;

        var status = PumpMoveCommand(command);
        _activeMove = command.Status == "running" ? command : null;
        return status;
    }

    private TaskStatusData PumpMoveCommand(BridgeMoveCommand command)
    {
        if (command.IsTerminal)
            return command.ToStatusData();

        if (!Context.IsWorldReady || Game1.player is null)
        {
            command.Fail("world_not_ready");
            _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", "world_not_ready");
            return command.ToStatusData();
        }

        var npc = BridgeNpcResolver.Resolve(command.NpcId);
        if (npc is null)
        {
            command.Fail("invalid_target");
            _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", "invalid_target");
            return command.ToStatusData();
        }

        var currentLocation = npc.currentLocation;
        if (currentLocation is null)
        {
            command.Fail("invalid_target");
            _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", "current_location_missing");
            return command.ToStatusData();
        }

        var targetLocation = Game1.getLocationFromName(command.LocationName);
        if (targetLocation is null)
        {
            command.Fail("invalid_target");
            _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", $"location_not_found:{command.LocationName}");
            return command.ToStatusData();
        }

        if (!ReferenceEquals(currentLocation, targetLocation))
        {
            command.Block("cross_location_unsupported");
            _logger.Write("task_blocked", command.NpcId, "move", command.TraceId, command.CommandId, "blocked", "cross_location_unsupported");
            return command.ToStatusData();
        }

        if (!IsTileSafeForMove(targetLocation, command.TargetTile))
        {
            command.Fail("invalid_target");
            _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", $"tile_blocked:{command.LocationName}:{command.TargetTile.X},{command.TargetTile.Y}");
            return command.ToStatusData();
        }

        var currentTile = new TileDto((int)npc.Tile.X, (int)npc.Tile.Y);

        if (command.Status == "queued")
        {
            if (!command.TryPrepareSchedulePath(npc, currentTile, targetLocation, out var pathFailure))
            {
                command.Fail(pathFailure ?? "path_unreachable");
                _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", command.BlockedReason);
                return command.ToStatusData();
            }

            command.Start();
            _logger.Write("task_running", command.NpcId, "move", command.TraceId, command.CommandId, "running", $"started;pathSteps={command.PathStepsRemaining}");
            return command.ToStatusData();
        }

        if (command.ConsumeStepDelayTick())
            return command.ToStatusData();

        if (currentTile.X == command.TargetTile.X && currentTile.Y == command.TargetTile.Y)
        {
            ApplyArrivalFacing(npc, command);
            command.Complete();
            _logger.Write("task_completed", command.NpcId, "move", command.TraceId, command.CommandId, "completed", null);
            return command.ToStatusData();
        }

        var nextTile = command.NextScheduleStepFrom(currentTile);
        if (nextTile is null)
        {
            command.Fail("path_exhausted");
            _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", "path_exhausted");
            return command.ToStatusData();
        }

        if (!IsTileSafeForMove(targetLocation, nextTile))
        {
            command.Fail("invalid_target");
            _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", $"step_blocked:{command.LocationName}:{nextTile.X},{nextTile.Y}");
            return command.ToStatusData();
        }

        npc.Halt();
        npc.setTilePosition(nextTile.X, nextTile.Y);
        command.RecordStep();

        if (nextTile.X == command.TargetTile.X && nextTile.Y == command.TargetTile.Y)
        {
            ApplyArrivalFacing(npc, command);
            command.Complete();
            _logger.Write("task_completed", command.NpcId, "move", command.TraceId, command.CommandId, "completed", null);
            return command.ToStatusData();
        }

        command.ResetStepDelay();
        _logger.Write("task_running", command.NpcId, "move", command.TraceId, command.CommandId, "running", $"tile={nextTile.X},{nextTile.Y};target={command.TargetTile.X},{command.TargetTile.Y}");
        return command.ToStatusData();
    }

    private static bool IsTileSafeForMove(GameLocation location, TileDto tile)
    {
        var vector = new Microsoft.Xna.Framework.Vector2(tile.X, tile.Y);
        return location.isTileLocationOpen(vector) && location.CanSpawnCharacterHere(vector);
    }

    private static void ApplyArrivalFacing(NPC npc, BridgeMoveCommand command)
    {
        if (command.FacingDirection is >= 0 and <= 3)
            npc.faceDirection(command.FacingDirection.Value);
    }

    public void Drain(string reason)
    {
        foreach (var command in _commands.Values.Where(command => command.Status is "queued" or "running"))
            command.Fail(reason);
        _activeMove = null;
    }

    public void Clear()
    {
        _commands.Clear();
        _idempotency.Clear();
        _activeMove = null;
        ClearPrivateChatInput(null);
        while (_pending.TryDequeue(out _))
        {
        }
    }

    private TaskStatusData? TryPumpUiCommand()
    {
        if (!_pendingUi.TryDequeue(out var command))
            return null;

        return command.Execute(this);
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

    private static string SanitizePrivateChatText(string value)
    {
        var normalized = value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        return normalized.Length <= 240 ? normalized : normalized[..240];
    }

    private void MarkPrivateChatInputOpened(string npcName, string conversationId, string traceId)
    {
        lock (_privateChatInputGate)
            _privateChatInput = new BridgePrivateChatInput(npcName, conversationId, traceId);
    }

    private void ClearPrivateChatInput(string? conversationId)
    {
        lock (_privateChatInputGate)
        {
            if (conversationId is null ||
                string.Equals(_privateChatInput?.ConversationId, conversationId, StringComparison.OrdinalIgnoreCase))
            {
                _privateChatInput = null;
            }
        }
    }

    private static TaskStatusData ToUiStatus<TData>(
        BridgeEnvelope<object> envelope,
        string action,
        BridgeResponse<TData> response)
        => new(
            "",
            envelope.TraceId,
            envelope.NpcId ?? "",
            action,
            response.Status ?? (response.Ok ? "completed" : "failed"),
            DateTime.UtcNow,
            0,
            response.Ok ? 1.0 : 0,
            response.Error?.Code,
            response.Error?.Code);

    private interface IBridgeUiCommand
    {
        TaskStatusData Execute(BridgeCommandQueue queue);
    }

    private sealed class BridgeSpeakUiCommand : IBridgeUiCommand
    {
        private readonly BridgeEnvelope<SpeakPayload> _envelope;
        private readonly TaskCompletionSource<BridgeResponse<SpeakData>> _completion;

        public BridgeSpeakUiCommand(
            BridgeEnvelope<SpeakPayload> envelope,
            TaskCompletionSource<BridgeResponse<SpeakData>> completion)
        {
            _envelope = envelope;
            _completion = completion;
        }

        public TaskStatusData Execute(BridgeCommandQueue queue)
        {
            try
            {
                var response = queue.ExecuteSpeak(_envelope);
                _completion.TrySetResult(response);
                return ToUiStatus(_envelope.ToUntyped(), "speak", response);
            }
            catch (Exception ex)
            {
                _completion.TrySetException(ex);
                return FailedUiStatus(_envelope.ToUntyped(), "speak", ex);
            }
        }
    }

    private sealed class BridgeOpenPrivateChatUiCommand : IBridgeUiCommand
    {
        private readonly BridgeEnvelope<OpenPrivateChatPayload> _envelope;
        private readonly TaskCompletionSource<BridgeResponse<OpenPrivateChatData>> _completion;

        public BridgeOpenPrivateChatUiCommand(
            BridgeEnvelope<OpenPrivateChatPayload> envelope,
            TaskCompletionSource<BridgeResponse<OpenPrivateChatData>> completion)
        {
            _envelope = envelope;
            _completion = completion;
        }

        public TaskStatusData Execute(BridgeCommandQueue queue)
        {
            try
            {
                var response = queue.ExecuteOpenPrivateChat(_envelope);
                _completion.TrySetResult(response);
                return ToUiStatus(_envelope.ToUntyped(), "open_private_chat", response);
            }
            catch (Exception ex)
            {
                _completion.TrySetException(ex);
                return FailedUiStatus(_envelope.ToUntyped(), "open_private_chat", ex);
            }
        }
    }

    private static TaskStatusData FailedUiStatus(BridgeEnvelope<object> envelope, string action, Exception ex)
        => new("", envelope.TraceId, envelope.NpcId ?? "", action, "failed", DateTime.UtcNow, 0, 0, ex.Message, ex.GetType().Name);
}

internal sealed record BridgePrivateChatInput(string NpcName, string ConversationId, string TraceId);

internal static class BridgeEnvelopeExtensions
{
    public static BridgeEnvelope<object> ToUntyped<TPayload>(this BridgeEnvelope<TPayload> envelope)
        => new(
            envelope.RequestId,
            envelope.TraceId,
            envelope.NpcId,
            envelope.SaveId,
            envelope.IdempotencyKey,
            envelope.Payload!);
}

public sealed class BridgeMoveCommand
{
    private const int StepDelayTicks = 8;

    private readonly DateTime _createdAtUtc = DateTime.UtcNow;
    private DateTime? _startedAtUtc;
    private int _stepDelayTicksRemaining;
    private int _stepsTaken;

    private Stack<Point>? _schedulePath;

    public BridgeMoveCommand(
        string commandId,
        string traceId,
        string npcId,
        string locationName,
        TileDto targetTile,
        int? facingDirection,
        string? idempotencyKey)
    {
        CommandId = commandId;
        TraceId = traceId;
        NpcId = npcId;
        LocationName = locationName;
        TargetTile = targetTile;
        FacingDirection = facingDirection;
        IdempotencyKey = idempotencyKey;
    }

    public string CommandId { get; }
    public string TraceId { get; }
    public string NpcId { get; }
    public string LocationName { get; }
    public TileDto TargetTile { get; }
    public int? FacingDirection { get; }
    public string? IdempotencyKey { get; }
    public string Status { get; private set; } = "queued";
    public string? BlockedReason { get; private set; }
    public string? ErrorCode { get; private set; }
    public bool IsTerminal => Status is "completed" or "cancelled" or "failed" or "blocked";
    public int PathStepsRemaining => _schedulePath?.Count ?? 0;

    public void Start()
    {
        if (Status != "queued")
            return;

        _startedAtUtc = DateTime.UtcNow;
        Status = "running";
        _stepDelayTicksRemaining = StepDelayTicks;
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

    public void Block(string reason)
    {
        Status = "blocked";
        ErrorCode = reason;
        BlockedReason = reason;
    }

    public bool ConsumeStepDelayTick()
    {
        if (_stepDelayTicksRemaining <= 0)
            return false;

        _stepDelayTicksRemaining--;
        return true;
    }

    public void ResetStepDelay()
        => _stepDelayTicksRemaining = StepDelayTicks;

    public void RecordStep()
        => _stepsTaken++;

    public bool TryPrepareSchedulePath(NPC npc, TileDto currentTile, GameLocation location, out string? failureReason)
    {
        failureReason = null;
        if (currentTile.X == TargetTile.X && currentTile.Y == TargetTile.Y)
            return true;

        _schedulePath = PathFindController.findPathForNPCSchedules(
            new Point(currentTile.X, currentTile.Y),
            new Point(TargetTile.X, TargetTile.Y),
            location,
            300,
            npc);

        TrimCurrentTileFromPath(currentTile);
        if (_schedulePath is { Count: > 0 })
            return true;

        failureReason = "path_unreachable";
        return false;
    }

    public TileDto? NextScheduleStepFrom(TileDto currentTile)
    {
        TrimCurrentTileFromPath(currentTile);
        if (_schedulePath is not { Count: > 0 })
            return null;

        var next = _schedulePath.Pop();
        return new TileDto(next.X, next.Y);
    }

    private void TrimCurrentTileFromPath(TileDto currentTile)
    {
        while (_schedulePath is { Count: > 0 } &&
               _schedulePath.Peek().X == currentTile.X &&
               _schedulePath.Peek().Y == currentTile.Y)
        {
            _schedulePath.Pop();
        }
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
            Status == "completed" ? 1.0 : Status == "running" ? Math.Min(0.9, 0.1 + (_stepsTaken * 0.2)) : 0,
            BlockedReason,
            ErrorCode);
}
