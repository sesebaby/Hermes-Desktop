using Hermes.Agent.Game;
using Hermes.Agent.Runtime;
using HermesDesktop.Models;
using Microsoft.Extensions.Logging;

namespace HermesDesktop.Services;

internal sealed class NpcRuntimeWorkspaceService
{
    private readonly INpcPackLoader _packLoader;
    private readonly NpcRuntimeSupervisor _supervisor;
    private readonly ILogger<NpcRuntimeWorkspaceService> _logger;

    public NpcRuntimeWorkspaceService(
        INpcPackLoader packLoader,
        NpcRuntimeSupervisor supervisor,
        ILogger<NpcRuntimeWorkspaceService> logger)
    {
        _packLoader = packLoader;
        _supervisor = supervisor;
        _logger = logger;
    }

    public string RuntimeRoot => Path.Combine(HermesEnvironment.HermesHomePath, "hermes-cs");

    public string PackRoot => Path.Combine(HermesEnvironment.AgentWorkingDirectory, "src", "game", "stardew", "personas");

    public NpcRuntimeWorkspaceSnapshot GetSnapshot()
    {
        IReadOnlyList<NpcRuntimeItem> items;
        try
        {
            var active = _supervisor.Snapshot()
                .Select(ToItem)
                .ToList();

            if (active.Count == 0)
            {
                active.AddRange(_packLoader.LoadPacks(PackRoot).Select(pack => new NpcRuntimeItem
                {
                    NpcId = pack.Manifest.NpcId,
                    DisplayName = pack.Manifest.DisplayName,
                    State = "Discovered",
                    SessionId = "",
                    LastTraceId = "",
                    LastError = ""
                }));
            }

            items = active;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read NPC runtime snapshot");
            return new NpcRuntimeWorkspaceSnapshot(
                Array.Empty<NpcRuntimeItem>(),
                "Unavailable",
                "",
                ex.Message,
                RuntimeRoot);
        }

        var lastTrace = items.Select(item => item.LastTraceId).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
        var lastError = items.Select(item => item.LastError).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
        var bridgeHealth = "Not connected";
        return new NpcRuntimeWorkspaceSnapshot(items, bridgeHealth, lastTrace, lastError, RuntimeRoot);
    }

    public void OpenRuntimeDirectory()
    {
        Directory.CreateDirectory(RuntimeRoot);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = RuntimeRoot,
            UseShellExecute = true
        });
    }

    private static NpcRuntimeItem ToItem(NpcRuntimeSnapshot snapshot)
        => new()
        {
            NpcId = snapshot.NpcId,
            DisplayName = snapshot.DisplayName,
            State = snapshot.State.ToString(),
            SessionId = snapshot.SessionId,
            LastTraceId = snapshot.LastTraceId ?? "",
            LastError = snapshot.LastError ?? ""
        };
}

internal sealed record NpcRuntimeWorkspaceSnapshot(
    IReadOnlyList<NpcRuntimeItem> Items,
    string BridgeHealth,
    string LastTraceId,
    string LastError,
    string RuntimeRoot);
