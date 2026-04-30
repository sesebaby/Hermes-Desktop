namespace Hermes.Agent.Games.Stardew;

using System.Text.Json.Nodes;
using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.LLM;
using Hermes.Agent.Runtime;
using Microsoft.Extensions.Logging;

public sealed class StardewPrivateChatOrchestrator
{
    private readonly IGameEventSource _events;
    private readonly IGameCommandService _commands;
    private readonly INpcPrivateChatAgentRunner _agentRunner;
    private readonly StardewPrivateChatOptions _options;
    private readonly HashSet<string> _completedOpenKeys = new(StringComparer.OrdinalIgnoreCase);
    private GameEventCursor _cursor = new(null);
    private StardewPrivateChatState _state = StardewPrivateChatState.Idle;
    private string? _conversationId;
    private int _turns;
    private StardewPendingPrivateChatOpen? _pendingOpen;

    public StardewPrivateChatOrchestrator(
        IGameEventSource events,
        IGameCommandService commands,
        INpcPrivateChatAgentRunner agentRunner,
        StardewPrivateChatOptions? options = null)
    {
        _events = events;
        _commands = commands;
        _agentRunner = agentRunner;
        _options = options ?? new StardewPrivateChatOptions();
    }

    public StardewPrivateChatState State => _state;

    public string? ConversationId => _conversationId;

    public async Task<int> DrainExistingEventsAsync(CancellationToken ct)
    {
        var records = await _events.PollAsync(_cursor, ct);
        foreach (var record in records)
            _cursor = new GameEventCursor(record.EventId);

        return records.Count;
    }

    public async Task<StardewPrivateChatStepResult> ProcessNextAsync(CancellationToken ct)
    {
        var processed = 0;
        await TryRetryPendingOpenAsync(ct);

        var records = await _events.PollAsync(_cursor, ct);
        foreach (var record in records)
        {
            await ProcessRecordAsync(record, ct);
            _cursor = new GameEventCursor(record.EventId);
            processed++;
        }

        return new StardewPrivateChatStepResult(_state, _conversationId, processed);
    }

    private async Task ProcessRecordAsync(GameEventRecord record, CancellationToken ct)
    {
        if (!IsTargetNpc(record.NpcId))
            return;

        if (string.Equals(record.EventType, "vanilla_dialogue_completed", StringComparison.OrdinalIgnoreCase))
        {
            await TryOpenAfterVanillaDialogueAsync(record, ct);
            return;
        }

        if (string.Equals(record.EventType, "player_private_message_submitted", StringComparison.OrdinalIgnoreCase))
            await TryReplyToPlayerMessageAsync(record, ct);

        if (string.Equals(record.EventType, "player_private_message_cancelled", StringComparison.OrdinalIgnoreCase))
            TryCancelPrivateChat(record);

        if (string.Equals(record.EventType, "private_chat_reply_closed", StringComparison.OrdinalIgnoreCase))
            await TryReopenAfterPrivateChatReplyClosedAsync(record, ct);
    }

    private async Task TryOpenAfterVanillaDialogueAsync(GameEventRecord record, CancellationToken ct)
    {
        if (_state is not StardewPrivateChatState.Idle)
            return;

        var dialogueEventId = GetCorrelationOrEventId(record);
        if (_completedOpenKeys.Contains(dialogueEventId) ||
            string.Equals(_pendingOpen?.OpenKey, dialogueEventId, StringComparison.OrdinalIgnoreCase))
            return;

        _conversationId = $"pc_{dialogueEventId}";
        _turns = 0;
        _state = StardewPrivateChatState.PendingOpen;
        _pendingOpen = new StardewPendingPrivateChatOpen(
            record.NpcId!,
            _conversationId,
            dialogueEventId,
            "auto_after_vanilla_dialogue",
            Attempts: 0);

        await TrySubmitPendingOpenAsync(ct);
    }

