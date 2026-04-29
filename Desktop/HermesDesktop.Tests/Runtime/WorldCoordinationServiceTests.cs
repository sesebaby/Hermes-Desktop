using Hermes.Agent.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Runtime;

[TestClass]
public class WorldCoordinationServiceTests
{
    [TestMethod]
    public void TryClaimMove_UsesSharedResourceClaims()
    {
        var service = new WorldCoordinationService(new ResourceClaimRegistry());
        var tile = new ClaimedTile("Town", 42, 17);

        var first = service.TryClaimMove("cmd-1", "haley", "trace-1", tile, tile, "idem-1");
        var second = service.TryClaimMove("cmd-2", "penny", "trace-2", tile, tile, "idem-2");

        Assert.IsTrue(first.Accepted);
        Assert.IsFalse(second.Accepted);
        Assert.AreEqual("command_conflict", second.ErrorCode);
    }
}
