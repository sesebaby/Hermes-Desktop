namespace StardewHermesBridge.Bridge;

using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Microsoft.Xna.Framework;
using StardewHermesBridge.Logging;
using StardewHermesBridge.Ui;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Pathfinding;

public sealed class BridgeCommandQueue
{
    private const int MaxReplanAttempts = 2;
    private const int WarpTransitionTimeoutTicks = 180;

    private readonly ConcurrentQueue<BridgeMoveCommand> _pending = new();
    private readonly ConcurrentQueue<IBridgeUiCommand> _pendingUi = new();
    private readonly ConcurrentDictionary<string, BridgeMoveCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _idempotency = new(StringComparer.OrdinalIgnoreCase);
    private readonly SmapiBridgeLogger _logger;
    private readonly BridgeEventBuffer _events;
    private readonly HermesPhoneState _phoneState;
    private readonly StardewMessageDisplayRouter _messageRouter;
    private readonly NpcOverheadBubbleOverlay _bubbleOverlay;
    private readonly Action<string>? _privateChatOpened;
    private readonly Action<string>? _privateChatSubmitted;
    private readonly Action<string, string, string>? _privateChatReplyDisplayed;
    private readonly object _privateChatInputGate = new();
    private BridgePrivateChatInput? _privateChatInput;
    private BridgeMoveCommand? _activeMove;

    public BridgeCommandQueue(
        SmapiBridgeLogger logger,
        BridgeEventBuffer? events = null,
        HermesPhoneState? phoneState = null,
        StardewMessageDisplayRouter? messageRouter = null,
        NpcOverheadBubbleOverlay? bubbleOverlay = null,
        Action<string>? privateChatOpened = null,
        Action<string>? privateChatSubmitted = null,
        Action<string, string, string>? privateChatReplyDisplayed = null)
    {
        _logger = logger;
        _events = events ?? new BridgeEventBuffer();
        _phoneState = phoneState ?? new HermesPhoneState();
        _bubbleOverlay = bubbleOverlay ?? new NpcOverheadBubbleOverlay(_events, logger);
        _messageRouter = messageRouter ?? new StardewMessageDisplayRouter(
            _phoneState,
            _bubbleOverlay,
            new HermesPhoneOverlay(_phoneState, _events, logger, null, privateChatSubmitted),
            _events,
            logger);
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

        var target = ResolveMoveTarget(envelope.Payload);
        if (!target.Ok)
            return Error<MoveAcceptedData>(envelope.TraceId, envelope.RequestId, target.ErrorCode!, target.ErrorMessage!, retryable: false);

        var commandId = $"cmd_move_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}"[..38];
        var command = new BridgeMoveCommand(
            commandId,
            envelope.TraceId,
            envelope.NpcId,
            target.LocationName!,
            target.Tile!,
            target.FacingDirection,
            envelope.IdempotencyKey,
            target.DestinationId,
            envelope.Payload.Thought);
        _commands[commandId] = command;
        if (!string.IsNullOrWhiteSpace(envelope.IdempotencyKey))
            _idempotency[envelope.IdempotencyKey] = commandId;

        _pending.Enqueue(command);
        _logger.Write("task_move_enqueued", command.NpcId, "move", command.TraceId, command.CommandId, "queued", null);
        return Accepted(envelope, command);
    }

    private static ResolvedMoveTarget ResolveMoveTarget(MovePayload payload)
    {
        if (!string.IsNullOrWhiteSpace(payload.DestinationId))
        {
            if (!BridgeDestinationRegistry.TryResolve(payload.DestinationId, out var destination))
            {
                return ResolvedMoveTarget.Failed(
                    "invalid_destination_id",
                    $"Unknown destinationId: {payload.DestinationId}.");
            }

            return ResolvedMoveTarget.Success(
                destination.LocationName,
                destination.Tile,
                payload.FacingDirection ?? destination.FacingDirection,
                destination.DestinationId);
        }

        if (payload.Target is null)
            return ResolvedMoveTarget.Failed("invalid_target", "target or destinationId is required.");

        return ResolvedMoveTarget.Success(
            payload.Target.LocationName,
            payload.Target.Tile,
            payload.FacingDirection,
            null);
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

        var npc = BridgeNpcResolver.Resolve(command.NpcId);
        if (npc is not null)
            StopNpcMotion(npc);

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
        var privateChat = string.Equals(channel, "private_chat", StringComparison.OrdinalIgnoreCase);
        var conversationId = string.IsNullOrWhiteSpace(envelope.Payload.ConversationId)
            ? envelope.RequestId
            : envelope.Payload.ConversationId;
        var inputMenuReply = privateChat &&
            string.Equals(envelope.Payload.Source, "input_menu", StringComparison.OrdinalIgnoreCase);
        if (inputMenuReply)
            _privateChatReplyDisplayed?.Invoke(npc.Name, conversationId, "dialogue");

        var display = _messageRouter.Display(npc, envelope.Payload.Text, channel, envelope.Payload.ConversationId, envelope.Payload.Source);
        if (privateChat)
        {
            _events.Record(
                "private_chat_reply_displayed",
                npc.Name,
                $"{npc.Name} private chat reply displayed.",
                conversationId,
                new JsonObject
                {
                    ["conversationId"] = conversationId,
                    ["route"] = display.Route,
                    ["reply_closed_source"] = display.ReplyClosedSource,
                    ["source"] = envelope.Payload.Source
                });
            if (!inputMenuReply)
                _privateChatReplyDisplayed?.Invoke(npc.Name, conversationId, display.Route);
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

    public Task<BridgeResponse<DebugRepositionData>> RepositionNpcAsync(BridgeEnvelope<DebugRepositionPayload> envelope, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(envelope.NpcId))
            return Task.FromResult(Error<DebugRepositionData>(envelope.TraceId, envelope.RequestId, "invalid_target", "npcId is required.", retryable: false));

        if (!string.IsNullOrWhiteSpace(envelope.Payload.Target) &&
            !string.Equals(envelope.Payload.Target, "town", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Error<DebugRepositionData>(
                envelope.TraceId,
                envelope.RequestId,
                "invalid_target",
                "Only target=town is supported.",
                retryable: false));
        }

        var completion = new TaskCompletionSource<BridgeResponse<DebugRepositionData>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingUi.Enqueue(new BridgeDebugRepositionUiCommand(envelope, completion));
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
                            ["source"] = "input_menu",
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
                        new JsonObject
                        {
                            ["conversationId"] = conversationId,
                            ["source"] = "input_menu"
                        });
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
        _logger.Write("action_open_private_chat_completed", npc.Name, "open_private_chat", envelope.TraceId, null, "completed", "input_menu_opened");
        return new BridgeResponse<OpenPrivateChatData>(
            true,
            envelope.TraceId,
            envelope.RequestId,
            null,
            "completed",
            new OpenPrivateChatData(npc.Name, true, conversationId, "input_menu_opened"),
            null,
            new { });
    }

