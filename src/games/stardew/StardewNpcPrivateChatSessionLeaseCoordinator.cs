namespace Hermes.Agent.Games.Stardew;

using Hermes.Agent.Game;
using Hermes.Agent.Runtime;

public sealed class StardewNpcPrivateChatSessionLeaseCoordinator : IPrivateChatSessionLeaseCoordinator
{
    private readonly string _runtimeRoot;
    private readonly NpcRuntimeSupervisor _runtimeSupervisor;
    private readonly StardewNpcRuntimeBindingResolver _bindingResolver;

    public StardewNpcPrivateChatSessionLeaseCoordinator(
        string runtimeRoot,
        NpcRuntimeSupervisor runtimeSupervisor,
        StardewNpcRuntimeBindingResolver bindingResolver)
    {
        _runtimeRoot = runtimeRoot;
        _runtimeSupervisor = runtimeSupervisor;
        _bindingResolver = bindingResolver;
    }

    public async Task<IPrivateChatSessionLease> AcquireAsync(PrivateChatSessionLeaseRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var binding = _bindingResolver.Resolve(request.NpcId, request.SaveId);
        var driver = await _runtimeSupervisor.GetOrCreateDriverAsync(binding.Descriptor, _runtimeRoot, ct);
        driver.Instance.Namespace.SeedPersonaPack(binding.Pack);

        var lease = driver.Instance.AcquirePrivateChatSessionLease(request.ConversationId, request.Owner, request.Reason);
        try
        {
            await driver.SyncAsync(ct);
            return new PersistedPrivateChatSessionLease(lease, driver);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private sealed class PersistedPrivateChatSessionLease : IPrivateChatSessionLease
    {
        private readonly IPrivateChatSessionLease _inner;
        private readonly NpcRuntimeDriver _driver;
        private bool _disposed;

        public PersistedPrivateChatSessionLease(IPrivateChatSessionLease inner, NpcRuntimeDriver driver)
        {
            _inner = inner;
            _driver = driver;
        }

        public string NpcId => _inner.NpcId;

        public string ConversationId => _inner.ConversationId;

        public string Owner => _inner.Owner;

        public int Generation => _inner.Generation;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _inner.Dispose();
            _driver.SyncAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
