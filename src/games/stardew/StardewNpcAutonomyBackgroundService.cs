namespace Hermes.Agent.Games.Stardew;

using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.LLM;
using Hermes.Agent.Runtime;
using Hermes.Agent.Skills;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

public sealed record StardewNpcAutonomyBackgroundOptions(
    IReadOnlyCollection<string> EnabledNpcIds,
    TimeSpan PollInterval = default,
    TimeSpan AutonomyWakeInterval = default);

public sealed class StardewNpcAutonomyBackgroundService : IDisposable
{
    internal static TimeSpan DefaultPollInterval { get; } = TimeSpan.FromSeconds(1);

    internal static TimeSpan DefaultAutonomyWakeInterval { get; } = TimeSpan.FromSeconds(20);

    internal static TimeSpan PrivateChatSessionLeaseTtl { get; } = TimeSpan.FromMinutes(5);

    private readonly object _gate = new();
    private readonly IStardewBridgeDiscovery _discovery;
    private readonly Func<StardewBridgeDiscoverySnapshot, IGameAdapter> _adapterFactory;
    private readonly IChatClient _chatClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly SkillManager _skillManager;
    private readonly ICronScheduler _cronScheduler;
    private readonly NpcRuntimeSupervisor _runtimeSupervisor;
    private readonly NpcRuntimeHost _runtimeHost;
    private readonly StardewNpcRuntimeBindingResolver _bindingResolver;
    private readonly StardewNpcAutonomyPromptSupplementBuilder _promptSupplementBuilder;
    private readonly INpcToolSurfaceSnapshotProvider _toolSnapshotProvider;
    private readonly StardewPrivateChatRuntimeAdapter _privateChatRuntimeAdapter;
    private readonly NpcAutonomyBudget _budget;
    private readonly WorldCoordinationService _worldCoordination;
    private readonly ILogger<StardewNpcAutonomyBackgroundService> _logger;
    private readonly IChatClient? _delegationChatClient;
    private readonly string _runtimeRoot;
    private readonly HashSet<string> _enabledNpcIds;
    private readonly bool _includeMemory;
    private readonly bool _includeUser;
    private readonly Dictionary<string, NpcAutonomyTracker> _trackers = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _autonomyWakeInterval;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private string? _bridgeKey;
    private string? _attachedSaveId;
    private StardewRuntimeHostStateStore? _hostStateStore;
    private GameEventCursor _bridgeEventCursor = new(null);