    private BridgeResponse<DebugRepositionData> ExecuteDebugReposition(BridgeEnvelope<DebugRepositionPayload> envelope)
    {
        var result = BridgeNpcDebugRepositioner.RepositionToTown(envelope.NpcId, _logger);
        if (!result.Ok)
        {
            return Error<DebugRepositionData>(
                envelope.TraceId,
                envelope.RequestId,
                result.ErrorCode ?? "debug_reposition_failed",
                result.ErrorMessage ?? "Debug reposition failed.",
                retryable: result.ErrorCode == "world_not_ready");
        }

        return new BridgeResponse<DebugRepositionData>(
            true,
            envelope.TraceId,
            envelope.RequestId,
            null,
            "completed",
            new DebugRepositionData(
                result.NpcId,
                result.FromLocationName,
                result.FromTile,
                result.TargetLocationName,
                result.TargetTile,
                result.FacingDirection,
                DebugTeleport: true),
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
                ["source"] = "input_menu",
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
            command.Fail("preflight_blocked", "world_not_ready");
            _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", "world_not_ready");
            return command.ToStatusData();
        }

        var npc = BridgeNpcResolver.Resolve(command.NpcId);
        if (npc is null)
        {
            command.Fail("preflight_blocked", "npc_not_found");
            _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", "invalid_target");
            return command.ToStatusData();
        }

        var currentLocation = npc.currentLocation;
        if (currentLocation is null)
        {
            command.Fail("preflight_blocked", "current_location_missing");
            _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", "current_location_missing");
            return command.ToStatusData();
        }

        var targetLocation = Game1.getLocationFromName(command.LocationName);
        if (targetLocation is null)
        {
            command.Fail("preflight_blocked", $"location_not_found:{command.LocationName}");
            _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", $"location_not_found:{command.LocationName}");
            return command.ToStatusData();
        }

        var currentTile = GetCurrentTile(npc);
        var currentLocationName = currentLocation.NameOrUniqueName ?? currentLocation.Name;

        if (command.IsAwaitingWarp)
            return HandleAwaitingWarp(command, npc);

        if (command.IsReplanningAfterWarp)
            return HandlePostWarpReplan(command, npc, currentLocation, currentLocationName, currentTile, targetLocation);

        if (!ReferenceEquals(currentLocation, targetLocation))
        {
            if (command.Status == "running" &&
                command.CurrentSegment is not null &&
                string.Equals(command.CurrentSegment.LocationName, currentLocationName, StringComparison.OrdinalIgnoreCase))
            {
                targetLocation = currentLocation;
            }
            else
            {
                var routeProbe = ProbeCrossLocationRoute(
                    npc,
                    currentLocationName,
                    currentTile,
                    currentLocation,
                    command.LocationName,
                    targetLocation,
                    command.TargetTile);
                command.RecordRouteProbe(routeProbe);
                if (!command.TryStartCrossMapSegment(routeProbe, out var failureCode))
                {
                    command.Fail(failureCode ?? "cross_location_route_unavailable");
                    _logger.Write(
                        "task_failed",
                        command.NpcId,
                        "move",
                        command.TraceId,
                        command.CommandId,
                        "failed",
                        FormatRouteProbeLogDetail(routeProbe));
                    return command.ToStatusData();
                }

                MaintainNpcMovementControl(npc);
                StopNpcMotion(npc);
                ShowMoveThoughtIfPresent(npc, command);
                _logger.Write(
                    "task_running",
                    command.NpcId,
                    "move",
                    command.TraceId,
                    command.CommandId,
                    "running",
                    $"cross_map_segment_started;{FormatRouteProbeLogDetail(routeProbe)}");
                return command.ToStatusData();
            }
        }

        if (command.Status == "running" &&
            command.CurrentSegment?.TargetKind == "warp_to_next_location" &&
            string.Equals(command.CurrentSegment.NextLocationName, currentLocationName, StringComparison.OrdinalIgnoreCase))
        {
            command.SetPhase("replanning_after_warp", currentLocationName);
            command.SetCrossMapPhase("replanning_after_warp");
            return command.ToStatusData();
        }

        var movementLocationName = command.CurrentSegment?.LocationName ?? command.LocationName;

        if (command.Status == "queued")
        {
            command.SetPhase("resolving_destination", currentLocationName, incrementRouteRevision: true);
            MaintainNpcMovementControl(npc);
            StopNpcMotion(npc);
            command.SetPhase("preflight", command.LocationName);
            var initialProbe = ProbeRoute(npc, currentTile, targetLocation, command.TargetTile);
            command.RecordRouteProbe(ToRouteProbeData(
                "same_location",
                initialProbe,
                currentLocationName,
                currentTile,
                command.LocationName,
                command.TargetTile));
            if (ShouldTryArrivalFallback(initialProbe))
            {
                command.SetPhase("resolving_arrival", command.LocationName, incrementRouteRevision: true);
                var resolved = BridgeMovementPathProbe.FindClosestReachableNeighbor(
                    npc, targetLocation, command.TargetTile, currentTile);
                if (resolved is not null)
                {
                    command.ReplaceTarget(resolved.Value.StandTile, resolved.Value.FacingDirection);
                    initialProbe = resolved.Value.Route;
                    command.RecordRouteProbe(ToRouteProbeData(
                        "same_location",
                        initialProbe,
                        currentLocationName,
                        currentTile,
                        command.LocationName,
                        command.TargetTile));
                    _logger.Write("task_target_resolved", command.NpcId, "move",
                        command.TraceId, command.CommandId, "running",
                        $"resolved={resolved.Value.StandTile.X},{resolved.Value.StandTile.Y};facing={resolved.Value.FacingDirection}");
                }
            }

            if (initialProbe.Status != BridgeRouteProbeStatus.RouteValid)
            {
                command.SetPhase("arriving");
                StopNpcMotion(npc);
                FailMoveForProbe(command, initialProbe, initial: true);
                _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", command.BlockedReason);
                return command.ToStatusData();
            }

            if (IsAtTileCenter(npc, command.TargetTile))
            {
                command.SetPhase("arriving", command.LocationName);
                ApplyArrivalFacing(npc, command);
                command.Complete();
                _logger.Write("task_completed", command.NpcId, "move", command.TraceId, command.CommandId, "completed", null);
                return command.ToStatusData();
            }

            command.SetPhase("planning_route", command.LocationName);
            command.ReplaceSchedulePath(initialProbe.Route);
            command.Start();
            ShowMoveThoughtIfPresent(npc, command);
            command.SetPhase("executing_segment", command.LocationName);
            _logger.Write("task_running", command.NpcId, "move", command.TraceId, command.CommandId, "running", $"started;pathSteps={command.PathStepsRemaining}");
            return command.ToStatusData();
        }

        MaintainNpcMovementControl(npc);

        var interruptReason = CheckInterrupt();
        if (interruptReason is not null)
        {
            StopNpcMotion(npc);
            command.Interrupt(interruptReason);
            _logger.Write("task_interrupted", command.NpcId, "move", command.TraceId, command.CommandId, "interrupted", interruptReason);
            return command.ToStatusData();
        }

        if (IsAtTileCenter(npc, command.TargetTile))
        {
            CompleteOrAwaitWarpAtSegmentTarget(command, npc);
            return command.ToStatusData();
        }

        var nextTile = command.NextScheduleStepFrom(currentTile);
        if (nextTile is null)
        {
            if (IsAtTileCenter(npc, command.TargetTile))
            {
                CompleteOrAwaitWarpAtSegmentTarget(command, npc);
            }
            else
            {
                StopNpcMotion(npc);
                command.Fail("path_exhausted");
                _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", "path_exhausted");
            }

            return command.ToStatusData();
        }

        var nextStepSafety = BridgeMovementPathProbe.CheckRouteStepSafety(targetLocation, nextTile);
        if (!nextStepSafety.IsSafe)
        {
            _logger.Write("step_blocked", command.NpcId, "move", command.TraceId, command.CommandId, "running", $"step_blocked:{movementLocationName}:{nextTile.X},{nextTile.Y};{nextStepSafety.FailureKind}");
            if (command.TryRecordReplanAttempt(MaxReplanAttempts, out var attempt))
            {
                command.SetPhase("replanning", movementLocationName, incrementRouteRevision: true);
                var replanProbe = ProbeRoute(npc, currentTile, targetLocation, command.TargetTile);
                command.RecordRouteProbe(ToRouteProbeData(
                    "same_location",
                    replanProbe,
                    targetLocation.NameOrUniqueName ?? targetLocation.Name,
                    currentTile,
                    movementLocationName,
                    command.TargetTile));
                if (replanProbe.Status == BridgeRouteProbeStatus.RouteValid && replanProbe.Route.Count > 0)
                {
                    command.ReplaceSchedulePath(replanProbe.Route);
                    command.SetPhase("executing_segment", movementLocationName);
                    _logger.Write("task_running", command.NpcId, "move", command.TraceId, command.CommandId, "running", $"route_replanned;blockedStep={nextTile.X},{nextTile.Y};attempt={attempt}");
                    return command.ToStatusData();
                }

                StopNpcMotion(npc);
                FailMoveForProbe(command, replanProbe, initial: false, fallbackTile: nextTile, fallbackFailureKind: nextStepSafety.FailureKind);
                _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", command.BlockedReason);
                return command.ToStatusData();
            }

            var failure = BridgeMoveFailureMapper.PathBlocked(movementLocationName, nextTile, nextStepSafety.FailureKind ?? "step_blocked");
            StopNpcMotion(npc);
            command.Fail(failure.ErrorCode, failure.BlockedReason);
            _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", command.BlockedReason);
            return command.ToStatusData();
        }

        var reachedStep = MoveTowardScheduleStep(npc, nextTile);
        if (!reachedStep)
        {
            _logger.Write("task_running", command.NpcId, "move", command.TraceId, command.CommandId, "running", $"movingToward={nextTile.X},{nextTile.Y};target={command.TargetTile.X},{command.TargetTile.Y}");
            return command.ToStatusData();
        }

        command.CompleteCurrentScheduleStep(nextTile);
        command.RecordStep();

        if (nextTile.X == command.TargetTile.X && nextTile.Y == command.TargetTile.Y)
        {
            CompleteOrAwaitWarpAtSegmentTarget(command, npc);
            return command.ToStatusData();
        }

        _logger.Write("task_running", command.NpcId, "move", command.TraceId, command.CommandId, "running", $"tile={nextTile.X},{nextTile.Y};target={command.TargetTile.X},{command.TargetTile.Y}");
        return command.ToStatusData();
    }

