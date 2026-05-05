namespace Hermes.Agent.Runtime;

using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.LLM;
using Hermes.Agent.Skills;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging;

public sealed class NpcRuntimeSupervisor
{
    private readonly object _gate = new();
    private readonly Dictionary<string, NpcRuntimeInstance> _instances = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NpcRuntimeDriver> _drivers = new(StringComparer.OrdinalIgnoreCase);
    private readonly INpcRuntimeTaskHydrator _taskHydrator;

    public NpcRuntimeSupervisor()
        : this(new NpcRuntimeTaskHydrator(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance))
    {
    }

    public NpcRuntimeSupervisor(INpcRuntimeTaskHydrator taskHydrator)
    {
        _taskHydrator = taskHydrator ?? throw new ArgumentNullException(nameof(taskHydrator));
    }

    public NpcRuntimeInstance Register(NpcRuntimeDescriptor descriptor, string runtimeRoot)
    {
        var key = BuildKey(descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId);
        lock (_gate)
        {
            if (_instances.ContainsKey(key))
                throw new InvalidOperationException($"NPC runtime already registered for '{key}'.");

            var npcNamespace = new NpcNamespace(
                runtimeRoot,
                descriptor.GameId,
                descriptor.SaveId,
                descriptor.NpcId,
                descriptor.ProfileId);
            var instance = new NpcRuntimeInstance(descriptor, npcNamespace);
            _instances[key] = instance;
            return instance;
        }
    }

    public async Task StartAsync(NpcRuntimeDescriptor descriptor, string runtimeRoot, CancellationToken ct)
    {
        var instance = Register(descriptor, runtimeRoot);
        await instance.StartAsync(ct);
        await instance.EnsureTasksHydratedAsync(_taskHydrator, ct);
    }

    public async Task<NpcRuntimeInstance> GetOrStartAsync(NpcRuntimeDescriptor descriptor, string runtimeRoot, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (string.IsNullOrWhiteSpace(runtimeRoot))
            throw new ArgumentException("Runtime root is required.", nameof(runtimeRoot));

        var key = BuildKey(descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId);
        NpcRuntimeInstance? instance;
        lock (_gate)
        {
            if (!_instances.TryGetValue(key, out instance))
            {
                var npcNamespace = new NpcNamespace(
                    runtimeRoot,
                    descriptor.GameId,
                    descriptor.SaveId,
                    descriptor.NpcId,
                    descriptor.ProfileId);
                instance = new NpcRuntimeInstance(descriptor, npcNamespace);
                _instances[key] = instance;
            }
        }

        await instance.StartAsync(ct);
        await instance.EnsureTasksHydratedAsync(_taskHydrator, ct);
        return instance;
    }

    public async Task<NpcRuntimeAgentHandle> GetOrCreatePrivateChatHandleAsync(
        NpcRuntimeDescriptor descriptor,
        NpcPack pack,
        string runtimeRoot,
        NpcRuntimeAgentBindingRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeRoot);
        request.Validate();

        var instance = await GetOrStartAsync(descriptor, runtimeRoot, ct);
        instance.Namespace.SeedPersonaPack(pack);

        var rebindKey = BuildRebindKey(
            request.ChannelKey,
            request.SystemPromptSupplement,
            request.IncludeMemory,
            request.IncludeUser,
            request.MaxToolIterations,
            request.ToolSurfaceSnapshotVersion,
            request.ToolSurface.Fingerprint);

