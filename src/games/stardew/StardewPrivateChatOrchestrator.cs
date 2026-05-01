namespace Hermes.Agent.Games.Stardew;

using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.LLM;
using Hermes.Agent.Runtime;
using Hermes.Agent.Skills;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging;

public sealed class StardewPrivateChatOrchestrator : IDisposable
{
    private readonly PrivateChatOrchestrator _inner;

    public StardewPrivateChatOrchestrator(
        IGameEventSource events,
        IGameCommandService commands,
        INpcPrivateChatAgentRunner agentRunner,
        StardewPrivateChatOptions? options = null,
        IPrivateChatSessionLeaseCoordinator? sessionLeaseCoordinator = null)
    {
        var stardewOptions = options ?? new StardewPrivateChatOptions();
        _inner = new PrivateChatOrchestrator(
            events,
            commands,
            new StardewPrivateChatAgentRunnerAdapter(agentRunner),
            new PrivateChatOrchestratorOptions(
                new PrivateChatPolicy(
                    NpcId: stardewOptions.NpcId ?? string.Empty,
                    SaveId: stardewOptions.SaveId,
                    GameId: "stardew-valley",
                    OpenPrompt: "Say something.",
                    OpenTriggerEventTypes:
                    [
                        "vanilla_dialogue_completed",
                        "vanilla_dialogue_unavailable"
                    ],
                    IsRetryableOpenFailure: IsRetryableOpenFailure),
                ToCoreReopenPolicy(stardewOptions.ReopenPolicy),
                stardewOptions.MaxTurnsPerSession,
                stardewOptions.MaxOpenAttempts,
                SessionLeaseCoordinator: sessionLeaseCoordinator));
    }

    public StardewPrivateChatState State => ToStardewState(_inner.State);

    public string? ConversationId => _inner.ConversationId;

    internal Task<int> DrainExistingEventsAsync(CancellationToken ct)
        => _inner.DrainExistingEventsAsync(ct);

    public void DrainRecords(IReadOnlyList<GameEventRecord> records)
        => _inner.DrainRecords(records);

    public void Dispose() => _inner.Dispose();

    internal async Task<StardewPrivateChatStepResult> ProcessNextAsync(CancellationToken ct)
    {
        var result = await _inner.ProcessNextAsync(ct);
        return new StardewPrivateChatStepResult(
            ToStardewState(result.State),
            result.ConversationId,
            result.EventsProcessed);
    }

    public async Task<StardewPrivateChatStepResult> ProcessRecordsAsync(
        IReadOnlyList<GameEventRecord> records,
        CancellationToken ct)
    {
        var result = await _inner.ProcessRecordsAsync(records, ct);
        return new StardewPrivateChatStepResult(
            ToStardewState(result.State),
            result.ConversationId,
            result.EventsProcessed);
    }