    private static bool ShouldTryArrivalFallback(BridgeRouteProbeResult probe)
        => probe.Status is BridgeRouteProbeStatus.TargetUnsafe or BridgeRouteProbeStatus.PathEmpty;

    private void CompleteOrAwaitWarpAtSegmentTarget(BridgeMoveCommand command, NPC npc)
    {
        if (command.CurrentSegment?.TargetKind == "warp_to_next_location")
        {
            StopNpcMotion(npc);
            command.BeginAwaitingWarp(Game1.ticks, WarpTransitionTimeoutTicks);
            _logger.Write("task_running", command.NpcId, "move", command.TraceId, command.CommandId, "running", $"awaiting_warp;nextLocation={command.CurrentSegment.NextLocationName}");
            if (TryTriggerWarpTransition(command, npc))
            {
                var currentLocationName = npc.currentLocation?.NameOrUniqueName ?? npc.currentLocation?.Name;
                if (command.TryCompleteAwaitingWarp(currentLocationName))
                {
                    _logger.Write("task_running", command.NpcId, "move", command.TraceId, command.CommandId, "running", $"replanning_after_warp;location={currentLocationName}");
                }
                else
                {
                    command.FailUnexpectedWarpLocation(currentLocationName);
                    _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", command.BlockedReason);
                }
            }
            return;
        }

        command.SetPhase("arriving", command.LocationName);
        ApplyArrivalFacing(npc, command);
        command.Complete();
        _logger.Write("task_completed", command.NpcId, "move", command.TraceId, command.CommandId, "completed", null);
    }

