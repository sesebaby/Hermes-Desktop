using Hermes.Agent.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Runtime;

[TestClass]
public class ResourceClaimRegistryTests
{
    [TestMethod]
    public void TryClaim_RejectsSecondCommandForSameNpc()
    {
        var registry = new ResourceClaimRegistry();

        var first = registry.TryClaim(new ResourceClaimRequest("cmd-1", "haley", "trace-1"));
        var second = registry.TryClaim(new ResourceClaimRequest("cmd-2", "haley", "trace-2"));

        Assert.IsTrue(first.Accepted);
        Assert.IsFalse(second.Accepted);
        Assert.AreEqual("command_conflict", second.ErrorCode);
        Assert.AreEqual("cmd-1", second.ConflictingClaim?.CommandId);
    }

    [TestMethod]
    public void TryClaim_ReplaysSameIdempotencyKeyWithoutNewClaim()
    {
        var registry = new ResourceClaimRegistry();

        var first = registry.TryClaim(new ResourceClaimRequest("cmd-1", "haley", "trace-1", IdempotencyKey: "idem-1"));
        var replay = registry.TryClaim(new ResourceClaimRequest("cmd-2", "haley", "trace-2", IdempotencyKey: "idem-1"));

        Assert.IsTrue(first.Accepted);
        Assert.IsTrue(replay.Accepted);
        Assert.IsTrue(replay.WasIdempotentReplay);
        Assert.AreEqual("cmd-1", replay.Claim?.CommandId);
        Assert.AreEqual(1, registry.Snapshot().Count);
    }

    [TestMethod]
    public void TryClaim_RejectsTargetTileConflictAndAllowsAfterRelease()
    {
        var registry = new ResourceClaimRegistry();
        var tile = new ClaimedTile("Town", 42, 17);

        var first = registry.TryClaim(new ResourceClaimRequest("cmd-1", "haley", "trace-1", TargetTile: tile));
        var conflict = registry.TryClaim(new ResourceClaimRequest("cmd-2", "penny", "trace-2", TargetTile: tile));
        var released = registry.Release("cmd-1");
        var afterRelease = registry.TryClaim(new ResourceClaimRequest("cmd-2", "penny", "trace-2", TargetTile: tile));

        Assert.IsTrue(first.Accepted);
        Assert.IsFalse(conflict.Accepted);
        Assert.IsTrue(released);
        Assert.IsTrue(afterRelease.Accepted);
    }
}
