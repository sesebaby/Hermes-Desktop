namespace Hermes.Agent.Game;

using System.Text.Json.Nodes;

public sealed class PrivateChatOrchestrator : IDisposable
{
    private readonly IGameEventSource _events;
    private readonly IGameCommandService _commands;
    private readonly IPrivateChatAgentRunner _agentRunner;
    private readonly PrivateChatOrchestratorOptions _options;
    private readonly HashSet<string> _completedOpenKeys = new(StringComparer.OrdinalIgnoreCase);
    private GameEventCursor _cursor = new(null);
    private PrivateChatState _state = PrivateChatState.Idle;
    private string? _conversationId;
    private string? _activeNpcId;
    private IPrivateChatSessionLease? _sessionLease;
    private int _turns;
    private PendingPrivateChatOpen? _pendingOpen;

    public PrivateChatOrchestrator(
        IGameEventSource events,
        IGameCommandService commands,
        IPrivateChatAgentRunner agentRunner,
        PrivateChatOrchestratorOptions options)
    {
        _events = events;
        _commands = commands;
        _agentRunner = agentRunner;
        _options = options;
    }

    public PrivateChatState State => _state;

    public string? ConversationId => _conversationId;

    internal async Task<int> DrainExistingEventsAsync(CancellationToken ct)
    {
        var batch = await _events.PollBatchAsync(_cursor, ct);
        DrainRecords(batch.Records);
        _cursor = batch.NextCursor;
        return batch.Records.Count;
    }

    internal async Task<PrivateChatStepResult> ProcessNextAsync(CancellationToken ct)
    {
        await TryRetryPendingOpenAsync(ct);
        var batch = await _events.PollBatchAsync(_cursor, ct);
        return await ProcessEventBatchCoreAsync(batch, ct);
    }

    public void DrainRecords(IReadOnlyList<GameEventRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        foreach (var record in records)
            AdvanceCursor(record);
    }

    public async Task<PrivateChatStepResult> ProcessRecordsAsync(IReadOnlyList<GameEventRecord> records, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(records);

        await TryRetryPendingOpenAsync(ct);
        return await ProcessRecordsCoreAsync(records, ct);
    }

    private async Task ProcessRecordAsync(GameEventRecord record, CancellationToken ct)
    {
        var policy = _options.Policy;
        if (!policy.IsTargetNpc(record.NpcId))
            return;

        if (policy.IsOpenTrigger(record.EventType))
        {
            await TryOpenAfterTriggerAsync(record, ct);
            return;
        }

        if (policy.IsSubmittedEvent(record.EventType))
            await TryReplyToPlayerMessageAsync(record, ct);

        if (policy.IsCancelledEvent(record.EventType))
            TryCancelPrivateChat(record);

        if (policy.IsReplyClosedEvent(record.EventType))
            await TryReopenAfterPrivateChatReplyClosedAsync(record, ct);
    }

