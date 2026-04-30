using Hermes.Agent.Games.Stardew;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Stardew;

[TestClass]
public class StardewGameAdapterTests
{
    [TestMethod]
    public void Constructor_ComposesSeparateCommandQueryAndEventServices()
    {
        var adapter = new StardewGameAdapter(new FakeSmapiClient(), "save-1", npcId: "haley");

        Assert.AreEqual("stardew", adapter.AdapterId);
        Assert.IsInstanceOfType(adapter.Commands, typeof(StardewCommandService));
        Assert.IsInstanceOfType(adapter.Queries, typeof(StardewQueryService));
        Assert.IsInstanceOfType(adapter.Events, typeof(StardewEventSource));
    }

    private sealed class FakeSmapiClient : ISmapiModApiClient
    {
        public Task<StardewBridgeResponse<TData>> SendAsync<TPayload, TData>(
            string route,
            StardewBridgeEnvelope<TPayload> envelope,
            CancellationToken ct)
            => throw new NotSupportedException();
    }
}