    public StardewNpcAutonomyBackgroundService(
        IStardewBridgeDiscovery discovery,
        HttpClient httpClient,
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        SkillManager skillManager,
        ICronScheduler cronScheduler,
        NpcRuntimeSupervisor runtimeSupervisor,
        NpcRuntimeHost runtimeHost,
        StardewNpcRuntimeBindingResolver bindingResolver,
        StardewNpcAutonomyPromptSupplementBuilder promptSupplementBuilder,
        INpcToolSurfaceSnapshotProvider toolSnapshotProvider,
        StardewPrivateChatRuntimeAdapter privateChatRuntimeAdapter,
        NpcAutonomyBudget budget,
        WorldCoordinationService worldCoordination,
        ILogger<StardewNpcAutonomyBackgroundService> logger,
        StardewNpcAutonomyBackgroundOptions options,
        bool includeMemory,
        bool includeUser,
        string runtimeRoot,
        IChatClient? delegationChatClient = null)
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
            runtimeHost,
            bindingResolver,
            promptSupplementBuilder,
            toolSnapshotProvider,
            privateChatRuntimeAdapter,
            budget,
            worldCoordination,
            logger,
            options,
            includeMemory,
            includeUser,
            runtimeRoot,
            delegationChatClient)
    {
    }

    public StardewNpcAutonomyBackgroundService(
        IStardewBridgeDiscovery discovery,
        Func<StardewBridgeDiscoverySnapshot, IGameAdapter> adapterFactory,
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        SkillManager skillManager,
        ICronScheduler cronScheduler,
        NpcRuntimeSupervisor runtimeSupervisor,
        NpcRuntimeHost runtimeHost,
        StardewNpcRuntimeBindingResolver bindingResolver,
        StardewNpcAutonomyPromptSupplementBuilder promptSupplementBuilder,
        INpcToolSurfaceSnapshotProvider toolSnapshotProvider,
        StardewPrivateChatRuntimeAdapter privateChatRuntimeAdapter,
        NpcAutonomyBudget budget,
        WorldCoordinationService worldCoordination,
        ILogger<StardewNpcAutonomyBackgroundService> logger,
        StardewNpcAutonomyBackgroundOptions options,
        bool includeMemory,
        bool includeUser,
        string runtimeRoot,
        IChatClient? delegationChatClient = null)
    {
        _discovery = discovery;
        _adapterFactory = adapterFactory;
        _chatClient = chatClient;
        _loggerFactory = loggerFactory;
        _skillManager = skillManager;
        _cronScheduler = cronScheduler;
        _runtimeSupervisor = runtimeSupervisor;
        _runtimeHost = runtimeHost;
        _bindingResolver = bindingResolver;
        ArgumentNullException.ThrowIfNull(promptSupplementBuilder);
        _promptSupplementBuilder = promptSupplementBuilder;
        _toolSnapshotProvider = toolSnapshotProvider;
        _privateChatRuntimeAdapter = privateChatRuntimeAdapter;
        _budget = budget;
        _worldCoordination = worldCoordination;
        _logger = logger;
        _delegationChatClient = delegationChatClient;
        _runtimeRoot = runtimeRoot;
        _includeMemory = includeMemory;
        _includeUser = includeUser;
        _enabledNpcIds = new HashSet<string>(
            (options.EnabledNpcIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim()),
            StringComparer.OrdinalIgnoreCase);
        _pollInterval = options.PollInterval == default ? DefaultPollInterval : options.PollInterval;
        _autonomyWakeInterval = options.AutonomyWakeInterval == default ? DefaultAutonomyWakeInterval : options.AutonomyWakeInterval;
        _cronScheduler.TaskDue += OnCronTaskDue;
    }

    internal TimeSpan PollInterval => _pollInterval;

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
        Task? loopTask;
        lock (_gate)
        {
            cts = _cts;
            _cts = null;
            loopTask = _loopTask;
            _loopTask = null;
        }

        MarkTrackedInstancesPaused(NpcAutonomyExitReason.Stopped.ToString());
        ReleaseTrackedActionClaims();
        var workerTasks = StopTrackers();
        _privateChatRuntimeAdapter.Reset();
        _bridgeEventCursor = new GameEventCursor(null);

        try
        {
            cts?.Cancel();
            var tasksToWait = loopTask is null
                ? workerTasks
                : [.. workerTasks, loopTask];
            WaitForTasks(tasksToWait, "Stopping Stardew autonomy background service", TimeSpan.FromSeconds(2));
            cts?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stopping Stardew autonomy background service failed non-fatally");
        }
    }

    public void Dispose()
    {
        _cronScheduler.TaskDue -= OnCronTaskDue;
        Stop();
    }

    public Task RunOneIterationAsync(CancellationToken ct)
        => RunOneIterationCoreAsync(ct, waitForWorkerCompletion: true);

    internal Task DispatchOneIterationAsync(CancellationToken ct)
        => RunOneIterationCoreAsync(ct, waitForWorkerCompletion: false);

    private async Task RunOneIterationCoreAsync(CancellationToken ct, bool waitForWorkerCompletion)
    {
        var iterationStartedAtUtc = DateTime.UtcNow;
        var iterationStopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "Stardew autonomy host iteration started; enabledNpcCount={EnabledNpcCount}; waitForWorkers={WaitForWorkers}; pollIntervalMs={PollIntervalMs}; bridgeCursor={BridgeCursor}; bridgeSequence={BridgeSequence}",
            _enabledNpcIds.Count,
            waitForWorkerCompletion,
            (long)_pollInterval.TotalMilliseconds,
            _bridgeEventCursor.Since ?? "-",
            _bridgeEventCursor.Sequence);

        if (!_discovery.TryReadLatest(out var snapshot, out var failureReason) || snapshot is null)
        {
            _privateChatRuntimeAdapter.Reset();
            MarkTrackedInstancesPaused(failureReason ?? StardewBridgeErrorCodes.BridgeUnavailable);
            _logger.LogInformation(
                "Stardew autonomy host iteration skipped; reason=discovery_unavailable; failureReason={FailureReason}",
                failureReason ?? StardewBridgeErrorCodes.BridgeUnavailable);
            return;
        }

        if (!StardewBridgeRuntimeIdentity.TryGetSaveId(snapshot, out var saveId))
        {
            _privateChatRuntimeAdapter.Reset();
            MarkTrackedInstancesPaused(StardewBridgeErrorCodes.BridgeStaleDiscovery);
            _logger.LogInformation("Stardew autonomy host iteration skipped; reason=missing_save_id");
            return;
        }

        var bridgeKey = BuildBridgeKey(snapshot, saveId);
        var hostStateStore = await GetOrCreateHostStateStoreAsync(saveId, bridgeKey, ct);
        var hostState = await hostStateStore.LoadAsync(ct);
        _bridgeEventCursor = hostState.SourceCursor;

        if (!string.Equals(bridgeKey, _bridgeKey, StringComparison.Ordinal))
        {
            if (_enabledNpcIds.Count > 0)
                await _runtimeHost.StartDiscoveredAsync(_bindingResolver.PackRoot, saveId, _enabledNpcIds.ToArray(), ct);

            await ResetTrackersForBridgeAsync(bridgeKey, ct);
            _bridgeKey = bridgeKey;
            _logger.LogInformation("Stardew autonomy bridge attached: {BridgeKey}", bridgeKey);
        }

        var hostAdapter = _adapterFactory(snapshot);
        var hasStagedBatch = hostState.StagedBatch is not null;
        var privateChatDrainOnly = hasStagedBatch
            ? hostState.StagedBatch!.PrivateChatDrainOnly
            : !hostState.InitialPrivateChatHistoryDrained && IsInitialCursor(hostState.SourceCursor);
        var initialPrivateChatHistoryDrained = hostState.InitialPrivateChatHistoryDrained || privateChatDrainOnly;
        var sharedEventBatch = hasStagedBatch
            ? hostState.StagedBatch!.ToBatch()
            : await hostAdapter.Events.PollBatchAsync(hostState.SourceCursor, ct);
        var shouldStageBatch = hasStagedBatch || ShouldStageBatch(hostState.SourceCursor, sharedEventBatch);
        _logger.LogInformation(
            "Stardew autonomy host batch ready; saveId={SaveId}; hasStagedBatch={HasStagedBatch}; eventCount={EventCount}; sourceCursor={SourceCursor}; sourceSequence={SourceSequence}; nextCursor={NextCursor}; nextSequence={NextSequence}; privateChatDrainOnly={PrivateChatDrainOnly}",
            saveId,
            hasStagedBatch,
            sharedEventBatch.Records.Count,
            hostState.SourceCursor.Since ?? "-",
            hostState.SourceCursor.Sequence,
            sharedEventBatch.NextCursor.Since ?? "-",
            sharedEventBatch.NextCursor.Sequence,
            privateChatDrainOnly);
        if (!hasStagedBatch && shouldStageBatch)
        {
            await hostStateStore.StageBatchAsync(
                hostState.SourceCursor,
                sharedEventBatch,
                privateChatDrainOnly,
                initialPrivateChatHistoryDrained,
                ct);
        }

        await _privateChatRuntimeAdapter.ProcessAsync(
            bridgeKey,
            saveId,
            hostAdapter,
            sharedEventBatch.Records,
            ct,
            drainOnly: privateChatDrainOnly);

        // enabledNpcIds gates autonomous NPC workers only. Shared private-chat ingress
        // must still be consumed so UI opening is not coupled to AI/autonomy config.
        if (_enabledNpcIds.Count == 0)
        {
            await FinalizeSharedEventBatchAsync(
                hostStateStore,
                hostState,
                sharedEventBatch,
                shouldStageBatch,
                initialPrivateChatHistoryDrained,
                ct);
            _bridgeEventCursor = sharedEventBatch.NextCursor;
            iterationStopwatch.Stop();
            _logger.LogInformation(
                "Stardew autonomy host iteration skipped; reason=no_enabled_npcs_after_private_chat_processing; startedAtUtc={StartedAtUtc:o}; durationMs={DurationMs}; nextCursor={NextCursor}; nextSequence={NextSequence}",
                iterationStartedAtUtc,
                iterationStopwatch.ElapsedMilliseconds,
                _bridgeEventCursor.Since ?? "-",
                _bridgeEventCursor.Sequence);
            return;
        }

        List<Task>? workerTasks = waitForWorkerCompletion ? new List<Task>(_enabledNpcIds.Count) : null;
        foreach (var npcId in _enabledNpcIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
        {
            var binding = _bindingResolver.Resolve(npcId, saveId);
            var tracker = await GetOrCreateTrackerAsync(binding, ct);
            var controller = tracker.Driver.Snapshot();
            var npcEventBatch = FilterBatchForRuntime(binding.Descriptor, sharedEventBatch, controller.EventCursor);
            tracker.Instance.SetInboxDepth(npcEventBatch.Records.Count + controller.IngressWorkItems.Count);
            _logger.LogInformation(
                "Stardew autonomy host dispatching NPC; npc={NpcId}; npcEventCount={NpcEventCount}; ingressCount={IngressCount}; trackerCursor={TrackerCursor}; trackerSequence={TrackerSequence}; nextWakeAtUtc={NextWakeAtUtc}; pendingWorkType={PendingWorkType}; pendingCommandId={PendingCommandId}; actionCommandId={ActionCommandId}",
                binding.Descriptor.NpcId,
                npcEventBatch.Records.Count,
                controller.IngressWorkItems.Count,
                controller.EventCursor.Since ?? "-",
                controller.EventCursor.Sequence,
                controller.NextWakeAtUtc,
                controller.PendingWorkItem?.WorkType ?? "-",
                controller.PendingWorkItem?.CommandId ?? "-",
                controller.ActionSlot?.CommandId ?? "-");
            var workerTask = await tracker.EnqueueAsync(
                new NpcAutonomyDispatch(
                    bridgeKey,
                    snapshot,
                    npcEventBatch,
                    sharedEventBatch.NextCursor,
                    HasPrivateChatIngress(sharedEventBatch),
                    HasHostProgress(hostState.SourceCursor, sharedEventBatch)),
                ct);
            workerTasks?.Add(workerTask);
        }

        if (workerTasks is not null)
            await Task.WhenAll(workerTasks);

        await FinalizeSharedEventBatchAsync(
            hostStateStore,
            hostState,
            sharedEventBatch,
            shouldStageBatch,
            initialPrivateChatHistoryDrained,
            ct);

        _bridgeEventCursor = sharedEventBatch.NextCursor;
        iterationStopwatch.Stop();
        _logger.LogInformation(
            "Stardew autonomy host iteration completed; startedAtUtc={StartedAtUtc:o}; durationMs={DurationMs}; nextCursor={NextCursor}; nextSequence={NextSequence}",
            iterationStartedAtUtc,
            iterationStopwatch.ElapsedMilliseconds,
            _bridgeEventCursor.Since ?? "-",
            _bridgeEventCursor.Sequence);
    }

    private static Task FinalizeSharedEventBatchAsync(
        StardewRuntimeHostStateStore hostStateStore,
        StardewRuntimeHostState hostState,
        GameEventBatch sharedEventBatch,
        bool shouldStageBatch,
        bool initialPrivateChatHistoryDrained,
        CancellationToken ct)
    {
        if (shouldStageBatch)
            return hostStateStore.CommitBatchAsync(sharedEventBatch.NextCursor, initialPrivateChatHistoryDrained, ct);

        return initialPrivateChatHistoryDrained != hostState.InitialPrivateChatHistoryDrained
            ? hostStateStore.CommitBatchAsync(hostState.SourceCursor, initialPrivateChatHistoryDrained, ct)
            : Task.CompletedTask;
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await DispatchOneIterationAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Stardew autonomy background iteration failed non-fatally");
            }

            await Task.Delay(_pollInterval, ct);
        }
    }

    private void OnCronTaskDue(object? sender, CronTaskDueEventArgs e)
        => _ = HandleCronTaskDueAsync(e.Task, e.FiredAt, CancellationToken.None);

    internal async Task HandleCronTaskDueAsync(CronTask task, DateTimeOffset firedAtUtc, CancellationToken ct)
    {
        if (_enabledNpcIds.Count == 0 || string.IsNullOrWhiteSpace(task.SessionId))
            return;

        if (!_discovery.TryReadLatest(out var snapshot, out var failureReason) || snapshot is null)
        {
            _logger.LogWarning(
                "Ignoring due cron task {TaskId} because Stardew discovery is unavailable: {Reason}",
                task.Id,
                failureReason ?? StardewBridgeErrorCodes.BridgeUnavailable);
            return;
        }

        if (!StardewBridgeRuntimeIdentity.TryGetSaveId(snapshot, out var saveId))
        {
            _logger.LogWarning("Ignoring due cron task {TaskId} because Stardew save identity is unavailable.", task.Id);
            return;
        }

        foreach (var npcId in _enabledNpcIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
        {
            var binding = _bindingResolver.Resolve(npcId, saveId);
            if (!MatchesNpcSession(task.SessionId, binding.Descriptor.SessionId))
                continue;

            await AppendScheduledPrivateChatIngressAsync(binding, task, firedAtUtc, ct);
            return;
        }

        _logger.LogInformation(
            "Ignoring due cron task {TaskId} because session {SessionId} does not belong to an enabled Stardew NPC runtime.",
            task.Id,
            task.SessionId);
    }

    private async Task ProcessTrackerDispatchAsync(
        NpcAutonomyTracker tracker,
        NpcAutonomyDispatch dispatch,
        CancellationToken ct)
    {
        var binding = tracker.Binding;
        var hostAdapter = _adapterFactory(dispatch.Snapshot);
        var deliveredCursor = dispatch.DeliveredCursor;
        var dispatchStartedAtUtc = DateTime.UtcNow;
        var dispatchStopwatch = Stopwatch.StartNew();
        try
        {
            var controller = tracker.Driver.Snapshot();
            _logger.LogInformation(
                "Stardew autonomy NPC dispatch started; npc={NpcId}; sharedEventCount={SharedEventCount}; deliveredCursor={DeliveredCursor}; deliveredSequence={DeliveredSequence}; eventCursor={EventCursor}; eventSequence={EventSequence}; nextWakeAtUtc={NextWakeAtUtc}; pendingWorkType={PendingWorkType}; pendingStatus={PendingStatus}; pendingCommandId={PendingCommandId}; actionCommandId={ActionCommandId}; ingressCount={IngressCount}",
                binding.Descriptor.NpcId,
                dispatch.SharedEventBatch.Records.Count,
                deliveredCursor.Since ?? "-",
                deliveredCursor.Sequence,
                controller.EventCursor.Since ?? "-",
                controller.EventCursor.Sequence,
                controller.NextWakeAtUtc,
                controller.PendingWorkItem?.WorkType ?? "-",
                controller.PendingWorkItem?.Status ?? "-",
                controller.PendingWorkItem?.CommandId ?? "-",
                controller.ActionSlot?.CommandId ?? "-",
                controller.IngressWorkItems.Count);

            if (await TryAdvancePendingActionAsync(binding, tracker, hostAdapter.Commands, deliveredCursor, dispatch.BridgeKey, ct))
                return;

            if (await TryProcessIngressWorkAsync(binding, tracker, hostAdapter, dispatch, deliveredCursor, ct))
                return;

            if (tracker.Instance.TryGetActivePrivateChatSessionLease(out var activeLease) && activeLease is not null)
            {
                if (DateTime.UtcNow - activeLease.AcquiredAtUtc >= PrivateChatSessionLeaseTtl &&
                    tracker.Instance.TryReleasePrivateChatSessionLease(activeLease))
                {
                    await tracker.Driver.SyncAsync(ct);
                    await WritePrivateChatLeaseDiagnosticAsync(binding, tracker, activeLease, "released", "stale_private_chat_session_lease", ct);
                    activeLease = null;
                }
            }

            if (activeLease is not null)
            {
                await PauseTrackerAsync(tracker, deliveredCursor, activeLease.Reason, dispatch.BridgeKey, null, ct);
                return;
            }

            if (!dispatch.HasPrivateChatIngress &&
                !dispatch.HasHostProgress &&
                !dispatch.WasQueuedBehindActiveWorker &&
                controller.NextWakeAtUtc.HasValue &&
                DateTime.UtcNow < controller.NextWakeAtUtc.Value)
            {
                await PauseTrackerAsync(tracker, deliveredCursor, "restart_cooldown", dispatch.BridgeKey, controller.NextWakeAtUtc, ct);
                return;
            }

            var systemPromptSupplement = _promptSupplementBuilder.Build(
                binding.Descriptor,
                tracker.Instance.Namespace,
                binding.Pack);
            _logger.LogInformation(
                "Stardew autonomy prompt contract built; npc={NpcId}; channel=autonomy; supplementChars={SupplementChars}; supplementHasMandatorySkills={SupplementHasMandatorySkills}",
                binding.Descriptor.NpcId,
                systemPromptSupplement.Length,
                systemPromptSupplement.Contains("## Skills (mandatory)", StringComparison.Ordinal));
            await using var llmSlot = await _budget.TryAcquireLlmSlotAsync(binding.Descriptor.NpcId, ct);
            if (llmSlot is null)
            {
                await PauseTrackerAsync(tracker, deliveredCursor, NpcAutonomyExitReason.LlmConcurrencyLimit.ToString(), dispatch.BridgeKey, null, ct);
                return;
            }
            _logger.LogInformation(
                "Stardew autonomy LLM slot acquired; npc={NpcId}; maxConcurrentLlmRequests={MaxConcurrentLlmRequests}",
                binding.Descriptor.NpcId,
                _budget.Options.MaxConcurrentLlmRequests);

            var toolSnapshot = _toolSnapshotProvider.Capture();
            var handle = await _runtimeSupervisor.GetOrCreateAutonomyHandleAsync(
                binding.Descriptor,
                binding.Pack,
                _runtimeRoot,
                new NpcRuntimeAutonomyBindingRequest(
                    ChannelKey: "autonomy",
                    AdapterKey: dispatch.BridgeKey,
                    IncludeMemory: _includeMemory,
                    IncludeUser: _includeUser,
                    MaxToolIterations: _budget.Options.MaxToolIterations,
                    AdapterFactory: () => _adapterFactory(dispatch.Snapshot),
                    GameToolFactory: (adapter, factStore) => StardewNpcToolFactory.CreateDefault(
                        adapter,
                        binding.Descriptor,
                        runtimeDriver: tracker.Driver,
                        worldCoordination: _worldCoordination,
                        recentActivityProvider: new StardewRecentActivityProvider(factStore, tracker.Driver),
                        logger: _logger),
                    LocalExecutorGameToolFactory: (adapter, factStore) => StardewNpcToolFactory.CreateLocalExecutorTools(
                        adapter,
                        binding.Descriptor,
                        runtimeDriver: tracker.Driver,
                        worldCoordination: _worldCoordination,
                        logger: _logger),
                    LocalExecutorRuntimeToolFactory: services => [new SkillViewTool(services.SkillManager)],
                    Services: new NpcRuntimeCompositionServices(
                        _chatClient,
                        _loggerFactory,
                        _skillManager,
                        _cronScheduler,
                        _delegationChatClient),
                    ToolSurface: toolSnapshot.ToolSurface,
                    ToolSurfaceSnapshotVersion: toolSnapshot.SnapshotVersion,
                    SystemPromptSupplement: systemPromptSupplement,
                    LocalExecutorToolFingerprint: StardewNpcToolFactory.LocalExecutorToolFingerprint(includeSkillView: true)),
                ct);

            _logger.LogInformation(
                "Stardew autonomy wake prepared; npc={NpcId}; mode=autonomy; noFactsInjected=true",
                binding.Descriptor.NpcId);
            using var llmTurnCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            llmTurnCts.CancelAfter(_budget.Options.EffectiveLlmTurnTimeout);
            NpcAutonomyTickResult tick;
            try
            {
                var llmStartedAtUtc = DateTime.UtcNow;
                var llmStopwatch = Stopwatch.StartNew();
                _logger.LogInformation(
                    "Stardew autonomy LLM turn started; npc={NpcId}; timeoutSeconds={TimeoutSeconds}; maxToolIterations={MaxToolIterations}; eventCount={EventCount}; toolSurfaceVersion={ToolSurfaceVersion}",
                    binding.Descriptor.NpcId,
                    _budget.Options.EffectiveLlmTurnTimeout.TotalSeconds,
                    _budget.Options.MaxToolIterations,
                    dispatch.SharedEventBatch.Records.Count,
                    toolSnapshot.SnapshotVersion);
                tick = await handle.Loop.RunOneTickAsync(handle.Instance, dispatch.SharedEventBatch, llmTurnCts.Token);
                llmStopwatch.Stop();
                _logger.LogInformation(
                    "Stardew autonomy LLM turn completed; npc={NpcId}; startedAtUtc={StartedAtUtc:o}; durationMs={DurationMs}; nextCursor={NextCursor}; nextSequence={NextSequence}",
                    binding.Descriptor.NpcId,
                    llmStartedAtUtc,
                    llmStopwatch.ElapsedMilliseconds,
                    tick.NextEventCursor?.Since ?? "-",
                    tick.NextEventCursor?.Sequence);
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested && llmTurnCts.IsCancellationRequested)
            {
                tracker.RestartCount++;
                tracker.Instance.RecordAutonomyRestart(dispatch.BridgeKey, tracker.Instance.Snapshot().CurrentAutonomyHandleGeneration);
                tracker.Instance.RecordError(NpcAutonomyExitReason.LlmTurnTimeout.ToString());

                if (_budget.CheckRestartLimit(tracker.RestartCount) is NpcAutonomyExitReason.MaxRestarts)
                {
                    await PauseTrackerAsync(tracker, deliveredCursor, NpcAutonomyExitReason.MaxRestarts.ToString(), dispatch.BridgeKey, null, ct);
                    _logger.LogWarning(ex, "Stardew autonomy LLM turn timed out and hit restart limit for {NpcId}", binding.Descriptor.NpcId);
                    return;
                }

                var nextWakeAtUtc = DateTime.UtcNow + _budget.Options.EffectiveRestartCooldown;
                await PauseTrackerAsync(tracker, deliveredCursor, NpcAutonomyExitReason.LlmTurnTimeout.ToString(), dispatch.BridgeKey, nextWakeAtUtc, ct);
                _logger.LogWarning(
                    ex,
                    "Stardew autonomy LLM turn timed out for {NpcId}; will retry after cooldown",
                    binding.Descriptor.NpcId);
                return;
            }

            await tracker.Driver.SetControllerStateAsync(tick.NextEventCursor ?? deliveredCursor, null, ct);
            tracker.RestartCount = 0;
            handle.Instance.MarkAutonomyRunning(dispatch.BridgeKey, handle.RebindGeneration, DateTime.UtcNow);
            await tracker.Driver.SetNextWakeAtUtcAsync(DateTime.UtcNow + _autonomyWakeInterval, ct);
            dispatchStopwatch.Stop();
            _logger.LogInformation(
                "Stardew autonomy NPC dispatch completed; npc={NpcId}; startedAtUtc={StartedAtUtc:o}; durationMs={DurationMs}; result=llm_turn_completed",
                binding.Descriptor.NpcId,
                dispatchStartedAtUtc,
                dispatchStopwatch.ElapsedMilliseconds);
        }
        catch (StardewNpcAutonomyPromptResourceException ex)
        {
            tracker.RestartCount = 0;
            await tracker.Instance.PauseAsync(ex.Message, ct);
            await PauseTrackerAsync(tracker, deliveredCursor, ex.Message, dispatch.BridgeKey, null, ct);
            _logger.LogWarning(ex, "Stardew autonomy prompt resources are incomplete for {NpcId}", binding.Descriptor.NpcId);
        }
        catch (Exception ex)
        {
            tracker.RestartCount++;
            tracker.Instance.RecordAutonomyRestart(dispatch.BridgeKey, tracker.Instance.Snapshot().CurrentAutonomyHandleGeneration);
            tracker.Instance.RecordError(ex.Message);

            if (_budget.CheckRestartLimit(tracker.RestartCount) is NpcAutonomyExitReason.MaxRestarts)
            {
                await PauseTrackerAsync(tracker, deliveredCursor, NpcAutonomyExitReason.MaxRestarts.ToString(), dispatch.BridgeKey, null, ct);
                _logger.LogWarning(ex, "Stardew autonomy loop hit restart limit for {NpcId}", binding.Descriptor.NpcId);
                return;
            }

            var nextWakeAtUtc = DateTime.UtcNow + _budget.Options.EffectiveRestartCooldown;
            await PauseTrackerAsync(tracker, deliveredCursor, "restart_cooldown", dispatch.BridgeKey, nextWakeAtUtc, ct);
            _logger.LogWarning(ex, "Stardew autonomy loop failed for {NpcId}; will retry", binding.Descriptor.NpcId);
        }
    }

    private async Task<NpcAutonomyTracker> GetOrCreateTrackerAsync(StardewNpcRuntimeBinding binding, CancellationToken ct)
    {
        if (_trackers.TryGetValue(binding.Descriptor.NpcId, out var existing))
            return existing;

        var driver = await _runtimeSupervisor.GetOrCreateDriverAsync(binding.Descriptor, _runtimeRoot, ct);
        driver.Instance.Namespace.SeedPersonaPack(binding.Pack);
        var tracker = new NpcAutonomyTracker(binding, driver.Instance, driver, ProcessTrackerDispatchAsync);
        _trackers[binding.Descriptor.NpcId] = tracker;
        _logger.LogInformation(
            "Stardew autonomy tracker created; npc={NpcId}; sessionId={SessionId}; saveId={SaveId}",
            binding.Descriptor.NpcId,
            binding.Descriptor.SessionId,
            binding.Descriptor.SaveId);
        return tracker;
    }

    private Task ResetTrackersForBridgeAsync(string bridgeKey, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _privateChatRuntimeAdapter.Reset();
        foreach (var tracker in _trackers.Values)
        {
            tracker.RestartCount = 0;
            tracker.Instance.MarkAutonomyPaused("bridge_rebind", bridgeKey, tracker.Instance.Snapshot().CurrentAutonomyHandleGeneration);
        }

        return Task.CompletedTask;
    }

    private static string TruncateForLog(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private void MarkTrackedInstancesPaused(string reason)
    {
        foreach (var tracker in _trackers.Values)
            tracker.Instance.MarkAutonomyPaused(reason, _bridgeKey, tracker.Instance.Snapshot().CurrentAutonomyHandleGeneration);
    }

    private async Task PauseTrackerAsync(
        NpcAutonomyTracker tracker,
        GameEventCursor deliveredCursor,
        string reason,
        string? bridgeKey,
        DateTime? nextWakeAtUtc,
        CancellationToken ct)
    {
        tracker.Instance.MarkAutonomyPaused(reason, bridgeKey, tracker.Instance.Snapshot().CurrentAutonomyHandleGeneration);
        await tracker.Driver.SetControllerStateAsync(deliveredCursor, nextWakeAtUtc, ct);
        var snapshot = tracker.Driver.Snapshot();
        _logger.LogInformation(
            "Stardew autonomy NPC dispatch skipped; npc={NpcId}; reason={Reason}; nextWakeAtUtc={NextWakeAtUtc}; deliveredCursor={DeliveredCursor}; deliveredSequence={DeliveredSequence}; pendingWorkType={PendingWorkType}; pendingStatus={PendingStatus}; pendingCommandId={PendingCommandId}; actionCommandId={ActionCommandId}; ingressCount={IngressCount}",
            tracker.Binding.Descriptor.NpcId,
            reason,
            nextWakeAtUtc,
            deliveredCursor.Since ?? "-",
            deliveredCursor.Sequence,
            snapshot.PendingWorkItem?.WorkType ?? "-",
            snapshot.PendingWorkItem?.Status ?? "-",
            snapshot.PendingWorkItem?.CommandId ?? "-",
            snapshot.ActionSlot?.CommandId ?? "-",
            snapshot.IngressWorkItems.Count);
    }

    private static string BuildBridgeKey(StardewBridgeDiscoverySnapshot snapshot, string saveId)
        => $"{snapshot.Options.Host}:{snapshot.Options.Port}:{snapshot.StartedAtUtc:O}:{saveId}";

    private async Task<StardewRuntimeHostStateStore> GetOrCreateHostStateStoreAsync(string saveId, string bridgeKey, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saveId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bridgeKey);
        ct.ThrowIfCancellationRequested();

        if (_hostStateStore is not null &&
            string.Equals(_attachedSaveId, saveId, StringComparison.OrdinalIgnoreCase))
        {
            var currentState = await _hostStateStore.LoadAsync(ct);
            if (!string.Equals(currentState.BridgeKey, bridgeKey, StringComparison.Ordinal))
            {
                await _hostStateStore.ResetForBridgeAsync(bridgeKey, currentState.InitialPrivateChatHistoryDrained, ct);
                _bridgeEventCursor = new GameEventCursor(null);
            }

            return _hostStateStore;
        }

        if (_trackers.Count > 0)
            PauseAndClearTrackersForSaveChange(saveId);

        ReleaseTrackedActionClaims();
        _privateChatRuntimeAdapter.Reset();
        _bridgeKey = null;
        _attachedSaveId = saveId;
        _hostStateStore = new StardewRuntimeHostStateStore(BuildHostStateDbPath(saveId));
        var hostState = await _hostStateStore.LoadAsync(ct);
        if (string.IsNullOrWhiteSpace(hostState.BridgeKey))
        {
            await _hostStateStore.SetBridgeKeyAsync(bridgeKey, ct);
            hostState = await _hostStateStore.LoadAsync(ct);
        }
        else if (!string.Equals(hostState.BridgeKey, bridgeKey, StringComparison.Ordinal))
        {
            await _hostStateStore.ResetForBridgeAsync(bridgeKey, hostState.InitialPrivateChatHistoryDrained, ct);
            hostState = await _hostStateStore.LoadAsync(ct);
        }

        _bridgeEventCursor = hostState.SourceCursor;
        return _hostStateStore;
    }

    private void PauseAndClearTrackersForSaveChange(string saveId)
    {
        var workerTasks = _trackers.Values.Select(tracker => tracker.WorkerTask).ToArray();
        foreach (var tracker in _trackers.Values)
        {
            ReleaseTrackedActionClaim(tracker);
            tracker.Stop();
            tracker.Instance.MarkAutonomyPaused($"save_change:{saveId}", _bridgeKey, tracker.Instance.Snapshot().CurrentAutonomyHandleGeneration);
        }

        WaitForTasks(workerTasks, $"Switching Stardew autonomy tracker scope to save '{saveId}'", TimeSpan.FromSeconds(2));

        foreach (var tracker in _trackers.Values)
            _runtimeSupervisor.Unregister(tracker.Instance.Descriptor);

        _trackers.Clear();
    }

    private Task[] StopTrackers()
    {
        var workerTasks = _trackers.Values.Select(tracker => tracker.WorkerTask).ToArray();
        foreach (var tracker in _trackers.Values)
            tracker.Stop();

        _trackers.Clear();
        return workerTasks;
    }

    private void WaitForTasks(IEnumerable<Task> tasks, string operation, TimeSpan timeout)
    {
        var taskArray = tasks.ToArray();
        if (taskArray.Length == 0)
            return;

        try
        {
            if (!Task.WaitAll(taskArray, timeout))
                _logger.LogWarning("{Operation} timed out while waiting for NPC runtime workers to stop", operation);
        }
        catch (AggregateException ex)
        {
            _logger.LogWarning(ex.Flatten(), "{Operation} observed non-fatal worker shutdown errors", operation);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Operation} observed non-fatal worker shutdown errors", operation);
        }
    }

    private void ReleaseTrackedActionClaims()
    {
        foreach (var tracker in _trackers.Values)
            ReleaseTrackedActionClaim(tracker);
    }

    private void ReleaseTrackedActionClaim(NpcAutonomyTracker tracker)
    {
        var snapshot = tracker.Driver.Snapshot();
        var claimId = snapshot.ActionSlot?.CommandId
            ?? snapshot.PendingWorkItem?.CommandId
            ?? snapshot.ActionSlot?.WorkItemId
            ?? snapshot.PendingWorkItem?.WorkItemId;
        if (!string.IsNullOrWhiteSpace(claimId))
            _worldCoordination.ReleaseClaim(claimId);
    }

    private async Task<bool> TryAdvancePendingActionAsync(
        StardewNpcRuntimeBinding binding,
        NpcAutonomyTracker tracker,
        IGameCommandService commandService,
        GameEventCursor deliveredCursor,
        string bridgeKey,
        CancellationToken ct)
    {
        var controller = tracker.Driver.Snapshot();
        if (controller.PendingWorkItem is null && controller.ActionSlot is null)
            return false;

        var runtimeActions = new StardewRuntimeActionController(tracker.Driver, _worldCoordination, null, null);

        var commandId = controller.PendingWorkItem?.CommandId ?? controller.ActionSlot?.CommandId;
        if (string.IsNullOrWhiteSpace(commandId))
        {
            var lookupStatus = !string.IsNullOrWhiteSpace(controller.PendingWorkItem?.IdempotencyKey)
                ? await commandService.TryGetByIdempotencyKeyAsync(controller.PendingWorkItem.IdempotencyKey, ct)
                : null;
            if (lookupStatus is not null)
            {
                var traceId = controller.ActionSlot?.TraceId ?? lookupStatus.CommandId;
                await runtimeActions.RecordStatusAsync(lookupStatus, ct);
                await WriteCommandTerminalEvidenceAsync(binding, tracker, lookupStatus, traceId, ct);

                if (StardewRuntimeActionController.IsInFlightStatus(lookupStatus.Status))
                {
                    await PauseTrackerAsync(tracker, deliveredCursor, $"command_{lookupStatus.Status}", bridgeKey, lookupStatus.RetryAfterUtc, ct);
                    return true;
                }

                if (StardewRuntimeActionController.IsTerminalStatus(lookupStatus.Status))
                    return true;
            }

            if (controller.ActionSlot?.TimeoutAtUtc is { } timeoutAtUtcWithoutCommand && DateTime.UtcNow >= timeoutAtUtcWithoutCommand)
            {
                var cancelledStatus = new GameCommandStatus(
                    controller.ActionSlot.WorkItemId,
                    binding.Descriptor.NpcId,
                    controller.PendingWorkItem?.WorkType ?? "action",
                    StardewCommandStatuses.Cancelled,
                    0,
                    StardewBridgeErrorCodes.ActionSlotTimeout,
                    StardewBridgeErrorCodes.ActionSlotTimeout,
                    UpdatedAtUtc: DateTime.UtcNow);

                cancelledStatus = NormalizeActionSlotTimeoutStatus(binding, controller, cancelledStatus);
                await runtimeActions.RecordStatusAsync(cancelledStatus, ct);
                await WriteActionSlotTimeoutEvidenceAsync(binding, tracker, cancelledStatus, controller.ActionSlot.TraceId, ct);
                await PauseTrackerAsync(
                    tracker,
                    deliveredCursor,
                    StardewBridgeErrorCodes.ActionSlotTimeout,
                    bridgeKey,
                    null,
                    ct);
                return true;
            }

            await PauseTrackerAsync(
                tracker,
                deliveredCursor,
                "command_submitting",
                bridgeKey,
                controller.ActionSlot?.TimeoutAtUtc ?? DateTime.UtcNow + _budget.Options.EffectiveRestartCooldown,
                ct);
            return true;
        }

        var status = await commandService.GetStatusAsync(commandId, ct);
        var statusTraceId = controller.ActionSlot?.TraceId ?? status.CommandId;
        await runtimeActions.RecordStatusAsync(status, ct);
        await WriteCommandTerminalEvidenceAsync(binding, tracker, status, statusTraceId, ct);

        if (!StardewRuntimeActionController.IsTerminalStatus(status.Status) &&
            controller.ActionSlot?.TimeoutAtUtc is { } timeoutAtUtc &&
            DateTime.UtcNow >= timeoutAtUtc)
        {
            var cancelledStatus = await commandService.CancelAsync(commandId, StardewBridgeErrorCodes.ActionSlotTimeout, ct);
            cancelledStatus = NormalizeActionSlotTimeoutStatus(binding, controller, cancelledStatus);
            await runtimeActions.RecordStatusAsync(cancelledStatus, ct);
            await WriteActionSlotTimeoutEvidenceAsync(binding, tracker, cancelledStatus, controller.ActionSlot.TraceId, ct);
            await PauseTrackerAsync(
                tracker,
                deliveredCursor,
                StardewBridgeErrorCodes.ActionSlotTimeout,
                bridgeKey,
                null,
                ct);
            return true;
        }

        if (StardewRuntimeActionController.IsInFlightStatus(status.Status))
        {
            await PauseTrackerAsync(tracker, deliveredCursor, $"command_{status.Status}", bridgeKey, status.RetryAfterUtc, ct);
            return true;
        }

        if (StardewRuntimeActionController.IsTerminalStatus(status.Status))
            return true;

        return true;
    }

    private static GameCommandStatus NormalizeActionSlotTimeoutStatus(
        StardewNpcRuntimeBinding binding,
        NpcRuntimeControllerSnapshot controller,
        GameCommandStatus status)
    {
        var commandId = string.IsNullOrWhiteSpace(status.CommandId)
            ? controller.ActionSlot?.CommandId ?? controller.ActionSlot?.WorkItemId ?? controller.PendingWorkItem?.CommandId ?? controller.PendingWorkItem?.WorkItemId ?? "action_slot"
            : status.CommandId;
        var action = string.IsNullOrWhiteSpace(status.Action)
            ? controller.PendingWorkItem?.WorkType ?? "action"
            : status.Action;
        return status with
        {
            CommandId = commandId,
            NpcId = string.IsNullOrWhiteSpace(status.NpcId) ? binding.Descriptor.NpcId : status.NpcId,
            Action = action,
            Status = StardewCommandStatuses.Cancelled,
            BlockedReason = StardewBridgeErrorCodes.ActionSlotTimeout,
            ErrorCode = StardewBridgeErrorCodes.ActionSlotTimeout,
            UpdatedAtUtc = DateTime.UtcNow,
            RetryAfterUtc = null
        };
    }

    private static async Task WriteActionSlotTimeoutEvidenceAsync(
        StardewNpcRuntimeBinding binding,
        NpcAutonomyTracker tracker,
        GameCommandStatus status,
        string? traceId,
        CancellationToken ct)
    {
        var writer = new NpcRuntimeLogWriter(Path.Combine(tracker.Instance.Namespace.ActivityPath, "runtime.jsonl"));
        await writer.WriteAsync(new NpcRuntimeLogRecord(
            DateTime.UtcNow,
            string.IsNullOrWhiteSpace(traceId) ? status.CommandId : traceId,
            binding.Descriptor.NpcId,
            binding.Descriptor.GameId,
            binding.Descriptor.SessionId,
            "task_continuity",
            "action_slot_timeout",
            "terminal",
            status.Status,
            CommandId: status.CommandId,
            Error: status.ErrorCode ?? status.BlockedReason), ct);
    }

    private static async Task WriteCommandTerminalEvidenceAsync(
        StardewNpcRuntimeBinding binding,
        NpcAutonomyTracker tracker,
        GameCommandStatus status,
        string? traceId,
        CancellationToken ct)
    {
        if (!StardewRuntimeActionController.IsTerminalStatus(status.Status))
            return;

        var writer = new NpcRuntimeLogWriter(Path.Combine(tracker.Instance.Namespace.ActivityPath, "runtime.jsonl"));
        await writer.WriteAsync(new NpcRuntimeLogRecord(
            DateTime.UtcNow,
            string.IsNullOrWhiteSpace(traceId) ? status.CommandId : traceId,
            binding.Descriptor.NpcId,
            binding.Descriptor.GameId,
            binding.Descriptor.SessionId,
            "task_continuity",
            "command_terminal",
            "terminal",
            status.Status,
            CommandId: status.CommandId,
            Error: status.ErrorCode ?? status.BlockedReason), ct);
    }

    private static Task WritePrivateChatLeaseDiagnosticAsync(
        StardewNpcRuntimeBinding binding,
        NpcAutonomyTracker tracker,
        NpcRuntimeSessionLeaseSnapshot lease,
        string stage,
        string result,
        CancellationToken ct)
    {
        var writer = new NpcRuntimeLogWriter(Path.Combine(tracker.Instance.Namespace.ActivityPath, "runtime.jsonl"));
        return writer.WriteAsync(new NpcRuntimeLogRecord(
            DateTime.UtcNow,
            $"private_chat_lease_{lease.ConversationId}_{lease.Generation}",
            binding.Descriptor.NpcId,
            binding.Descriptor.GameId,
            binding.Descriptor.SessionId,
            "diagnostic",
            "private_chat_session_lease",
            stage,
            result,
            Error: $"conversationId={lease.ConversationId};owner={lease.Owner};generation={lease.Generation}"), ct);
    }

    private string BuildHostStateDbPath(string saveId)
        => Path.Combine(
            _runtimeRoot,
            "runtime",
            "stardew",
            "games",
            NpcNamespace.Sanitize("stardew-valley"),
            "saves",
            NpcNamespace.Sanitize(saveId),
            "host",
            "state.db");

    private static bool ShouldStageBatch(GameEventCursor sourceCursor, GameEventBatch batch)
        => batch.Records.Count > 0 || !CursorsEqual(sourceCursor, batch.NextCursor);

    private async Task AppendScheduledPrivateChatIngressAsync(
        StardewNpcRuntimeBinding binding,
        CronTask task,
        DateTimeOffset firedAtUtc,
        CancellationToken ct)
    {
        var traceId = $"trace_scheduled_{binding.Descriptor.NpcId}_{task.Id}_{firedAtUtc.UtcDateTime.Ticks}";
        var idempotencyKey = $"idem_scheduled_{binding.Descriptor.NpcId}_{task.Id}_{firedAtUtc.UtcDateTime.Ticks}";
        var tracker = await GetOrCreateTrackerAsync(binding, ct);
        await tracker.Driver.EnqueueIngressWorkItemAsync(
            new NpcRuntimeIngressWorkItemSnapshot(
                $"ingress_scheduled_{binding.Descriptor.NpcId}_{task.Id}_{firedAtUtc.UtcDateTime.Ticks}",
                "scheduled_private_chat",
                "queued",
                firedAtUtc.UtcDateTime,
                idempotencyKey,
                traceId,
                new JsonObject
                {
                    ["prompt"] = task.Prompt,
                    ["conversationId"] = $"scheduled_task:{task.Id}",
                    ["taskId"] = task.Id
                }),
            ct);
        tracker.Instance.SetInboxDepth(tracker.Driver.Snapshot().IngressWorkItems.Count);

        _logger.LogInformation(
            "Scheduled Stardew cron task {TaskId} queued durable private chat ingress for {NpcId}.",
            task.Id,
            binding.Descriptor.NpcId);
    }

    private static bool IsInitialCursor(GameEventCursor cursor)
        => string.IsNullOrWhiteSpace(cursor.Since) && !cursor.Sequence.HasValue;

    private static bool CursorsEqual(GameEventCursor left, GameEventCursor right)
        => string.Equals(left.Since, right.Since, StringComparison.Ordinal) &&
           left.Sequence == right.Sequence;

    private static bool HasPrivateChatIngress(GameEventBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        return batch.Records.Any(record =>
            string.Equals(record.EventType, "vanilla_dialogue_completed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(record.EventType, "vanilla_dialogue_unavailable", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(record.EventType, "player_private_message_submitted", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(record.EventType, "player_private_message_cancelled", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(record.EventType, "private_chat_reply_displayed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(record.EventType, "private_chat_reply_closed", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasHostProgress(GameEventCursor sourceCursor, GameEventBatch batch)
    {
        ArgumentNullException.ThrowIfNull(sourceCursor);
        ArgumentNullException.ThrowIfNull(batch);

        return batch.Records.Count > 0 || !CursorsEqual(sourceCursor, batch.NextCursor);
    }

    private static bool MatchesNpcSession(string scheduledSessionId, string runtimeSessionId)
        => string.Equals(scheduledSessionId, runtimeSessionId, StringComparison.OrdinalIgnoreCase) ||
           scheduledSessionId.StartsWith(runtimeSessionId + ":", StringComparison.OrdinalIgnoreCase);

    private async Task<bool> TryProcessIngressWorkAsync(
        StardewNpcRuntimeBinding binding,
        NpcAutonomyTracker tracker,
        IGameAdapter hostAdapter,
        NpcAutonomyDispatch dispatch,
        GameEventCursor deliveredCursor,
        CancellationToken ct)
    {
        var workItem = tracker.Driver.Snapshot().IngressWorkItems
            .FirstOrDefault(item => string.Equals(item.WorkType, "npc_delegated_action", StringComparison.OrdinalIgnoreCase)) ??
            tracker.Driver.Snapshot().IngressWorkItems
                .FirstOrDefault(item => string.Equals(item.WorkType, "scheduled_private_chat", StringComparison.OrdinalIgnoreCase));
        if (workItem is null)
            return false;

        if (string.Equals(workItem.WorkType, "npc_delegated_action", StringComparison.OrdinalIgnoreCase))
            return await TryProcessDelegatedActionIngressWorkAsync(binding, tracker, hostAdapter, dispatch, workItem, deliveredCursor, ct);

        var action = new GameAction(
            binding.Descriptor.NpcId,
            binding.Descriptor.GameId,
            GameActionType.OpenPrivateChat,
            string.IsNullOrWhiteSpace(workItem.TraceId) ? $"trace_ingress_{Guid.NewGuid():N}" : workItem.TraceId!,
            string.IsNullOrWhiteSpace(workItem.IdempotencyKey) ? $"idem_ingress_{Guid.NewGuid():N}" : workItem.IdempotencyKey!,
            new GameActionTarget("player"),
            Payload: ClonePayload(workItem.Payload),
            BodyBinding: binding.Descriptor.EffectiveBodyBinding);

        var runtimeActions = new StardewRuntimeActionController(tracker.Driver, _worldCoordination, null, null);
        var preparedAction = await runtimeActions.TryBeginAsync(action, ct);
        if (preparedAction?.BlockedResult is not null)
        {
            await tracker.Driver.AcknowledgeEventCursorAsync(deliveredCursor, ct);
            return true;
        }

        var result = await hostAdapter.Commands.SubmitAsync(action, ct);
        await runtimeActions.RecordSubmitResultAsync(preparedAction, result, ct);
        if (result.Accepted || !result.Retryable)
            await tracker.Driver.RemoveIngressWorkItemAsync(workItem.WorkItemId, ct);

        await tracker.Driver.AcknowledgeEventCursorAsync(deliveredCursor, ct);
        return true;
    }

    private async Task<bool> TryProcessDelegatedActionIngressWorkAsync(
        StardewNpcRuntimeBinding binding,
        NpcAutonomyTracker tracker,
        IGameAdapter hostAdapter,
        NpcAutonomyDispatch dispatch,
        NpcRuntimeIngressWorkItemSnapshot workItem,
        GameEventCursor deliveredCursor,
        CancellationToken ct)
    {
        var controller = tracker.Driver.Snapshot();
        if (controller.ActionSlot is not null || controller.PendingWorkItem is not null)
            return true;

        var payload = workItem.Payload ?? [];
        var action = ReadPayloadString(payload, "action");
        var reason = ReadPayloadString(payload, "reason");
        if (string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(reason))
        {
            await WriteIngressDiagnosticAsync(
                binding,
                tracker,
                workItem,
                "malformed",
                "missing_action_or_reason",
                ct);
            await tracker.Driver.RemoveIngressWorkItemAsync(workItem.WorkItemId, ct);
            await tracker.Driver.AcknowledgeEventCursorAsync(deliveredCursor, ct);
            return true;
        }

        var intentText = ReadPayloadString(payload, "intentText");
        var conversationId = ReadPayloadString(payload, "conversationId");
        var isMove = string.Equals(action, "move", StringComparison.OrdinalIgnoreCase);

        if (isMove &&
            !string.IsNullOrWhiteSpace(conversationId) &&
            !HasPrivateChatReplyDisplayedOrClosed(dispatch.SharedEventBatch, binding.Descriptor, conversationId))
        {
            await WriteIngressDiagnosticAsync(
                binding,
                tracker,
                workItem,
                "deferred",
                "waiting_private_chat_reply_displayed",
                ct);
            await tracker.Driver.AcknowledgeEventCursorAsync(deliveredCursor, ct);
            return true;
        }

        if (isMove)
        {
            if (!TryReadMoveTarget(payload, out var target, out var targetError))
            {
                await WriteIngressDiagnosticAsync(
                    binding,
                    tracker,
                    workItem,
                    "malformed",
                    targetError,
                    ct);
                await tracker.Driver.RemoveIngressWorkItemAsync(workItem.WorkItemId, ct);
                await tracker.Driver.AcknowledgeEventCursorAsync(deliveredCursor, ct);
                return true;
            }

            var moveTarget = target!;
            var runtimeActions = new StardewRuntimeActionController(tracker.Driver, _worldCoordination, null, null);
            var movePayload = new JsonObject
            {
                ["targetSource"] = moveTarget.Source
            };
            if (moveTarget.FacingDirection is not null)
                movePayload["facingDirection"] = moveTarget.FacingDirection.Value;

            var moveAction = new GameAction(
                binding.Descriptor.NpcId,
                binding.Descriptor.GameId,
                GameActionType.Move,
                string.IsNullOrWhiteSpace(workItem.TraceId) ? $"trace_ingress_{Guid.NewGuid():N}" : workItem.TraceId!,
                string.IsNullOrWhiteSpace(workItem.IdempotencyKey) ? $"idem_ingress_{Guid.NewGuid():N}" : workItem.IdempotencyKey!,
                new GameActionTarget(
                    "tile",
                    moveTarget.LocationName,
                    new GameTile(moveTarget.X, moveTarget.Y)),
                reason.Trim(),
                movePayload,
                BodyBinding: binding.Descriptor.EffectiveBodyBinding);

            var preparedAction = await runtimeActions.TryBeginAsync(moveAction, ct);
            if (preparedAction?.BlockedResult is not null)
            {
                await tracker.Driver.AcknowledgeEventCursorAsync(deliveredCursor, ct);
                return true;
            }

            var result = await hostAdapter.Commands.SubmitAsync(moveAction, ct);
            await runtimeActions.RecordSubmitResultAsync(preparedAction, result, ct);
            if (result.Accepted || !result.Retryable)
                await tracker.Driver.RemoveIngressWorkItemAsync(workItem.WorkItemId, ct);

            await tracker.Driver.AcknowledgeEventCursorAsync(deliveredCursor, ct);
            return true;
        }

        var intentJson = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["action"] = action.Trim(),
            ["reason"] = MergeReasonAndIntentText(reason.Trim(), intentText)
        }.Where(pair => pair.Value is not null).ToDictionary(pair => pair.Key, pair => pair.Value));

        var handle = await _runtimeSupervisor.GetOrCreateAutonomyHandleAsync(
            binding.Descriptor,
            binding.Pack,
            _runtimeRoot,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: dispatch.BridgeKey,
                IncludeMemory: _includeMemory,
                IncludeUser: _includeUser,
                MaxToolIterations: _budget.Options.MaxToolIterations,
                AdapterFactory: () => hostAdapter,
                GameToolFactory: (adapter, factStore) => StardewNpcToolFactory.CreateDefault(
                    adapter,
                    binding.Descriptor,
                    runtimeDriver: tracker.Driver,
                    worldCoordination: _worldCoordination,
                    recentActivityProvider: new StardewRecentActivityProvider(factStore, tracker.Driver),
                    logger: _logger),
                LocalExecutorGameToolFactory: (adapter, factStore) => StardewNpcToolFactory.CreateLocalExecutorTools(
                    adapter,
                    binding.Descriptor,
                    runtimeDriver: tracker.Driver,
                    worldCoordination: _worldCoordination,
                    logger: _logger),
                LocalExecutorRuntimeToolFactory: services => [new SkillViewTool(services.SkillManager)],
                Services: new NpcRuntimeCompositionServices(
                    _chatClient,
                    _loggerFactory,
                    _skillManager,
                    _cronScheduler,
                    _delegationChatClient),
                ToolSurface: NpcToolSurface.FromTools([]),
                SystemPromptSupplement: _promptSupplementBuilder.Build(binding.Descriptor, tracker.Instance.Namespace, binding.Pack),
                LocalExecutorToolFingerprint: StardewNpcToolFactory.LocalExecutorToolFingerprint(includeSkillView: true)),
            ct);

        var tick = await handle.Loop.RunDelegatedIntentAsync(
            handle.Instance,
            string.IsNullOrWhiteSpace(workItem.TraceId) ? $"trace_ingress_{Guid.NewGuid():N}" : workItem.TraceId!,
            intentJson,
            ct);
        if (tick.DecisionResponse?.StartsWith("local_executor_completed:", StringComparison.OrdinalIgnoreCase) is true ||
            tick.DecisionResponse?.StartsWith("local_executor_blocked:", StringComparison.OrdinalIgnoreCase) is true ||
            tick.DecisionResponse?.StartsWith("local_executor_escalated:", StringComparison.OrdinalIgnoreCase) is true)
        {
            await tracker.Driver.RemoveIngressWorkItemAsync(workItem.WorkItemId, ct);
        }

        await tracker.Driver.AcknowledgeEventCursorAsync(tick.NextEventCursor ?? deliveredCursor, ct);
        return true;
    }

    private static Task WriteIngressDiagnosticAsync(
        StardewNpcRuntimeBinding binding,
        NpcAutonomyTracker tracker,
        NpcRuntimeIngressWorkItemSnapshot workItem,
        string stage,
        string result,
        CancellationToken ct)
    {
        var writer = new NpcRuntimeLogWriter(Path.Combine(tracker.Instance.Namespace.ActivityPath, "runtime.jsonl"));
        return writer.WriteAsync(
            new NpcRuntimeLogRecord(
                DateTime.UtcNow,
                string.IsNullOrWhiteSpace(workItem.TraceId) ? workItem.WorkItemId : workItem.TraceId!,
                binding.Descriptor.NpcId,
                binding.Descriptor.GameId,
                binding.Descriptor.SessionId,
                "ingress",
                workItem.WorkType,
                stage,
                result,
                CommandId: workItem.WorkItemId,
                Error: result),
            ct);
    }

    private static string? ReadPayloadString(JsonObject payload, string propertyName)
        => payload.TryGetPropertyValue(propertyName, out var value) && value is JsonValue jsonValue &&
           jsonValue.TryGetValue<string>(out var text)
            ? text
            : null;

    private static bool TryReadMoveTarget(JsonObject payload, out DelegatedMoveTarget? target, out string error)
    {
        target = default;
        error = "";
        if (!payload.TryGetPropertyValue("target", out var targetNode) ||
            targetNode is not JsonObject targetObject)
        {
            error = "move_target_required";
            return false;
        }

        var locationName = ReadPayloadString(targetObject, "locationName");
        var source = ReadPayloadString(targetObject, "source");
        if (string.IsNullOrWhiteSpace(locationName))
        {
            error = "target_location_required";
            return false;
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            error = "target_source_required";
            return false;
        }

        if (!TryReadPayloadInt(targetObject, "x", out var x))
        {
            error = "target_x_required";
            return false;
        }

        if (!TryReadPayloadInt(targetObject, "y", out var y))
        {
            error = "target_y_required";
            return false;
        }

        var facingDirection = TryReadPayloadInt(targetObject, "facingDirection", out var facing)
            ? facing
            : (int?)null;
        target = new DelegatedMoveTarget(
            locationName.Trim(),
            x,
            y,
            source.Trim(),
            facingDirection);
        return true;
    }

    private static bool TryReadPayloadInt(JsonObject payload, string propertyName, out int value)
    {
        value = default;
        return payload.TryGetPropertyValue(propertyName, out var node) &&
               node is JsonValue jsonValue &&
               jsonValue.TryGetValue<int>(out value);
    }

    private static bool HasPrivateChatReplyDisplayedOrClosed(
        GameEventBatch batch,
        NpcRuntimeDescriptor descriptor,
        string conversationId)
    {
        foreach (var record in batch.Records)
        {
            if (!(string.Equals(record.EventType, "private_chat_reply_displayed", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(record.EventType, "private_chat_reply_closed", StringComparison.OrdinalIgnoreCase)) ||
                !IsRelevantToRuntime(descriptor, record))
            {
                continue;
            }

            if (record.Payload is null)
                continue;

            var closedConversationId = ReadPayloadString(record.Payload, "conversationId");
            if (string.Equals(closedConversationId, conversationId.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string MergeReasonAndIntentText(string reason, string? intentText)
    {
        if (string.IsNullOrWhiteSpace(intentText))
            return reason;

        var trimmedIntent = intentText.Trim();
        if (reason.Contains(trimmedIntent, StringComparison.OrdinalIgnoreCase))
            return reason;

        return $"{reason}; player request: {trimmedIntent}";
    }

    private static JsonObject ClonePayload(JsonObject? payload)
    {
        var clone = new JsonObject();
        if (payload is null)
            return clone;

        foreach (var pair in payload)
            clone[pair.Key] = pair.Value?.DeepClone();

        return clone;
    }

    private sealed record DelegatedMoveTarget(
        string LocationName,
        int X,
        int Y,
        string Source,
        int? FacingDirection);

    private static GameEventBatch FilterBatchForRuntime(
        NpcRuntimeDescriptor descriptor,
        GameEventBatch sharedEventBatch,
        GameEventCursor persistedCursor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(sharedEventBatch);
        ArgumentNullException.ThrowIfNull(persistedCursor);

        var recordsAfterCursor = FilterRecordsAfterCursor(sharedEventBatch.Records, persistedCursor);
        var records = recordsAfterCursor
            .Where(record => IsRelevantToRuntime(descriptor, record))
            .ToArray();
        return new GameEventBatch(records, sharedEventBatch.NextCursor);
    }

    private static IReadOnlyList<GameEventRecord> FilterRecordsAfterCursor(
        IReadOnlyList<GameEventRecord> records,
        GameEventCursor persistedCursor)
    {
        if (records.Count == 0)
            return [];

        if (persistedCursor.Sequence.HasValue && records.Any(record => record.Sequence.HasValue))
        {
            return records
                .Where(record => !record.Sequence.HasValue || record.Sequence.Value > persistedCursor.Sequence.Value)
                .ToArray();
        }

        if (string.IsNullOrWhiteSpace(persistedCursor.Since))
            return records;

        var sinceIndex = records
            .Select((record, index) => new { record, index })
            .FirstOrDefault(item => string.Equals(item.record.EventId, persistedCursor.Since, StringComparison.OrdinalIgnoreCase))
            ?.index;
        return sinceIndex.HasValue
            ? records.Skip(sinceIndex.Value + 1).ToArray()
            : records;
    }

    private static bool IsRelevantToRuntime(NpcRuntimeDescriptor descriptor, GameEventRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.NpcId))
            return true;

        var body = descriptor.EffectiveBodyBinding;
        return string.Equals(descriptor.NpcId, record.NpcId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(body.TargetEntityId, record.NpcId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(body.SmapiName, record.NpcId, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class NpcAutonomyTracker
    {
        private readonly object _gate = new();
        private readonly SemaphoreSlim _dispatchSignal = new(0);
        private readonly CancellationTokenSource _workerCts = new();
        private readonly Func<NpcAutonomyTracker, NpcAutonomyDispatch, CancellationToken, Task> _dispatchRunner;
        private NpcAutonomyDispatch? _pendingDispatch;
        private bool _isDispatching;
        private bool _stopped;

        public NpcAutonomyTracker(
            StardewNpcRuntimeBinding binding,
            NpcRuntimeInstance instance,
            NpcRuntimeDriver driver,
            Func<NpcAutonomyTracker, NpcAutonomyDispatch, CancellationToken, Task> dispatchRunner)
        {
            Binding = binding;
            Instance = instance;
            Driver = driver;
            _dispatchRunner = dispatchRunner;
            WorkerTask = Task.Run(() => RunAsync(_workerCts.Token));
        }

        public StardewNpcRuntimeBinding Binding { get; }

        public Task WorkerTask { get; }

        public async Task<Task> EnqueueAsync(NpcAutonomyDispatch dispatch, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            bool shouldSignal;
            lock (_gate)
            {
                if (_stopped)
                    throw new InvalidOperationException($"NPC autonomy tracker '{Binding.Descriptor.NpcId}' has been stopped.");

                shouldSignal = _pendingDispatch is null;
                if (_pendingDispatch is null)
                {
                    if (_isDispatching)
                        dispatch.MarkQueuedBehindActiveWorker();

                    _pendingDispatch = dispatch;
                }
                else
                {
                    _pendingDispatch.MergeFrom(dispatch);
                }
            }

            if (shouldSignal)
                _dispatchSignal.Release();

            await Task.CompletedTask;
            return dispatch.CompletionTask;
        }

        public void Stop()
        {
            lock (_gate)
            {
                if (_stopped)
                    return;

                _stopped = true;
            }

            _workerCts.Cancel();
        }

        private async Task RunAsync(CancellationToken ct)
        {
            try
            {
                while (true)
                {
                    await _dispatchSignal.WaitAsync(ct);

                    while (true)
                    {
                        NpcAutonomyDispatch? dispatch;
                        lock (_gate)
                        {
                            dispatch = _pendingDispatch;
                            _pendingDispatch = null;
                        }

                        if (dispatch is null)
                            break;

                        lock (_gate)
                            _isDispatching = true;

                        try
                        {
                            await _dispatchRunner(this, dispatch, ct);
                            dispatch.TrySetResult();
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested)
                        {
                            dispatch.TrySetCanceled(ct);
                            CancelPendingDispatches(ct);
                            return;
                        }
                        catch (Exception ex)
                        {
                            dispatch.TrySetException(ex);
                            Instance.RecordError(ex.Message);
                        }
                        finally
                        {
                            lock (_gate)
                                _isDispatching = false;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            finally
            {
                CancelPendingDispatches(ct);
                _dispatchSignal.Dispose();
                _workerCts.Dispose();
            }
        }

        public NpcRuntimeInstance Instance { get; }

        public NpcRuntimeDriver Driver { get; }

        public int RestartCount { get; set; }

        private void CancelPendingDispatches(CancellationToken ct)
        {
            NpcAutonomyDispatch? pendingDispatch;
            lock (_gate)
            {
                pendingDispatch = _pendingDispatch;
                _pendingDispatch = null;
            }

            pendingDispatch?.TrySetCanceled(ct);
        }
    }

    private sealed class NpcAutonomyDispatch
    {
        private readonly List<TaskCompletionSource<bool>> _completions;

        public NpcAutonomyDispatch(
            string bridgeKey,
            StardewBridgeDiscoverySnapshot snapshot,
            GameEventBatch sharedEventBatch,
            GameEventCursor deliveredCursor,
            bool hasPrivateChatIngress,
            bool hasHostProgress)
        {
            BridgeKey = bridgeKey;
            Snapshot = snapshot;
            SharedEventBatch = sharedEventBatch;
            DeliveredCursor = deliveredCursor;
            HasPrivateChatIngress = hasPrivateChatIngress;
            HasHostProgress = hasHostProgress;
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _completions = [completion];
            CompletionTask = completion.Task;
        }

        public string BridgeKey { get; private set; }

        public StardewBridgeDiscoverySnapshot Snapshot { get; private set; }

        public GameEventBatch SharedEventBatch { get; private set; }

        public GameEventCursor DeliveredCursor { get; private set; }

        public bool HasPrivateChatIngress { get; private set; }

        public bool HasHostProgress { get; private set; }

        public bool WasQueuedBehindActiveWorker { get; private set; }

        public Task CompletionTask { get; }

        public void MarkQueuedBehindActiveWorker()
            => WasQueuedBehindActiveWorker = true;

        public void MergeFrom(NpcAutonomyDispatch newer)
        {
            ArgumentNullException.ThrowIfNull(newer);

            SharedEventBatch = MergeBatches(SharedEventBatch, newer.SharedEventBatch);
            DeliveredCursor = newer.DeliveredCursor;
            BridgeKey = newer.BridgeKey;
            Snapshot = newer.Snapshot;
            HasPrivateChatIngress = HasPrivateChatIngress || newer.HasPrivateChatIngress;
            HasHostProgress = HasHostProgress || newer.HasHostProgress;
            WasQueuedBehindActiveWorker = WasQueuedBehindActiveWorker || newer.WasQueuedBehindActiveWorker;
            _completions.AddRange(newer._completions);
        }

        public void TrySetResult()
        {
            foreach (var completion in _completions)
                completion.TrySetResult(true);
        }

        public void TrySetException(Exception ex)
        {
            ArgumentNullException.ThrowIfNull(ex);

            foreach (var completion in _completions)
                completion.TrySetException(ex);
        }

        public void TrySetCanceled(CancellationToken ct)
        {
            foreach (var completion in _completions)
                completion.TrySetCanceled(ct);
        }

        private static GameEventBatch MergeBatches(GameEventBatch current, GameEventBatch newer)
        {
            if (current.Records.Count == 0)
                return new GameEventBatch(newer.Records.ToArray(), newer.NextCursor);

            if (newer.Records.Count == 0)
                return new GameEventBatch(current.Records.ToArray(), newer.NextCursor);

            var mergedRecords = new List<GameEventRecord>(current.Records.Count + newer.Records.Count);
            mergedRecords.AddRange(current.Records);
            mergedRecords.AddRange(newer.Records);
            return new GameEventBatch(mergedRecords, newer.NextCursor);
        }
    }
}
