namespace Hermes.Agent.Games.Stardew;

using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.LLM;
using Hermes.Agent.Runtime;
using Hermes.Agent.Skills;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging;

public sealed class StardewPrivateChatOrchestrator
{
    private readonly PrivateChatOrchestrator _inner;

    public StardewPrivateChatOrchestrator(
        IGameEventSource events,
        IGameCommandService commands,
        INpcPrivateChatAgentRunner agentRunner,
        StardewPrivateChatOptions? options = null)
    {
        var stardewOptions = options ?? new StardewPrivateChatOptions();
        _inner = new PrivateChatOrchestrator(
            events,
            commands,
            new StardewPrivateChatAgentRunnerAdapter(agentRunner),
            new PrivateChatOrchestratorOptions(
                new PrivateChatPolicy(
                    NpcId: stardewOptions.NpcId,
                    SaveId: stardewOptions.SaveId,
                    GameId: "stardew-valley",
                    OpenPrompt: "Say something to Haley.",
                    OpenTriggerEventTypes:
                    [
                        "vanilla_dialogue_completed",
                        "vanilla_dialogue_unavailable"
                    ],
                    IsRetryableOpenFailure: IsRetryableOpenFailure),
                ToCoreReopenPolicy(stardewOptions.ReopenPolicy),
                stardewOptions.MaxTurnsPerSession,
                stardewOptions.MaxOpenAttempts));
    }

    public StardewPrivateChatState State => ToStardewState(_inner.State);

    public string? ConversationId => _inner.ConversationId;

    public Task<int> DrainExistingEventsAsync(CancellationToken ct)
        => _inner.DrainExistingEventsAsync(ct);

    public async Task<StardewPrivateChatStepResult> ProcessNextAsync(CancellationToken ct)
    {
        var result = await _inner.ProcessNextAsync(ct);
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
    private readonly SkillManager _skillManager;
    private readonly ICronScheduler _cronScheduler;
    private readonly Func<IEnumerable<ITool>> _discoveredToolProvider;
    private readonly int _maxToolIterations;

    public StardewNpcPrivateChatAgentRunner(
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        string runtimeRoot,
        SkillManager skillManager,
        ICronScheduler cronScheduler,
        Func<IEnumerable<ITool>>? discoveredToolProvider = null,
        int maxToolIterations = 25)
    {
        _chatClient = chatClient;
        _loggerFactory = loggerFactory;
        _runtimeRoot = runtimeRoot;
        _skillManager = skillManager;
        _cronScheduler = cronScheduler;
        _discoveredToolProvider = discoveredToolProvider ?? (() => Enumerable.Empty<ITool>());
        _maxToolIterations = Math.Max(2, maxToolIterations);
    }

    public async Task<NpcPrivateChatReply> ReplyAsync(NpcPrivateChatRequest request, CancellationToken ct)
    {
        var npcId = string.IsNullOrWhiteSpace(request.NpcId) ? "haley" : request.NpcId.Trim().ToLowerInvariant();
        var displayName = ResolveDisplayName(npcId, request.NpcId);
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
            BuildPrivateChatSystemPrompt(displayName),
            skillManager: _skillManager);
        var agent = new NpcAgentFactory().Create(
            _chatClient,
            context,
            Enumerable.Empty<ITool>(),
            _loggerFactory,
            maxToolIterations: _maxToolIterations);
        AgentCapabilityAssembler.RegisterBuiltInTools(
            agent,
            new AgentCapabilityServices
            {
                ChatClient = _chatClient,
                ToolRegistry = context.ToolRegistry,
                TodoStore = context.TodoStore,
                CronScheduler = _cronScheduler,
                MemoryManager = context.MemoryManager,
                PluginManager = context.PluginManager,
                TranscriptRecallService = context.TranscriptRecallService,
                SkillManager = _skillManager,
                CheckpointDirectory = Path.Combine(npcNamespace.RuntimeRoot, "checkpoints"),
                MemoryAvailable = true
            });
        AgentCapabilityAssembler.RegisterDiscoveredTools(agent, context.ToolRegistry, _discoveredToolProvider());

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

    private static string BuildPrivateChatSystemPrompt(string displayName)
        =>
            $"You are the Stardew Valley NPC {displayName} in a private chat with the player. " +
            $"Reply as {displayName} with one concise spoken response. Do not include labels, markdown, or tool narration.";

    private static string ResolveDisplayName(string npcId, string rawNpcId)
        => npcId switch
        {
            "haley" => "Haley",
            "penny" => "Penny",
            _ => string.IsNullOrWhiteSpace(rawNpcId) ? npcId : rawNpcId.Trim()
        };
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