    private TaskStatusData HandleAwaitingWarp(BridgeMoveCommand command, NPC npc)
    {
        var currentLocation = npc.currentLocation;
        if (currentLocation is null)
        {
            command.Fail("preflight_blocked", "current_location_missing");
            _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", "current_location_missing");
            return command.ToStatusData();
        }

        var currentLocationName = currentLocation.NameOrUniqueName ?? currentLocation.Name;
        if (command.TryCompleteAwaitingWarp(currentLocationName))
        {
            _logger.Write("task_running", command.NpcId, "move", command.TraceId, command.CommandId, "running", $"replanning_after_warp;location={currentLocationName}");
            return command.ToStatusData();
        }

        if (!string.Equals(command.CurrentSegment?.LocationName, currentLocationName, StringComparison.OrdinalIgnoreCase))
        {
            command.FailUnexpectedWarpLocation(currentLocationName);
            _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", command.BlockedReason);
            return command.ToStatusData();
        }

        if (!command.WarpTransitionAttempted)
        {
            TryTriggerWarpTransition(command, npc);
            currentLocationName = npc.currentLocation?.NameOrUniqueName ?? npc.currentLocation?.Name ?? currentLocationName;
            if (command.TryCompleteAwaitingWarp(currentLocationName))
            {
                _logger.Write("task_running", command.NpcId, "move", command.TraceId, command.CommandId, "running", $"replanning_after_warp;location={currentLocationName}");
                return command.ToStatusData();
            }
        }

        if (command.HasWarpTransitionTimedOut(Game1.ticks))
        {
            command.FailWarpTransitionTimeout(currentLocationName);
            _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", command.BlockedReason);
            return command.ToStatusData();
        }

        _logger.Write("task_running", command.NpcId, "move", command.TraceId, command.CommandId, "running", $"awaiting_warp;nextLocation={command.ExpectedNextLocationName};ticks={Game1.ticks}");
        return command.ToStatusData();
    }

