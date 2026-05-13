using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Hermes.Agent.LLM;
using Hermes.Agent.Runtime;
using Hermes.Agent.Skills;
using Hermes.Agent.Tasks;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Stardew;

[TestClass]
public sealed class StardewNpcAutonomyBackgroundServiceTests
{
    private string _tempDir = null!;
    private string _packRoot = null!;
    private string _gamingSkillRoot = null!;
    private SkillManager _skillManager = null!;
    private readonly List<StardewNpcAutonomyBackgroundService> _services = [];

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-autonomy-background-tests", Guid.NewGuid().ToString("N"));
        _packRoot = Path.Combine(_tempDir, "packs");
        _gamingSkillRoot = Path.Combine(_tempDir, "skills", "gaming");

        var skillsDir = Path.Combine(_tempDir, "skills", "autonomy");
        Directory.CreateDirectory(skillsDir);
        CreateGamingSkillFixtures(_gamingSkillRoot);
        File.WriteAllText(
            Path.Combine(skillsDir, "SKILL.md"),
            """
            ---
            name: npc-autonomy-skill
            description: Keep autonomy actions grounded in observed Stardew facts.
            ---
            Use game facts before taking autonomous action.
            """);
        _skillManager = new SkillManager(Path.Combine(_tempDir, "skills"), NullLogger<SkillManager>.Instance);

