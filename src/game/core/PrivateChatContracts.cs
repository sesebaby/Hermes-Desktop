namespace Hermes.Agent.Game;

public sealed record PrivateChatOrchestratorOptions(
    PrivateChatPolicy Policy,
    PrivateChatSessionReopenPolicy ReopenPolicy = PrivateChatSessionReopenPolicy.OnceAfterReply,
    int MaxTurnsPerSession = 3,
    int MaxOpenAttempts = 60,
    IPrivateChatSessionLeaseCoordinator? SessionLeaseCoordinator = null,
    string SessionLeaseOwner = "private_chat",
    string SessionLeaseReason = "private_chat_session_active");

public sealed record PrivateChatPolicy(
    string NpcId,
    string SaveId,
    string GameId,
    string OpenPrompt,
    IReadOnlyCollection<string> OpenTriggerEventTypes,
    string SubmittedEventType = "player_private_message_submitted",
    string CancelledEventType = "player_private_message_cancelled",
    string ReplyClosedEventType = "private_chat_reply_closed",
    Func<GameEventRecord, string?>? GetConversationId = null,
    Func<GameEventRecord, string?>? GetPlayerText = null,
    Func<GameCommandResult, bool>? IsRetryableOpenFailure = null,
    Func<string, string, string, string>? BuildOpenIdempotencyKey = null,
    Func<string, string, string, string>? BuildReplyIdempotencyKey = null)
{
    public bool IsTargetNpc(string? npcId)
        => !string.IsNullOrWhiteSpace(npcId) &&
           (string.IsNullOrWhiteSpace(NpcId) ||
            string.Equals(npcId, NpcId, StringComparison.OrdinalIgnoreCase));

    public bool IsOpenTrigger(string eventType)
        => OpenTriggerEventTypes.Any(trigger => string.Equals(trigger, eventType, StringComparison.OrdinalIgnoreCase));

    public bool IsSubmittedEvent(string eventType)
        => string.Equals(eventType, SubmittedEventType, StringComparison.OrdinalIgnoreCase);

    public bool IsCancelledEvent(string eventType)
        => string.Equals(eventType, CancelledEventType, StringComparison.OrdinalIgnoreCase);

    public bool IsReplyClosedEvent(string eventType)
        => string.Equals(eventType, ReplyClosedEventType, StringComparison.OrdinalIgnoreCase);

    public string? ExtractConversationId(GameEventRecord record)
        => GetConversationId?.Invoke(record) ??
           GetPayloadString(record, "conversationId") ??
           record.CorrelationId;

    public string? ExtractPlayerText(GameEventRecord record)
        => GetPlayerText?.Invoke(record) ?? GetPayloadString(record, "text");

    public bool ShouldRetryOpen(GameCommandResult result)
        => result.Retryable || (IsRetryableOpenFailure?.Invoke(result) ?? false);

    public string GetOpenIdempotencyKey(string npcId, string openKey)
        => BuildOpenIdempotencyKey?.Invoke(SaveId, npcId, openKey) ??
           $"private_chat:{SaveId}:{npcId}:{openKey}";

    public string GetReplyIdempotencyKey(string npcId, string conversationId)
        => BuildReplyIdempotencyKey?.Invoke(SaveId, npcId, conversationId) ??
           $"private_chat_reply:{SaveId}:{npcId}:{conversationId}";

    private static string? GetPayloadString(GameEventRecord record, string propertyName)
        => record.Payload is not null && record.Payload.TryGetPropertyValue(propertyName, out var node)
            ? node?.GetValue<string>()
            : null;
}

public interface IPrivateChatAgentRunner
{
    Task<PrivateChatAgentReply> ReplyAsync(PrivateChatAgentRequest request, CancellationToken ct);
}

public sealed record PrivateChatAgentRequest(
    string NpcId,
    string SaveId,
    string ConversationId,
    string PlayerText);

public sealed record PrivateChatAgentReply(string Text);

public sealed record PrivateChatSessionLeaseRequest(
    string NpcId,
    string SaveId,
    string ConversationId,
    string Owner,
    string Reason);

public interface IPrivateChatSessionLease : IDisposable
{
    string NpcId { get; }

    string ConversationId { get; }

    string Owner { get; }

    int Generation { get; }
}

public interface IPrivateChatSessionLeaseCoordinator
{
    Task<IPrivateChatSessionLease> AcquireAsync(PrivateChatSessionLeaseRequest request, CancellationToken ct);
}

public enum PrivateChatSessionReopenPolicy
{
    Never,
    OnceAfterReply,
    UntilCancelled
}

public enum PrivateChatState
{
    Idle,
    PendingOpen,
    AwaitingPlayerInput,
    WaitingAgentReply,
    ShowingReply,
    WaitingReplyDismissal
}

public sealed record PrivateChatStepResult(
    PrivateChatState State,
    string? ConversationId,
    int EventsProcessed);