    private TaskStatusData HandlePostWarpReplan(
        BridgeMoveCommand command,
        NPC npc,
        GameLocation currentLocation,
        string currentLocationName,
        TileDto currentTile,
        GameLocation targetLocation)
    {
        command.SetPhase("replanning_after_warp", currentLocationName, incrementRouteRevision: true);
        if (ReferenceEquals(currentLocation, targetLocation))
        {
            var finalTarget = command.FinalTarget?.Tile ?? command.TargetTile;
            var finalProbe = ProbeRoute(npc, currentTile, currentLocation, finalTarget);
            command.RecordRouteProbe(ToRouteProbeData(
                "same_location",
                finalProbe,
                currentLocationName,
                currentTile,
                command.LocationName,
                finalTarget));
            if (!command.TryStartPostWarpFinalTargetSegment(currentLocationName, finalProbe, out var failureCode))
            {
                command.Fail(failureCode ?? "target_tile_unreachable");
                _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", command.BlockedReason);
                return command.ToStatusData();
            }

            _logger.Write("task_running", command.NpcId, "move", command.TraceId, command.CommandId, "running", $"post_warp_final_segment_started;pathSteps={command.PathStepsRemaining}");
            return command.ToStatusData();
        }

        var routeProbe = ProbeCrossLocationRoute(
            npc,
            currentLocationName,
            currentTile,
            currentLocation,
            command.LocationName,
            targetLocation,
            command.FinalTarget?.Tile ?? command.TargetTile);
        command.RecordRouteProbe(routeProbe);
        if (!command.TryStartCrossMapSegment(routeProbe, out var crossFailureCode))
        {
            command.Fail(crossFailureCode ?? "cross_location_route_unavailable");
            _logger.Write(
                "task_failed",
                command.NpcId,
                "move",
                command.TraceId,
                command.CommandId,
                "failed",
                FormatRouteProbeLogDetail(routeProbe));
            return command.ToStatusData();
        }

        StopNpcMotion(npc);
        _logger.Write(
            "task_running",
            command.NpcId,
            "move",
            command.TraceId,
            command.CommandId,
            "running",
            $"post_warp_cross_map_segment_started;{FormatRouteProbeLogDetail(routeProbe)}");
        return command.ToStatusData();
    }

    private static bool TryTriggerWarpTransition(BridgeMoveCommand command, NPC npc)
    {
        if (command.CurrentSegment?.TargetTile is null || npc.currentLocation is null)
            return false;

        var triggered = VanillaNpcWarpTransition.TryTrigger(npc, npc.currentLocation, command.CurrentSegment.TargetTile);
        command.MarkWarpTransitionAttempted(npc.currentLocation?.NameOrUniqueName ?? npc.currentLocation?.Name);
        return triggered;
    }

