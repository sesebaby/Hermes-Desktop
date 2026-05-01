namespace StardewHermesBridge.Bridge;

using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using StardewHermesBridge.Dialogue;
using StardewHermesBridge.Logging;
using StardewHermesBridge.Ui;
using StardewModdingAPI;
using StardewValley;

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

        var npc = Game1.getCharacterFromName(npcId, mustBeVillager: false, includeEventActors: false);
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

        var npc = Game1.getCharacterFromName(npcId, mustBeVillager: false, includeEventActors: false);
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
