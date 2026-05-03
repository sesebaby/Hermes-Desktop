namespace Hermes.Agent.Games.Stardew;

using Hermes.Agent.Game;

public sealed class StardewCommandService : IGameCommandService
{
    private readonly ISmapiModApiClient _client;
    private readonly string _saveId;

    public StardewCommandService(ISmapiModApiClient client, string saveId)
    {
        _client = client;
        _saveId = string.IsNullOrWhiteSpace(saveId) ? "unknown-save" : saveId;
    }

    public async Task<GameCommandResult> SubmitAsync(GameAction action, CancellationToken ct)
    {
        if (action.Type == GameActionType.Speak)
            return await SubmitSpeakAsync(action, ct);

        if (action.Type == GameActionType.OpenPrivateChat)
            return await SubmitOpenPrivateChatAsync(action, ct);

        if (action.Type != GameActionType.Move)
            return new GameCommandResult(false, "", StardewCommandStatuses.Failed, "unsupported_action", action.TraceId);

        if (action.Target.Tile is null || string.IsNullOrWhiteSpace(action.Target.LocationName))
            return new GameCommandResult(false, "", StardewCommandStatuses.Failed, StardewBridgeErrorCodes.InvalidTarget, action.TraceId);

        var requestId = $"req_{Guid.NewGuid():N}";
        var payload = new StardewMoveRequest(
            new StardewMoveTarget(
                action.Target.LocationName,
                new StardewTile(action.Target.Tile.X, action.Target.Tile.Y)),
            action.Reason);
        var envelope = new StardewBridgeEnvelope<StardewMoveRequest>(
            requestId,
            action.TraceId,
            ResolveNpcId(action),
            _saveId,
            action.IdempotencyKey,
            payload);

        var response = await _client.SendAsync<StardewMoveRequest, StardewMoveAcceptedData>(
            StardewBridgeRoutes.TaskMove,
            envelope,
            ct);

        return ToCommandResult(response, action.TraceId);
    }

    private async Task<GameCommandResult> SubmitOpenPrivateChatAsync(GameAction action, CancellationToken ct)
    {
        var prompt = action.Payload?["prompt"]?.ToString();
        var conversationId = action.Payload?["conversationId"]?.ToString();
        var envelope = new StardewBridgeEnvelope<StardewOpenPrivateChatRequest>(
            $"req_{Guid.NewGuid():N}",
            action.TraceId,
            ResolveNpcId(action),
            _saveId,
            action.IdempotencyKey,
            new StardewOpenPrivateChatRequest(prompt, conversationId));

        var response = await _client.SendAsync<StardewOpenPrivateChatRequest, StardewOpenPrivateChatData>(
            StardewBridgeRoutes.ActionOpenPrivateChat,
            envelope,
            ct);

        if (response.Ok && response.Data?.Opened is not true)
            return new GameCommandResult(false, response.CommandId ?? "", StardewCommandStatuses.Failed, "open_private_chat_not_opened", action.TraceId);

        return ToCommandResult(response, action.TraceId);
    }

    private async Task<GameCommandResult> SubmitSpeakAsync(GameAction action, CancellationToken ct)
    {
        var text = action.Payload?["text"]?.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return new GameCommandResult(false, "", StardewCommandStatuses.Failed, StardewBridgeErrorCodes.InvalidTarget, action.TraceId);

        var channel = action.Payload?["channel"]?.ToString();
        if (string.IsNullOrWhiteSpace(channel))
            channel = "player";

        var conversationId = action.Payload?["conversationId"]?.ToString();
        var envelope = new StardewBridgeEnvelope<StardewSpeakRequest>(
            $"req_{Guid.NewGuid():N}",
            action.TraceId,
            ResolveNpcId(action),
            _saveId,
            action.IdempotencyKey,
            new StardewSpeakRequest(text, channel, conversationId));

        var response = await _client.SendAsync<StardewSpeakRequest, StardewSpeakData>(
            StardewBridgeRoutes.ActionSpeak,
            envelope,
            ct);

        return ToCommandResult(response, action.TraceId);
    }

    public async Task<GameCommandStatus> GetStatusAsync(string commandId, CancellationToken ct)
    {
        var request = new StardewTaskStatusRequest(commandId);
        var envelope = new StardewBridgeEnvelope<StardewTaskStatusRequest>(
            $"req_{Guid.NewGuid():N}",
            $"trace_status_{Guid.NewGuid():N}",
            null,
            _saveId,
            null,
            request);

        var response = await _client.SendAsync<StardewTaskStatusRequest, StardewTaskStatusData>(
            StardewBridgeRoutes.TaskStatus,
            envelope,
            ct);

        return ToCommandStatus(response, commandId);
    }

    public async Task<GameCommandStatus?> TryGetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return null;

        var request = new StardewTaskLookupRequest(idempotencyKey);
        var envelope = new StardewBridgeEnvelope<StardewTaskLookupRequest>(
            $"req_{Guid.NewGuid():N}",
            $"trace_lookup_{Guid.NewGuid():N}",
            null,
            _saveId,
            idempotencyKey,
            request);

        var response = await _client.SendAsync<StardewTaskLookupRequest, StardewTaskStatusData>(
            StardewBridgeRoutes.TaskLookup,
            envelope,
            ct);

        if (!response.Ok &&
            string.Equals(response.Error?.Code, StardewBridgeErrorCodes.CommandNotFound, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return ToCommandStatus(response, response.CommandId ?? string.Empty);
    }

    public async Task<GameCommandStatus> CancelAsync(string commandId, string reason, CancellationToken ct)
    {
        var request = new StardewTaskCancelRequest(commandId, reason);
        var envelope = new StardewBridgeEnvelope<StardewTaskCancelRequest>(
            $"req_{Guid.NewGuid():N}",
            $"trace_cancel_{Guid.NewGuid():N}",
            null,
            _saveId,
            null,
            request);

        var response = await _client.SendAsync<StardewTaskCancelRequest, StardewTaskStatusData>(
            StardewBridgeRoutes.TaskCancel,
            envelope,
            ct);

        return ToCommandStatus(response, commandId);
    }

    private static GameCommandResult ToCommandResult<TData>(StardewBridgeResponse<TData> response, string fallbackTraceId)
        => new(
            response.Ok,
            response.CommandId ?? "",
            response.Status ?? (response.Ok ? StardewCommandStatuses.Queued : StardewCommandStatuses.Failed),
            response.Error?.Code,
            string.IsNullOrWhiteSpace(response.TraceId) ? fallbackTraceId : response.TraceId,
            response.Error?.Retryable == true);

    private static string ResolveNpcId(GameAction action)
        => !string.IsNullOrWhiteSpace(action.BodyBinding?.TargetEntityId)
            ? action.BodyBinding.TargetEntityId!
            : action.NpcId;

    private static GameCommandStatus ToCommandStatus(StardewBridgeResponse<StardewTaskStatusData> response, string fallbackCommandId)
    {
        if (response.Data is null)
        {
            return new GameCommandStatus(
                response.CommandId ?? fallbackCommandId,
                "",
                "",
                response.Status ?? StardewCommandStatuses.Failed,
                0,
                null,
                response.Error?.Code);
        }

        return new GameCommandStatus(
            response.Data.CommandId,
            response.Data.NpcId,
            response.Data.Action,
            response.Data.Status,
            response.Data.Progress,
            response.Data.BlockedReason,
            response.Data.ErrorCode,
            response.Data.StartedAtUtc,
            response.Data.UpdatedAtUtc,
            response.Data.ElapsedMs,
            response.Data.RetryAfterUtc);
    }
}