    private static bool IsRetryableOpenFailure(GameCommandResult result)
        => result.Retryable ||
           string.Equals(result.FailureReason, StardewBridgeErrorCodes.MenuBlocked, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(result.FailureReason, StardewBridgeErrorCodes.WorldNotReady, StringComparison.OrdinalIgnoreCase);

    private static PrivateChatSessionReopenPolicy ToCoreReopenPolicy(PrivateChatReopenPolicy policy)
        => policy switch
        {
            PrivateChatReopenPolicy.Never => PrivateChatSessionReopenPolicy.Never,
            PrivateChatReopenPolicy.OnceAfterReply => PrivateChatSessionReopenPolicy.OnceAfterReply,
            PrivateChatReopenPolicy.UntilCancelled => PrivateChatSessionReopenPolicy.UntilCancelled,
            _ => PrivateChatSessionReopenPolicy.OnceAfterReply
        };

    private static StardewPrivateChatState ToStardewState(PrivateChatState state)
        => state switch
        {
            PrivateChatState.Idle => StardewPrivateChatState.Idle,
            PrivateChatState.PendingOpen => StardewPrivateChatState.PendingOpen,
            PrivateChatState.AwaitingPlayerInput => StardewPrivateChatState.AwaitingPlayerInput,
            PrivateChatState.WaitingAgentReply => StardewPrivateChatState.WaitingAgentReply,
            PrivateChatState.ShowingReply => StardewPrivateChatState.ShowingReply,
            PrivateChatState.WaitingReplyDismissal => StardewPrivateChatState.WaitingReplyDismissal,
            _ => StardewPrivateChatState.Idle
        };

    private sealed class StardewPrivateChatAgentRunnerAdapter : IPrivateChatAgentRunner
    {
        private readonly INpcPrivateChatAgentRunner _inner;

        public StardewPrivateChatAgentRunnerAdapter(INpcPrivateChatAgentRunner inner)
        {
            _inner = inner;
        }

        public async Task<PrivateChatAgentReply> ReplyAsync(PrivateChatAgentRequest request, CancellationToken ct)
        {
            var reply = await _inner.ReplyAsync(
                new NpcPrivateChatRequest(request.NpcId, request.SaveId, request.ConversationId, request.PlayerText),
                ct);
            return new PrivateChatAgentReply(reply.Text);
        }
    }
}

public interface INpcPrivateChatAgentRunner
{
    Task<NpcPrivateChatReply> ReplyAsync(NpcPrivateChatRequest request, CancellationToken ct);
}

public sealed class StardewNpcPrivateChatAgentRunner : INpcPrivateChatAgentRunner
{
    private readonly IChatClient _chatClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _runtimeRoot;
    private readonly NpcRuntimeSupervisor _runtimeSupervisor;
    private readonly SkillManager _skillManager;
    private readonly ICronScheduler _cronScheduler;
    private readonly StardewNpcRuntimeBindingResolver _bindingResolver;
    private readonly INpcToolSurfaceSnapshotProvider _toolSnapshotProvider;
    private readonly bool _includeMemory;
    private readonly bool _includeUser;
    private readonly int _maxToolIterations;

    public StardewNpcPrivateChatAgentRunner(
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        string runtimeRoot,
        NpcRuntimeSupervisor runtimeSupervisor,
        SkillManager skillManager,
        ICronScheduler cronScheduler,
        StardewNpcRuntimeBindingResolver bindingResolver,
        INpcToolSurfaceSnapshotProvider toolSnapshotProvider,
        bool includeMemory = true,
        bool includeUser = true,
        int maxToolIterations = 25)
    {
        _chatClient = chatClient;
        _loggerFactory = loggerFactory;
        _runtimeRoot = runtimeRoot;
        _runtimeSupervisor = runtimeSupervisor;
        _skillManager = skillManager;
        _cronScheduler = cronScheduler;
        _bindingResolver = bindingResolver;
        _toolSnapshotProvider = toolSnapshotProvider;
        _includeMemory = includeMemory;
        _includeUser = includeUser;
        _maxToolIterations = Math.Max(2, maxToolIterations);
    }

    public StardewNpcPrivateChatAgentRunner(
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        string runtimeRoot,
        NpcRuntimeSupervisor runtimeSupervisor,
        SkillManager skillManager,
        ICronScheduler cronScheduler,
        StardewNpcRuntimeBindingResolver bindingResolver,
        bool includeMemory = true,
        bool includeUser = true,
        Func<IEnumerable<ITool>>? discoveredToolProvider = null,
        int maxToolIterations = 25)
        : this(
            chatClient,
            loggerFactory,
            runtimeRoot,
            runtimeSupervisor,
            skillManager,
            cronScheduler,
            bindingResolver,
            new NpcToolSurfaceSnapshotProvider(discoveredToolProvider ?? (() => Enumerable.Empty<ITool>())),
            includeMemory,
            includeUser,
            maxToolIterations)
    {
    }

