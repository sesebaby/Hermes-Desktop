using Hermes.Agent.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Runtime;

[TestClass]
public class NpcRuntimeSupervisorTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "hermes-runtime-supervisor-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public async Task StartAsync_RegistersRunningInstanceAndCreatesNamespace()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var descriptor = CreateDescriptor("haley");

        await supervisor.StartAsync(descriptor, _tempDir, CancellationToken.None);

        var snapshot = supervisor.Snapshot().Single();
        Assert.AreEqual("haley", snapshot.NpcId);
        Assert.AreEqual(NpcRuntimeState.Running, snapshot.State);
        Assert.IsTrue(Directory.Exists(Path.Combine(_tempDir, "runtime", "stardew", "games", "stardew-valley", "saves", "save-1", "npc", "haley", "profiles", "default")));
    }

    [TestMethod]
    public async Task StartAsync_RejectsDuplicateNpcRuntimeForSameProfile()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var descriptor = CreateDescriptor("haley");
        await supervisor.StartAsync(descriptor, _tempDir, CancellationToken.None);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            await supervisor.StartAsync(descriptor, _tempDir, CancellationToken.None));
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
