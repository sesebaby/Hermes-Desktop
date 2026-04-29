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
            action.NpcId,
            _saveId,
            action.IdempotencyKey,
            payload);

        var response = await _client.SendAsync<StardewMoveRequest, StardewMoveAcceptedData>(
            StardewBridgeRoutes.TaskMove,
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
            string.IsNullOrWhiteSpace(response.TraceId) ? fallbackTraceId : response.TraceId);

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
            response.Data.ErrorCode);
    }
}