    private async Task TryReplyToPlayerMessageAsync(GameEventRecord record, CancellationToken ct)
    {
        if (_state is not StardewPrivateChatState.AwaitingPlayerInput || string.IsNullOrWhiteSpace(_conversationId))
            return;

        var submittedConversationId = GetPayloadString(record.Payload, "conversationId") ?? record.CorrelationId;
        if (!string.Equals(submittedConversationId, _conversationId, StringComparison.OrdinalIgnoreCase))
            return;

        var playerText = GetPayloadString(record.Payload, "text");
        if (string.IsNullOrWhiteSpace(playerText))
        {
            EndSession();
            return;
        }

        _state = StardewPrivateChatState.WaitingAgentReply;
        NpcPrivateChatReply reply;
        try
        {
            reply = await _agentRunner.ReplyAsync(
                new NpcPrivateChatRequest(record.NpcId!, _options.SaveId, _conversationId, playerText),
                ct);
        }
        catch
        {
            EndSession();
            return;
        }

        if (string.IsNullOrWhiteSpace(reply.Text))
        {
            EndSession();
            return;
        }

        _state = StardewPrivateChatState.ShowingReply;
        var speakResult = await SubmitSpeakAsync(record.NpcId!, reply.Text, _conversationId, ct);
        _turns++;
        if (!speakResult.Accepted || ShouldEndAfterReply())
        {
            EndSession();
            return;
        }

        _state = StardewPrivateChatState.WaitingReplyDismissal;
    }

    private async Task TryReopenAfterPrivateChatReplyClosedAsync(GameEventRecord record, CancellationToken ct)
    {
        if (_state is not StardewPrivateChatState.WaitingReplyDismissal || string.IsNullOrWhiteSpace(_conversationId))
            return;

        var closedConversationId = GetPayloadString(record.Payload, "conversationId") ?? record.CorrelationId;
        if (!string.Equals(closedConversationId, _conversationId, StringComparison.OrdinalIgnoreCase))
            return;

        var nextConversationId = $"{_conversationId}_turn{_turns + 1}";
        _conversationId = nextConversationId;
        _pendingOpen = new StardewPendingPrivateChatOpen(
            record.NpcId!,
            nextConversationId,
            nextConversationId,
            "reopen_after_reply",
            Attempts: 0);
        _state = StardewPrivateChatState.PendingOpen;

        await TrySubmitPendingOpenAsync(ct);
    }

    private void TryCancelPrivateChat(GameEventRecord record)
    {
        if (_state is not StardewPrivateChatState.AwaitingPlayerInput || string.IsNullOrWhiteSpace(_conversationId))
            return;

        var cancelledConversationId = GetPayloadString(record.Payload, "conversationId") ?? record.CorrelationId;
        if (string.Equals(cancelledConversationId, _conversationId, StringComparison.OrdinalIgnoreCase))
            EndSession();
    }

    private async Task TryRetryPendingOpenAsync(CancellationToken ct)
    {
        if (_state is StardewPrivateChatState.PendingOpen && _pendingOpen is not null)
            await TrySubmitPendingOpenAsync(ct);
    }

    private async Task TrySubmitPendingOpenAsync(CancellationToken ct)
    {
        if (_pendingOpen is not { } pending)
            return;

        pending = pending with { Attempts = pending.Attempts + 1 };
        _pendingOpen = pending;
        _state = StardewPrivateChatState.PendingOpen;
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
            _state = StardewPrivateChatState.AwaitingPlayerInput;
            return;
        }