        CreatePack("haley", "Haley");
        CreatePack("penny", "Penny");
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var service in _services)
            service.Stop();

        _services.Clear();

        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_OnlyStartsWhitelistedNpcAndRecordsAutomaticTick()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("I will wait near the library.");
        var adapter = CreateAdapter("penny");
        var supervisor = new NpcRuntimeSupervisor();
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["penny"]);

        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(1, chatClient.CompleteWithToolsCalls);
        Assert.AreEqual(1, supervisor.Snapshot().Count);
        var snapshot = supervisor.Snapshot().Single();
        Assert.AreEqual("penny", snapshot.NpcId);
        Assert.AreEqual(NpcAutonomyLoopState.Running, snapshot.AutonomyLoopState);
        Assert.IsNotNull(snapshot.LastAutomaticTickAtUtc);
        Assert.AreEqual(1, snapshot.CurrentAutonomyHandleGeneration);
        Assert.IsFalse(string.IsNullOrWhiteSpace(snapshot.CurrentBridgeKey));
    }

    [TestMethod]
    public void Constructor_WithoutExplicitPollInterval_UsesLowCostIdleCadence()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("unused");
        var adapter = CreateAdapter("haley");
        var supervisor = new NpcRuntimeSupervisor();

        var service = new StardewNpcAutonomyBackgroundService(
            discovery,
            _ => adapter,
            chatClient,
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            supervisor,
            new NpcRuntimeHost(new FileSystemNpcPackLoader(), supervisor, _tempDir),
            new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot),
            new StardewNpcAutonomyPromptSupplementBuilder(new FixedStardewGamingSkillRootProvider(_gamingSkillRoot)),
            new NpcToolSurfaceSnapshotProvider(() => [new DiscoveredNoopTool("mcp_dynamic_test")]),
            new StardewPrivateChatRuntimeAdapter(
                new NoopPrivateChatAgentRunner(),
                NullLogger<StardewPrivateChatRuntimeAdapter>.Instance),
            new NpcAutonomyBudget(new NpcAutonomyBudgetOptions(MaxToolIterations: 2, MaxConcurrentLlmRequests: 1, MaxRestartsPerScene: 2)),
            new WorldCoordinationService(new ResourceClaimRegistry()),
            NullLogger<StardewNpcAutonomyBackgroundService>.Instance,
            new StardewNpcAutonomyBackgroundOptions(["haley"]),
            true,
            true,
            _tempDir);
        _services.Add(service);

        Assert.AreEqual(TimeSpan.FromSeconds(1), service.PollInterval);
    }

    [TestMethod]
    public void Constructor_WithExplicitPollInterval_PreservesOverride()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("unused");
        var adapter = CreateAdapter("haley");
        var supervisor = new NpcRuntimeSupervisor();
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"],
            pollInterval: TimeSpan.FromMilliseconds(10));

        Assert.AreEqual(TimeSpan.FromMilliseconds(10), service.PollInterval);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenHostPollsEmptyBeforeAutonomyWake_DoesNotRunSecondLlmTurn()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("I will wait near the library.");
        var adapter = CreateAdapter("haley");
        var supervisor = new NpcRuntimeSupervisor();
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"],
            autonomyWakeInterval: TimeSpan.FromMinutes(1));

        await service.RunOneIterationAsync(CancellationToken.None);
        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(
            1,
            chatClient.CompleteWithToolsCalls,
            "Fast host polling must not turn an empty autonomy wake into one LLM call per poll interval.");
    }

    [TestMethod]
    public async Task RunOneIterationAsync_AutonomyParentHasControlledActionToolSurface()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("I will wait near the library.");
        var adapter = CreateAdapter("penny");
        var supervisor = new NpcRuntimeSupervisor();
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["penny"]);

        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(1, chatClient.CompleteWithToolsCalls);
        var snapshot = supervisor.Snapshot().Single(snapshot => snapshot.NpcId == "penny");
        var pack = new FileSystemNpcPackLoader().LoadPacks(_packRoot).Single(pack => pack.Manifest.NpcId == "penny");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-42");
        var handle = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: snapshot.CurrentBridgeKey!,
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () => adapter,
                GameToolFactory: gameAdapter =>
                [
                    new DiscoveredNoopTool("stardew_status"),
                    new DiscoveredNoopTool("stardew_navigate_to_tile")
                ],
                Services: new NpcRuntimeCompositionServices(
                    chatClient,
                    NullLoggerFactory.Instance,
                    _skillManager,
                    new NoopCronScheduler()),
                ToolSurface: NpcToolSurface.FromTools([])),
            CancellationToken.None);

        Assert.AreEqual(3, handle.AgentHandle.Agent.Tools.Count);
        Assert.IsTrue(handle.AgentHandle.Agent.Tools.ContainsKey("skill_view"));
        Assert.IsTrue(handle.AgentHandle.Agent.Tools.ContainsKey("stardew_status"));
        Assert.IsTrue(handle.AgentHandle.Agent.Tools.ContainsKey("stardew_navigate_to_tile"));
        Assert.IsFalse(handle.AgentHandle.Agent.Tools.ContainsKey("agent"));
        Assert.IsFalse(handle.AgentHandle.Agent.Tools.ContainsKey("todo"));
        Assert.IsFalse(handle.AgentHandle.Agent.Tools.ContainsKey("memory"));
    }

    [TestMethod]
    public async Task RunOneIterationAsync_MissingRequiredSkillPausesNpcWithoutLlmRetryLoop()
    {
        File.Delete(Path.Combine(_gamingSkillRoot, "stardew-social.md"));
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("unused");
        var adapter = CreateAdapter("haley");
        var supervisor = new NpcRuntimeSupervisor();
        var budget = new NpcAutonomyBudget(new NpcAutonomyBudgetOptions(MaxToolIterations: 2, MaxConcurrentLlmRequests: 1, MaxRestartsPerScene: 2));
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"],
            budget: budget);

        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(0, chatClient.CompleteWithToolsCalls);
        var snapshot = supervisor.Snapshot().Single();
        Assert.AreEqual("haley", snapshot.NpcId);
        Assert.AreEqual(NpcAutonomyLoopState.Paused, snapshot.AutonomyLoopState);
        Assert.IsNull(snapshot.LastAutomaticTickAtUtc);
        StringAssert.Contains(snapshot.PauseReason, "stardew-social");
        StringAssert.Contains(snapshot.PauseReason, Path.Combine(_gamingSkillRoot, "stardew-social.md"));
        Assert.AreEqual(0, snapshot.AutonomyRestartCount);

        await using var heldSlot = await budget.TryAcquireLlmSlotAsync("external", CancellationToken.None);
        Assert.IsNotNull(heldSlot);

        await service.RunOneIterationAsync(CancellationToken.None);

        snapshot = supervisor.Snapshot().Single();
        Assert.AreEqual(0, chatClient.CompleteWithToolsCalls);
        Assert.AreEqual(NpcAutonomyLoopState.Paused, snapshot.AutonomyLoopState);
        StringAssert.Contains(snapshot.PauseReason, "stardew-social");
        Assert.IsFalse(
            string.Equals(snapshot.PauseReason, NpcAutonomyExitReason.LlmConcurrencyLimit.ToString(), StringComparison.Ordinal),
            "A prompt resource pause must short-circuit later autonomy ticks before acquiring an LLM slot.");
        Assert.AreEqual(0, snapshot.AutonomyRestartCount);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenPromptResourcePauseBecomesResolvable_ResumesAutonomy()
    {
        var missingSkillPath = Path.Combine(_gamingSkillRoot, "stardew-social.md");
        File.Delete(missingSkillPath);
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("I will walk somewhere quiet.");
        var adapter = CreateAdapter("haley");
        var supervisor = new NpcRuntimeSupervisor();
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"],
            autonomyWakeInterval: TimeSpan.FromMilliseconds(1));

        await service.RunOneIterationAsync(CancellationToken.None);

        var paused = supervisor.Snapshot().Single();
        Assert.AreEqual(NpcAutonomyLoopState.Paused, paused.AutonomyLoopState);
        StringAssert.Contains(paused.PauseReason, "stardew-social");
        Assert.AreEqual(0, chatClient.CompleteWithToolsCalls);

        File.WriteAllText(missingSkillPath, "stardew-social restored guidance");

        await service.RunOneIterationAsync(CancellationToken.None);

        var resumed = supervisor.Snapshot().Single();
        Assert.AreEqual(1, chatClient.CompleteWithToolsCalls);
        Assert.AreEqual(NpcAutonomyLoopState.Running, resumed.AutonomyLoopState);
        Assert.IsNotNull(resumed.LastAutomaticTickAtUtc);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_ActivePrivateChatLeasePausesAutonomy()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("unused");
        var adapter = CreateAdapter("haley");
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var instance = await supervisor.GetOrStartAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        instance.Namespace.SeedPersonaPack(binding.Pack);
        instance.AcquirePrivateChatSessionLease("pc-1", "private_chat", "private_chat_session_active");
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"],
            autonomyWakeInterval: TimeSpan.FromMilliseconds(1));

        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(0, chatClient.CompleteWithToolsCalls);
        var snapshot = supervisor.Snapshot().Single();
        Assert.AreEqual(NpcAutonomyLoopState.Paused, snapshot.AutonomyLoopState);
        Assert.AreEqual("private_chat_session_active", snapshot.PauseReason);
        Assert.IsNull(snapshot.LastAutomaticTickAtUtc);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_StalePrivateChatLeaseIsReleasedAndAutonomyResumes()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("I will keep going after the stale private chat lease is cleared.");
        var adapter = CreateAdapter("haley");
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var driver = await supervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        driver.Instance.Namespace.SeedPersonaPack(binding.Pack);
        driver.Instance.RestorePrivateChatSessionLease(new NpcRuntimeSessionLeaseSnapshot(
            "pc-stale",
            "private_chat",
            "private_chat_session_active",
            1,
            DateTime.UtcNow.AddMinutes(-30)));
        await driver.SyncAsync(CancellationToken.None);
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"],
            autonomyWakeInterval: TimeSpan.FromMilliseconds(1));

        await service.RunOneIterationAsync(CancellationToken.None);

        var snapshot = supervisor.Snapshot().Single();
        Assert.IsNull(snapshot.ActivePrivateChatSessionLease);
        Assert.AreEqual(1, chatClient.CompleteWithToolsCalls);
        Assert.AreEqual(NpcAutonomyLoopState.Running, snapshot.AutonomyLoopState);
        Assert.IsNotNull(snapshot.LastAutomaticTickAtUtc);

        var logPath = Path.Combine(
            _tempDir,
            "runtime",
            "stardew",
            "games",
            "stardew-valley",
            "saves",
            "save-42",
            "npc",
            "haley",
            "profiles",
            "default",
            "activity",
            "runtime.jsonl");
        var records = ReadRuntimeLogRecords(logPath);
        Assert.IsTrue(records.Any(record =>
            record.GetProperty("actionType").GetString() == "diagnostic" &&
            record.GetProperty("target").GetString() == "private_chat_session_lease" &&
            record.GetProperty("stage").GetString() == "released" &&
            record.GetProperty("result").GetString() == "stale_private_chat_session_lease"));
    }

    [TestMethod]
    public async Task RunOneIterationAsync_OpenPrivateChatWithoutPlayerMessageDoesNotPauseAutonomy()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("I will keep going after the phone opens.");
        var adapter = new FakeGameAdapter(
            new FakeCommandService(),
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley is idle.",
                ["location=Town"])),
            new FakeEventSource(
            [
                new GameEventRecord(
                    "evt-1",
                    "vanilla_dialogue_completed",
                    "Haley",
                    DateTime.UtcNow,
                    "Haley vanilla dialogue completed.",
                    Sequence: 1)
            ]));
        var supervisor = new NpcRuntimeSupervisor();
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"]);

        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(1, chatClient.CompleteWithToolsCalls);
        var snapshot = supervisor.Snapshot().Single();
        Assert.AreEqual(NpcAutonomyLoopState.Running, snapshot.AutonomyLoopState);
        Assert.IsNull(snapshot.PauseReason);
        Assert.IsNotNull(snapshot.LastAutomaticTickAtUtc);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenNpcIsPaused_StillAdvancesTrackerCursorToSharedBatchWatermark()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("unused");
        var adapter = new FakeGameAdapter(
            new FakeCommandService(),
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley is idle.",
                ["location=Town"])),
            new FakeEventSource(
                new GameEventBatch(
                    [
                        new GameEventRecord("evt-11", "time_changed", null, DateTime.UtcNow, "The clock advanced.", Sequence: 11)
                    ],
                    new GameEventCursor("evt-11", 14))));
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var instance = await supervisor.GetOrStartAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        instance.Namespace.SeedPersonaPack(binding.Pack);
        instance.AcquirePrivateChatSessionLease("pc-1", "private_chat", "private_chat_session_active");
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"]);

        await service.RunOneIterationAsync(CancellationToken.None);

        var trackerCursor = GetTrackerCursor(service, "haley");
        Assert.AreEqual("evt-11", trackerCursor.Since);
        Assert.AreEqual(14L, trackerCursor.Sequence);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenNpcIsPaused_ExposesRelevantIngressDepthInSnapshot()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("unused");
        var adapter = new FakeGameAdapter(
            new FakeCommandService(),
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley is idle.",
                ["location=Town"])),
            new FakeEventSource(
                new GameEventBatch(
                    [
                        new GameEventRecord("evt-11", "time_changed", "haley", DateTime.UtcNow, "Haley heard the clock tick.", Sequence: 11),
                        new GameEventRecord("evt-12", "time_changed", "penny", DateTime.UtcNow.AddSeconds(1), "Penny heard the clock tick.", Sequence: 12)
                    ],
                    new GameEventCursor("evt-12", 12))));
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var instance = await supervisor.GetOrStartAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        instance.Namespace.SeedPersonaPack(binding.Pack);
        instance.AcquirePrivateChatSessionLease("pc-1", "private_chat", "private_chat_session_active");
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"]);

        await service.RunOneIterationAsync(CancellationToken.None);

        var snapshot = supervisor.Snapshot().Single();
        var inboxDepthProperty = snapshot.Controller.GetType().GetProperty("InboxDepth");
        Assert.IsNotNull(inboxDepthProperty, "Runtime controller snapshot should expose an ingress/inbox depth metric for observability.");
        Assert.AreEqual(1, inboxDepthProperty.GetValue(snapshot.Controller));
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WithMultipleEnabledNpc_PollsBridgeEventsOnlyOnce()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("I will wait near the library.");
        var events = new CountingEventSource(
        [
            new GameEventRecord("evt-1", "time_changed", null, DateTime.UtcNow, "The clock advanced."),
        ]);
        var adapter = new FakeGameAdapter(
            new FakeCommandService(),
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley is idle.",
                ["location=Town"])),
            events);
        var supervisor = new NpcRuntimeSupervisor();
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley", "penny"]);

        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(
            1,
            events.PollCalls,
            "The runtime host must own bridge polling once and fan events out to NPC runtimes instead of letting each NPC loop poll independently.");
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenWorkerIsBusy_ReturnsAfterDispatchWithoutWaitingForCompletion()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new BlockingChatClient("I will wait near the library.");
        var adapter = CreateAdapter("haley");
        var supervisor = new NpcRuntimeSupervisor();
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"]);

        var iteration = service.DispatchOneIterationAsync(CancellationToken.None);
        var completed = await Task.WhenAny(iteration, Task.Delay(250));

        Assert.AreSame(iteration, completed, "The host should dispatch/wake NPC workers and commit bridge progress without waiting for a slow worker tick to finish.");

        await iteration;
        await chatClient.WaitUntilEnteredAsync(CancellationToken.None);
        var workerTask = GetTrackerWorkerTask(service, "haley");
        Assert.IsFalse(workerTask.IsCompleted, "The NPC worker should still be running independently after host dispatch returns.");

        chatClient.Release();
        service.Stop();
        await workerTask.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenLlmTurnTimesOut_ReleasesWorkerAndRetriesLater()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new BlockFirstChatClient("I will wait near the library.");
        var adapter = CreateAdapter("haley");
        var supervisor = new NpcRuntimeSupervisor();
        var budget = new NpcAutonomyBudget(new NpcAutonomyBudgetOptions(
            MaxToolIterations: 2,
            MaxConcurrentLlmRequests: 1,
            RestartCooldown: TimeSpan.FromMilliseconds(1),
            MaxRestartsPerScene: 2,
            LlmTurnTimeout: TimeSpan.FromMilliseconds(250)));
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"],
            budget: budget);

        var firstIteration = service.RunOneIterationAsync(CancellationToken.None);
        await chatClient.WaitUntilFirstCallEnteredAsync(CancellationToken.None);
        await firstIteration.WaitAsync(TimeSpan.FromSeconds(1));

        var timedOut = supervisor.Snapshot().Single();
        Assert.AreEqual(NpcAutonomyLoopState.Paused, timedOut.AutonomyLoopState);
        Assert.AreEqual(NpcAutonomyExitReason.LlmTurnTimeout.ToString(), timedOut.PauseReason);
        Assert.AreEqual(1, chatClient.CompleteWithToolsCalls);
        await using (var recoveredSlot = await budget.TryAcquireLlmSlotAsync("external", CancellationToken.None))
        {
            Assert.IsNotNull(recoveredSlot, "The timed-out autonomy turn must release the LLM slot so later dispatches can retry.");
        }

        await Task.Delay(25);
        await service.RunOneIterationAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
        await chatClient.WaitForCallCountAsync(2, TimeSpan.FromSeconds(1));

        var retried = supervisor.Snapshot().Single();
        Assert.AreEqual(
            NpcAutonomyLoopState.Running,
            retried.AutonomyLoopState,
            $"Retry left loop paused. reason={retried.PauseReason}; nextWake={retried.Controller.NextWakeAtUtc:o}; calls={chatClient.CompleteWithToolsCalls}");
        Assert.AreEqual(2, chatClient.CompleteWithToolsCalls);
    }

    [TestMethod]
    public async Task DispatchOneIterationAsync_WhenWorkerIsBusy_CoalescesQueuedDispatchesPerNpc()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new BlockFirstChatClient("I will wait near the library.");
        var adapter = CreateAdapter("haley");
        var supervisor = new NpcRuntimeSupervisor();
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"]);

        await service.DispatchOneIterationAsync(CancellationToken.None);
        await chatClient.WaitUntilFirstCallEnteredAsync(CancellationToken.None);

        await Task.WhenAll(
            service.DispatchOneIterationAsync(CancellationToken.None),
            service.DispatchOneIterationAsync(CancellationToken.None));

        chatClient.ReleaseFirstCall();
        await chatClient.WaitForCallCountAsync(2, TimeSpan.FromSeconds(1));
        await Task.Delay(150);

        Assert.AreEqual(
            2,
            chatClient.CompleteWithToolsCalls,
            "A slow NPC worker should keep at most one queued autonomy follow-up; older queued dispatches must coalesce instead of accumulating without bound.");
    }

    [TestMethod]
    public async Task DispatchOneIterationAsync_WhenWorkerIsBusy_MergesQueuedEventCursorsWithoutInjectingEvents()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new BlockFirstChatClient("I will wait near the library.");
        var adapter = new FakeGameAdapter(
            new FakeCommandService(),
            new DynamicQueryService(),
            new ScriptedEventSource(
                new GameEventBatch(
                    [
                        new GameEventRecord("evt-1", "time_changed", "haley", DateTime.UtcNow, "Haley noticed the first clock tick.", Sequence: 1)
                    ],
                    new GameEventCursor("evt-1", 1)),
                new GameEventBatch(
                    [
                        new GameEventRecord("evt-2", "time_changed", "haley", DateTime.UtcNow.AddSeconds(1), "Haley noticed the second clock tick.", Sequence: 2)
                    ],
                    new GameEventCursor("evt-2", 2)),
                new GameEventBatch(
                    [
                        new GameEventRecord("evt-3", "time_changed", "haley", DateTime.UtcNow.AddSeconds(2), "Haley noticed the third clock tick.", Sequence: 3)
                    ],
                    new GameEventCursor("evt-3", 3))));
        var supervisor = new NpcRuntimeSupervisor();
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"]);

        await service.DispatchOneIterationAsync(CancellationToken.None);
        await chatClient.WaitUntilFirstCallEnteredAsync(CancellationToken.None);

        await Task.WhenAll(
            service.DispatchOneIterationAsync(CancellationToken.None),
            service.DispatchOneIterationAsync(CancellationToken.None));

        chatClient.ReleaseFirstCall();
        await chatClient.WaitForCallCountAsync(2, TimeSpan.FromSeconds(1));

        var secondRequest = chatClient.GetCapturedRequest(2);
        Assert.IsFalse(secondRequest.Contains("Haley noticed the first clock tick.", StringComparison.Ordinal));
        Assert.IsFalse(secondRequest.Contains("Haley noticed the second clock tick.", StringComparison.Ordinal));
        Assert.IsFalse(secondRequest.Contains("Haley noticed the third clock tick.", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task DispatchOneIterationAsync_WhenBurstTriggeredWhileWorkerIsBusy_KeepsOnlyOneFollowUpTick()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new BlockFirstChatClient("I will wait near the library.");
        var adapter = CreateAdapter("haley");
        var supervisor = new NpcRuntimeSupervisor();
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"]);

        await service.DispatchOneIterationAsync(CancellationToken.None);
        await chatClient.WaitUntilFirstCallEnteredAsync(CancellationToken.None);

        var burst = Enumerable.Range(0, 25)
            .Select(_ => service.DispatchOneIterationAsync(CancellationToken.None))
            .ToArray();
        await Task.WhenAll(burst);

        chatClient.ReleaseFirstCall();
        await chatClient.WaitForCallCountAsync(2, TimeSpan.FromSeconds(1));
        await Task.Delay(200);

        Assert.AreEqual(
            2,
            chatClient.CompleteWithToolsCalls,
            "Even after many host wake-ups while the worker is still busy, the same NPC should keep only one merged follow-up tick instead of rebuilding an ever-growing backlog.");
    }

    [TestMethod]
    public async Task Stop_WhenWorkerIsInFlight_WaitsForWorkerShutdown()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new BlockingChatClient("I will wait near the library.");
        var adapter = CreateAdapter("haley");
        var supervisor = new NpcRuntimeSupervisor();
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"]);

        await service.DispatchOneIterationAsync(CancellationToken.None);
        await chatClient.WaitUntilEnteredAsync(CancellationToken.None);

        var workerTask = GetTrackerWorkerTask(service, "haley");
        Assert.IsFalse(workerTask.IsCompleted);

        service.Stop();

        Assert.IsTrue(workerTask.IsCompleted, "Stop should not return before in-flight NPC workers observe cancellation and exit.");
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WithMultipleEnabledNpc_CreatesDedicatedWorkerTaskPerNpc()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("I will wait near the library.");
        var adapter = CreateAdapter("haley");
        var supervisor = new NpcRuntimeSupervisor();
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley", "penny"]);

        await service.RunOneIterationAsync(CancellationToken.None);

        var haleyWorker = GetTrackerWorkerTask(service, "haley");
        var pennyWorker = GetTrackerWorkerTask(service, "penny");

        Assert.AreNotSame(haleyWorker, pennyWorker);
        Assert.IsFalse(haleyWorker.IsCompleted, "Haley should keep her dedicated runtime worker alive after the first host dispatch.");
        Assert.IsFalse(pennyWorker.IsCompleted, "Penny should keep her dedicated runtime worker alive after the first host dispatch.");
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenLlmSlotBecomesAvailable_RetriesPausedNpcOnNextIteration()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("I will wait near the library.");
        var adapter = CreateAdapter("haley");
        var supervisor = new NpcRuntimeSupervisor();
        var budget = new NpcAutonomyBudget(new NpcAutonomyBudgetOptions(MaxToolIterations: 2, MaxConcurrentLlmRequests: 1, MaxRestartsPerScene: 2));
        await using var heldSlot = await budget.TryAcquireLlmSlotAsync("external", CancellationToken.None);
        Assert.IsNotNull(heldSlot);

        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"],
            budget: budget);

        await service.RunOneIterationAsync(CancellationToken.None);

        var paused = supervisor.Snapshot().Single();
        Assert.AreEqual(NpcAutonomyLoopState.Paused, paused.AutonomyLoopState);
        Assert.AreEqual(NpcAutonomyExitReason.LlmConcurrencyLimit.ToString(), paused.PauseReason);
        Assert.IsNull(paused.LastAutomaticTickAtUtc);
        Assert.AreEqual(0, chatClient.CompleteWithToolsCalls);

        await heldSlot.DisposeAsync();
        await service.RunOneIterationAsync(CancellationToken.None);

        var resumed = supervisor.Snapshot().Single();
        Assert.AreEqual(NpcAutonomyLoopState.Running, resumed.AutonomyLoopState);
        Assert.IsTrue(resumed.LastAutomaticTickAtUtc.HasValue);
        Assert.AreEqual(1, chatClient.CompleteWithToolsCalls);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenPendingActionIsRunning_PausesWithoutStartingNewChat()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("unused");
        var commands = new ScriptedCommandService(
            statusSequence:
            [
                new GameCommandStatus("cmd-1", "haley", "move", StardewCommandStatuses.Running, 0.5, null, null)
            ]);
        var adapter = new FakeGameAdapter(
            commands,
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley is walking.",
                ["location=Town"])),
            new FakeEventSource([]));
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var driver = await supervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        await driver.SetPendingWorkItemAsync(
            new NpcRuntimePendingWorkItemSnapshot("work-1", "move", "cmd-1", StardewCommandStatuses.Queued, DateTime.UtcNow),
            CancellationToken.None);
        await driver.SetActionSlotAsync(
            new NpcRuntimeActionSlotSnapshot("action", "work-1", "cmd-1", "trace-1", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(1)),
            CancellationToken.None);
        var service = CreateService(discovery, _ => adapter, chatClient, supervisor, enabledNpcIds: ["haley"]);

        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(0, chatClient.CompleteWithToolsCalls);
        var snapshot = supervisor.Snapshot().Single();
        Assert.AreEqual(NpcAutonomyLoopState.Paused, snapshot.AutonomyLoopState);
        Assert.AreEqual("command_running", snapshot.PauseReason);
        Assert.AreEqual(StardewCommandStatuses.Running, snapshot.Controller.PendingWorkItem?.Status);
        Assert.IsNotNull(snapshot.Controller.ActionSlot);
        Assert.AreEqual(1, commands.StatusCalls);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenPendingActionCompletes_ReleasesClaimAndClearsControllerState()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("unused");
        var commands = new ScriptedCommandService(
            statusSequence:
            [
                new GameCommandStatus("cmd-1", "haley", "move", StardewCommandStatuses.Completed, 1, null, null)
            ]);
        var coordination = new WorldCoordinationService(new ResourceClaimRegistry());
        var targetTile = new ClaimedTile("Town", 42, 17);
        var claimed = coordination.TryClaimMove("work-1", "haley", "trace-1", targetTile, targetTile, "idem-1");
        Assert.IsTrue(claimed.Accepted);

        var adapter = new FakeGameAdapter(
            commands,
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley is arriving.",
                ["location=Town"])),
            new FakeEventSource([]));
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var driver = await supervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        await driver.SetPendingWorkItemAsync(
            new NpcRuntimePendingWorkItemSnapshot("work-1", "move", "cmd-1", StardewCommandStatuses.Running, DateTime.UtcNow),
            CancellationToken.None);
        await driver.SetActionSlotAsync(
            new NpcRuntimeActionSlotSnapshot("action", "work-1", "cmd-1", "trace-1", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(1)),
            CancellationToken.None);
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"],
            worldCoordination: coordination);

        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(0, chatClient.CompleteWithToolsCalls);
        var snapshot = supervisor.Snapshot().Single();
        Assert.IsNull(snapshot.Controller.PendingWorkItem);
        Assert.IsNull(snapshot.Controller.ActionSlot);
        Assert.IsTrue(
            coordination.TryClaimMove("work-2", "penny", "trace-2", targetTile, targetTile, "idem-2").Accepted,
            "Completed actions must release their short resource claim so another NPC can move into the tile.");
        Assert.AreEqual(1, commands.StatusCalls);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenExpiredPendingActionAlreadyCompleted_DoesNotCancelCompletedCommand()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("unused");
        var commands = new ScriptedCommandService(
            statusSequence:
            [
                new GameCommandStatus("cmd-1", "haley", "move", StardewCommandStatuses.Completed, 1, null, null)
            ]);
        var adapter = new FakeGameAdapter(
            commands,
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley has reached the beach.",
                ["location=Beach"])),
            new FakeEventSource([]));
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var driver = await supervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        await driver.SetPendingWorkItemAsync(
            new NpcRuntimePendingWorkItemSnapshot("work-1", "move", "cmd-1", StardewCommandStatuses.Running, DateTime.UtcNow.AddMinutes(-2)),
            CancellationToken.None);
        await driver.SetActionSlotAsync(
            new NpcRuntimeActionSlotSnapshot("action", "work-1", "cmd-1", "trace-1", DateTime.UtcNow.AddMinutes(-2), DateTime.UtcNow.AddSeconds(-1)),
            CancellationToken.None);
        var service = CreateService(discovery, _ => adapter, chatClient, supervisor, enabledNpcIds: ["haley"]);

        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(1, commands.StatusCalls);
        Assert.AreEqual(0, commands.CancelRequests.Count);
        var snapshot = supervisor.Snapshot().Single();
        Assert.IsNull(snapshot.Controller.PendingWorkItem);
        Assert.IsNull(snapshot.Controller.ActionSlot);
        Assert.AreEqual(StardewCommandStatuses.Completed, snapshot.Controller.LastTerminalCommandStatus?.Status);
        Assert.IsNull(snapshot.Controller.LastTerminalCommandStatus?.ErrorCode);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenPendingActionEndsBlocked_SurfacesTerminalStatusAndWritesRuntimeEvidence()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("unused");
        var commands = new ScriptedCommandService(
            statusSequence:
            [
                new GameCommandStatus("cmd-1", "haley", "move", StardewCommandStatuses.Blocked, 1, "path_blocked", "path_blocked")
            ]);
        var adapter = new FakeGameAdapter(
            commands,
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley is blocked while moving.",
                ["location=Town"])),
            new FakeEventSource([]));
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var driver = await supervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        await driver.SetPendingWorkItemAsync(
            new NpcRuntimePendingWorkItemSnapshot("work-1", "move", "cmd-1", StardewCommandStatuses.Running, DateTime.UtcNow),
            CancellationToken.None);
        await driver.SetActionSlotAsync(
            new NpcRuntimeActionSlotSnapshot("action", "work-1", "cmd-1", "trace-1", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(1)),
            CancellationToken.None);
        var service = CreateService(discovery, _ => adapter, chatClient, supervisor, enabledNpcIds: ["haley"]);

        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(0, chatClient.CompleteWithToolsCalls);
        var snapshot = supervisor.Snapshot().Single();
        Assert.AreEqual(StardewCommandStatuses.Blocked, snapshot.Controller.LastTerminalCommandStatus?.Status);
        Assert.IsTrue(supervisor.TryGetTaskView(binding.Descriptor.SessionId, out var taskView));
        Assert.IsNotNull(taskView);
        Assert.AreEqual(0, taskView.ActiveSnapshot.Todos.Count, "Background service must not mutate todo truth from terminal command status.");

        var logPath = Path.Combine(
            _tempDir,
            "runtime",
            "stardew",
            "games",
            "stardew-valley",
            "saves",
            "save-42",
            "npc",
            "haley",
            "profiles",
            "default",
            "activity",
            "runtime.jsonl");
        var records = ReadRuntimeLogRecords(logPath);
        Assert.IsTrue(records.Any(record =>
            record.GetProperty("actionType").GetString() == "task_continuity" &&
            record.GetProperty("target").GetString() == "command_terminal" &&
            record.GetProperty("stage").GetString() == "terminal" &&
            record.GetProperty("result").GetString() == StardewCommandStatuses.Blocked &&
            record.TryGetProperty("commandId", out var commandId) &&
            commandId.GetString() == "cmd-1"),
            "Terminal blocked command truth must be visible as a structured append-only runtime event.");
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenPendingActionHasNoCommandId_KeepsControllerStateAndClaimUntilTimeout()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("unused");
        var coordination = new WorldCoordinationService(new ResourceClaimRegistry());
        var targetTile = new ClaimedTile("Town", 42, 17);
        var claimed = coordination.TryClaimMove("work-1", "haley", "trace-1", targetTile, targetTile, "idem-1");
        Assert.IsTrue(claimed.Accepted);

        var adapter = new FakeGameAdapter(
            new FakeCommandService(),
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley is waiting for the bridge to confirm her move.",
                ["location=Town"])),
            new FakeEventSource([]));
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var driver = await supervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        await driver.SetPendingWorkItemAsync(
            new NpcRuntimePendingWorkItemSnapshot("work-1", "move", null, "submitting", DateTime.UtcNow),
            CancellationToken.None);
        await driver.SetActionSlotAsync(
            new NpcRuntimeActionSlotSnapshot("action", "work-1", null, "trace-1", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(1)),
            CancellationToken.None);
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"],
            worldCoordination: coordination);

        await service.RunOneIterationAsync(CancellationToken.None);

        var snapshot = supervisor.Snapshot().Single();
        Assert.AreEqual(NpcAutonomyLoopState.Paused, snapshot.AutonomyLoopState);
        Assert.AreEqual("command_submitting", snapshot.PauseReason);
        Assert.IsNotNull(snapshot.Controller.PendingWorkItem);
        Assert.IsNotNull(snapshot.Controller.ActionSlot);
        Assert.IsFalse(
            coordination.TryClaimMove("work-2", "penny", "trace-2", targetTile, targetTile, "idem-2").Accepted,
            "An action that may already be accepted by the bridge must keep its short claim until timeout or explicit terminal recovery.");
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenActionSlotTimesOut_RecordsTimeoutFactForNextTick()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CapturingChatClient(
            """
            {
              "action": "wait",
              "reason": "recover after timeout",
              "waitReason": "I will reconsider after the previous action timed out."
            }
            """);
        var commands = new ScriptedCommandService(
            statusSequence:
            [
                new GameCommandStatus("cmd-1", "haley", "move", StardewCommandStatuses.Running, 1, null, null)
            ]);
        var coordination = new WorldCoordinationService(new ResourceClaimRegistry());
        var targetTile = new ClaimedTile("Town", 42, 17);
        Assert.IsTrue(coordination.TryClaimMove("work-1", "haley", "trace-1", targetTile, targetTile, "idem-1").Accepted);
        var adapter = new FakeGameAdapter(
            commands,
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley is recovering after a stale action.",
                ["location=Town"])),
            new FakeEventSource([]));
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var driver = await supervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        await driver.SetPendingWorkItemAsync(
            new NpcRuntimePendingWorkItemSnapshot("work-1", "move", "cmd-1", StardewCommandStatuses.Running, DateTime.UtcNow),
            CancellationToken.None);
        await driver.SetActionSlotAsync(
            new NpcRuntimeActionSlotSnapshot("action", "work-1", "cmd-1", "trace-1", DateTime.UtcNow.AddMinutes(-2), DateTime.UtcNow.AddMinutes(-1)),
            CancellationToken.None);
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"],
            worldCoordination: coordination);

        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(1, commands.CancelRequests.Count);
        var timeoutSnapshot = supervisor.Snapshot().Single();
        Assert.IsNull(timeoutSnapshot.Controller.PendingWorkItem);
        Assert.IsNull(timeoutSnapshot.Controller.ActionSlot);
        Assert.IsNull(timeoutSnapshot.Controller.NextWakeAtUtc, "Timeout cleanup must not choose a fixed restart cooldown for the parent.");
        Assert.AreEqual(StardewCommandStatuses.Cancelled, timeoutSnapshot.Controller.LastTerminalCommandStatus?.Status);
        Assert.AreEqual(StardewBridgeErrorCodes.ActionSlotTimeout, timeoutSnapshot.Controller.LastTerminalCommandStatus?.ErrorCode);
        Assert.IsTrue(
            coordination.TryClaimMove("work-2", "penny", "trace-2", targetTile, targetTile, "idem-2").Accepted,
            "Timed-out actions must release their short resource claim.");

        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(1, chatClient.CapturedRequests.Count);
        StringAssert.Contains(chatClient.CapturedRequests[0], "action_slot_timeout");

        var logPath = Path.Combine(
            _tempDir,
            "runtime",
            "stardew",
            "games",
            "stardew-valley",
            "saves",
            "save-42",
            "npc",
            "haley",
            "profiles",
            "default",
            "activity",
            "runtime.jsonl");
        var records = ReadRuntimeLogRecords(logPath);
        Assert.IsTrue(records.Any(record =>
            record.GetProperty("actionType").GetString() == "task_continuity" &&
            record.GetProperty("target").GetString() == "action_slot_timeout" &&
            record.GetProperty("stage").GetString() == "terminal" &&
            record.TryGetProperty("commandId", out var commandId) &&
            commandId.GetString() == "cmd-1"),
            "The timeout must be persisted as a structured runtime fact.");
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenActionSlotTimesOutWithoutCommandId_ClearsSlotAndRecordsFact()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("unused");
        var coordination = new WorldCoordinationService(new ResourceClaimRegistry());
        var targetTile = new ClaimedTile("Town", 42, 17);
        Assert.IsTrue(coordination.TryClaimMove("work-1", "haley", "trace-1", targetTile, targetTile, "idem-1").Accepted);
        var adapter = new FakeGameAdapter(
            new FakeCommandService(),
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley is recovering after a stale local action slot.",
                ["location=Town"])),
            new FakeEventSource([]));
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var driver = await supervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        await driver.SetPendingWorkItemAsync(
            new NpcRuntimePendingWorkItemSnapshot("work-1", "move", null, "submitting", DateTime.UtcNow),
            CancellationToken.None);
        await driver.SetActionSlotAsync(
            new NpcRuntimeActionSlotSnapshot("action", "work-1", null, "trace-1", DateTime.UtcNow.AddMinutes(-2), DateTime.UtcNow.AddMinutes(-1)),
            CancellationToken.None);
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"],
            worldCoordination: coordination);

        await service.RunOneIterationAsync(CancellationToken.None);

        var snapshot = supervisor.Snapshot().Single();
        Assert.IsNull(snapshot.Controller.PendingWorkItem);
        Assert.IsNull(snapshot.Controller.ActionSlot);
        Assert.IsNull(snapshot.Controller.NextWakeAtUtc, "A commandless timeout must not synthesize a fixed RetryAfterUtc.");
        Assert.AreEqual(StardewCommandStatuses.Cancelled, snapshot.Controller.LastTerminalCommandStatus?.Status);
        Assert.AreEqual(StardewBridgeErrorCodes.ActionSlotTimeout, snapshot.Controller.LastTerminalCommandStatus?.ErrorCode);
        Assert.IsTrue(coordination.TryClaimMove("work-2", "penny", "trace-2", targetTile, targetTile, "idem-2").Accepted);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WithDelegatedMoveTarget_SubmitsHostMoveWithoutLocalExecutor()
    {
        var discovery = CreateDiscovery("save-42");
        var delegationClient = new CapturingStreamingChatClient();
        var commands = new ScriptedCommandService(
            [
                new GameCommandStatus("cmd-active", "haley", "move", StardewCommandStatuses.Running, 0.5, null, null)
            ]);
        var adapter = new FakeGameAdapter(
            commands,
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley has a private-chat host task submission.",
                ["location=Town"])),
            new FakeEventSource([]));
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var driver = await supervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        await driver.EnqueueIngressWorkItemAsync(
            new NpcRuntimeIngressWorkItemSnapshot(
                "ingress-delegate-1",
                "stardew_host_task_submission",
                "queued",
                DateTime.UtcNow,
                "idem-delegate-1",
                "trace-delegate-1",
                new()
                {
                    ["action"] = "move",
                    ["reason"] = "meet the player at the beach now",
                    ["rootTodoId"] = "meet-beach-now",
                    ["target"] = new JsonObject
                    {
                        ["locationName"] = "Beach",
                        ["x"] = 32,
                        ["y"] = 34,
                        ["source"] = "map-skill:stardew.navigation.poi.beach-shoreline",
                        ["facingDirection"] = 2
                    }
                }),
            CancellationToken.None);
        var service = CreateService(
            discovery,
            _ => adapter,
            new CountingChatClient("unused"),
            supervisor,
            enabledNpcIds: ["haley"],
            delegationChatClient: delegationClient);

        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(0, delegationClient.StructuredStreamCalls, "Private-chat move with a mechanical target must not call the delegation/local-executor model.");
        Assert.AreEqual(1, commands.Submitted.Count);
        var submitted = commands.Submitted.Single();
        Assert.AreEqual(GameActionType.Move, submitted.Type);
        Assert.AreEqual("tile", submitted.Target.Kind);
        Assert.AreEqual("Beach", submitted.Target.LocationName);
        Assert.AreEqual(new GameTile(32, 34), submitted.Target.Tile);
        Assert.AreEqual("meet the player at the beach now", submitted.Reason);
        Assert.AreEqual("map-skill:stardew.navigation.poi.beach-shoreline", submitted.Payload?["targetSource"]?.GetValue<string>());
        Assert.AreEqual(2, submitted.Payload?["facingDirection"]?.GetValue<int>());
        Assert.AreEqual(0, supervisor.Snapshot().Single().Controller.IngressWorkItems.Count);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WithDelegatedMoveAndBlockedClosureGuard_SubmitsMoveAsFreshPlayerRequest()
    {
        var discovery = CreateDiscovery("save-42");
        var commands = new ScriptedCommandService(
            [
                new GameCommandStatus("cmd-active", "haley", "move", StardewCommandStatuses.Running, 0.5, null, null)
            ]);
        var adapter = new FakeGameAdapter(
            commands,
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley has a private-chat host task submission from the player.",
                ["location=Town"])),
            new FakeEventSource([]));
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var driver = await supervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        await driver.SetActionChainGuardAsync(
            new NpcRuntimeActionChainGuardSnapshot(
                "chain-stale-closure",
                "blocked_until_closure",
                "legacy_closure_missing",
                true,
                "old-todo",
                "trace-old-private-chat",
                DateTime.UtcNow.AddMinutes(-20),
                DateTime.UtcNow.AddSeconds(-1),
                "move",
                "move:Town:42:17",
                ConsecutiveActions: 1,
                ConsecutiveFailures: 0,
                ConsecutiveSameActionFailures: 0,
                LastTerminalStatus: StardewCommandStatuses.Completed,
                LastReasonCode: null,
                ClosureMissingCount: 5,
                DeferredIngressAttempts: 0),
            CancellationToken.None);
        await driver.EnqueueIngressWorkItemAsync(
            new NpcRuntimeIngressWorkItemSnapshot(
                "ingress-delegate-blocked-chain",
                "stardew_host_task_submission",
                "queued",
                DateTime.UtcNow,
                "idem-delegate-blocked-chain",
                "trace-delegate-blocked-chain",
                new()
                {
                    ["action"] = "move",
                    ["reason"] = "meet the player at the beach now",
                    ["rootTodoId"] = "meet-beach-now",
                    ["target"] = new JsonObject
                    {
                        ["locationName"] = "Beach",
                        ["x"] = 32,
                        ["y"] = 34,
                        ["source"] = "map-skill:stardew.navigation.poi.beach-shoreline"
                    }
                }),
            CancellationToken.None);
        var service = CreateService(
            discovery,
            _ => adapter,
            new CountingChatClient("unused"),
            supervisor,
            enabledNpcIds: ["haley"]);

        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(1, commands.Submitted.Count);
        var submitted = commands.Submitted.Single();
        Assert.AreEqual(GameActionType.Move, submitted.Type);
        Assert.AreEqual("Beach", submitted.Target.LocationName);
        Assert.AreEqual(new GameTile(32, 34), submitted.Target.Tile);
        var snapshot = supervisor.Snapshot().Single().Controller;
        Assert.AreEqual(0, snapshot.IngressWorkItems.Count);
        Assert.AreEqual("open", snapshot.ActionChainGuard?.GuardStatus);
        Assert.IsFalse(snapshot.ActionChainGuard?.BlockedUntilClosure ?? true);
        Assert.AreEqual("trace-delegate-blocked-chain", snapshot.ActionChainGuard?.RootTraceId);
        Assert.AreEqual("move:Beach:32:34", snapshot.ActionChainGuard?.LastTargetKey);
        Assert.AreEqual(1, snapshot.ActionChainGuard?.ConsecutiveActions);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WithDelegatedMoveAndLegacyPathBlockedGuard_SubmitsMoveWhenSlotFree()
    {
        var discovery = CreateDiscovery("save-42");
        var commands = new ScriptedCommandService(
            [
                new GameCommandStatus("cmd-active", "haley", "move", StardewCommandStatuses.Running, 0.5, null, null)
            ]);
        var adapter = new FakeGameAdapter(
            commands,
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley has a private-chat host task submission from the player.",
                ["location=Town"])),
            new FakeEventSource([]));
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var driver = await supervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        await driver.SetActionChainGuardAsync(
            new NpcRuntimeActionChainGuardSnapshot(
                "chain-path-blocked",
                "blocked_until_closure",
                StardewBridgeErrorCodes.PathBlocked,
                true,
                "todo-path-blocked",
                "trace-path-blocked",
                DateTime.UtcNow.AddMinutes(-20),
                DateTime.UtcNow.AddSeconds(-1),
                "move",
                "move:Town:42:17",
                ConsecutiveActions: 1,
                ConsecutiveFailures: 2,
                ConsecutiveSameActionFailures: 2,
                LastTerminalStatus: StardewCommandStatuses.Blocked,
                LastReasonCode: StardewBridgeErrorCodes.PathBlocked,
                ClosureMissingCount: 0,
                DeferredIngressAttempts: 0),
            CancellationToken.None);
        await driver.EnqueueIngressWorkItemAsync(
            new NpcRuntimeIngressWorkItemSnapshot(
                "ingress-delegate-path-blocked-chain",
                "stardew_host_task_submission",
                "queued",
                DateTime.UtcNow,
                "idem-delegate-path-blocked-chain",
                "trace-delegate-path-blocked-chain",
                new()
                {
                    ["action"] = "move",
                    ["reason"] = "meet the player at the beach now",
                    ["rootTodoId"] = "meet-beach-now",
                    ["target"] = new JsonObject
                    {
                        ["locationName"] = "Beach",
                        ["x"] = 32,
                        ["y"] = 34,
                        ["source"] = "map-skill:stardew.navigation.poi.beach-shoreline"
                    }
                }),
            CancellationToken.None);
        var service = CreateService(
            discovery,
            _ => adapter,
            new CountingChatClient("unused"),
            supervisor,
            enabledNpcIds: ["haley"]);

        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(1, commands.Submitted.Count);
        var submitted = commands.Submitted.Single();
        Assert.AreEqual(GameActionType.Move, submitted.Type);
        Assert.AreEqual("Beach", submitted.Target.LocationName);
        Assert.AreEqual(new GameTile(32, 34), submitted.Target.Tile);
        var snapshot = supervisor.Snapshot().Single().Controller;
        Assert.AreEqual(0, snapshot.IngressWorkItems.Count);
        Assert.AreEqual("open", snapshot.ActionChainGuard?.GuardStatus);
        Assert.IsFalse(snapshot.ActionChainGuard?.BlockedUntilClosure ?? true);
        Assert.IsNull(snapshot.ActionChainGuard?.BlockedReasonCode);
        Assert.AreEqual("trace-delegate-path-blocked-chain", snapshot.ActionChainGuard?.RootTraceId);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WithDelegatedMoveAfterPrivateChatReplyDisplayed_SubmitsMoveWithoutWaitingForClose()
    {
        var discovery = CreateDiscovery("save-42");
        var delegationClient = new CapturingStreamingChatClient(
            new StreamEvent.ToolUseComplete(
                "call-skill",
                "skill_view",
                Json("""{"name":"stardew-navigation","file_path":"references/poi/beach-shoreline.md"}""")),
            new StreamEvent.ToolUseComplete(
                "call-nav",
                "stardew_navigate_to_tile",
                Json("""{"locationName":"Beach","x":32,"y":34,"source":"map-skill:stardew.navigation.poi.beach-shoreline","reason":"meet the player at the beach now"}""")));
        var commands = new ScriptedCommandService(
            [
                new GameCommandStatus("cmd-active", "haley", "move", StardewCommandStatuses.Running, 0.5, null, null)
            ]);
        var adapter = new FakeGameAdapter(
            commands,
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley has a private-chat host task submission.",
                ["location=Town"])),
            new ScriptedEventSource(
                new GameEventBatch(
                    [
                        new GameEventRecord(
                            "evt-reply-displayed-1",
                            "private_chat_reply_displayed",
                            "Haley",
                            DateTime.UtcNow,
                            "Haley private chat reply is ready for the next player click.",
                            Payload: new JsonObject { ["conversationId"] = "conversation-beach" },
                            Sequence: 1)
                    ],
                    new GameEventCursor("evt-reply-displayed-1", 1))));
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var driver = await supervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        await driver.EnqueueIngressWorkItemAsync(
            new NpcRuntimeIngressWorkItemSnapshot(
                "ingress-delegate-wait-reply",
                "stardew_host_task_submission",
                "queued",
                DateTime.UtcNow,
                "idem-delegate-wait-reply",
                "trace-delegate-wait-reply",
                new()
                {
                    ["action"] = "move",
                    ["reason"] = "meet the player at the beach now",
                    ["rootTodoId"] = "meet-beach-now",
                    ["target"] = new JsonObject
                    {
                        ["locationName"] = "Beach",
                        ["x"] = 32,
                        ["y"] = 34,
                        ["source"] = "map-skill:stardew.navigation.poi.beach-shoreline"
                    },
                    ["conversationId"] = "conversation-beach"
                }),
            CancellationToken.None);
        var service = CreateService(
            discovery,
            _ => adapter,
            new CountingChatClient("unused"),
            supervisor,
            enabledNpcIds: ["haley"],
            delegationChatClient: delegationClient);

        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(0, delegationClient.StructuredStreamCalls);
        Assert.AreEqual(1, commands.Submitted.Count);
        Assert.AreEqual("meet-beach-now", commands.Submitted.Single().Payload?["rootTodoId"]?.GetValue<string>());
        Assert.AreEqual("conversation-beach", commands.Submitted.Single().Payload?["conversationId"]?.GetValue<string>());
        Assert.AreEqual(0, supervisor.Snapshot().Single().Controller.IngressWorkItems.Count);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_DelegatedIngress_WhenSlotBusy_DefersWithAttemptCountAndWake()
    {
        var discovery = CreateDiscovery("save-42");
        var commands = new ScriptedCommandService(
            [
                new GameCommandStatus("cmd-active", "haley", "move", StardewCommandStatuses.Running, 0.5, null, null)
            ]);
        var adapter = new FakeGameAdapter(
            commands,
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley has queued private-chat movement while another action is active.",
                ["location=Town"])),
            new FakeEventSource([]));
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var driver = await supervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        await driver.SetActionSlotAsync(
            new NpcRuntimeActionSlotSnapshot(
                "action",
                "work-active",
                "cmd-active",
                "trace-active",
                DateTime.UtcNow,
                DateTime.UtcNow.AddMinutes(1)),
            CancellationToken.None);
        await driver.EnqueueIngressWorkItemAsync(
            new NpcRuntimeIngressWorkItemSnapshot(
                "ingress-delegate-busy",
                "stardew_host_task_submission",
                "queued",
                DateTime.UtcNow,
                "idem-delegate-busy",
                "trace-delegate-busy",
                new()
                {
                    ["action"] = "move",
                    ["reason"] = "continue the player promise",
                    ["target"] = new JsonObject
                    {
                        ["locationName"] = "Beach",
                        ["x"] = 32,
                        ["y"] = 34,
                        ["source"] = "map-skill:test"
                    }
                }),
            CancellationToken.None);
        var service = CreateService(
            discovery,
            _ => adapter,
            new CountingChatClient("unused"),
            supervisor,
            enabledNpcIds: ["haley"]);

        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(0, commands.Submitted.Count);
        var snapshot = supervisor.Snapshot().Single().Controller;
        Assert.IsNotNull(snapshot.ActionSlot);
        var ingress = snapshot.IngressWorkItems.Single();
        Assert.AreEqual("ingress-delegate-busy", ingress.WorkItemId);
        Assert.AreEqual("deferred", ingress.Status);
        Assert.AreEqual(1, ingress.DeferredAttempts);
        Assert.IsNotNull(snapshot.NextWakeAtUtc);
        var logPath = Path.Combine(
            _tempDir,
            "runtime",
            "stardew",
            "games",
            "stardew-valley",
            "saves",
            "save-42",
            "npc",
            "haley",
            "profiles",
            "default",
            "activity",
            "runtime.jsonl");
        var records = ReadRuntimeLogRecords(logPath);
        Assert.IsTrue(records.Any(record =>
            record.GetProperty("actionType").GetString() == "ingress" &&
            record.GetProperty("target").GetString() == "stardew_host_task_submission" &&
            record.GetProperty("stage").GetString() == "deferred" &&
            record.GetProperty("result").GetString() == "host_task_submission_deferred:action_slot_busy" &&
            record.TryGetProperty("commandId", out var commandId) &&
            commandId.GetString() == "ingress-delegate-busy"));
    }

    [TestMethod]
    public async Task RunOneIterationAsync_HostTaskSubmissionIngress_WhenDeferBudgetExceededWithoutActiveSlot_BlocksAndRemovesIngress()
    {
        var discovery = CreateDiscovery("save-42");
        var commands = new ScriptedCommandService(
            [
                new GameCommandStatus("cmd-active", "haley", "move", StardewCommandStatuses.Running, 0.5, null, null)
            ]);
        var adapter = new FakeGameAdapter(
            commands,
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley has stale host task submission movement ingress.",
                ["location=Town"])),
            new FakeEventSource([]));
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var driver = await supervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        await driver.EnqueueIngressWorkItemAsync(
            new NpcRuntimeIngressWorkItemSnapshot(
                "ingress-delegate-stale",
                "stardew_host_task_submission",
                "deferred",
                DateTime.UtcNow,
                "idem-delegate-stale",
                "trace-delegate-stale",
                new()
                {
                    ["action"] = "move",
                    ["reason"] = "continue the player promise",
                    ["target"] = new JsonObject
                    {
                        ["locationName"] = "Beach",
                        ["x"] = 32,
                        ["y"] = 34,
                        ["source"] = "map-skill:test"
                    }
                },
                DeferredAttempts: 1),
            CancellationToken.None);
        var service = CreateService(
            discovery,
            _ => adapter,
            new CountingChatClient("unused"),
            supervisor,
            enabledNpcIds: ["haley"],
            budget: new NpcAutonomyBudget(new NpcAutonomyBudgetOptions(
                MaxToolIterations: 2,
                MaxConcurrentLlmRequests: 1,
                MaxRestartsPerScene: 2,
                ActionChainGuard: new NpcActionChainGuardOptions(MaxDeferredIngressAttempts: 1))));

        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(0, commands.Submitted.Count);
        var snapshot = supervisor.Snapshot().Single().Controller;
        Assert.AreEqual(0, snapshot.IngressWorkItems.Count);
        Assert.AreEqual(StardewCommandStatuses.Blocked, snapshot.LastTerminalCommandStatus?.Status);
        Assert.AreEqual(StardewBridgeErrorCodes.HostTaskSubmissionDeferredExceeded, snapshot.LastTerminalCommandStatus?.ErrorCode);
        Assert.AreEqual("ingress-delegate-stale", snapshot.LastTerminalCommandStatus?.CommandId);
        Assert.AreEqual("move", snapshot.LastTerminalCommandStatus?.Action);
        Assert.IsNotNull(snapshot.NextWakeAtUtc);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_DelegatedIngress_DeferBudgetExceededWithActiveSlot_DoesNotOverwriteActiveTerminal()
    {
        var discovery = CreateDiscovery("save-42");
        var commands = new ScriptedCommandService(
            [
                new GameCommandStatus("cmd-active", "haley", "move", StardewCommandStatuses.Running, 0.5, null, null)
            ]);
        var adapter = new FakeGameAdapter(
            commands,
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley has stale host task submission movement ingress while active command remains running.",
                ["location=Town"])),
            new FakeEventSource([]));
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var driver = await supervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        await driver.SetLastTerminalCommandStatusAsync(
            new GameCommandStatus("cmd-previous", "haley", "move", StardewCommandStatuses.Completed, 1, null, null),
            CancellationToken.None);
        await driver.SetActionSlotAsync(
            new NpcRuntimeActionSlotSnapshot(
                "action",
                "work-active",
                "cmd-active",
                "trace-active",
                DateTime.UtcNow,
                DateTime.UtcNow.AddMinutes(1)),
            CancellationToken.None);
        await driver.EnqueueIngressWorkItemAsync(
            new NpcRuntimeIngressWorkItemSnapshot(
                "ingress-delegate-stale-active",
                "stardew_host_task_submission",
                "deferred",
                DateTime.UtcNow,
                "idem-delegate-stale-active",
                "trace-delegate-stale-active",
                new()
                {
                    ["action"] = "move",
                    ["reason"] = "continue the player promise",
                    ["target"] = new JsonObject
                    {
                        ["locationName"] = "Beach",
                        ["x"] = 32,
                        ["y"] = 34,
                        ["source"] = "map-skill:test"
                    }
                },
                DeferredAttempts: 1),
            CancellationToken.None);
        var service = CreateService(
            discovery,
            _ => adapter,
            new CountingChatClient("unused"),
            supervisor,
            enabledNpcIds: ["haley"],
            budget: new NpcAutonomyBudget(new NpcAutonomyBudgetOptions(
                MaxToolIterations: 2,
                MaxConcurrentLlmRequests: 1,
                MaxRestartsPerScene: 2,
                ActionChainGuard: new NpcActionChainGuardOptions(MaxDeferredIngressAttempts: 1))));

        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(0, commands.Submitted.Count);
        var snapshot = supervisor.Snapshot().Single().Controller;
        Assert.AreEqual("cmd-active", snapshot.ActionSlot?.CommandId);
        Assert.AreEqual("cmd-previous", snapshot.LastTerminalCommandStatus?.CommandId);
        Assert.AreEqual(StardewCommandStatuses.Completed, snapshot.LastTerminalCommandStatus?.Status);
        var ingress = snapshot.IngressWorkItems.Single();
        Assert.AreEqual("blocked", ingress.Status);
        Assert.AreEqual(2, ingress.DeferredAttempts);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenClosureMissingDiagnostic_DoesNotCreateRepairWake()
    {
        var discovery = CreateDiscovery("save-42");
        var adapter = new FakeGameAdapter(
            new FakeCommandService(),
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley has just completed a host task submission action and must close the active todo.",
                ["location=Town"])),
            new FakeEventSource([]));
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var instance = await supervisor.GetOrStartAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        instance.TodoStore.Write(
            binding.Descriptor.SessionId,
            [new SessionTodoInput("meet-now", "Meet the player now", "in_progress")]);
        var driver = await supervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        await driver.SetLastTerminalCommandStatusAsync(
            new GameCommandStatus(
                "cmd-move-1",
                "haley",
                "move",
                StardewCommandStatuses.Completed,
                1,
                null,
                null),
            CancellationToken.None);
        await driver.SetActionChainGuardAsync(
            new NpcRuntimeActionChainGuardSnapshot(
                "chain-closure-service",
                "open",
                null,
                false,
                "meet-now",
                "trace-root-1",
                new DateTime(2026, 5, 11, 7, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 11, 7, 1, 0, DateTimeKind.Utc),
                "move",
                "move:Town:42:17",
                ConsecutiveActions: 1,
                ConsecutiveFailures: 0,
                ConsecutiveSameActionFailures: 0,
                LastTerminalStatus: StardewCommandStatuses.Completed,
                LastReasonCode: null,
                ClosureMissingCount: 0,
                DeferredIngressAttempts: 0),
            CancellationToken.None);
        var chatClient = new CountingChatClient(
            [
                "到了。",
                "no-action: arrived and waiting for the player to speak first"
            ]);
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"],
            autonomyWakeInterval: TimeSpan.FromMinutes(5));

        await service.RunOneIterationAsync(CancellationToken.None);

        var snapshot = supervisor.Snapshot().Single().Controller;
        Assert.AreEqual(2, chatClient.CompleteWithToolsCalls);
        var logPath = Path.Combine(
            _tempDir,
            "runtime",
            "stardew",
            "games",
            "stardew-valley",
            "saves",
            "save-42",
            "npc",
            "haley",
            "profiles",
            "default",
            "activity",
            "runtime.jsonl");
        var records = File.Exists(logPath) ? ReadRuntimeLogRecords(logPath) : [];
        Assert.IsTrue(
            records.Any(record =>
                record.GetProperty("actionType").GetString() == "task_continuity" &&
                record.GetProperty("target").GetString() == "task_continuity_no_action"),
            "Expected task continuity self-check resolution. Records: " + string.Join(Environment.NewLine, records.Select(record => record.GetRawText())));
        Assert.IsFalse(
            records.Any(record =>
                record.GetProperty("actionType").GetString() == "task_continuity" &&
                record.GetProperty("target").GetString() == "task_continuity_unresolved"),
            "Self-check no-action should avoid unresolved closure diagnostics. Records: " + string.Join(Environment.NewLine, records.Select(record => record.GetRawText())));
        Assert.AreEqual(0, snapshot.ActionChainGuard?.ClosureMissingCount);
        Assert.IsNotNull(snapshot.NextWakeAtUtc);
        Assert.IsTrue(
            snapshot.NextWakeAtUtc!.Value >= DateTime.UtcNow.AddMinutes(4),
            $"Closure diagnostics should not create a short repair wake. nextWake={snapshot.NextWakeAtUtc:o}");
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WithMalformedHostTaskSubmissionIngress_DropsWithDiagnosticAndTerminalStatus()
    {
        var discovery = CreateDiscovery("save-42");
        var delegationClient = new CapturingStreamingChatClient();
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var driver = await supervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        await driver.EnqueueIngressWorkItemAsync(
            new NpcRuntimeIngressWorkItemSnapshot(
                "ingress-bad-1",
                "stardew_host_task_submission",
                "queued",
                DateTime.UtcNow,
                "idem-bad-1",
                "trace-bad-1",
                new()
                {
                    ["intentText"] = "go to the beach now"
                }),
            CancellationToken.None);
        var service = CreateService(
            discovery,
            _ => new FakeGameAdapter(
                new FakeCommandService(),
                new FakeQueryService(new GameObservation("haley", "stardew-valley", DateTime.UtcNow, "Haley has malformed ingress.", ["location=Town"])),
                new FakeEventSource([])),
            new CountingChatClient("unused"),
            supervisor,
            enabledNpcIds: ["haley"],
            delegationChatClient: delegationClient);

        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(0, delegationClient.StructuredStreamCalls);
        var snapshot = supervisor.Snapshot().Single().Controller;
        Assert.AreEqual(0, snapshot.IngressWorkItems.Count);
        Assert.AreEqual(StardewCommandStatuses.Blocked, snapshot.LastTerminalCommandStatus?.Status);
        Assert.AreEqual("missing_action_or_reason", snapshot.LastTerminalCommandStatus?.ErrorCode);
        Assert.AreEqual("host_task_submission", snapshot.LastTerminalCommandStatus?.Action);
        var logPath = Path.Combine(
            _tempDir,
            "runtime",
            "stardew",
            "games",
            "stardew-valley",
            "saves",
            "save-42",
            "npc",
            "haley",
            "profiles",
            "default",
            "activity",
            "runtime.jsonl");
        var records = ReadRuntimeLogRecords(logPath);
        Assert.IsTrue(records.Any(record =>
            record.GetProperty("actionType").GetString() == "ingress" &&
            record.GetProperty("target").GetString() == "stardew_host_task_submission" &&
            record.GetProperty("stage").GetString() == "malformed" &&
            record.GetProperty("result").GetString() == "missing_action_or_reason"));
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WithNonMoveHostTaskSubmission_BlocksWithoutDelegationFallback()
    {
        var discovery = CreateDiscovery("save-42");
        var delegationClient = new CapturingStreamingChatClient();
        var commands = new FakeCommandService();
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var driver = await supervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        await driver.EnqueueIngressWorkItemAsync(
            new NpcRuntimeIngressWorkItemSnapshot(
                "ingress-observe-1",
                "stardew_host_task_submission",
                "queued",
                DateTime.UtcNow,
                "idem-observe-1",
                "trace-observe-1",
                new()
                {
                    ["action"] = "observe",
                    ["reason"] = "look around the square"
                }),
            CancellationToken.None);
        var service = CreateService(
            discovery,
            _ => new FakeGameAdapter(
                commands,
                new FakeQueryService(new GameObservation("haley", "stardew-valley", DateTime.UtcNow, "Haley has unsupported host task submission ingress.", ["location=Town"])),
                new FakeEventSource([])),
            new CountingChatClient("unused"),
            supervisor,
            enabledNpcIds: ["haley"],
            delegationChatClient: delegationClient);

        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(0, delegationClient.StructuredStreamCalls, "Non-move host task submission must not trigger delegation/local-executor fallback.");
        Assert.AreEqual(0, commands.Submitted.Count, "Unsupported non-move host task submissions must not submit a bridge command.");
        var snapshot = supervisor.Snapshot().Single().Controller;
        Assert.AreEqual(0, snapshot.IngressWorkItems.Count);
        Assert.AreEqual(StardewCommandStatuses.Blocked, snapshot.LastTerminalCommandStatus?.Status);
        Assert.AreEqual("unsupported_host_task_submission_action", snapshot.LastTerminalCommandStatus?.ErrorCode);
        Assert.AreEqual("observe", snapshot.LastTerminalCommandStatus?.Action);

        var logPath = Path.Combine(
            _tempDir,
            "runtime",
            "stardew",
            "games",
            "stardew-valley",
            "saves",
            "save-42",
            "npc",
            "haley",
            "profiles",
            "default",
            "activity",
            "runtime.jsonl");
        var records = ReadRuntimeLogRecords(logPath);
        Assert.IsTrue(records.Any(record =>
            record.GetProperty("actionType").GetString() == "ingress" &&
            record.GetProperty("target").GetString() == "stardew_host_task_submission" &&
            record.GetProperty("stage").GetString() == "blocked" &&
            record.GetProperty("result").GetString() == "unsupported_host_task_submission_action:observe"));
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WithFutureWindowHostTaskSubmission_BlocksWithTerminalFact()
    {
        var discovery = CreateDiscovery("save-42");
        var delegationClient = new CapturingStreamingChatClient();
        var commands = new FakeCommandService();
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var driver = await supervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        await driver.EnqueueIngressWorkItemAsync(
            new NpcRuntimeIngressWorkItemSnapshot(
                "ingress-craft-1",
                "stardew_host_task_submission",
                "queued",
                DateTime.UtcNow,
                "idem-craft-1",
                "trace-craft-1",
                new()
                {
                    ["action"] = "craft",
                    ["reason"] = "make a snack"
                }),
            CancellationToken.None);
        var service = CreateService(
            discovery,
            _ => new FakeGameAdapter(
                commands,
                new FakeQueryService(new GameObservation("haley", "stardew-valley", DateTime.UtcNow, "Haley has future window host task submission ingress.", ["location=Town"])),
                new FakeEventSource([])),
            new CountingChatClient("unused"),
            supervisor,
            enabledNpcIds: ["haley"],
            delegationChatClient: delegationClient);

        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(0, delegationClient.StructuredStreamCalls);
        Assert.AreEqual(0, commands.Submitted.Count);
        var snapshot = supervisor.Snapshot().Single().Controller;
        Assert.AreEqual(0, snapshot.IngressWorkItems.Count);
        Assert.AreEqual(StardewCommandStatuses.Blocked, snapshot.LastTerminalCommandStatus?.Status);
        Assert.AreEqual("unsupported_host_task_submission_action", snapshot.LastTerminalCommandStatus?.ErrorCode);
        Assert.AreEqual("craft", snapshot.LastTerminalCommandStatus?.Action);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenPendingActionCanBeFoundByIdempotency_RebindsCommandAndPausesAsRunning()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("unused");
        var commands = new LookupCommandService(new GameCommandStatus(
            "cmd-lookup-1",
            "haley",
            "move",
            StardewCommandStatuses.Running,
            0.4,
            null,
            null,
            StartedAtUtc: DateTime.UtcNow.AddSeconds(-3),
            UpdatedAtUtc: DateTime.UtcNow,
            ElapsedMs: 3000,
            RetryAfterUtc: DateTime.UtcNow.AddSeconds(5)));
        var coordination = new WorldCoordinationService(new ResourceClaimRegistry());
        var targetTile = new ClaimedTile("Town", 42, 17);
        Assert.IsTrue(coordination.TryClaimMove("work-1", "haley", "trace-1", targetTile, targetTile, "idem-1").Accepted);
        var adapter = new FakeGameAdapter(
            commands,
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley is waiting for the bridge to confirm her move.",
                ["location=Town"])),
            new FakeEventSource([]));
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var driver = await supervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        await driver.SetPendingWorkItemAsync(
            new NpcRuntimePendingWorkItemSnapshot("work-1", "move", null, "submitting", DateTime.UtcNow, "idem-1"),
            CancellationToken.None);
        await driver.SetActionSlotAsync(
            new NpcRuntimeActionSlotSnapshot("action", "work-1", null, "trace-1", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(1)),
            CancellationToken.None);
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"],
            worldCoordination: coordination);

        await service.RunOneIterationAsync(CancellationToken.None);

        var snapshot = supervisor.Snapshot().Single();
        Assert.AreEqual(NpcAutonomyLoopState.Paused, snapshot.AutonomyLoopState);
        Assert.AreEqual("command_running", snapshot.PauseReason);
        Assert.AreEqual("cmd-lookup-1", snapshot.Controller.PendingWorkItem?.CommandId);
        Assert.AreEqual("cmd-lookup-1", snapshot.Controller.ActionSlot?.CommandId);
        Assert.AreEqual(1, commands.LookupCalls);
        Assert.AreEqual(0, commands.StatusCalls);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_RebuildUsesPersistedHostCursorInsteadOfPollingFromHead()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("I will wait near the library.");
        var events = new CursorAwareEventSource(
            new Dictionary<string, GameEventBatch?>
            {
                ["<root>"] = new GameEventBatch(
                    [
                        new GameEventRecord("evt-11", "time_changed", "haley", DateTime.UtcNow, "The clock advanced.", Sequence: 11)
                    ],
                    new GameEventCursor("evt-11", 14)),
                ["14"] = new GameEventBatch(
                    [
                        new GameEventRecord("evt-15", "time_changed", "haley", DateTime.UtcNow.AddSeconds(1), "The clock advanced again.", Sequence: 15)
                    ],
                    new GameEventCursor("evt-15", 18))
            });
        var adapter = new FakeGameAdapter(
            new FakeCommandService(),
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley is idle.",
                ["location=Town"])),
            events);

        var supervisor1 = new NpcRuntimeSupervisor();
        var service1 = CreateService(discovery, _ => adapter, chatClient, supervisor1, enabledNpcIds: ["haley"]);
        await service1.RunOneIterationAsync(CancellationToken.None);

        var supervisor2 = new NpcRuntimeSupervisor();
        var service2 = CreateService(discovery, _ => adapter, chatClient, supervisor2, enabledNpcIds: ["haley"]);
        await service2.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(2, events.SeenCursors.Count);
        Assert.IsNull(events.SeenCursors[0].Sequence);
        Assert.AreEqual(14L, events.SeenCursors[1].Sequence);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenPreviousFanoutFailed_ReplaysStagedBatchWithoutRepollingBridge()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("unused");
        var events = new CountingEventSource(
        [
            new GameEventRecord("evt-1", "time_changed", null, DateTime.UtcNow, "The clock advanced.", Sequence: 1),
        ]);
        var adapter = new FakeGameAdapter(
            new FakeCommandService(),
            new FakeQueryService(new GameObservation(
                "sam",
                "stardew-valley",
                DateTime.UtcNow,
                "Sam is idle.",
                ["location=Town"])),
            events);

        var supervisor1 = new NpcRuntimeSupervisor();
        var service1 = CreateService(discovery, _ => adapter, chatClient, supervisor1, enabledNpcIds: ["sam"]);
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => service1.RunOneIterationAsync(CancellationToken.None));

        var supervisor2 = new NpcRuntimeSupervisor();
        var service2 = CreateService(discovery, _ => adapter, chatClient, supervisor2, enabledNpcIds: ["sam"]);
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => service2.RunOneIterationAsync(CancellationToken.None));

        Assert.AreEqual(
            1,
            events.PollCalls,
            "The host must replay the staged batch after a fanout failure instead of polling the bridge head again.");
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenSaveChanges_RebuildsTrackerScopeForNewSave()
    {
        var discovery = new MutableDiscovery(CreateSnapshot("save-42"));
        var chatClient = new CountingChatClient("I will wait near the library.");
        var adapter = CreateAdapter("haley");
        var supervisor = new NpcRuntimeSupervisor();
        var service = CreateService(discovery, _ => adapter, chatClient, supervisor, enabledNpcIds: ["haley"]);

        await service.RunOneIterationAsync(CancellationToken.None);

        discovery.SetSnapshot(CreateSnapshot("save-99"));
        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual("save-99", GetTrackerSaveId(service, "haley"));
        Assert.AreEqual(1, supervisor.Snapshot().Count, "Save switch should not leave the old save runtime hanging in the supervisor snapshot.");
        Assert.AreEqual("save-99", supervisor.Snapshot().Single().SaveId);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenBridgeRebindsForSameSave_KeepsTrackerCursor()
    {
        var startedAt = DateTimeOffset.UtcNow;
        var discovery = new MutableDiscovery(CreateSnapshot("save-42", startedAt));
        var chatClient = new CountingChatClient("I will wait near the library.");
        var events = new CursorAwareEventSource(
            new Dictionary<string, GameEventBatch?>
            {
                ["<root>"] = new GameEventBatch(
                    [
                        new GameEventRecord("evt-11", "time_changed", "haley", DateTime.UtcNow, "The clock advanced.", Sequence: 11)
                    ],
                    new GameEventCursor("evt-11", 14)),
                ["14"] = new GameEventBatch(Array.Empty<GameEventRecord>(), new GameEventCursor("evt-11", 14))
            });
        var adapter = new FakeGameAdapter(
            new FakeCommandService(),
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley is idle.",
                ["location=Town"])),
            events);
        var supervisor = new NpcRuntimeSupervisor();
        var service = CreateService(discovery, _ => adapter, chatClient, supervisor, enabledNpcIds: ["haley"]);

        await service.RunOneIterationAsync(CancellationToken.None);

        discovery.SetSnapshot(CreateSnapshot("save-42", startedAt.AddSeconds(5)));
        await service.RunOneIterationAsync(CancellationToken.None);

        var trackerCursor = GetTrackerCursor(service, "haley");
        Assert.AreEqual("evt-11", trackerCursor.Since);
        Assert.AreEqual(14L, trackerCursor.Sequence);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenBridgeRebindsWithLowerSequence_RebasesHostCursorSoPrivateChatTriggerIsProcessed()
    {
        var startedAt = DateTimeOffset.UtcNow;
        var discovery = new MutableDiscovery(CreateSnapshot("save-42", startedAt));
        var chatClient = new CountingChatClient("I will wait near the library.");
        var commands = new RecordingCommandService();
        var events = new CursorAwareEventSource(
            new Dictionary<string, GameEventBatch?>
            {
                ["<root>"] = new GameEventBatch(
                    [
                        new GameEventRecord("evt-11", "time_changed", "haley", DateTime.UtcNow, "old bridge event", Sequence: 11)
                    ],
                    new GameEventCursor("evt-11", 14)),
                ["14"] = new GameEventBatch(Array.Empty<GameEventRecord>(), new GameEventCursor("evt-11", 1)),
                ["<new-bridge-root>"] = new GameEventBatch(
                    [
                        new GameEventRecord("evt-2", "vanilla_dialogue_completed", "Haley", DateTime.UtcNow.AddSeconds(1), "Haley finished vanilla dialogue.", Sequence: 2)
                    ],
                    new GameEventCursor("evt-2", 2))
            });
        var adapter = new FakeGameAdapter(
            commands,
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley is idle.",
                ["location=Town"])),
            events);
        var supervisor = new NpcRuntimeSupervisor();
        var service = CreateService(discovery, _ => adapter, chatClient, supervisor, enabledNpcIds: ["haley"]);

        await service.RunOneIterationAsync(CancellationToken.None);
        discovery.SetSnapshot(CreateSnapshot("save-42", startedAt.AddSeconds(5)));
        await service.RunOneIterationAsync(CancellationToken.None);
        await service.RunOneIterationAsync(CancellationToken.None);

        CollectionAssert.AreEqual(
            new long?[] { null, null, 2 },
            events.SeenCursors.Select(cursor => cursor.Sequence).ToArray());
        Assert.AreEqual(1, commands.Submitted.Count(action => action.Type == GameActionType.OpenPrivateChat));
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenInitialAttachPollsEmptyBatch_NextPrivateChatTriggerIsProcessed()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("I will wait near the library.");
        var commands = new RecordingCommandService();
        var events = new ScriptedEventSource(
            new GameEventBatch(Array.Empty<GameEventRecord>(), new GameEventCursor()),
            new GameEventBatch(
                [
                    new GameEventRecord("evt-21", "vanilla_dialogue_completed", "Haley", DateTime.UtcNow, "Haley finished vanilla dialogue.", Sequence: 21)
                ],
                new GameEventCursor("evt-21", 21)));
        var adapter = new FakeGameAdapter(
            commands,
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley is idle.",
                ["location=Town"])),
            events);
        var supervisor = new NpcRuntimeSupervisor();
        var service = CreateService(discovery, _ => adapter, chatClient, supervisor, enabledNpcIds: ["haley"]);

        await service.RunOneIterationAsync(CancellationToken.None);
        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(1, commands.Submitted.Count(action => action.Type == GameActionType.OpenPrivateChat));
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WithNoEnabledNpc_ProcessesPrivateChatTriggerWithoutStartingAutonomy()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("I will wait near the library.");
        var privateChatAgent = new CountingPrivateChatAgentRunner();
        var commands = new RecordingCommandService();
        var events = new ScriptedEventSource(
            new GameEventBatch(Array.Empty<GameEventRecord>(), new GameEventCursor()),
            new GameEventBatch(
                [
                    new GameEventRecord("evt-41", "vanilla_dialogue_completed", "Haley", DateTime.UtcNow, "Haley finished vanilla dialogue.", Sequence: 41)
                ],
                new GameEventCursor("evt-41", 41)));
        var adapter = new FakeGameAdapter(
            commands,
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley is idle.",
                ["location=Town"])),
            events);
        var supervisor = new NpcRuntimeSupervisor();
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: [],
            privateChatAgentRunner: privateChatAgent);

        await service.RunOneIterationAsync(CancellationToken.None);
        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(1, commands.Submitted.Count(action => action.Type == GameActionType.OpenPrivateChat));
        var runtime = supervisor.Snapshot().Single();
        Assert.AreEqual("cmd-1", runtime.Controller.LastTerminalCommandStatus?.CommandId);
        Assert.AreEqual("open_private_chat", runtime.Controller.LastTerminalCommandStatus?.Action);
        Assert.AreEqual(0, chatClient.CompleteWithToolsCalls);
        Assert.AreEqual(0, privateChatAgent.Requests.Count);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenPrivateChatProcessingFails_StillDispatchesAutonomyAndKeepsBatchStaged()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("I will wait near the library.");
        var commands = new ThrowingOpenPrivateChatCommandService();
        var events = new ScriptedEventSource(
            new GameEventBatch(Array.Empty<GameEventRecord>(), new GameEventCursor(null, 0)),
            new GameEventBatch(
                [
                    new GameEventRecord(
                        "evt-43",
                        "vanilla_dialogue_completed",
                        "Haley",
                        DateTime.UtcNow,
                        "Haley finished vanilla dialogue.",
                        Sequence: 43)
                ],
                new GameEventCursor("evt-43", 43)));
        var adapter = new FakeGameAdapter(
            commands,
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley is idle.",
                ["location=Town"])),
            events);
        var supervisor = new NpcRuntimeSupervisor();
        var service = CreateService(discovery, _ => adapter, chatClient, supervisor, enabledNpcIds: ["haley"]);

        await service.RunOneIterationAsync(CancellationToken.None);
        await service.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(1, chatClient.CompleteWithToolsCalls, "A private-chat bridge failure must not prevent the enabled NPC autonomy worker from running.");
        var hostState = await new StardewRuntimeHostStateStore(GetHostStateDbPath("save-42")).LoadAsync(CancellationToken.None);
        Assert.IsNotNull(hostState.StagedBatch, "The failing private-chat batch must remain staged so the host can retry it later instead of silently advancing the shared cursor.");
        Assert.AreEqual("evt-43", hostState.StagedBatch!.NextCursor.Since);
        Assert.IsNull(hostState.SourceCursor.Since);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WithNoEnabledNpc_CommitsPrivateChatCursorSoServiceRebuildDoesNotReopen()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("I will wait near the library.");
        var commands = new RecordingCommandService();
        var events = new CursorAwareEventSource(
            new Dictionary<string, GameEventBatch?>
            {
                ["<root>"] = new GameEventBatch(Array.Empty<GameEventRecord>(), new GameEventCursor(null, 0)),
                ["0"] = new GameEventBatch(
                    [
                        new GameEventRecord("evt-42", "vanilla_dialogue_completed", "Haley", DateTime.UtcNow, "Haley finished vanilla dialogue.", Sequence: 42)
                    ],
                    new GameEventCursor("evt-42", 42)),
                ["42"] = new GameEventBatch(Array.Empty<GameEventRecord>(), new GameEventCursor("evt-42", 42))
            });
        var adapter = new FakeGameAdapter(
            commands,
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley is idle.",
                ["location=Town"])),
            events);

        var supervisor1 = new NpcRuntimeSupervisor();
        var service1 = CreateService(discovery, _ => adapter, chatClient, supervisor1, enabledNpcIds: []);
        await service1.RunOneIterationAsync(CancellationToken.None);
        await service1.RunOneIterationAsync(CancellationToken.None);

        var supervisor2 = new NpcRuntimeSupervisor();
        var service2 = CreateService(discovery, _ => adapter, chatClient, supervisor2, enabledNpcIds: []);
        await service2.RunOneIterationAsync(CancellationToken.None);

        Assert.AreEqual(1, commands.Submitted.Count(action => action.Type == GameActionType.OpenPrivateChat));
        CollectionAssert.AreEqual(new long?[] { null, 0, 42 }, events.SeenCursors.Select(cursor => cursor.Sequence).ToArray());
        Assert.AreEqual("open_private_chat", supervisor1.Snapshot().Single().Controller.LastTerminalCommandStatus?.Action);
        Assert.AreEqual(0, supervisor2.Snapshot().Count);
        Assert.AreEqual(0, chatClient.CompleteWithToolsCalls);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_WhenDrainOnlyBatchIsReplayed_DoesNotOpenPrivateChat()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("unused");
        var commands = new RecordingCommandService();
        var events = new CountingEventSource(
        [
            new GameEventRecord("evt-31", "vanilla_dialogue_completed", "Haley", DateTime.UtcNow, "Historical Haley dialogue completed.", Sequence: 31),
        ]);
        var adapter = new FakeGameAdapter(
            commands,
            new FakeQueryService(new GameObservation(
                "sam",
                "stardew-valley",
                DateTime.UtcNow,
                "Sam is idle.",
                ["location=Town"])),
            events);

        var supervisor1 = new NpcRuntimeSupervisor();
        var service1 = CreateService(discovery, _ => adapter, chatClient, supervisor1, enabledNpcIds: ["sam"]);
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => service1.RunOneIterationAsync(CancellationToken.None));

        var supervisor2 = new NpcRuntimeSupervisor();
        var service2 = CreateService(discovery, _ => adapter, chatClient, supervisor2, enabledNpcIds: ["sam"]);
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => service2.RunOneIterationAsync(CancellationToken.None));

        Assert.AreEqual(1, events.PollCalls, "The staged historical batch should replay without re-polling the bridge.");
        Assert.AreEqual(0, commands.Submitted.Count(action => action.Type == GameActionType.OpenPrivateChat));
    }

    [TestMethod]
    public async Task CronTaskDue_WhenTaskBelongsToNpcSession_AppendsDurableIngressBeforeSubmitting()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CountingChatClient("unused");
        var commands = new RecordingCommandService();
        var adapter = new FakeGameAdapter(
            commands,
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley is idle.",
                ["location=Town"])),
            new FakeEventSource([]));
        var supervisor = new NpcRuntimeSupervisor();
        var service = CreateService(
            discovery,
            _ => adapter,
            chatClient,
            supervisor,
            enabledNpcIds: ["haley"]);
        var task = new CronTask(
            "task-haley-talk",
            "haley_talk",
            "* * * * *",
            "一分钟后主动和我对话",
            DateTimeOffset.UtcNow,
            Recurring: false,
            Durable: true,
            SessionId: "sdv_save-42_haley_default:private_chat:pc-1");

        await service.HandleCronTaskDueAsync(task, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.AreEqual(0, commands.Submitted.Count, "Cron due must only append durable ingress; the autonomy worker submits the side effect later.");
        var queued = supervisor.Snapshot().Single().Controller;
        Assert.AreEqual(1, queued.IngressWorkItems.Count);
        Assert.AreEqual("scheduled_private_chat", queued.IngressWorkItems.Single().WorkType);
        Assert.IsNull(queued.PendingWorkItem);
        Assert.IsNull(queued.ActionSlot);

        await service.RunOneIterationAsync(CancellationToken.None);

        var action = commands.Submitted.Single();
        Assert.AreEqual(GameActionType.OpenPrivateChat, action.Type);
        Assert.AreEqual("haley", action.NpcId);
        Assert.AreEqual("stardew-valley", action.GameId);
        Assert.AreEqual("一分钟后主动和我对话", action.Payload?["prompt"]?.ToString());
        Assert.AreEqual("scheduled_task:task-haley-talk", action.Payload?["conversationId"]?.ToString());
        var completed = supervisor.Snapshot().Single().Controller;
        Assert.AreEqual(0, completed.IngressWorkItems.Count);
        Assert.AreEqual("cmd-1", completed.LastTerminalCommandStatus?.CommandId);
        Assert.AreEqual("open_private_chat", completed.LastTerminalCommandStatus?.Action);
        Assert.AreEqual(StardewCommandStatuses.Completed, completed.LastTerminalCommandStatus?.Status);
    }

    [TestMethod]
    public async Task RunOneIterationAsync_FiltersSharedBatchByNpcPersistedCursorBeforeWake()
    {
        var discovery = CreateDiscovery("save-42");
        var chatClient = new CapturingChatClient("I will wait near the library.");
        var events = new CountingEventSource(
        [
            new GameEventRecord("evt-11", "time_changed", "haley", DateTime.UtcNow, "old Haley event", Sequence: 11),
            new GameEventRecord("evt-12", "time_changed", "haley", DateTime.UtcNow.AddSeconds(1), "new Haley event", Sequence: 12),
        ]);
        var adapter = new FakeGameAdapter(
            new FakeCommandService(),
            new FakeQueryService(new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley is idle.",
                ["location=Town"])),
            events);
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("haley", "save-42");
        var driver = await supervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        await driver.SetControllerStateAsync(new GameEventCursor("evt-11", 11), null, CancellationToken.None);
        var service = CreateService(discovery, _ => adapter, chatClient, supervisor, enabledNpcIds: ["haley"]);

        await service.RunOneIterationAsync(CancellationToken.None);

        var request = chatClient.CapturedRequests.Single();
        Assert.IsFalse(request.Contains("old Haley event", StringComparison.Ordinal), "Host fanout must filter records already acknowledged by this NPC before worker dispatch.");
        Assert.IsFalse(request.Contains("new Haley event", StringComparison.Ordinal), "Wake-only autonomy must not inject matching event records into the parent prompt.");
        var trackerCursor = GetTrackerCursor(service, "haley");
        Assert.AreEqual("evt-12", trackerCursor.Since);
        Assert.AreEqual(12L, trackerCursor.Sequence);
    }

    private StardewNpcAutonomyBackgroundService CreateService(
        IStardewBridgeDiscovery discovery,
        Func<StardewBridgeDiscoverySnapshot, IGameAdapter> adapterFactory,
        IChatClient chatClient,
        NpcRuntimeSupervisor supervisor,
        IReadOnlyCollection<string> enabledNpcIds,
        WorldCoordinationService? worldCoordination = null,
        NpcAutonomyBudget? budget = null,
        ICronScheduler? cronScheduler = null,
        INpcPrivateChatAgentRunner? privateChatAgentRunner = null,
        IChatClient? delegationChatClient = null,
        TimeSpan? pollInterval = null,
        TimeSpan? autonomyWakeInterval = null)
    {
        var bindingResolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var service = new StardewNpcAutonomyBackgroundService(
            discovery,
            adapterFactory,
            chatClient,
            NullLoggerFactory.Instance,
            _skillManager,
            cronScheduler ?? new NoopCronScheduler(),
            supervisor,
            new NpcRuntimeHost(new FileSystemNpcPackLoader(), supervisor, _tempDir),
            bindingResolver,
            new StardewNpcAutonomyPromptSupplementBuilder(new FixedStardewGamingSkillRootProvider(_gamingSkillRoot)),
            new NpcToolSurfaceSnapshotProvider(() => [new DiscoveredNoopTool("mcp_dynamic_test")]),
            new StardewPrivateChatRuntimeAdapter(
                privateChatAgentRunner ?? new NoopPrivateChatAgentRunner(),
                NullLogger<StardewPrivateChatRuntimeAdapter>.Instance,
                sessionLeaseCoordinator: new StardewNpcPrivateChatSessionLeaseCoordinator(_tempDir, supervisor, bindingResolver),
                bindingResolver: bindingResolver,
                runtimeRoot: _tempDir,
                runtimeSupervisor: supervisor),
            budget ?? new NpcAutonomyBudget(new NpcAutonomyBudgetOptions(MaxToolIterations: 2, MaxConcurrentLlmRequests: 1, MaxRestartsPerScene: 2)),
            worldCoordination ?? new WorldCoordinationService(new ResourceClaimRegistry()),
            NullLogger<StardewNpcAutonomyBackgroundService>.Instance,
            new StardewNpcAutonomyBackgroundOptions(
                enabledNpcIds,
                pollInterval ?? TimeSpan.FromMilliseconds(10),
                autonomyWakeInterval ?? TimeSpan.FromMilliseconds(10)),
            true,
            true,
            _tempDir,
            delegationChatClient);
        _services.Add(service);
        return service;
    }

    private string GetHostStateDbPath(string saveId)
        => Path.Combine(
            _tempDir,
            "runtime",
            "stardew",
            "games",
            NpcNamespace.Sanitize("stardew-valley"),
            "saves",
            NpcNamespace.Sanitize(saveId),
            "host",
            "state.db");

    private static async Task WaitForSubmittedCountAsync(
        RecordingCommandService commands,
        int expectedCount,
        TimeSpan timeout)
    {
        var deadlineUtc = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadlineUtc)
        {
            if (commands.Submitted.Count >= expectedCount)
                return;

            await Task.Delay(10);
        }

        Assert.Fail($"Timed out waiting for {expectedCount} submitted command(s). Observed {commands.Submitted.Count}.");
    }

    private static FakeDiscovery CreateDiscovery(string saveId)
        => new(CreateSnapshot(saveId));

    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private static StardewBridgeDiscoverySnapshot CreateSnapshot(string saveId, DateTimeOffset? startedAtUtc = null)
        => new(
            new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token" },
            startedAtUtc ?? DateTimeOffset.UtcNow,
            1234,
            saveId);

    private static FakeGameAdapter CreateAdapter(string npcId)
        => new(
            new FakeCommandService(),
            new DynamicQueryService(),
            new FakeEventSource([]));

    private void CreatePack(string npcId, string displayName)
    {
        var root = Path.Combine(_packRoot, npcId, "default");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "SOUL.md"), $"# {displayName}\n\n{npcId}-pack-soul");
        File.WriteAllText(Path.Combine(root, "facts.md"), $"{displayName} facts");
        File.WriteAllText(Path.Combine(root, "voice.md"), $"{displayName} voice");
        File.WriteAllText(Path.Combine(root, "boundaries.md"), $"{displayName} boundaries");
        File.WriteAllText(
            Path.Combine(root, "skills.json"),
            """{"required":["stardew-core","stardew-social","stardew-navigation","stardew-task-continuity"],"optional":[]}""");

        var manifest = new NpcPackManifest
        {
            SchemaVersion = 1,
            NpcId = npcId,
            GameId = "stardew-valley",
            ProfileId = "default",
            DefaultProfileId = "default",
            DisplayName = displayName,
            SmapiName = displayName,
            Aliases = [npcId, displayName],
            TargetEntityId = npcId,
            AdapterId = "stardew",
            SoulFile = "SOUL.md",
            FactsFile = "facts.md",
            VoiceFile = "voice.md",
            BoundariesFile = "boundaries.md",
            SkillsFile = "skills.json",
            Capabilities = ["move", "speak"]
        };
        File.WriteAllText(
            Path.Combine(root, FileSystemNpcPackLoader.ManifestFileName),
            JsonSerializer.Serialize(manifest));
    }

    private static void CreateGamingSkillFixtures(string gamingSkillRoot)
    {
        Directory.CreateDirectory(gamingSkillRoot);
        File.WriteAllText(Path.Combine(gamingSkillRoot, "stardew-core.md"), "stardew-core test guidance");
        File.WriteAllText(Path.Combine(gamingSkillRoot, "stardew-social.md"), "stardew-social test guidance");
        File.WriteAllText(Path.Combine(gamingSkillRoot, "stardew-navigation.md"), "stardew-navigation test guidance");
        var taskContinuityRoot = Path.Combine(gamingSkillRoot, "stardew-task-continuity");
        Directory.CreateDirectory(taskContinuityRoot);
        File.WriteAllText(Path.Combine(taskContinuityRoot, "SKILL.md"), "stardew-task-continuity test guidance");
    }

    private static GameEventCursor GetTrackerCursor(StardewNpcAutonomyBackgroundService service, string npcId)
    {
        var snapshot = GetTrackerDriverSnapshot(service, npcId);
        var cursorProperty = snapshot.GetType().GetProperty("EventCursor");
        Assert.IsNotNull(cursorProperty);
        return (GameEventCursor)(cursorProperty.GetValue(snapshot) ?? throw new AssertFailedException("Tracker cursor missing."));
    }

    private static string GetTrackerSaveId(StardewNpcAutonomyBackgroundService service, string npcId)
    {
        var tracker = GetTracker(service, npcId);
        var instanceProperty = tracker.GetType().GetProperty("Instance");
        Assert.IsNotNull(instanceProperty);
        var instance = instanceProperty.GetValue(tracker);
        Assert.IsNotNull(instance);

        var descriptorProperty = instance.GetType().GetProperty("Descriptor");
        Assert.IsNotNull(descriptorProperty);
        var descriptor = descriptorProperty.GetValue(instance);
        Assert.IsNotNull(descriptor);

        var saveIdProperty = descriptor.GetType().GetProperty("SaveId");
        Assert.IsNotNull(saveIdProperty);
        return (string)(saveIdProperty.GetValue(descriptor) ?? throw new AssertFailedException("Tracker save id missing."));
    }

    private static object GetTrackerDriverSnapshot(StardewNpcAutonomyBackgroundService service, string npcId)
    {
        var tracker = GetTracker(service, npcId);
        var driverProperty = tracker.GetType().GetProperty("Driver");
        Assert.IsNotNull(driverProperty);
        var driver = driverProperty.GetValue(tracker);
        Assert.IsNotNull(driver);

        var snapshotMethod = driver.GetType().GetMethod("Snapshot");
        Assert.IsNotNull(snapshotMethod);
        return snapshotMethod.Invoke(driver, null) ?? throw new AssertFailedException("Tracker snapshot missing.");
    }

    private static IReadOnlyList<JsonElement> ReadRuntimeLogRecords(string logPath)
    {
        using var document = JsonDocument.Parse("[" + string.Join(",", File.ReadAllLines(logPath)) + "]");
        return document.RootElement.EnumerateArray()
            .Select(element => element.Clone())
            .ToArray();
    }

    private static Task GetTrackerWorkerTask(StardewNpcAutonomyBackgroundService service, string npcId)
    {
        var tracker = GetTracker(service, npcId);
        var workerProperty = tracker.GetType().GetProperty("WorkerTask");
        Assert.IsNotNull(workerProperty);
        return (Task)(workerProperty.GetValue(tracker) ?? throw new AssertFailedException("Tracker worker task missing."));
    }

    private static object GetTracker(StardewNpcAutonomyBackgroundService service, string npcId)
    {
        var trackersField = typeof(StardewNpcAutonomyBackgroundService).GetField("_trackers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(trackersField);
        var trackers = trackersField.GetValue(service);
        Assert.IsNotNull(trackers);

        var tryGetValue = trackers.GetType().GetMethod("TryGetValue");
        Assert.IsNotNull(tryGetValue);
        var parameters = new object?[] { npcId, null };
        var found = (bool)(tryGetValue.Invoke(trackers, parameters) ?? false);
        Assert.IsTrue(found);
        return parameters[1] ?? throw new AssertFailedException("Tracker missing.");
    }

    private sealed class FakeDiscovery : IStardewBridgeDiscovery
    {
        private readonly StardewBridgeDiscoverySnapshot _snapshot;

        public FakeDiscovery(StardewBridgeDiscoverySnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public string DiscoveryFilePath => "fake-discovery.json";

        public bool TryReadLatest(out StardewBridgeDiscoverySnapshot? snapshot, out string? failureReason)
        {
            snapshot = _snapshot;
            failureReason = null;
            return true;
        }
    }

    private sealed class MutableDiscovery : IStardewBridgeDiscovery
    {
        private StardewBridgeDiscoverySnapshot _snapshot;

        public MutableDiscovery(StardewBridgeDiscoverySnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public string DiscoveryFilePath => "mutable-discovery.json";

        public void SetSnapshot(StardewBridgeDiscoverySnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public bool TryReadLatest(out StardewBridgeDiscoverySnapshot? snapshot, out string? failureReason)
        {
            snapshot = _snapshot;
            failureReason = null;
            return true;
        }
    }

    private sealed class FakeGameAdapter : IGameAdapter
    {
        public FakeGameAdapter(IGameCommandService commands, IGameQueryService queries, IGameEventSource events)
        {
            Commands = commands;
            Queries = queries;
            Events = events;
        }

        public string AdapterId => "stardew";

        public IGameCommandService Commands { get; }

        public IGameQueryService Queries { get; }

        public IGameEventSource Events { get; }
    }

    private sealed class FakeQueryService : IGameQueryService
    {
        private readonly GameObservation _observation;

        public FakeQueryService(GameObservation observation)
        {
            _observation = observation;
        }

        public Task<GameObservation> ObserveAsync(string npcId, CancellationToken ct)
            => Task.FromResult(_observation);

        public Task<WorldSnapshot> GetWorldSnapshotAsync(string npcId, CancellationToken ct)
            => Task.FromResult(new WorldSnapshot("stardew-valley", "save-42", _observation.TimestampUtc, [], []));
    }

    private sealed class DynamicQueryService : IGameQueryService
    {
        public Task<GameObservation> ObserveAsync(string npcId, CancellationToken ct)
            => Task.FromResult(new GameObservation(
                npcId,
                "stardew-valley",
                DateTime.UtcNow,
                $"{npcId} is idle.",
                ["location=Town"]));

        public Task<WorldSnapshot> GetWorldSnapshotAsync(string npcId, CancellationToken ct)
            => Task.FromResult(new WorldSnapshot("stardew-valley", "save-42", DateTime.UtcNow, [], []));
    }

    private sealed class FakeEventSource : IGameEventSource
    {
        private readonly GameEventBatch _batch;

        public FakeEventSource(IReadOnlyList<GameEventRecord> records)
        {
            _batch = new GameEventBatch(records, GameEventCursor.Advance(new GameEventCursor(), records));
        }

        public FakeEventSource(GameEventBatch batch)
        {
            _batch = batch;
        }

        public Task<IReadOnlyList<GameEventRecord>> PollAsync(GameEventCursor cursor, CancellationToken ct)
            => Task.FromResult(_batch.Records);

        public Task<GameEventBatch> PollBatchAsync(GameEventCursor cursor, CancellationToken ct)
            => Task.FromResult(_batch);
    }

    private sealed class CountingEventSource : IGameEventSource
    {
        private readonly GameEventBatch _batch;

        public CountingEventSource(IReadOnlyList<GameEventRecord> records)
        {
            _batch = new GameEventBatch(records, GameEventCursor.Advance(new GameEventCursor(), records));
        }

        public int PollCalls { get; private set; }

        public Task<IReadOnlyList<GameEventRecord>> PollAsync(GameEventCursor cursor, CancellationToken ct)
        {
            PollCalls++;
            return Task.FromResult(_batch.Records);
        }

        public Task<GameEventBatch> PollBatchAsync(GameEventCursor cursor, CancellationToken ct)
        {
            PollCalls++;
            return Task.FromResult(_batch);
        }
    }

    private sealed class ScriptedEventSource : IGameEventSource
    {
        private readonly Queue<GameEventBatch> _batches;

        public ScriptedEventSource(params GameEventBatch[] batches)
        {
            _batches = new Queue<GameEventBatch>(batches);
        }

        public Task<IReadOnlyList<GameEventRecord>> PollAsync(GameEventCursor cursor, CancellationToken ct)
            => Task.FromResult(PollBatch(cursor).Records);

        public Task<GameEventBatch> PollBatchAsync(GameEventCursor cursor, CancellationToken ct)
            => Task.FromResult(PollBatch(cursor));

        private GameEventBatch PollBatch(GameEventCursor cursor)
        {
            if (_batches.Count > 0)
                return _batches.Dequeue();

            return new GameEventBatch(Array.Empty<GameEventRecord>(), cursor);
        }
    }

    private sealed class CursorAwareEventSource : IGameEventSource
    {
        private readonly IReadOnlyDictionary<string, GameEventBatch?> _batches;

        public CursorAwareEventSource(IReadOnlyDictionary<string, GameEventBatch?> batches)
        {
            _batches = batches;
        }

        public List<GameEventCursor> SeenCursors { get; } = new();

        public Task<IReadOnlyList<GameEventRecord>> PollAsync(GameEventCursor cursor, CancellationToken ct)
            => Task.FromResult(PollBatch(cursor).Records);

        public Task<GameEventBatch> PollBatchAsync(GameEventCursor cursor, CancellationToken ct)
            => Task.FromResult(PollBatch(cursor));

        private GameEventBatch PollBatch(GameEventCursor cursor)
        {
            SeenCursors.Add(cursor);
            var key = cursor.Sequence?.ToString() ?? "<root>";
            if (SeenCursors.Count > 1 && cursor.Sequence is null && _batches.ContainsKey("<new-bridge-root>"))
                key = "<new-bridge-root>";

            if (_batches.TryGetValue(key, out var batch) && batch is not null)
                return batch;

            return new GameEventBatch(Array.Empty<GameEventRecord>(), cursor);
        }
    }

    private class RecordingCommandService : IGameCommandService
    {
        public List<GameAction> Submitted { get; } = new();

        public List<(string CommandId, string Reason)> CancelRequests { get; } = new();

        public virtual Task<GameCommandResult> SubmitAsync(GameAction action, CancellationToken ct)
        {
            Submitted.Add(action);
            return Task.FromResult(new GameCommandResult(true, "cmd-1", StardewCommandStatuses.Completed, null, action.TraceId));
        }

        public virtual Task<GameCommandStatus> GetStatusAsync(string commandId, CancellationToken ct)
            => Task.FromResult(new GameCommandStatus(commandId, "Penny", "debug", StardewCommandStatuses.Completed, 1, null, null));

        public virtual Task<GameCommandStatus?> TryGetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
            => Task.FromResult<GameCommandStatus?>(null);

        public virtual Task<GameCommandStatus> CancelAsync(string commandId, string reason, CancellationToken ct)
        {
            CancelRequests.Add((commandId, reason));
            return Task.FromResult(new GameCommandStatus(commandId, "Penny", "debug", StardewCommandStatuses.Cancelled, 1, reason, null));
        }
    }

    private sealed class FakeCommandService : RecordingCommandService
    {
    }

    private sealed class ThrowingOpenPrivateChatCommandService : RecordingCommandService
    {
        public override Task<GameCommandResult> SubmitAsync(GameAction action, CancellationToken ct)
        {
            if (action.Type == GameActionType.OpenPrivateChat)
                throw new InvalidOperationException("private chat bridge failure");

            return base.SubmitAsync(action, ct);
        }
    }

    private sealed class ScriptedCommandService : RecordingCommandService
    {
        private readonly Queue<GameCommandStatus> _statuses;

        public ScriptedCommandService(IReadOnlyList<GameCommandStatus> statusSequence)
        {
            _statuses = new Queue<GameCommandStatus>(statusSequence);
        }

        public int StatusCalls { get; private set; }

        public override Task<GameCommandStatus> GetStatusAsync(string commandId, CancellationToken ct)
        {
            StatusCalls++;
            if (_statuses.Count > 0)
                return Task.FromResult(_statuses.Dequeue());

            return Task.FromResult(new GameCommandStatus(commandId, "haley", "move", StardewCommandStatuses.Completed, 1, null, null));
        }
    }

    private sealed class LookupCommandService : RecordingCommandService
    {
        private readonly GameCommandStatus? _lookupStatus;

        public LookupCommandService(GameCommandStatus? lookupStatus)
        {
            _lookupStatus = lookupStatus;
        }

        public int LookupCalls { get; private set; }

        public int StatusCalls { get; private set; }

        public override Task<GameCommandStatus> GetStatusAsync(string commandId, CancellationToken ct)
        {
            StatusCalls++;
            return Task.FromResult(new GameCommandStatus(commandId, "haley", "move", StardewCommandStatuses.Completed, 1, null, null));
        }

        public override Task<GameCommandStatus?> TryGetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
        {
            LookupCalls++;
            return Task.FromResult(_lookupStatus);
        }
    }

    private sealed class CountingChatClient : IChatClient
    {
        private readonly Queue<string> _responses;
        private int _completeWithToolsCalls;

        public CountingChatClient(string response)
            : this([response])
        {
        }

        public CountingChatClient(IEnumerable<string> responses)
        {
            _responses = new Queue<string>(responses);
        }

        public int CompleteWithToolsCalls => _completeWithToolsCalls;

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
        {
            Interlocked.Increment(ref _completeWithToolsCalls);
            return Task.FromResult(GetNextResponse());
        }

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            Interlocked.Increment(ref _completeWithToolsCalls);
            return Task.FromResult(new ChatResponse { Content = GetNextResponse(), FinishReason = "stop" });
        }

        public async IAsyncEnumerable<string> StreamAsync(IEnumerable<Message> messages, [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<StreamEvent> StreamAsync(
            string? systemPrompt,
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition>? tools = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        private string GetNextResponse()
            => _responses.Count > 0 ? _responses.Dequeue() : "wait";
    }

    private sealed class DelegationStreamingChatClient : IChatClient
    {
        public int StructuredStreamCalls { get; private set; }
        public string? LastSystemPrompt { get; private set; }

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("ok");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
            => Task.FromResult(new ChatResponse { Content = "delegation", FinishReason = "stop" });

        public async IAsyncEnumerable<string> StreamAsync(IEnumerable<Message> messages, [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<StreamEvent> StreamAsync(
            string? systemPrompt,
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition>? tools = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            StructuredStreamCalls++;
            LastSystemPrompt = systemPrompt;
            yield return new StreamEvent.TokenDelta("delegated");
            yield return new StreamEvent.MessageComplete("stop", new UsageStats(1, 1));
        }
    }

    private sealed class CapturingChatClient : IChatClient
    {
        private readonly string _response;
        private readonly List<string> _capturedRequests = [];

        public CapturingChatClient(string response)
        {
            _response = response;
        }

        public IReadOnlyList<string> CapturedRequests
        {
            get
            {
                lock (_capturedRequests)
                    return _capturedRequests.ToArray();
            }
        }

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
        {
            lock (_capturedRequests)
                _capturedRequests.Add(string.Join("\n", messages.Select(message => message.Content)));

            return Task.FromResult(_response);
        }

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            lock (_capturedRequests)
                _capturedRequests.Add(string.Join("\n", messages.Select(message => message.Content)));

            return Task.FromResult(new ChatResponse { Content = _response, FinishReason = "stop" });
        }

        public async IAsyncEnumerable<string> StreamAsync(IEnumerable<Message> messages, [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<StreamEvent> StreamAsync(
            string? systemPrompt,
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition>? tools = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class CapturingStreamingChatClient : IChatClient
    {
        private readonly Queue<StreamEvent> _events;

        public CapturingStreamingChatClient(params StreamEvent[] events)
        {
            _events = new Queue<StreamEvent>(events);
        }

        public int StructuredStreamCalls { get; private set; }

        public List<string> UserMessages { get; } = [];

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("unused");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
            => Task.FromResult(new ChatResponse { Content = "unused", FinishReason = "stop" });

        public async IAsyncEnumerable<string> StreamAsync(IEnumerable<Message> messages, [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<StreamEvent> StreamAsync(
            string? systemPrompt,
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition>? tools = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            StructuredStreamCalls++;
            UserMessages.Clear();
            UserMessages.AddRange(messages
                .Where(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
                .Select(message => message.Content));

            while (_events.Count > 0)
            {
                await Task.Yield();
                yield return _events.Dequeue();
            }
        }
    }

    private sealed class BlockingChatClient : IChatClient
    {
        private readonly string _response;
        private readonly TaskCompletionSource<bool> _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingChatClient(string response)
        {
            _response = response;
        }

        public async Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
        {
            _entered.TrySetResult(true);
            await _release.Task.WaitAsync(ct);
            return _response;
        }

        public async Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            _entered.TrySetResult(true);
            await _release.Task.WaitAsync(ct);
            return new ChatResponse { Content = _response, FinishReason = "stop" };
        }

        public Task WaitUntilEnteredAsync(CancellationToken ct)
            => _entered.Task.WaitAsync(ct);

        public void Release()
            => _release.TrySetResult(true);

        public async IAsyncEnumerable<string> StreamAsync(IEnumerable<Message> messages, [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<StreamEvent> StreamAsync(
            string? systemPrompt,
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition>? tools = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class BlockFirstChatClient : IChatClient
    {
        private readonly string _response;
        private readonly TaskCompletionSource<bool> _firstEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _firstRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<string> _capturedRequests = [];
        private int _completeWithToolsCalls;

        public BlockFirstChatClient(string response)
        {
            _response = response;
        }

        public int CompleteWithToolsCalls => Volatile.Read(ref _completeWithToolsCalls);

        public async Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
        {
            var callNumber = RecordCall(messages);

            if (callNumber == 1)
            {
                _firstEntered.TrySetResult(true);
                await _firstRelease.Task.WaitAsync(ct);
            }

            return _response;
        }

        public async Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            var callNumber = RecordCall(messages);

            if (callNumber == 1)
            {
                _firstEntered.TrySetResult(true);
                await _firstRelease.Task.WaitAsync(ct);
            }

            return new ChatResponse { Content = _response, FinishReason = "stop" };
        }

        private int RecordCall(IEnumerable<Message> messages)
        {
            var callNumber = Interlocked.Increment(ref _completeWithToolsCalls);
            lock (_capturedRequests)
            {
                _capturedRequests.Add(string.Join("\n", messages.Select(message => message.Content)));
            }

            return callNumber;
        }

        public Task WaitUntilFirstCallEnteredAsync(CancellationToken ct)
            => _firstEntered.Task.WaitAsync(ct);

        public void ReleaseFirstCall()
            => _firstRelease.TrySetResult(true);

        public string GetCapturedRequest(int oneBasedCallNumber)
        {
            lock (_capturedRequests)
            {
                if (oneBasedCallNumber <= 0 || oneBasedCallNumber > _capturedRequests.Count)
                    throw new AssertFailedException($"Captured request #{oneBasedCallNumber} is unavailable. Total captured: {_capturedRequests.Count}.");

                return _capturedRequests[oneBasedCallNumber - 1];
            }
        }

        public async Task WaitForCallCountAsync(int expectedCount, TimeSpan timeout)
        {
            var deadlineUtc = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadlineUtc)
            {
                if (CompleteWithToolsCalls >= expectedCount)
                    return;

                await Task.Delay(10);
            }

            throw new AssertFailedException($"Timed out waiting for {expectedCount} chat calls. Observed {CompleteWithToolsCalls}.");
        }

        public async IAsyncEnumerable<string> StreamAsync(IEnumerable<Message> messages, [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<StreamEvent> StreamAsync(
            string? systemPrompt,
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition>? tools = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class NoopPrivateChatAgentRunner : INpcPrivateChatAgentRunner
    {
        public Task<NpcPrivateChatReply> ReplyAsync(NpcPrivateChatRequest request, CancellationToken ct)
            => Task.FromResult(new NpcPrivateChatReply("noop"));
    }

    private sealed class CountingPrivateChatAgentRunner : INpcPrivateChatAgentRunner
    {
        public List<NpcPrivateChatRequest> Requests { get; } = new();

        public Task<NpcPrivateChatReply> ReplyAsync(NpcPrivateChatRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(new NpcPrivateChatReply("noop"));
        }
    }

    private sealed class NoopCronScheduler : ICronScheduler
    {
        public event EventHandler<CronTaskDueEventArgs>? TaskDue
        {
            add { }
            remove { }
        }

        public void Schedule(CronTask task)
        {
        }

        public void Cancel(string taskId)
        {
        }

        public CronTask? GetTask(string taskId) => null;

        public IReadOnlyList<CronTask> GetAllTasks() => Array.Empty<CronTask>();

        public DateTimeOffset? GetNextRun(string taskId) => null;
    }

    private sealed class ManualCronScheduler : ICronScheduler
    {
        public event EventHandler<CronTaskDueEventArgs>? TaskDue;

        public void Fire(CronTask task, DateTimeOffset firedAt)
            => TaskDue?.Invoke(this, new CronTaskDueEventArgs { Task = task, FiredAt = firedAt });

        public void Schedule(CronTask task)
        {
        }

        public void Cancel(string taskId)
        {
        }

        public CronTask? GetTask(string taskId) => null;

        public IReadOnlyList<CronTask> GetAllTasks() => Array.Empty<CronTask>();

        public DateTimeOffset? GetNextRun(string taskId) => null;
    }

    private sealed class DiscoveredNoopTool : ITool
    {
        public DiscoveredNoopTool(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public string Description => "test tool";

        public Type ParametersType => typeof(object);

        public Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
            => Task.FromResult(ToolResult.Ok("ok"));
    }
}