    private void ShowMoveThoughtIfPresent(NPC npc, BridgeMoveCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.Thought))
            _bubbleOverlay.ShowMoveThought(npc, command.Thought, command.CommandId);
    }

    private static BridgeRouteProbeResult ProbeRoute(NPC npc, TileDto currentTile, GameLocation location, TileDto targetTile)
        => BridgeMovementPathProbe.Probe(
            currentTile,
            targetTile,
            tile => BridgeMovementPathProbe.CheckTargetAffordance(location, tile),
            () => BridgeMovementPathProbe.FindSchedulePath(npc, location, currentTile, targetTile),
            tile => BridgeMovementPathProbe.CheckRouteStepSafety(location, tile));

    private static RouteProbeData ProbeCrossLocationRoute(
        NPC npc,
        string? currentLocationName,
        TileDto currentTile,
        GameLocation currentLocation,
        string targetLocationName,
        GameLocation targetLocation,
        TileDto targetTile)
    {
        var locationRoute = currentLocationName is null
            ? null
            : WarpPathfindingCache.GetLocationRoute(currentLocationName, targetLocationName, npc.Gender);
        return BridgeMovementPathProbe.BuildCrossLocationRouteProbe(
            currentLocationName,
            currentTile,
            targetLocationName,
            targetTile,
            locationRoute,
            nextLocationName =>
            {
                var warpPoint = currentLocation.getWarpPointTo(nextLocationName);
                return warpPoint == Point.Zero ? null : new TileDto(warpPoint.X, warpPoint.Y);
            },
            () =>
            {
                var warpPoint = currentLocation.getWarpPointTo(locationRoute?[1] ?? targetLocation.NameOrUniqueName ?? targetLocation.Name);
                return warpPoint == Point.Zero
                    ? new Stack<TileDto>()
                    : BridgeMovementPathProbe.FindSchedulePath(
                        npc,
                        currentLocation,
                        currentTile,
                        new TileDto(warpPoint.X, warpPoint.Y));
            },
            tile => BridgeMovementPathProbe.CheckRouteStepSafety(currentLocation, tile));
    }

    private static RouteProbeData ToRouteProbeData(
        string mode,
        BridgeRouteProbeResult probe,
        string? currentLocationName,
        TileDto currentTile,
        string targetLocationName,
        TileDto targetTile)
    {
        var status = probe.Status == BridgeRouteProbeStatus.RouteValid
            ? "route_found"
            : probe.FailureKind ?? probe.Status.ToString();
        return new RouteProbeData(
            mode,
            status,
            currentLocationName,
            currentTile,
            targetLocationName,
            targetTile,
            probe.Route,
            probe.Route.Count == 0
                ? null
                : new RouteProbeSegmentData(
                    currentLocationName ?? targetLocationName,
                    probe.Route[0],
                    "tile",
                    string.Equals(currentLocationName, targetLocationName, StringComparison.OrdinalIgnoreCase)
                        ? null
                        : targetLocationName),
            probe.Status == BridgeRouteProbeStatus.RouteValid ? null : probe.FailureKind,
            probe.Status == BridgeRouteProbeStatus.RouteValid ? null : probe.FailureDetail);
    }

    internal static string FormatRouteProbeLogDetail(RouteProbeData routeProbe)
    {
        ArgumentNullException.ThrowIfNull(routeProbe);

        var from = FormatLocationTile(routeProbe.CurrentLocationName, routeProbe.CurrentTile);
        var target = FormatLocationTile(routeProbe.TargetLocationName, routeProbe.TargetTile);
        var next = FormatNextSegment(routeProbe.NextSegment);
        return string.Join(
            ";",
            $"routeProbeStatus={ValueOrDash(routeProbe.Status)}",
            $"mode={ValueOrDash(routeProbe.Mode)}",
            $"from={from}",
            $"target={target}",
            $"next={next}",
            $"routeSteps={routeProbe.Route.Count}",
            $"failure={ValueOrDash(routeProbe.FailureCode)}");
    }

    private static string FormatLocationTile(string? locationName, TileDto? tile)
        => tile is null
            ? $"{ValueOrDash(locationName)}:-"
            : $"{ValueOrDash(locationName)}:{tile.X},{tile.Y}";

    private static string FormatNextSegment(RouteProbeSegmentData? segment)
    {
        if (segment is null)
            return "-";

        var from = FormatLocationTile(segment.LocationName, segment.StandTile);
        return string.IsNullOrWhiteSpace(segment.NextLocationName)
            ? $"{from}({ValueOrDash(segment.TargetKind)})"
            : $"{from}->{segment.NextLocationName}({ValueOrDash(segment.TargetKind)})";
    }

    private static string ValueOrDash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value;

    private static void FailMoveForProbe(
        BridgeMoveCommand command,
        BridgeRouteProbeResult probe,
        bool initial,
        TileDto? fallbackTile = null,
        string? fallbackFailureKind = null)
    {
        var failure = BridgeMoveFailureMapper.FromProbe(
            probe,
            initial,
            command.LocationName,
            command.TargetTile,
            fallbackTile,
            fallbackFailureKind);
        command.Fail(failure.ErrorCode, failure.BlockedReason);
    }

    private static void ApplyArrivalFacing(NPC npc, BridgeMoveCommand command)
    {
        StopNpcMotion(npc);
        if (command.FacingDirection is >= 0 and <= 3)
            npc.faceDirection(command.FacingDirection.Value);
    }

    private static void MaintainNpcMovementControl(NPC npc)
    {
        npc.controller = null;
        if (npc.temporaryController is not null)
            npc.temporaryController = null;
        if (npc.DirectionsToNewLocation is not null)
            npc.DirectionsToNewLocation = null;
        if (!npc.farmerPassesThrough)
            npc.farmerPassesThrough = true;
        if (npc.IsWalkingInSquare)
            npc.IsWalkingInSquare = false;
    }

    private static bool MoveTowardScheduleStep(NPC npc, TileDto nextTile)
    {
        var moveSpeed = Math.Max(1, npc.speed);
        var targetPosition = GetTileCenter(nextTile);
        var directionToNext = GetDirection(npc.getStandingPosition(), targetPosition);
        npc.faceDirection(directionToNext);

        if (Vector2.Distance(npc.getStandingPosition(), targetPosition) <= moveSpeed)
        {
            StopNpcMotion(npc);
            return true;
        }

        var velocity = Utility.getVelocityTowardPoint(npc.getStandingPosition(), targetPosition, moveSpeed);
        npc.Position += velocity;
        npc.animateInFacingDirection(Game1.currentGameTime);
        return false;
    }

    private static void StopNpcMotion(NPC npc)
    {
        npc.Halt();
        npc.xVelocity = 0f;
        npc.yVelocity = 0f;
        npc.Sprite.StopAnimation();
    }

    private static bool IsAtTileCenter(NPC npc, TileDto tile)
        => Vector2.Distance(npc.getStandingPosition(), GetTileCenter(tile)) <= Math.Max(1, npc.speed);

    private static TileDto GetCurrentTile(NPC npc)
        => new(npc.TilePoint.X, npc.TilePoint.Y);

    private static Vector2 GetTileCenter(TileDto tile)
        => new(tile.X * Game1.tileSize + Game1.tileSize / 2f, tile.Y * Game1.tileSize + Game1.tileSize / 2f);

    private static int GetDirection(Vector2 from, Vector2 to)
    {
        var delta = to - from;
        if (Math.Abs(delta.X) >= Math.Abs(delta.Y))
            return delta.X > 0 ? 1 : 3;

        return delta.Y > 0 ? 2 : 0;
    }

    private static string? CheckInterrupt()
    {
        if (Game1.eventUp)
            return "event_active";

        if (Game1.activeClickableMenu is DialogueBox)
            return "dialogue_started";

        return null;
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

    private void MarkPrivateChatInputOpened(string npcName, string conversationId, string traceId)
    {
        lock (_privateChatInputGate)
        {
            _privateChatInput = new BridgePrivateChatInput(npcName, conversationId, traceId);
        }
    }

    private void ClearPrivateChatInput(string? conversationId)
    {
        lock (_privateChatInputGate)
        {
            if (conversationId is null ||
                (_privateChatInput is not null &&
                 string.Equals(_privateChatInput.ConversationId, conversationId, StringComparison.Ordinal)))
            {
                _privateChatInput = null;
            }
        }
    }

    private static string SanitizePrivateChatText(string? text)
        => (text ?? string.Empty).Trim();

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

    private sealed class BridgeDebugRepositionUiCommand : IBridgeUiCommand
    {
        private readonly BridgeEnvelope<DebugRepositionPayload> _envelope;
        private readonly TaskCompletionSource<BridgeResponse<DebugRepositionData>> _completion;

        public BridgeDebugRepositionUiCommand(
            BridgeEnvelope<DebugRepositionPayload> envelope,
            TaskCompletionSource<BridgeResponse<DebugRepositionData>> completion)
        {
            _envelope = envelope;
            _completion = completion;
        }

        public TaskStatusData Execute(BridgeCommandQueue queue)
        {
            try
            {
                var response = queue.ExecuteDebugReposition(_envelope);
                _completion.TrySetResult(response);
                return ToUiStatus(_envelope.ToUntyped(), "debug_reposition", response);
            }
            catch (Exception ex)
            {
                _completion.TrySetException(ex);
                return FailedUiStatus(_envelope.ToUntyped(), "debug_reposition", ex);
            }
        }
    }

    private static TaskStatusData FailedUiStatus(BridgeEnvelope<object> envelope, string action, Exception ex)
        => new("", envelope.TraceId, envelope.NpcId ?? "", action, "failed", DateTime.UtcNow, 0, 0, ex.Message, ex.GetType().Name);
}