        if (IsRetryableOpenFailure(result) && pending.Attempts < Math.Max(1, _options.MaxOpenAttempts))
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
            ["prompt"] = "Say something to Haley."
        };
        var action = new GameAction(
            npcId,
            "stardew-valley",
            GameActionType.OpenPrivateChat,
            $"trace_private_chat_{Guid.NewGuid():N}",
            $"private_chat:{_options.SaveId}:{npcId}:{openKey}",
            new GameActionTarget("player"),
            reason,
            payload);
        return _commands.SubmitAsync(action, ct);
    }

    private Task<GameCommandResult> SubmitSpeakAsync(string npcId, string text, string conversationId, CancellationToken ct)
    {
        var payload = new JsonObject
        {
            ["text"] = text,
            ["channel"] = "private_chat",
            ["conversationId"] = conversationId
        };
        var action = new GameAction(
            npcId,
            "stardew-valley",
            GameActionType.Speak,
            $"trace_private_chat_reply_{Guid.NewGuid():N}",
            $"private_chat_reply:{_options.SaveId}:{npcId}:{conversationId}",
            new GameActionTarget("player"),
            "private chat reply",
            payload);
        return _commands.SubmitAsync(action, ct);
    }

    private bool IsTargetNpc(string? npcId)
        => !string.IsNullOrWhiteSpace(npcId) &&
           string.Equals(npcId, _options.NpcId, StringComparison.OrdinalIgnoreCase);

    private bool ShouldEndAfterReply()
        => _options.ReopenPolicy is PrivateChatReopenPolicy.Never ||
           _turns >= Math.Max(1, _options.MaxTurnsPerSession);

    private static bool IsRetryableOpenFailure(GameCommandResult result)
        => result.Retryable ||
           string.Equals(result.FailureReason, StardewBridgeErrorCodes.MenuBlocked, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(result.FailureReason, StardewBridgeErrorCodes.WorldNotReady, StringComparison.OrdinalIgnoreCase);

    private void EndSession()
    {
        _state = StardewPrivateChatState.Idle;
        _conversationId = null;
        _pendingOpen = null;
    }

    private static string GetCorrelationOrEventId(GameEventRecord record)
        => string.IsNullOrWhiteSpace(record.CorrelationId) ? record.EventId : record.CorrelationId;

    private static string? GetPayloadString(JsonObject? payload, string propertyName)
        => payload is not null && payload.TryGetPropertyValue(propertyName, out var node)
            ? node?.GetValue<string>()
            : null;
}

public interface INpcPrivateChatAgentRunner
{
    Task<NpcPrivateChatReply> ReplyAsync(NpcPrivateChatRequest request, CancellationToken ct);
}

public sealed class StardewNpcPrivateChatAgentRunner : INpcPrivateChatAgentRunner
{
    private const string PrivateChatSystemPrompt =
        "You are the Stardew Valley NPC Haley in a private chat with the player. " +
        "Reply as Haley with one concise spoken response. Do not include labels, markdown, or tool narration.";

    private readonly IChatClient _chatClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _runtimeRoot;

    public StardewNpcPrivateChatAgentRunner(
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        string runtimeRoot)
    {
        _chatClient = chatClient;
        _loggerFactory = loggerFactory;
        _runtimeRoot = runtimeRoot;
    }

    public async Task<NpcPrivateChatReply> ReplyAsync(NpcPrivateChatRequest request, CancellationToken ct)
    {
        var npcId = string.IsNullOrWhiteSpace(request.NpcId) ? "haley" : request.NpcId.Trim().ToLowerInvariant();
        var displayName = string.Equals(npcId, "haley", StringComparison.OrdinalIgnoreCase) ? "Haley" : request.NpcId.Trim();
        var saveId = string.IsNullOrWhiteSpace(request.SaveId) ? "manual-debug" : request.SaveId.Trim();
        var descriptor = new NpcRuntimeDescriptor(
            npcId,
            displayName,
            "stardew-valley",
            saveId,
            "default",
            "stardew",
            saveId,
            $"sdv_{saveId}_{npcId}_default");
        var npcNamespace = new NpcNamespace(_runtimeRoot, descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId);
        var context = new NpcRuntimeContextFactory().Create(
            npcNamespace,
            _chatClient,
            _loggerFactory,
            PrivateChatSystemPrompt);
        var agent = new NpcAgentFactory().Create(
            _chatClient,
            context,
            Array.Empty<ITool>(),
            _loggerFactory,
            maxToolIterations: 1);
        var response = await agent.ChatAsync(
            BuildPrivateChatMessage(displayName, request.PlayerText),
            new Session
            {
                Id = $"{descriptor.SessionId}:private_chat:{request.ConversationId}",
                Platform = descriptor.AdapterId
            },
            ct);

        return new NpcPrivateChatReply(response.Trim());
    }

    private static string BuildPrivateChatMessage(string displayName, string playerText)
        =>
            $"Private chat for {displayName}.\n" +
            "The player explicitly typed this message to you. Respond directly in-character.\n\n" +
            $"Player: {playerText}";
}

