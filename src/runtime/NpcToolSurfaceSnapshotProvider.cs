namespace Hermes.Agent.Runtime;

using Hermes.Agent.Core;

public sealed class NpcToolSurfaceSnapshotProvider : INpcToolSurfaceSnapshotProvider
{
    private readonly object _gate = new();
    private readonly Func<IEnumerable<ITool>> _toolProvider;
    private readonly Func<DateTime> _nowUtc;
    private string? _lastFingerprint;
    private long _snapshotVersion;

    public NpcToolSurfaceSnapshotProvider(
        Func<IEnumerable<ITool>> toolProvider,
        Func<DateTime>? nowUtc = null)
    {
        _toolProvider = toolProvider ?? throw new ArgumentNullException(nameof(toolProvider));
        _nowUtc = nowUtc ?? (() => DateTime.UtcNow);
    }

    public NpcToolSurfaceSnapshot Capture()
    {
        var toolSurface = NpcToolSurface.FromTools(_toolProvider());
        lock (_gate)
        {
            if (!string.Equals(_lastFingerprint, toolSurface.Fingerprint, StringComparison.Ordinal))
            {
                _lastFingerprint = toolSurface.Fingerprint;
                _snapshotVersion++;
            }

            return new NpcToolSurfaceSnapshot(toolSurface, _snapshotVersion, _nowUtc());
        }
    }
}
