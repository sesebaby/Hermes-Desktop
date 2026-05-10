using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Stardew;

[TestClass]
public sealed class StardewRuntimeHostStateStoreTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-host-state-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public async Task SetBridgeKeyAsync_WithExistingCursor_PreservesCursorAndInitialDrainState()
    {
        var store = new StardewRuntimeHostStateStore(Path.Combine(_tempDir, "state.db"));
        var cursor = new GameEventCursor("evt-9", 9);
        await store.CommitBatchAsync(cursor, initialPrivateChatHistoryDrained: true, CancellationToken.None);

        await store.SetBridgeKeyAsync("localhost:123:2026-05-10T00:00:00.0000000Z:save-42", CancellationToken.None);

        var state = await store.LoadAsync(CancellationToken.None);
        Assert.AreEqual("evt-9", state.SourceCursor.Since);
        Assert.AreEqual(9L, state.SourceCursor.Sequence);
        Assert.IsTrue(state.InitialPrivateChatHistoryDrained);
        Assert.AreEqual("localhost:123:2026-05-10T00:00:00.0000000Z:save-42", state.BridgeKey);
    }
}
