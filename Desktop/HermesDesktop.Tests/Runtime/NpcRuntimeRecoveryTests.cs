using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Hermes.Agent.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

namespace HermesDesktop.Tests.Runtime;

[TestClass]
public sealed class NpcRuntimeRecoveryTests
{
    private string _tempDir = null!;
    private string _packRoot = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "hermes-runtime-recovery-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _packRoot = Path.Combine(_tempDir, "packs");
        CreatePack("haley", "Haley");
        CreatePack("penny", "Penny");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public async Task GetOrCreateDriverAsync_PersistsControllerStateAcrossSupervisorRebuild()
    {
        var descriptor = CreateDescriptor("haley");
        var createdAt = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        var startedAt = createdAt.AddMinutes(1);
        var nextWakeAt = startedAt.AddMinutes(5);
        var supervisor1 = new NpcRuntimeSupervisor();
        var driver1 = await supervisor1.GetOrCreateDriverAsync(descriptor, _tempDir, CancellationToken.None);

        await driver1.SetControllerStateAsync(new GameEventCursor("evt-12", 14), nextWakeAt, CancellationToken.None);
        await driver1.SetPendingWorkItemAsync(
            new NpcRuntimePendingWorkItemSnapshot("work-1", "autonomy_turn", "cmd-1", "running", createdAt),
            CancellationToken.None);
        await driver1.SetActionSlotAsync(
            new NpcRuntimeActionSlotSnapshot("action", "work-1", "cmd-1", "trace-1", startedAt, startedAt.AddMinutes(1)),
            CancellationToken.None);

        var supervisor2 = new NpcRuntimeSupervisor();
        var driver2 = await supervisor2.GetOrCreateDriverAsync(descriptor, _tempDir, CancellationToken.None);
        var controller = driver2.Snapshot();
        var runtime = supervisor2.Snapshot().Single();

        Assert.AreEqual("evt-12", controller.EventCursor.Since);
        Assert.AreEqual(14L, controller.EventCursor.Sequence);
        Assert.AreEqual(nextWakeAt, controller.NextWakeAtUtc);
        Assert.AreEqual("work-1", controller.PendingWorkItem?.WorkItemId);
        Assert.AreEqual("cmd-1", controller.PendingWorkItem?.CommandId);
        Assert.AreEqual("action", controller.ActionSlot?.SlotName);
        Assert.AreEqual("trace-1", controller.ActionSlot?.TraceId);

        Assert.AreEqual("evt-12", runtime.Controller.EventCursor.Since);
        Assert.AreEqual(14L, runtime.Controller.EventCursor.Sequence);
        Assert.AreEqual(nextWakeAt, runtime.Controller.NextWakeAtUtc);
        Assert.AreEqual("work-1", runtime.Controller.PendingWorkItem?.WorkItemId);
        Assert.AreEqual("action", runtime.Controller.ActionSlot?.SlotName);
    }

    [TestMethod]
    public async Task GetOrCreateDriverAsync_PersistsIngressWorkItemsWithoutPendingActionSlot()
    {
        var descriptor = CreateDescriptor("haley");
        var createdAt = new DateTime(2026, 5, 3, 6, 30, 0, DateTimeKind.Utc);
        var supervisor1 = new NpcRuntimeSupervisor();
        var driver1 = await supervisor1.GetOrCreateDriverAsync(descriptor, _tempDir, CancellationToken.None);
        var workItem = new NpcRuntimeIngressWorkItemSnapshot(
            "ingress-scheduled-1",
            "scheduled_private_chat",
            "queued",
            createdAt,
            IdempotencyKey: "idem-scheduled-1",
            TraceId: "trace-scheduled-1",
            Payload: new System.Text.Json.Nodes.JsonObject
            {
                ["prompt"] = "一分钟后主动和我对话",
                ["conversationId"] = "scheduled_task:task-haley-talk"
            });

        await driver1.EnqueueIngressWorkItemAsync(workItem, CancellationToken.None);

        var supervisor2 = new NpcRuntimeSupervisor();
        var driver2 = await supervisor2.GetOrCreateDriverAsync(descriptor, _tempDir, CancellationToken.None);
        var controller = driver2.Snapshot();

        Assert.AreEqual(1, controller.IngressWorkItems.Count);
        Assert.AreEqual("ingress-scheduled-1", controller.IngressWorkItems.Single().WorkItemId);
        Assert.IsNull(controller.PendingWorkItem);
        Assert.IsNull(controller.ActionSlot);
    }

