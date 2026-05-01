using Hermes.Agent.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Runtime;

[TestClass]
public sealed class NpcRuntimeInstanceTests
{
    [TestMethod]
    public async Task AcquirePrivateChatSessionLease_ReplacingLeasePreventsOldGenerationFromReleasingNewLease()
    {
        var descriptor = CreateDescriptor("haley");
        var instance = new NpcRuntimeInstance(
            descriptor,
            new NpcNamespace(Path.Combine(Path.GetTempPath(), "hermes-runtime-instance-tests", Guid.NewGuid().ToString("N")), descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId));
        await instance.StartAsync(CancellationToken.None);

        var first = instance.AcquirePrivateChatSessionLease("pc-1", "private_chat", "private_chat_session_active");
        var second = instance.AcquirePrivateChatSessionLease("pc-2", "private_chat", "private_chat_session_active");

        first.Dispose();
        var snapshotAfterFirstDispose = instance.Snapshot();
        Assert.IsNotNull(snapshotAfterFirstDispose.ActivePrivateChatSessionLease);
        Assert.AreEqual("pc-2", snapshotAfterFirstDispose.ActivePrivateChatSessionLease!.ConversationId);
        Assert.AreEqual(2, snapshotAfterFirstDispose.ActivePrivateChatSessionLease.Generation);

        second.Dispose();
        Assert.IsNull(instance.Snapshot().ActivePrivateChatSessionLease);
    }

    [TestMethod]
    public async Task MarkAutonomyStatus_UpdatesStructuredSnapshotFields()
    {
        var descriptor = CreateDescriptor("penny");
        var instance = new NpcRuntimeInstance(
            descriptor,
            new NpcNamespace(Path.Combine(Path.GetTempPath(), "hermes-runtime-instance-tests", Guid.NewGuid().ToString("N")), descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId));
        await instance.StartAsync(CancellationToken.None);
        var tickAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);

        instance.MarkAutonomyPaused("private_chat_session_active", "bridge-a", 3);
        var paused = instance.Snapshot();
        Assert.AreEqual(NpcAutonomyLoopState.Paused, paused.AutonomyLoopState);
        Assert.AreEqual("private_chat_session_active", paused.PauseReason);
        Assert.AreEqual("bridge-a", paused.CurrentBridgeKey);
        Assert.AreEqual(3, paused.CurrentAutonomyHandleGeneration);

        instance.MarkAutonomyRunning("bridge-a", 4, tickAt);
        instance.RecordAutonomyRestart("bridge-a", 4);
        var running = instance.Snapshot();
        Assert.AreEqual(NpcAutonomyLoopState.Running, running.AutonomyLoopState);
        Assert.IsNull(running.PauseReason);
        Assert.AreEqual(tickAt, running.LastAutomaticTickAtUtc);
        Assert.AreEqual("bridge-a", running.CurrentBridgeKey);
        Assert.AreEqual(4, running.CurrentAutonomyHandleGeneration);
        Assert.AreEqual(1, running.AutonomyRestartCount);
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
}
