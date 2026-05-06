namespace Hermes.Agent.Games.Stardew;

using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.LLM;
using Hermes.Agent.Runtime;
using Hermes.Agent.Skills;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json.Nodes;

public sealed record StardewNpcAutonomyBackgroundOptions(
    IReadOnlyCollection<string> EnabledNpcIds,
    TimeSpan PollInterval = default);

public sealed class StardewNpcAutonomyBackgroundService : IDisposable
{
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
    private readonly string _runtimeRoot;
    private readonly HashSet<string> _enabledNpcIds;
    private readonly bool _includeMemory;
    private readonly bool _includeUser;
    private readonly Dictionary<string, NpcAutonomyTracker> _trackers = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _pollInterval;
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
            runtimeRoot)
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
        string runtimeRoot)
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
        _runtimeRoot = runtimeRoot;
        _includeMemory = includeMemory;
        _includeUser = includeUser;
        _enabledNpcIds = new HashSet<string>(
            (options.EnabledNpcIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim()),
            StringComparer.OrdinalIgnoreCase);
        _pollInterval = options.PollInterval == default ? TimeSpan.FromSeconds(2) : options.PollInterval;
        _cronScheduler.TaskDue += OnCronTaskDue;
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

        var hostStateStore = await GetOrCreateHostStateStoreAsync(saveId, ct);
        var hostState = await hostStateStore.LoadAsync(ct);
        _bridgeEventCursor = hostState.SourceCursor;

        var bridgeKey = BuildBridgeKey(snapshot, saveId);
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
                    sharedEventBatch.NextCursor),
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

            if (await TryProcessIngressWorkAsync(binding, tracker, hostAdapter.Commands, deliveredCursor, ct))
                return;

            if (tracker.Instance.TryGetActivePrivateChatSessionLease(out var activeLease) && activeLease is not null)
            {
                await PauseTrackerAsync(tracker, deliveredCursor, activeLease.Reason, dispatch.BridgeKey, null, ct);
                return;
            }

            if (controller.NextWakeAtUtc.HasValue && DateTime.UtcNow < controller.NextWakeAtUtc.Value)
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
                    Services: new NpcRuntimeCompositionServices(
                        _chatClient,
                        _loggerFactory,
                        _skillManager,
                        _cronScheduler),
                    ToolSurface: toolSnapshot.ToolSurface,
                    ToolSurfaceSnapshotVersion: toolSnapshot.SnapshotVersion,
                    SystemPromptSupplement: systemPromptSupplement),
                ct);

            var observeStopwatch = Stopwatch.StartNew();
            var observation = await hostAdapter.Queries.ObserveAsync(binding.Descriptor.EffectiveBodyBinding, ct);
            observeStopwatch.Stop();
            _logger.LogInformation(
                "Stardew autonomy observation completed; npc={NpcId}; observedNpc={ObservedNpcId}; summary={Summary}; gameTime={GameTime}; gameClock={GameClock}; location={Location}; tile={Tile}; factCount={FactCount}; durationMs={DurationMs}",
                binding.Descriptor.NpcId,
                observation.NpcId,
                TruncateForLog(observation.Summary, 180),
                FindFactValue(observation.Facts, "gameTime") ?? "-",
                FindFactValue(observation.Facts, "gameClock") ?? "-",
                FindFactValue(observation.Facts, "location") ?? "-",
                FindFactValue(observation.Facts, "tile") ?? "-",
                observation.Facts.Count,
                observeStopwatch.ElapsedMilliseconds);
            using var llmTurnCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            llmTurnCts.CancelAfter(_budget.Options.EffectiveLlmTurnTimeout);
            NpcAutonomyTickResult tick;
            try
            {
                var llmStartedAtUtc = DateTime.UtcNow;
                var llmStopwatch = Stopwatch.StartNew();
            _logger.LogInformation(
                "Stardew autonomy LLM turn started; npc={NpcId}; timeoutSeconds={TimeoutSeconds}; maxToolIterations={MaxToolIterations}; observationFacts={ObservationFactCount}; eventCount={EventCount}; toolSurfaceVersion={ToolSurfaceVersion}",
                binding.Descriptor.NpcId,
                _budget.Options.EffectiveLlmTurnTimeout.TotalSeconds,
                _budget.Options.MaxToolIterations,
                observation.Facts.Count,
                dispatch.SharedEventBatch.Records.Count,
                toolSnapshot.SnapshotVersion);
                tick = await handle.Loop.RunOneTickAsync(handle.Instance, observation, dispatch.SharedEventBatch, llmTurnCts.Token);
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

    private static string? FindFactValue(IReadOnlyList<string> facts, string key)
    {
        var prefix = key + "=";
        var fact = facts.FirstOrDefault(value => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return fact is null ? null : fact[prefix.Length..];
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

    private async Task<StardewRuntimeHostStateStore> GetOrCreateHostStateStoreAsync(string saveId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saveId);
        ct.ThrowIfCancellationRequested();

        if (_hostStateStore is not null &&
            string.Equals(_attachedSaveId, saveId, StringComparison.OrdinalIgnoreCase))
        {
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

        if (controller.ActionSlot?.TimeoutAtUtc is { } timeoutAtUtc && DateTime.UtcNow >= timeoutAtUtc)
        {
            var cancelledStatus = !string.IsNullOrWhiteSpace(controller.ActionSlot.CommandId)
                ? await commandService.CancelAsync(controller.ActionSlot.CommandId, StardewBridgeErrorCodes.ActionSlotTimeout, ct)
                : new GameCommandStatus(
                    controller.ActionSlot.WorkItemId,
                    binding.Descriptor.NpcId,
                    controller.PendingWorkItem?.WorkType ?? "action",
                    StardewCommandStatuses.Cancelled,
                    0,
                    StardewBridgeErrorCodes.ActionSlotTimeout,
                    StardewBridgeErrorCodes.ActionSlotTimeout,
                    UpdatedAtUtc: DateTime.UtcNow,
                    RetryAfterUtc: DateTime.UtcNow + _budget.Options.EffectiveRestartCooldown);

            await runtimeActions.RecordStatusAsync(cancelledStatus, ct);
            await PauseTrackerAsync(
                tracker,
                deliveredCursor,
                StardewBridgeErrorCodes.ActionSlotTimeout,
                bridgeKey,
                cancelledStatus.RetryAfterUtc ?? DateTime.UtcNow + _budget.Options.EffectiveRestartCooldown,
                ct);
            return true;
        }

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

        if (StardewRuntimeActionController.IsInFlightStatus(status.Status))
        {
            await PauseTrackerAsync(tracker, deliveredCursor, $"command_{status.Status}", bridgeKey, status.RetryAfterUtc, ct);
            return true;
        }

        if (StardewRuntimeActionController.IsTerminalStatus(status.Status))
            return true;

        return true;
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

    private static bool MatchesNpcSession(string scheduledSessionId, string runtimeSessionId)
        => string.Equals(scheduledSessionId, runtimeSessionId, StringComparison.OrdinalIgnoreCase) ||
           scheduledSessionId.StartsWith(runtimeSessionId + ":", StringComparison.OrdinalIgnoreCase);

    private async Task<bool> TryProcessIngressWorkAsync(
        StardewNpcRuntimeBinding binding,
        NpcAutonomyTracker tracker,
        IGameCommandService commandService,
        GameEventCursor deliveredCursor,
        CancellationToken ct)
    {
        var workItem = tracker.Driver.Snapshot().IngressWorkItems
            .FirstOrDefault(item => string.Equals(item.WorkType, "scheduled_private_chat", StringComparison.OrdinalIgnoreCase));
        if (workItem is null)
            return false;

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

        var result = await commandService.SubmitAsync(action, ct);
        await runtimeActions.RecordSubmitResultAsync(preparedAction, result, ct);
        if (result.Accepted || !result.Retryable)
            await tracker.Driver.RemoveIngressWorkItemAsync(workItem.WorkItemId, ct);

        await tracker.Driver.AcknowledgeEventCursorAsync(deliveredCursor, ct);
        return true;
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
            GameEventCursor deliveredCursor)
        {
            BridgeKey = bridgeKey;
            Snapshot = snapshot;
            SharedEventBatch = sharedEventBatch;
            DeliveredCursor = deliveredCursor;
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _completions = [completion];
            CompletionTask = completion.Task;
        }

        public string BridgeKey { get; private set; }

        public StardewBridgeDiscoverySnapshot Snapshot { get; private set; }

        public GameEventBatch SharedEventBatch { get; private set; }

        public GameEventCursor DeliveredCursor { get; private set; }

        public Task CompletionTask { get; }

        public void MergeFrom(NpcAutonomyDispatch newer)
        {
            ArgumentNullException.ThrowIfNull(newer);

            SharedEventBatch = MergeBatches(SharedEventBatch, newer.SharedEventBatch);
            DeliveredCursor = newer.DeliveredCursor;
            BridgeKey = newer.BridgeKey;
            Snapshot = newer.Snapshot;
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