    [TestMethod]
    public async Task GetOrCreateDriverAsync_PersistsActionChainGuardWithoutPendingActionSlot()
    {
        var descriptor = CreateDescriptor("haley");
        var startedAt = new DateTime(2026, 5, 11, 7, 0, 0, DateTimeKind.Utc);
        var updatedAt = startedAt.AddSeconds(30);
        var supervisor1 = new NpcRuntimeSupervisor();
        var driver1 = await supervisor1.GetOrCreateDriverAsync(descriptor, _tempDir, CancellationToken.None);

        await driver1.SetActionChainGuardAsync(
            new NpcRuntimeActionChainGuardSnapshot(
                "chain-1",
                "open",
                null,
                false,
                "todo-1",
                "trace-root-1",
                startedAt,
                updatedAt,
                "move",
                "move:Town:42:17",
                2,
                1,
                1,
                StardewCommandStatuses.Blocked,
                StardewBridgeErrorCodes.PathBlocked,
                1,
                0),
            CancellationToken.None);

        var supervisor2 = new NpcRuntimeSupervisor();
        var driver2 = await supervisor2.GetOrCreateDriverAsync(descriptor, _tempDir, CancellationToken.None);
        var controller = driver2.Snapshot();

        Assert.IsNull(controller.PendingWorkItem);
        Assert.IsNull(controller.ActionSlot);
        Assert.IsNotNull(controller.ActionChainGuard);
        Assert.AreEqual("chain-1", controller.ActionChainGuard!.ChainId);
        Assert.AreEqual("todo-1", controller.ActionChainGuard.RootTodoId);
        Assert.AreEqual("move:Town:42:17", controller.ActionChainGuard.LastTargetKey);
        Assert.AreEqual(2, controller.ActionChainGuard.ConsecutiveActions);
        Assert.AreEqual(1, controller.ActionChainGuard.ConsecutiveFailures);
        Assert.AreEqual(StardewBridgeErrorCodes.PathBlocked, controller.ActionChainGuard.LastReasonCode);
    }

    [TestMethod]
    public async Task GetOrCreateDriverAsync_RestoresLeaseSnapshotAndKeepsGenerationMonotonic()
    {
        var descriptor = CreateDescriptor("penny");
        var supervisor1 = new NpcRuntimeSupervisor();
        var driver1 = await supervisor1.GetOrCreateDriverAsync(descriptor, _tempDir, CancellationToken.None);
        var lease = driver1.Instance.AcquirePrivateChatSessionLease("pc-1", "private_chat", "private_chat_session_active");
        await driver1.SyncAsync(CancellationToken.None);

        var supervisor2 = new NpcRuntimeSupervisor();
        var driver2 = await supervisor2.GetOrCreateDriverAsync(descriptor, _tempDir, CancellationToken.None);
        var restored = supervisor2.Snapshot().Single();

        Assert.IsNotNull(restored.ActivePrivateChatSessionLease);
        Assert.AreEqual("pc-1", restored.ActivePrivateChatSessionLease!.ConversationId);
        Assert.AreEqual(1, restored.ActivePrivateChatSessionLease.Generation);
        Assert.AreEqual(NpcAutonomyLoopState.Paused, restored.AutonomyLoopState);

        var replacement = driver2.Instance.AcquirePrivateChatSessionLease("pc-2", "private_chat", "private_chat_session_active");
        Assert.AreEqual(2, driver2.Instance.Snapshot().ActivePrivateChatSessionLease?.Generation);

        replacement.Dispose();
        lease.Dispose();
    }

    [TestMethod]
    public async Task PrivateChatSessionLeaseCoordinator_PersistsAcquireAndDisposeAcrossSupervisorRebuild()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var resolver = new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot);
        var binding = resolver.Resolve("penny", "save-1");
        var coordinator = new StardewNpcPrivateChatSessionLeaseCoordinator(_tempDir, supervisor, resolver);

        var lease = await coordinator.AcquireAsync(
            new PrivateChatSessionLeaseRequest("penny", "save-1", "pc-1", "private_chat", "private_chat_session_active"),
            CancellationToken.None);

        var restoredSupervisor = new NpcRuntimeSupervisor();
        _ = await restoredSupervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        var restored = restoredSupervisor.Snapshot().Single();

        Assert.IsNotNull(restored.ActivePrivateChatSessionLease);
        Assert.AreEqual("pc-1", restored.ActivePrivateChatSessionLease!.ConversationId);
        Assert.AreEqual("private_chat", restored.ActivePrivateChatSessionLease.Owner);
        Assert.AreEqual(1, restored.ActivePrivateChatSessionLease.Generation);

        lease.Dispose();

        var clearedSupervisor = new NpcRuntimeSupervisor();
        _ = await clearedSupervisor.GetOrCreateDriverAsync(binding.Descriptor, _tempDir, CancellationToken.None);
        var cleared = clearedSupervisor.Snapshot().Single();

        Assert.IsNull(cleared.ActivePrivateChatSessionLease);
    }

    private static NpcRuntimeDescriptor CreateDescriptor(string npcId)
        => new(
            npcId,
            npcId,
            "stardew-valley",
            "save-1",
            "default",
            "stardew",
            "pack-root",
            $"sdv_save-1_{npcId}_default");

    private void CreatePack(string npcId, string displayName)
    {
        var root = Path.Combine(_packRoot, npcId, "default");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "SOUL.md"), $"# {displayName}\n\n{npcId}-pack-soul");
        File.WriteAllText(Path.Combine(root, "facts.md"), $"{displayName} facts");
        File.WriteAllText(Path.Combine(root, "voice.md"), $"{displayName} voice");
        File.WriteAllText(Path.Combine(root, "boundaries.md"), $"{displayName} boundaries");
        File.WriteAllText(Path.Combine(root, "skills.json"), """{"required":[],"optional":[]}""");

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
}