public sealed class StardewPrivateChatBackgroundService : IDisposable
{
    private readonly object _gate = new();
    private readonly IStardewBridgeDiscovery _discovery;
    private readonly Func<StardewBridgeOptions, IGameAdapter> _adapterFactory;
    private readonly INpcPrivateChatAgentRunner _agentRunner;
    private readonly StardewPrivateChatOptions _options;
    private readonly ILogger<StardewPrivateChatBackgroundService> _logger;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private string? _bridgeKey;
    private StardewPrivateChatOrchestrator? _orchestrator;

    public StardewPrivateChatBackgroundService(
        IStardewBridgeDiscovery discovery,
        HttpClient httpClient,
        INpcPrivateChatAgentRunner agentRunner,
        ILogger<StardewPrivateChatBackgroundService> logger,
        StardewPrivateChatOptions? options = null)
        : this(
            discovery,
            bridgeOptions => new StardewGameAdapter(new SmapiModApiClient(httpClient, bridgeOptions), "manual-debug"),
            agentRunner,
            logger,
            options)
    {
    }

    public StardewPrivateChatBackgroundService(
        IStardewBridgeDiscovery discovery,
        Func<StardewBridgeOptions, IGameAdapter> adapterFactory,
        INpcPrivateChatAgentRunner agentRunner,
        ILogger<StardewPrivateChatBackgroundService> logger,
        StardewPrivateChatOptions? options = null)
    {
        _discovery = discovery;
        _adapterFactory = adapterFactory;
        _agentRunner = agentRunner;
        _logger = logger;
        _options = options ?? new StardewPrivateChatOptions();
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_loopTask is { IsCompleted: false })
                return;

            _cts = new CancellationTokenSource();
            _loopTask = Task.Run(() => RunLoopAsync(_cts.Token));
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        lock (_gate)
        {
            cts = _cts;
            _cts = null;
            _loopTask = null;
            _orchestrator = null;
            _bridgeKey = null;
        }

        try
        {
            cts?.Cancel();
            cts?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stopping Stardew private-chat background service failed non-fatally");
        }
    }

    public void Dispose() => Stop();

    private async Task RunLoopAsync(CancellationToken ct)
    {
        var delay = _options.PollInterval == default ? TimeSpan.FromSeconds(1) : _options.PollInterval;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!_discovery.TryReadLatest(out var snapshot, out _) || snapshot is null)
                {
                    await Task.Delay(delay, ct);
                    continue;
                }

                var key = $"{snapshot.Options.Host}:{snapshot.Options.Port}:{snapshot.StartedAtUtc:O}";
                if (!string.Equals(key, _bridgeKey, StringComparison.Ordinal))
                {
                    var adapter = _adapterFactory(snapshot.Options);
                    _orchestrator = new StardewPrivateChatOrchestrator(adapter.Events, adapter.Commands, _agentRunner, _options);
                    await _orchestrator.DrainExistingEventsAsync(ct);
                    _bridgeKey = key;
                    _logger.LogInformation("Stardew private-chat bridge attached: {BridgeKey}", key);
                }

                if (_orchestrator is not null)
                    await _orchestrator.ProcessNextAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Stardew private-chat background poll failed non-fatally");
            }

            await Task.Delay(delay, ct);
        }
    }
}

public sealed record NpcPrivateChatRequest(
    string NpcId,
    string SaveId,
    string ConversationId,
    string PlayerText);

public sealed record NpcPrivateChatReply(string Text);

public sealed record StardewPrivateChatOptions(
    string NpcId = "haley",
    string SaveId = "manual-debug",
    PrivateChatReopenPolicy ReopenPolicy = PrivateChatReopenPolicy.OnceAfterReply,
    int MaxTurnsPerSession = 3,
    int MaxOpenAttempts = 60,
    TimeSpan PollInterval = default);

public enum PrivateChatReopenPolicy
{
    Never,
    OnceAfterReply,
    UntilCancelled
}

public enum StardewPrivateChatState
{
    Idle,
    PendingOpen,
    AwaitingPlayerInput,
    WaitingAgentReply,
    ShowingReply,
    WaitingReplyDismissal
}

public sealed record StardewPrivateChatStepResult(
    StardewPrivateChatState State,
    string? ConversationId,
    int EventsProcessed);

internal sealed record StardewPendingPrivateChatOpen(
    string NpcId,
    string ConversationId,
    string OpenKey,
    string Reason,
    int Attempts);