internal sealed record ResolvedMoveTarget(
    bool Ok,
    string? LocationName,
    TileDto? Tile,
    int? FacingDirection,
    string? DestinationId,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static ResolvedMoveTarget Success(
        string locationName,
        TileDto tile,
        int? facingDirection,
        string? destinationId)
        => new(true, locationName, tile, facingDirection, destinationId, null, null);

    public static ResolvedMoveTarget Failed(string errorCode, string errorMessage)
        => new(false, null, null, null, null, errorCode, errorMessage);
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
    private int _stepsTaken;

    private Stack<TileDto>? _schedulePath;
    private int _replanAttempts;
    private TileDto? _resolvedTargetTile;

    public BridgeMoveCommand(
        string commandId,
        string traceId,
        string npcId,
        string locationName,
        TileDto targetTile,
        int? facingDirection,
        string? idempotencyKey,
        string? destinationId = null,
        string? thought = null)
    {
        CommandId = commandId;
        TraceId = traceId;
        NpcId = npcId;
        LocationName = locationName;
        TargetTile = targetTile;
        FacingDirection = facingDirection;
        IdempotencyKey = idempotencyKey;
        DestinationId = destinationId;
        Thought = thought;
    }

    public string CommandId { get; }
    public string TraceId { get; }
    public string NpcId { get; }
    public string LocationName { get; }
    public TileDto TargetTile { get; private set; }
    public int? FacingDirection { get; private set; }
    public string? IdempotencyKey { get; }
    public string? DestinationId { get; }
    public string? Thought { get; }
    public RouteProbeData? RouteProbe { get; private set; }
    public string Status { get; private set; } = "queued";
    public string? BlockedReason { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? InterruptionReason { get; private set; }
    public string Phase { get; private set; } = "queued";
    public string CurrentLocationName { get; private set; } = string.Empty;
    public string? CrossMapPhase { get; private set; }
    public BridgeMoveFinalTargetData? FinalTarget { get; private set; }
    public BridgeMoveSegmentData? CurrentSegment { get; private set; }
    public string? LastFailureCode { get; private set; }
    public string? ExpectedNextLocationName { get; private set; }
    public int? AwaitingWarpStartedTick { get; private set; }
    public int? WarpTimeoutTicks { get; private set; }
    public string? LastKnownLocationName { get; private set; }
    public bool WarpTransitionAttempted { get; private set; }
    public bool IsAwaitingWarp => string.Equals(CrossMapPhase, "awaiting_warp", StringComparison.OrdinalIgnoreCase);
    public bool IsReplanningAfterWarp => string.Equals(CrossMapPhase, "replanning_after_warp", StringComparison.OrdinalIgnoreCase);
    public int RouteRevision { get; private set; }
    public bool IsTerminal => Status is "completed" or "cancelled" or "failed" or "blocked" or "interrupted";
    public int PathStepsRemaining => _schedulePath?.Count ?? 0;

    public void SetPhase(string phase, string? locationName = null, bool incrementRouteRevision = false)
    {
        Phase = phase;
        if (locationName is not null)
            CurrentLocationName = locationName;
        if (incrementRouteRevision)
            RouteRevision++;
    }

    public void SetCrossMapPhase(string phase)
        => CrossMapPhase = phase;

    public void Start()
    {
        if (Status != "queued")
            return;

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
        => Fail(errorCode, errorCode);

    public void Fail(string errorCode, string blockedReason)
    {
        Status = "failed";
        ErrorCode = errorCode;
        LastFailureCode = errorCode;
        BlockedReason = blockedReason;
    }

    public void Block(string reason)
    {
        Status = "blocked";
        ErrorCode = reason;
        LastFailureCode = reason;
        BlockedReason = reason;
    }

    public void Interrupt(string reason)
    {
        Status = "interrupted";
        InterruptionReason = reason;
        BlockedReason = reason;
    }

    public void RecordStep()
        => _stepsTaken++;

    public void ReplaceSchedulePath(IReadOnlyList<TileDto> route)
        => _schedulePath = BridgeMovementPathProbe.ToSchedulePath(route);

    public void ReplaceTarget(TileDto resolvedTile, int resolvedFacing)
    {
        _resolvedTargetTile = TargetTile;
        TargetTile = resolvedTile;
        FacingDirection = resolvedFacing;
    }

    public void RecordRouteProbe(RouteProbeData routeProbe)
        => RouteProbe = routeProbe;

    public void StartCrossMapSegment(RouteProbeData routeProbe)
    {
        if (!TryStartCrossMapSegment(routeProbe, out var failureCode))
            throw new InvalidOperationException(failureCode ?? "cross_location_route_unavailable");
    }

    public bool TryStartCrossMapSegment(RouteProbeData routeProbe, out string? failureCode)
    {
        failureCode = null;
        if (!string.Equals(routeProbe.Status, "route_found", StringComparison.OrdinalIgnoreCase))
        {
            failureCode = routeProbe.FailureCode ?? routeProbe.Status;
            return false;
        }

        if (routeProbe.NextSegment?.StandTile is null)
        {
            failureCode = "cross_location_segment_missing";
            return false;
        }

        FinalTarget ??= new BridgeMoveFinalTargetData(LocationName, TargetTile, FacingDirection);
        CurrentSegment = new BridgeMoveSegmentData(
            routeProbe.NextSegment.LocationName,
            routeProbe.NextSegment.StandTile,
            routeProbe.NextSegment.TargetKind,
            routeProbe.NextSegment.NextLocationName);
        TargetTile = routeProbe.NextSegment.StandTile;
        CrossMapPhase = "executing_segment";
        SetPhase("executing_segment", CurrentSegment.LocationName, incrementRouteRevision: true);
        ReplaceSchedulePath(routeProbe.Route);
        Start();
        return true;
    }

    internal bool TryStartPostWarpFinalTargetSegment(
        string currentLocationName,
        BridgeRouteProbeResult probe,
        out string? failureCode)
    {
        failureCode = null;
        var finalTarget = FinalTarget;
        if (finalTarget is null)
        {
            failureCode = "final_target_missing";
            return false;
        }

        if (probe.Status != BridgeRouteProbeStatus.RouteValid)
        {
            failureCode = probe.FailureKind ?? "target_tile_unreachable";
            return false;
        }

        CurrentSegment = new BridgeMoveSegmentData(
            currentLocationName,
            finalTarget.Tile,
            "final_target_tile",
            null);
        TargetTile = finalTarget.Tile;
        FacingDirection = finalTarget.FacingDirection;
        CrossMapPhase = "executing_segment";
        SetPhase("executing_segment", currentLocationName, incrementRouteRevision: true);
        ReplaceSchedulePath(probe.Route);
        return true;
    }

    public void BeginAwaitingWarp(int currentTick, int timeoutTicks)
    {
        if (CurrentSegment is null)
            throw new InvalidOperationException("Cannot await a warp without a current segment.");

        ExpectedNextLocationName = CurrentSegment.NextLocationName;
        AwaitingWarpStartedTick = currentTick;
        WarpTimeoutTicks = timeoutTicks;
        LastKnownLocationName = CurrentSegment.LocationName;
        WarpTransitionAttempted = false;
        SetPhase("awaiting_warp", CurrentSegment.LocationName);
        SetCrossMapPhase("awaiting_warp");
    }

    public void MarkWarpTransitionAttempted(string? currentLocationName)
    {
        WarpTransitionAttempted = true;
        if (!string.IsNullOrWhiteSpace(currentLocationName))
            LastKnownLocationName = currentLocationName;
    }

    public bool TryCompleteAwaitingWarp(string? currentLocationName)
    {
        if (!IsAwaitingWarp ||
            string.IsNullOrWhiteSpace(ExpectedNextLocationName) ||
            string.IsNullOrWhiteSpace(currentLocationName) ||
            !string.Equals(ExpectedNextLocationName, currentLocationName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        SetPhase("replanning_after_warp", currentLocationName);
        SetCrossMapPhase("replanning_after_warp");
        LastKnownLocationName = currentLocationName;
        return true;
    }

    public bool HasWarpTransitionTimedOut(int currentTick)
    {
        if (!IsAwaitingWarp || AwaitingWarpStartedTick is null || WarpTimeoutTicks is null)
            return false;

        return currentTick - AwaitingWarpStartedTick.Value > WarpTimeoutTicks.Value;
    }

    public void FailWarpTransitionTimeout(string? currentLocationName)
    {
        var actual = string.IsNullOrWhiteSpace(currentLocationName) ? LastKnownLocationName : currentLocationName;
        Fail(
            "warp_transition_timeout",
            $"warp_transition_timeout:expected={ValueOrUnknown(ExpectedNextLocationName)};actual={ValueOrUnknown(actual)}");
    }

    public void FailUnexpectedWarpLocation(string? currentLocationName)
    {
        Fail(
            "unexpected_location_after_warp",
            $"unexpected_location_after_warp:expected={ValueOrUnknown(ExpectedNextLocationName)};actual={ValueOrUnknown(currentLocationName)}");
    }

    public bool TryRecordReplanAttempt(int maxAttempts, out int attempt)
    {
        if (_replanAttempts >= maxAttempts)
        {
            attempt = _replanAttempts;
            return false;
        }

        _replanAttempts++;
        attempt = _replanAttempts;
        return true;
    }

    public TileDto? NextScheduleStepFrom(TileDto currentTile)
    {
        if (_schedulePath is not { Count: > 0 })
            return null;

        return _schedulePath.Peek();
    }

    public void CompleteCurrentScheduleStep(TileDto completedTile)
    {
        if (_schedulePath is { Count: > 0 } &&
            _schedulePath.Peek().X == completedTile.X &&
            _schedulePath.Peek().Y == completedTile.Y)
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
            Status == "completed" ? 1.0 : Status == "running" ? 0.5 : 0,
            BlockedReason,
            ErrorCode,
            InterruptionReason,
            DestinationId,
            Phase,
            CurrentLocationName,
            TargetTile,
            RouteRevision > 0 ? RouteRevision : null,
            RouteProbe,
            CrossMapPhase,
            FinalTarget,
            CurrentSegment,
            LastFailureCode);

    private static string ValueOrUnknown(string? value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value;
}

internal static class VanillaNpcWarpTransition
{
    public static bool TryTrigger(NPC npc, GameLocation location, TileDto segmentTargetTile)
    {
        var controller = new PathFindController(
            new Stack<Point>(new[] { new Point(segmentTargetTile.X, segmentTargetTile.Y) }),
            npc,
            location);
        controller.handleWarps(npc.GetBoundingBox());
        return !ReferenceEquals(npc.currentLocation, location);
    }
}
