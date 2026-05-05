namespace StardewHermesBridge.Bridge;

using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Microsoft.Xna.Framework;
using StardewHermesBridge.Logging;
using StardewHermesBridge.Ui;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

public sealed class BridgeCommandQueue
{
    private const int MaxReplanAttempts = 2;

    private readonly ConcurrentQueue<BridgeMoveCommand> _pending = new();
    private readonly ConcurrentQueue<IBridgeUiCommand> _pendingUi = new();
    private readonly ConcurrentDictionary<string, BridgeMoveCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _idempotency = new(StringComparer.OrdinalIgnoreCase);
    private readonly SmapiBridgeLogger _logger;
    private readonly BridgeEventBuffer _events;
    private readonly HermesPhoneState _phoneState;
    private readonly StardewMessageDisplayRouter _messageRouter;
    private readonly Action<string>? _privateChatOpened;
    private readonly Action<string>? _privateChatSubmitted;
    private readonly Action<string, string>? _privateChatReplyDisplayed;
    private BridgeMoveCommand? _activeMove;

    public BridgeCommandQueue(
        SmapiBridgeLogger logger,
        BridgeEventBuffer? events = null,
        HermesPhoneState? phoneState = null,
        StardewMessageDisplayRouter? messageRouter = null,
        Action<string>? privateChatOpened = null,
        Action<string>? privateChatSubmitted = null,
        Action<string, string>? privateChatReplyDisplayed = null)
    {
        _logger = logger;
        _events = events ?? new BridgeEventBuffer();
        _phoneState = phoneState ?? new HermesPhoneState();
        _messageRouter = messageRouter ?? new StardewMessageDisplayRouter(
            _phoneState,
            new NpcOverheadBubbleOverlay(_events, logger),
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
            target.DestinationId);
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
        var display = _messageRouter.Display(npc, envelope.Payload.Text, channel, envelope.Payload.ConversationId);
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
                new JsonObject
                {
                    ["conversationId"] = conversationId,
                    ["route"] = display.Route,
                    ["reply_closed_source"] = display.ReplyClosedSource
                });
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

        var npc = BridgeNpcResolver.Resolve(npcId);
        if (npc is null)
        {
            _logger.Write("action_open_private_chat_failed", npcId, "open_private_chat", envelope.TraceId, null, "failed", "invalid_target");
            return Error<OpenPrivateChatData>(envelope.TraceId, envelope.RequestId, "invalid_target", "NPC was not found.", retryable: false);
        }

        var conversationId = string.IsNullOrWhiteSpace(envelope.Payload.ConversationId)
            ? envelope.RequestId
            : envelope.Payload.ConversationId;
        _phoneState.OpenThread(npc.Name, conversationId);

        _events.Record(
            "private_chat_opened",
            npc.Name,
            $"{npc.Name} private chat phone thread opened.",
            conversationId,
            new JsonObject
            {
                ["conversationId"] = conversationId,
                ["prompt"] = envelope.Payload.Prompt,
                ["threadId"] = _phoneState.VisibleThreadId,
                ["openState"] = "thread_opened"
            });
        _privateChatOpened?.Invoke(npc.Name);
        _logger.Write("action_open_private_chat_completed", npc.Name, "open_private_chat", envelope.TraceId, null, "completed", "thread_opened");
        return new BridgeResponse<OpenPrivateChatData>(
            true,
            envelope.TraceId,
            envelope.RequestId,
            null,
            "completed",
            new OpenPrivateChatData(npc.Name, true, _phoneState.VisibleThreadId, "thread_opened"),
            null,
            new { });
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

        if (!ReferenceEquals(currentLocation, targetLocation))
        {
            command.Block("cross_location_unsupported");
            _logger.Write("task_blocked", command.NpcId, "move", command.TraceId, command.CommandId, "blocked", "cross_location_unsupported");
            return command.ToStatusData();
        }

        var currentTile = GetCurrentTile(npc);