    private async Task TryOpenAfterTriggerAsync(GameEventRecord record, CancellationToken ct)
    {
        if (_state is not PrivateChatState.Idle)
            return;

        var openKey = _options.Policy.ExtractConversationId(record) ?? record.EventId;
        if (_completedOpenKeys.Contains(openKey) ||
            string.Equals(_pendingOpen?.OpenKey, openKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var npcId = string.IsNullOrWhiteSpace(record.NpcId) ? _options.Policy.NpcId : record.NpcId;
        _conversationId = $"pc_{openKey}";
        _turns = 0;
        _state = PrivateChatState.PendingOpen;
        _pendingOpen = new PendingPrivateChatOpen(
            npcId!,
            _conversationId,
            openKey,
            "auto_after_private_chat_trigger",
            Attempts: 0);

        await TrySubmitPendingOpenAsync(ct);
    }

    private async Task TryReplyToPlayerMessageAsync(GameEventRecord record, CancellationToken ct)
    {
        if (_state is not PrivateChatState.AwaitingPlayerInput || string.IsNullOrWhiteSpace(_conversationId))
            return;
        if (!string.Equals(record.NpcId, _activeNpcId, StringComparison.OrdinalIgnoreCase))
            return;

        var submittedConversationId = _options.Policy.ExtractConversationId(record);
        if (!string.Equals(submittedConversationId, _conversationId, StringComparison.OrdinalIgnoreCase))
            return;

        var playerText = _options.Policy.ExtractPlayerText(record);
        if (string.IsNullOrWhiteSpace(playerText))
        {
            EndSession();
            return;
        }

        if (!await TryAcquireSessionLeaseAsync(_activeNpcId!, _conversationId, ct))
            return;

        _state = PrivateChatState.WaitingAgentReply;
        try
        {
            PrivateChatAgentReply reply;
            try
            {
                reply = await _agentRunner.ReplyAsync(
                    new PrivateChatAgentRequest(_activeNpcId!, _options.Policy.SaveId, _conversationId, playerText),
                    ct);
            }
            catch
            {
                var failureSource = _options.Policy.ExtractReplySource(record);
                await SubmitSpeakAsync(
                    _activeNpcId!,
                    "AI connection failed. Check Hermes provider settings.",
                    _conversationId,
                    failureSource,
                    ct,
                    messageKind: "system_error");
                EndSession();
                return;
            }

            if (string.IsNullOrWhiteSpace(reply.Text))
            {
                EndSession();
                return;
            }

            _state = PrivateChatState.ShowingReply;
            var replySource = _options.Policy.ExtractReplySource(record);
            var speakResult = await SubmitSpeakAsync(_activeNpcId!, reply.Text, _conversationId, replySource, ct);
            _turns++;
            if (!speakResult.Accepted || ShouldEndAfterReply())
            {
                EndSession();
                return;
            }

            _state = PrivateChatState.WaitingReplyDismissal;
        }
        finally
        {
            ReleaseSessionLease();
        }
    }

    private async Task TryReopenAfterPrivateChatReplyClosedAsync(GameEventRecord record, CancellationToken ct)
    {
        if (_state is not PrivateChatState.WaitingReplyDismissal || string.IsNullOrWhiteSpace(_conversationId))
            return;
        if (!string.Equals(record.NpcId, _activeNpcId, StringComparison.OrdinalIgnoreCase))
            return;

        var closedConversationId = _options.Policy.ExtractConversationId(record);
        if (!string.Equals(closedConversationId, _conversationId, StringComparison.OrdinalIgnoreCase))
            return;

        var nextConversationId = $"{_conversationId}_turn{_turns + 1}";
        _conversationId = nextConversationId;
        _pendingOpen = new PendingPrivateChatOpen(
            _activeNpcId!,
            nextConversationId,
            nextConversationId,
            "reopen_after_private_chat_reply",
            Attempts: 0);
        _state = PrivateChatState.PendingOpen;

        await TrySubmitPendingOpenAsync(ct);
    }

    private void TryCancelPrivateChat(GameEventRecord record)
    {
        if (_state is not PrivateChatState.AwaitingPlayerInput || string.IsNullOrWhiteSpace(_conversationId))
            return;
        if (!string.Equals(record.NpcId, _activeNpcId, StringComparison.OrdinalIgnoreCase))
            return;

        var cancelledConversationId = _options.Policy.ExtractConversationId(record);
        if (string.Equals(cancelledConversationId, _conversationId, StringComparison.OrdinalIgnoreCase))
            EndSession();
    }

    private async Task TryRetryPendingOpenAsync(CancellationToken ct)
    {
        if (_state is PrivateChatState.PendingOpen && _pendingOpen is not null)
            await TrySubmitPendingOpenAsync(ct);
    }

    private async Task TrySubmitPendingOpenAsync(CancellationToken ct)
    {
        if (_pendingOpen is not { } pending)
            return;

        pending = pending with { Attempts = pending.Attempts + 1 };
        _pendingOpen = pending;
        _state = PrivateChatState.PendingOpen;
        _conversationId = pending.ConversationId;

        var result = await SubmitOpenPrivateChatAsync(
            pending.NpcId,
            pending.ConversationId,
            pending.OpenKey,
            pending.Reason,
            ct);

        if (result.Accepted)
        {
            _completedOpenKeys.Add(pending.OpenKey);
            _pendingOpen = null;
            _activeNpcId = pending.NpcId;
            _state = PrivateChatState.AwaitingPlayerInput;
            return;
        }

        if (_options.Policy.ShouldRetryOpen(result) && pending.Attempts < Math.Max(1, _options.MaxOpenAttempts))
            return;

        _completedOpenKeys.Add(pending.OpenKey);
        EndSession();
    }

    private Task<GameCommandResult> SubmitOpenPrivateChatAsync(
        string npcId,
        string conversationId,
        string openKey,
        string reason,
        CancellationToken ct)
    {
        var payload = new JsonObject
        {
            ["conversationId"] = conversationId,
            ["prompt"] = _options.Policy.OpenPrompt
        };
        var action = new GameAction(
            npcId,
            _options.Policy.GameId,
            GameActionType.OpenPrivateChat,
            $"trace_private_chat_{Guid.NewGuid():N}",
            _options.Policy.GetOpenIdempotencyKey(npcId, openKey),
            new GameActionTarget("player"),
            reason,
            payload,
            _options.Policy.GetBodyBinding(npcId));
        return _commands.SubmitAsync(action, ct);
    }

    private Task<GameCommandResult> SubmitSpeakAsync(
        string npcId,
        string text,
        string conversationId,
        string source,
        CancellationToken ct,
        string? messageKind = null)
    {
        var payload = new JsonObject
        {
            ["text"] = text,
            ["channel"] = "private_chat",
            ["conversationId"] = conversationId,
            ["source"] = source
        };
        if (!string.IsNullOrWhiteSpace(messageKind))
            payload["message_kind"] = messageKind;

        var action = new GameAction(
            npcId,
            _options.Policy.GameId,
            GameActionType.Speak,
            $"trace_private_chat_reply_{Guid.NewGuid():N}",
            _options.Policy.GetReplyIdempotencyKey(npcId, conversationId),
            new GameActionTarget("player"),
            "private chat reply",
            payload,
            _options.Policy.GetBodyBinding(npcId));
        return _commands.SubmitAsync(action, ct);
    }

    private bool ShouldEndAfterReply()
        => _options.ReopenPolicy is PrivateChatSessionReopenPolicy.Never ||
           _turns >= Math.Max(1, _options.MaxTurnsPerSession);

    public void Dispose() => EndSession();

    private async Task<PrivateChatStepResult> ProcessEventBatchCoreAsync(GameEventBatch batch, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var result = await ProcessRecordsCoreAsync(batch.Records, ct);
        _cursor = batch.NextCursor;
        return result;
    }

    private async Task<PrivateChatStepResult> ProcessRecordsCoreAsync(IReadOnlyList<GameEventRecord> records, CancellationToken ct)
    {
        var processed = 0;
        foreach (var record in records)
        {
            await ProcessRecordAsync(record, ct);
            AdvanceCursor(record);
            processed++;
        }

        return new PrivateChatStepResult(_state, _conversationId, processed);
    }

    private void AdvanceCursor(GameEventRecord record)
        => _cursor = GameEventCursor.FromRecord(record);

    private async Task<bool> TryAcquireSessionLeaseAsync(string npcId, string conversationId, CancellationToken ct)
    {
        if (_options.SessionLeaseCoordinator is null)
            return true;

        try
        {
            _sessionLease = await _options.SessionLeaseCoordinator.AcquireAsync(
                new PrivateChatSessionLeaseRequest(
                    npcId,
                    _options.Policy.SaveId,
                    conversationId,
                    _options.SessionLeaseOwner,
                    _options.SessionLeaseReason),
                ct);
            return true;
        }
        catch
        {
            _sessionLease = null;
            return false;
        }
    }

    private void EndSession()
    {
        ReleaseSessionLease();
        _state = PrivateChatState.Idle;
        _conversationId = null;
        _activeNpcId = null;
        _pendingOpen = null;
    }

    private void ReleaseSessionLease()
    {
        _sessionLease?.Dispose();
        _sessionLease = null;
    }
}

internal sealed record PendingPrivateChatOpen(
    string NpcId,
    string ConversationId,
    string OpenKey,
    string Reason,
    int Attempts);