    public async Task<NpcPrivateChatReply> ReplyAsync(NpcPrivateChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NpcId))
            throw new ArgumentException("NPC id is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.SaveId))
            throw new ArgumentException("Save id is required.", nameof(request));

        var saveId = request.SaveId.Trim();
        var binding = _bindingResolver.Resolve(request.NpcId, saveId);
        var descriptor = binding.Descriptor;
        var toolSnapshot = _toolSnapshotProvider.Capture();
        var handle = await _runtimeSupervisor.GetOrCreatePrivateChatHandleAsync(
            descriptor,
            binding.Pack,
            _runtimeRoot,
            new NpcRuntimeAgentBindingRequest(
                ChannelKey: "private_chat",
                SystemPromptSupplement: BuildPrivateChatSystemPrompt(descriptor.DisplayName),
                IncludeMemory: _includeMemory,
                IncludeUser: _includeUser,
                MaxToolIterations: _maxToolIterations,
                Services: new NpcRuntimeCompositionServices(
                    _chatClient,
                    _loggerFactory,
                    _skillManager,
                    _cronScheduler),
                ToolSurface: toolSnapshot.ToolSurface,
                ToolSurfaceSnapshotVersion: toolSnapshot.SnapshotVersion),
            ct);

        var response = await handle.Agent.ChatAsync(
            BuildPrivateChatMessage(descriptor.DisplayName, request.PlayerText),
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

    private static string BuildPrivateChatSystemPrompt(string displayName)
        =>
            $"You are the Stardew Valley NPC {displayName} in a private chat with the player. " +
            $"Reply as {displayName} with one concise spoken response. Do not include labels, markdown, or tool narration.";
}

public sealed class StardewPrivateChatRuntimeAdapter : IDisposable
{
    private readonly object _gate = new();
    private readonly INpcPrivateChatAgentRunner _agentRunner;
    private readonly StardewPrivateChatOptions _options;
    private readonly ILogger<StardewPrivateChatRuntimeAdapter> _logger;
    private readonly IPrivateChatSessionLeaseCoordinator? _sessionLeaseCoordinator;
    private string? _bridgeKey;
    private StardewPrivateChatOrchestrator? _orchestrator;

    public StardewPrivateChatRuntimeAdapter(
        INpcPrivateChatAgentRunner agentRunner,
        ILogger<StardewPrivateChatRuntimeAdapter> logger,
        StardewPrivateChatOptions? options = null,
        IPrivateChatSessionLeaseCoordinator? sessionLeaseCoordinator = null)
    {
        _agentRunner = agentRunner;
        _logger = logger;
        _options = options ?? new StardewPrivateChatOptions();
        _sessionLeaseCoordinator = sessionLeaseCoordinator;
    }

    public async Task ProcessAsync(
        string bridgeKey,
        string saveId,
        IGameAdapter adapter,
        IReadOnlyList<GameEventRecord> records,
        CancellationToken ct,
        bool drainOnly = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bridgeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(saveId);
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(records);

        EnsureBridgeAttachment(bridgeKey, saveId, adapter, ct);

        StardewPrivateChatOrchestrator? orchestrator;
        lock (_gate)
        {
            orchestrator = _orchestrator;
        }

        if (orchestrator is null)
            return;

        if (drainOnly)
        {
            orchestrator.DrainRecords(records);
            return;
        }

        await orchestrator.ProcessRecordsAsync(records, ct);
    }

    public void Reset()
    {
        lock (_gate)
        {
            _bridgeKey = null;
            DisposeOrchestratorNoThrow();
        }
    }

    public void Dispose() => Reset();

    private void EnsureBridgeAttachment(
        string bridgeKey,
        string saveId,
        IGameAdapter adapter,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (string.Equals(_bridgeKey, bridgeKey, StringComparison.Ordinal))
                return;

            DisposeOrchestratorNoThrow();
            _orchestrator = new StardewPrivateChatOrchestrator(
                adapter.Events,
                adapter.Commands,
                _agentRunner,
                _options with { SaveId = saveId },
                _sessionLeaseCoordinator);
            _bridgeKey = bridgeKey;
        }

        _logger.LogInformation("Stardew private-chat runtime bridge attached: {BridgeKey}", bridgeKey);
    }

    private void DisposeOrchestratorNoThrow()
    {
        if (_orchestrator is null)
            return;

        try
        {
            _orchestrator.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Disposing Stardew private-chat orchestrator failed non-fatally");
        }

        _orchestrator = null;
    }
}

public sealed record NpcPrivateChatRequest(
    string NpcId,
    string SaveId,
    string ConversationId,
    string PlayerText);

public sealed record NpcPrivateChatReply(string Text);

public sealed record StardewPrivateChatOptions(
    string? NpcId = null,
    string SaveId = "",
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