        if (command.Status == "queued")
        {
            command.SetPhase("resolving_destination", currentLocation?.NameOrUniqueName ?? currentLocation?.Name, incrementRouteRevision: true);
            MaintainNpcMovementControl(npc);
            StopNpcMotion(npc);
            command.SetPhase("preflight", command.LocationName);
            var initialProbe = ProbeRoute(npc, currentTile, targetLocation, command.TargetTile);
            if (ShouldTryArrivalFallback(initialProbe))
            {
                command.SetPhase("resolving_arrival", command.LocationName, incrementRouteRevision: true);
                var resolved = BridgeMovementPathProbe.FindClosestReachableNeighbor(
                    npc, targetLocation, command.TargetTile, currentTile);
                if (resolved is not null)
                {
                    command.ReplaceTarget(resolved.Value.StandTile, resolved.Value.FacingDirection);
                    initialProbe = resolved.Value.Route;
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
            command.SetPhase("arriving", command.LocationName);
            ApplyArrivalFacing(npc, command);
            command.Complete();
            _logger.Write("task_completed", command.NpcId, "move", command.TraceId, command.CommandId, "completed", null);
            return command.ToStatusData();
        }

        var nextTile = command.NextScheduleStepFrom(currentTile);
        if (nextTile is null)
        {
            if (IsAtTileCenter(npc, command.TargetTile))
            {
                command.SetPhase("arriving", command.LocationName);
                ApplyArrivalFacing(npc, command);
                command.Complete();
                _logger.Write("task_completed", command.NpcId, "move", command.TraceId, command.CommandId, "completed", null);
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
            _logger.Write("step_blocked", command.NpcId, "move", command.TraceId, command.CommandId, "running", $"step_blocked:{command.LocationName}:{nextTile.X},{nextTile.Y};{nextStepSafety.FailureKind}");
            if (command.TryRecordReplanAttempt(MaxReplanAttempts, out var attempt))
            {
                command.SetPhase("replanning", command.LocationName, incrementRouteRevision: true);
                var replanProbe = ProbeRoute(npc, currentTile, targetLocation, command.TargetTile);
                if (replanProbe.Status == BridgeRouteProbeStatus.RouteValid && replanProbe.Route.Count > 0)
                {
                    command.ReplaceSchedulePath(replanProbe.Route);
                    command.SetPhase("executing_segment", command.LocationName);
                    _logger.Write("task_running", command.NpcId, "move", command.TraceId, command.CommandId, "running", $"route_replanned;blockedStep={nextTile.X},{nextTile.Y};attempt={attempt}");
                    return command.ToStatusData();
                }

                StopNpcMotion(npc);
                FailMoveForProbe(command, replanProbe, initial: false, fallbackTile: nextTile, fallbackFailureKind: nextStepSafety.FailureKind);
                _logger.Write("task_failed", command.NpcId, "move", command.TraceId, command.CommandId, "failed", command.BlockedReason);
                return command.ToStatusData();
            }

            var failure = BridgeMoveFailureMapper.PathBlocked(command.LocationName, nextTile, nextStepSafety.FailureKind ?? "step_blocked");
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
            command.SetPhase("arriving", command.LocationName);
            ApplyArrivalFacing(npc, command);
            command.Complete();
            _logger.Write("task_completed", command.NpcId, "move", command.TraceId, command.CommandId, "completed", null);
            return command.ToStatusData();
        }

        _logger.Write("task_running", command.NpcId, "move", command.TraceId, command.CommandId, "running", $"tile={nextTile.X},{nextTile.Y};target={command.TargetTile.X},{command.TargetTile.Y}");
        return command.ToStatusData();
    }

    private static bool ShouldTryArrivalFallback(BridgeRouteProbeResult probe)
        => probe.Status is BridgeRouteProbeStatus.TargetUnsafe or BridgeRouteProbeStatus.PathEmpty;

    private static BridgeRouteProbeResult ProbeRoute(NPC npc, TileDto currentTile, GameLocation location, TileDto targetTile)
        => BridgeMovementPathProbe.Probe(
            currentTile,
            targetTile,
            tile => BridgeMovementPathProbe.CheckTargetAffordance(location, tile),
            () => BridgeMovementPathProbe.FindSchedulePath(npc, location, currentTile, targetTile),
            tile => BridgeMovementPathProbe.CheckRouteStepSafety(location, tile));

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
        string? destinationId = null)
    {
        CommandId = commandId;
        TraceId = traceId;
        NpcId = npcId;
        LocationName = locationName;
        TargetTile = targetTile;
        FacingDirection = facingDirection;
        IdempotencyKey = idempotencyKey;
        DestinationId = destinationId;
    }

    public string CommandId { get; }
    public string TraceId { get; }
    public string NpcId { get; }
    public string LocationName { get; }
    public TileDto TargetTile { get; private set; }
    public int? FacingDirection { get; private set; }
    public string? IdempotencyKey { get; }
    public string? DestinationId { get; }
    public string Status { get; private set; } = "queued";
    public string? BlockedReason { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? InterruptionReason { get; private set; }
    public string Phase { get; private set; } = "queued";
    public string CurrentLocationName { get; private set; } = string.Empty;
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
        BlockedReason = blockedReason;
    }

    public void Block(string reason)
    {
        Status = "blocked";
        ErrorCode = reason;
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
            RouteRevision > 0 ? RouteRevision : null);
}