        return instance.GetOrCreatePrivateChatHandle(
            rebindKey,
            generation => CreatePrivateChatHandle(instance, request, generation));
    }

    public async Task<NpcRuntimeAutonomyHandle> GetOrCreateAutonomyHandleAsync(
        NpcRuntimeDescriptor descriptor,
        NpcPack pack,
        string runtimeRoot,
        NpcRuntimeAutonomyBindingRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeRoot);
        request.Validate();

        var instance = await GetOrStartAsync(descriptor, runtimeRoot, ct);
        instance.Namespace.SeedPersonaPack(pack);

        var rebindKey = BuildRebindKey(
            request.ChannelKey,
            request.AdapterKey,
            request.SystemPromptSupplement,
            request.IncludeMemory,
            request.IncludeUser,
            request.MaxToolIterations,
            request.ToolSurfaceSnapshotVersion,
            request.ToolSurface.Fingerprint);

        return instance.GetOrCreateAutonomyHandle(
            rebindKey,
            generation => CreateAutonomyHandle(instance, request, generation));
    }

    public async Task<NpcRuntimeDriver> GetOrCreateDriverAsync(
        NpcRuntimeDescriptor descriptor,
        string runtimeRoot,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeRoot);

        var key = BuildKey(descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId);
        lock (_gate)
        {
            if (_drivers.TryGetValue(key, out var existing))
                return existing;
        }

        var instance = await GetOrStartAsync(descriptor, runtimeRoot, ct);
        var created = new NpcRuntimeDriver(instance, new NpcRuntimeStateStore(instance.Namespace.RuntimeStateDbPath));
        await created.InitializeAsync(ct);

        lock (_gate)
        {
            if (_drivers.TryGetValue(key, out var existing))
                return existing;

            _drivers[key] = created;
            return created;
        }
    }

    public async Task StopAsync(string gameId, string saveId, string npcId, string profileId, CancellationToken ct)
    {
        var key = BuildKey(gameId, saveId, npcId, profileId);
        NpcRuntimeInstance? instance;
        lock (_gate)
            instance = _instances.GetValueOrDefault(key);

        if (instance is not null)
            await instance.StopAsync(ct);
    }

    public bool Unregister(NpcRuntimeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var key = BuildKey(descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId);
        lock (_gate)
        {
            var removedInstance = _instances.Remove(key);
            var removedDriver = _drivers.Remove(key);
            return removedInstance || removedDriver;
        }
    }

    public IReadOnlyList<NpcRuntimeSnapshot> Snapshot()
    {
        lock (_gate)
            return _instances.Values.Select(instance => instance.Snapshot()).OrderBy(item => item.NpcId).ToArray();
    }

    public bool TryGetTaskView(string sessionId, out NpcRuntimeTaskView? taskView)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        lock (_gate)
        {
            foreach (var instance in _instances.Values)
            {
                if (!instance.TryGetTaskView(sessionId, out taskView))
                    continue;

                return true;
            }
        }

        taskView = null;
        return false;
    }

    private static NpcRuntimeAgentHandle CreatePrivateChatHandle(
        NpcRuntimeInstance instance,
        NpcRuntimeAgentBindingRequest request,
        int generation)
        => CreateAgentHandle(
            instance,
            request.ChannelKey,
            BuildRebindKey(
                request.ChannelKey,
                request.SystemPromptSupplement,
                request.IncludeMemory,
                request.IncludeUser,
                request.MaxToolIterations,
                request.ToolSurfaceSnapshotVersion,
                request.ToolSurface.Fingerprint),
            request.Services,
            request.SystemPromptSupplement,
            request.IncludeMemory,
            request.IncludeUser,
            request.MaxToolIterations,
            request.ToolSurface.Tools,
            generation,
            request.ToolSurface.Fingerprint);

    private static NpcRuntimeAutonomyHandle CreateAutonomyHandle(
        NpcRuntimeInstance instance,
        NpcRuntimeAutonomyBindingRequest request,
        int generation)
    {
        var adapter = request.AdapterFactory();
        var factStore = new NpcObservationFactStore();
        var gameTools = request.GameToolFactory(adapter, factStore).ToArray();
        var combinedTools = gameTools.Concat(request.ToolSurface.Tools).ToArray();
        var rebindKey = BuildRebindKey(
            request.ChannelKey,
            request.AdapterKey,
            request.SystemPromptSupplement,
            request.IncludeMemory,
            request.IncludeUser,
            request.MaxToolIterations,
            request.ToolSurfaceSnapshotVersion,
            request.ToolSurface.Fingerprint);
        var combinedToolSurface = NpcToolSurface.FromTools(combinedTools);
        var agentHandle = CreateAgentHandle(
            instance,
            request.ChannelKey,
            rebindKey,
            request.Services,
            request.SystemPromptSupplement,
            request.IncludeMemory,
            request.IncludeUser,
            request.MaxToolIterations,
            combinedToolSurface.Tools,
            generation,
            combinedToolSurface.Fingerprint);

        var loop = new NpcAutonomyLoop(
            adapter,
            factStore,
            agentHandle.Agent,
            new NpcRuntimeLogWriter(Path.Combine(instance.Namespace.ActivityPath, "runtime.jsonl")),
            agentHandle.Context.MemoryManager,
            request.Services.LoggerFactory.CreateLogger<NpcAutonomyLoop>());

        return new NpcRuntimeAutonomyHandle(
            instance,
            agentHandle,
            adapter,
            loop,
            request.ChannelKey,
            rebindKey,
            generation,
            combinedToolSurface.Fingerprint);
    }

    private static NpcRuntimeAgentHandle CreateAgentHandle(
        NpcRuntimeInstance instance,
        string channelKey,
        string rebindKey,
        NpcRuntimeCompositionServices services,
        string? systemPromptSupplement,
        bool includeMemory,
        bool includeUser,
        int maxToolIterations,
        IEnumerable<ITool> tools,
        int generation,
        string toolFingerprint)
    {
        var context = new NpcRuntimeContextFactory().Create(
            instance.Namespace,
            services.ChatClient,
            services.LoggerFactory,
            services.SkillManager,
            systemPromptSupplement,
            includeMemory: includeMemory,
            includeUser: includeUser,
            sharedTodoStore: instance.TodoStore);

        var agent = new NpcAgentFactory().Create(
            services.ChatClient,
            context,
            Enumerable.Empty<ITool>(),
            services.LoggerFactory,
            maxToolIterations: maxToolIterations);

        AgentCapabilityAssembler.RegisterAllTools(
            agent,
            new AgentCapabilityServices
            {
                ChatClient = services.ChatClient,
                ToolRegistry = context.ToolRegistry,
                TodoStore = context.TodoStore,
                CronScheduler = services.CronScheduler,
                MemoryManager = context.MemoryManager,
                PluginManager = context.PluginManager,
                TranscriptRecallService = context.TranscriptRecallService,
                SkillManager = services.SkillManager,
                CheckpointDirectory = Path.Combine(instance.Namespace.RuntimeRoot, "checkpoints"),
                MemoryAvailable = includeMemory || includeUser
            },
            tools);

        return new NpcRuntimeAgentHandle(
            instance,
            context,
            agent,
            channelKey,
            rebindKey,
            generation,
            toolFingerprint);
    }

    private static string BuildRebindKey(params object?[] parts)
        => string.Join("|", parts.Select(part => part?.ToString() ?? string.Empty));

    private static string BuildKey(string gameId, string saveId, string npcId, string profileId)
        => $"{gameId}:{saveId}:{npcId}:{profileId}";
}
