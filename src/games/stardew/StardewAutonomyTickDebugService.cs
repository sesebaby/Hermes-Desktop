namespace Hermes.Agent.Games.Stardew;

using Hermes.Agent.Game;
using Hermes.Agent.LLM;
using Hermes.Agent.Runtime;
using Microsoft.Extensions.Logging;

public sealed class StardewAutonomyTickDebugService
{
    private readonly IStardewBridgeDiscovery _discovery;
    private readonly Func<StardewBridgeOptions, IGameAdapter> _adapterFactory;
    private readonly IChatClient _chatClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _runtimeRoot;

    public StardewAutonomyTickDebugService(
        IStardewBridgeDiscovery discovery,
        HttpClient httpClient,
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        string runtimeRoot)
        : this(
            discovery,
            options => new StardewGameAdapter(new SmapiModApiClient(httpClient, options), "manual-debug"),
            chatClient,
            loggerFactory,
            runtimeRoot)
    {
    }

    public StardewAutonomyTickDebugService(
        IStardewBridgeDiscovery discovery,
        Func<StardewBridgeOptions, IGameAdapter> adapterFactory,
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        string runtimeRoot)
    {
        _discovery = discovery;
        _adapterFactory = adapterFactory;
        _chatClient = chatClient;
        _loggerFactory = loggerFactory;
        _runtimeRoot = runtimeRoot;
    }

    public async Task<StardewAutonomyTickDebugResult> RunOneTickAsync(string npcId, CancellationToken ct)
    {
        var normalizedNpcId = string.IsNullOrWhiteSpace(npcId) ? "haley" : npcId.Trim().ToLowerInvariant();
        if (!_discovery.TryReadLatest(out var snapshot, out var failureReason) || snapshot is null)
        {
            return StardewAutonomyTickDebugResult.Failed(
                normalizedNpcId,
                failureReason ?? StardewBridgeErrorCodes.BridgeUnavailable);
        }

        try
        {
            var descriptor = new NpcRuntimeDescriptor(
                normalizedNpcId,
                normalizedNpcId,
                "stardew-valley",
                "manual-debug",
                "default",
                "stardew",
                "manual-debug",
                $"sdv_manual-debug_{normalizedNpcId}_default");
            var npcNamespace = new NpcNamespace(_runtimeRoot, descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId);
            var context = new NpcRuntimeContextFactory().Create(npcNamespace, _chatClient, _loggerFactory);
            var adapter = _adapterFactory(snapshot.Options);
            var tools = StardewNpcToolFactory.CreateDefault(adapter, descriptor);
            var agent = new NpcAgentFactory().Create(_chatClient, context, tools, _loggerFactory, maxToolIterations: 6);
            var loop = new NpcAutonomyLoop(
                adapter,
                new NpcObservationFactStore(),
                agent,
                new NpcRuntimeLogWriter(Path.Combine(npcNamespace.ActivityPath, "runtime.jsonl")),
                context.MemoryManager);

            var tick = await loop.RunOneTickAsync(descriptor, new GameEventCursor(null), ct);
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
