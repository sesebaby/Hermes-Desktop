using Hermes.Agent.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Runtime;

[TestClass]
public class NpcAutonomyBudgetTests
{
    [TestMethod]
    public async Task TryAcquireLlmSlotAsync_AllowsOnlyConfiguredConcurrency()
    {
        var budget = new NpcAutonomyBudget(new NpcAutonomyBudgetOptions(MaxConcurrentLlmRequests: 1));

        await using var first = await budget.TryAcquireLlmSlotAsync("haley", CancellationToken.None);
        var second = await budget.TryAcquireLlmSlotAsync("penny", CancellationToken.None);

        Assert.IsNotNull(first);
        Assert.IsNull(second);
    }

    [TestMethod]
    public async Task TryAcquireLlmSlotAsync_ReleasesSlotWhenLeaseDisposed()
    {
        var budget = new NpcAutonomyBudget(new NpcAutonomyBudgetOptions(MaxConcurrentLlmRequests: 1));
        var first = await budget.TryAcquireLlmSlotAsync("haley", CancellationToken.None);
        Assert.IsNotNull(first);
        await first.DisposeAsync();

        await using var second = await budget.TryAcquireLlmSlotAsync("penny", CancellationToken.None);

        Assert.IsNotNull(second);
        Assert.AreEqual("penny", second.NpcId);
    }

    [TestMethod]
    public void CheckToolIterationLimit_ReturnsExitReasonAtLimit()
    {
        var budget = new NpcAutonomyBudget(new NpcAutonomyBudgetOptions(MaxToolIterations: 2));

        Assert.AreEqual(NpcAutonomyExitReason.None, budget.CheckToolIterationLimit(1));
        Assert.AreEqual(NpcAutonomyExitReason.MaxToolIterations, budget.CheckToolIterationLimit(2));
    }
}
