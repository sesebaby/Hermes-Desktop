namespace Hermes.Agent.Games.Stardew;

using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.LLM;
using Hermes.Agent.Runtime;
using Hermes.Agent.Skills;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging;

public sealed class StardewAutonomyTickDebugService
{
    private readonly IStardewBridgeDiscovery _discovery;
    private readonly Func<StardewBridgeDiscoverySnapshot, IGameAdapter> _adapterFactory;
    private readonly IChatClient _chatClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _runtimeRoot;
    private readonly NpcRuntimeSupervisor _runtimeSupervisor;
    private readonly SkillManager _skillManager;
    private readonly ICronScheduler _cronScheduler;
    private readonly StardewNpcRuntimeBindingResolver _bindingResolver;
    private readonly Func<IEnumerable<ITool>> _discoveredToolProvider;
    private readonly bool _includeMemory;
    private readonly bool _includeUser;
    private readonly int _maxToolIterations;

    public StardewAutonomyTickDebugService(
        IStardewBridgeDiscovery discovery,
        HttpClient httpClient,
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        SkillManager skillManager,
        ICronScheduler cronScheduler,
        NpcRuntimeSupervisor runtimeSupervisor,
        StardewNpcRuntimeBindingResolver bindingResolver,
        Func<IEnumerable<ITool>>? discoveredToolProvider,
        bool includeMemory,
        bool includeUser,
        int maxToolIterations,
        string runtimeRoot)
        : this(
            discovery,
            snapshot => new StardewGameAdapter(
                new SmapiModApiClient(httpClient, snapshot.Options),
                StardewBridgeRuntimeIdentity.RequireSaveId(snapshot)),
            chatClient,
            loggerFactory,
            skillManager,
            cronScheduler,
            runtimeSupervisor,
            bindingResolver,
            discoveredToolProvider,
            includeMemory,
            includeUser,
            maxToolIterations,
            runtimeRoot)
    {
    }

    public StardewAutonomyTickDebugService(
        IStardewBridgeDiscovery discovery,
        Func<StardewBridgeDiscoverySnapshot, IGameAdapter> adapterFactory,
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        SkillManager skillManager,
        ICronScheduler cronScheduler,
        NpcRuntimeSupervisor runtimeSupervisor,
        StardewNpcRuntimeBindingResolver bindingResolver,
        Func<IEnumerable<ITool>>? discoveredToolProvider,
        bool includeMemory,
        bool includeUser,
        int maxToolIterations,
        string runtimeRoot)
    {
        _discovery = discovery;
        _adapterFactory = adapterFactory;
        _chatClient = chatClient;
        _loggerFactory = loggerFactory;
        _skillManager = skillManager;
        _cronScheduler = cronScheduler;
        _runtimeSupervisor = runtimeSupervisor;
        _bindingResolver = bindingResolver;
        _discoveredToolProvider = discoveredToolProvider ?? (() => Enumerable.Empty<ITool>());
        _includeMemory = includeMemory;
        _includeUser = includeUser;
        _maxToolIterations = Math.Max(2, maxToolIterations);
        _runtimeRoot = runtimeRoot;
    }

    public async Task<StardewAutonomyTickDebugResult> RunOneTickAsync(string npcId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(npcId))
            return StardewAutonomyTickDebugResult.Failed(string.Empty, StardewBridgeErrorCodes.InvalidTarget);

        var normalizedNpcId = npcId.Trim();
        if (!_discovery.TryReadLatest(out var snapshot, out var failureReason) || snapshot is null)
        {
            return StardewAutonomyTickDebugResult.Failed(
                normalizedNpcId,
                failureReason ?? StardewBridgeErrorCodes.BridgeUnavailable);
        }

        if (!StardewBridgeRuntimeIdentity.TryGetSaveId(snapshot, out var saveId))
            return StardewAutonomyTickDebugResult.Failed(normalizedNpcId, StardewBridgeErrorCodes.BridgeStaleDiscovery);

        try
        {
            var binding = _bindingResolver.Resolve(normalizedNpcId, saveId);
            var descriptor = binding.Descriptor;
            var handle = await _runtimeSupervisor.GetOrCreateAutonomyHandleAsync(
                descriptor,
                binding.Pack,
                _runtimeRoot,
                new NpcRuntimeAutonomyBindingRequest(
                    ChannelKey: "autonomy",
                    AdapterKey: $"{snapshot.Options.Host}:{snapshot.Options.Port}:{snapshot.StartedAtUtc:O}:{saveId}",
                    IncludeMemory: _includeMemory,
                    IncludeUser: _includeUser,
                    MaxToolIterations: _maxToolIterations,
                    AdapterFactory: () => _adapterFactory(snapshot),
                    GameToolFactory: adapter => StardewNpcToolFactory.CreateDefault(adapter, descriptor),
                    Services: new NpcRuntimeCompositionServices(
                        _chatClient,
                        _loggerFactory,
                        _skillManager,
                        _cronScheduler),
                    ToolSurface: NpcToolSurface.FromTools(_discoveredToolProvider())),
                ct);

            var tick = await handle.Loop.RunOneTickAsync(handle.Instance, new GameEventCursor(null), ct);
            return new StardewAutonomyTickDebugResult(
                true,
                tick.NpcId,
                tick.TraceId,
                tick.ObservationFacts,
                tick.EventFacts,
                string.IsNullOrWhiteSpace(tick.DecisionResponse) ? null : tick.DecisionResponse,
                null);
        }
        catch (Exception ex)
        {
            return StardewAutonomyTickDebugResult.Failed(normalizedNpcId, ex.Message);
        }
    }
}

public sealed record StardewAutonomyTickDebugResult(
    bool Success,
    string NpcId,
    string? TraceId,
    int ObservationFacts,
    int EventFacts,
    string? DecisionResponse,
    string? FailureReason)
{
    public static StardewAutonomyTickDebugResult Failed(string npcId, string failureReason)
        => new(false, npcId, null, 0, 0, null, failureReason);
}
