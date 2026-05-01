using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Hermes.Agent.Runtime;
using HermesDesktop.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.ApplicationModel.Resources;
using System.Globalization;

namespace HermesDesktop.Services;

internal sealed class NpcRuntimeWorkspaceService
{
    private static readonly ResourceLoader ResourceLoader = new();
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
        var runtimeSnapshots = Array.Empty<NpcRuntimeSnapshot>();
        try
        {
            runtimeSnapshots = _supervisor.Snapshot().ToArray();
            var active = runtimeSnapshots
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
        var bridgeHealth = runtimeSnapshots.Any(IsBridgeAttached)
            ? GetResource("DashNpcRuntimeBridgeAttached", "Attached")
            : GetResource("DashNpcRuntimeBridgeNotConnected", "Not connected");
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
            LastError = snapshot.LastError ?? "",
            LoopAndWaitSummary = FormatLoopAndWaitSummary(snapshot),
            LeaseAndActionSummary = FormatLeaseAndActionSummary(snapshot),
            PendingAndCursorSummary = FormatIngressPendingCursorSummary(snapshot)
        };

    private static string FormatLoopAndWaitSummary(NpcRuntimeSnapshot snapshot)
        => FormatResource(
            "DashNpcRuntimeLoopSummaryFormat",
            "Loop: {0} | Wait: {1}",
            snapshot.AutonomyLoopState.ToString(),
            FormatWaitSummary(snapshot));

    private static string FormatLeaseAndActionSummary(NpcRuntimeSnapshot snapshot)
        => FormatResource(
            "DashNpcRuntimeLeaseActionSummaryFormat",
            "Session: {0} | Action: {1}",
            FormatLeaseSummary(snapshot),
            FormatActionSummary(snapshot));

    private static string FormatIngressPendingCursorSummary(NpcRuntimeSnapshot snapshot)
        => FormatResource(
            "DashNpcRuntimeIngressPendingCursorSummaryFormat",
            "Ingress: {0} | Pending: {1} | Cursor: {2}",
            snapshot.Controller.InboxDepth,
            FormatPendingSummary(snapshot),
            FormatCursorSummary(snapshot.Controller.EventCursor));

    private static string FormatWaitSummary(NpcRuntimeSnapshot snapshot)
    {
        if (snapshot.Controller.NextWakeAtUtc.HasValue)
        {
            var wakeText = snapshot.Controller.NextWakeAtUtc.Value
                .ToLocalTime()
                .ToString("T", CultureInfo.CurrentCulture);
            if (!string.IsNullOrWhiteSpace(snapshot.PauseReason))
            {
                return FormatResource(
                    "DashNpcRuntimeWaitUntilFormat",
                    "{0} until {1}",
                    snapshot.PauseReason,
                    wakeText);
            }

            return wakeText;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.PauseReason))
            return snapshot.PauseReason;

        return GetResource("DashNpcRuntimeNone", "-");
    }

    private static string FormatLeaseSummary(NpcRuntimeSnapshot snapshot)
    {
        if (snapshot.ActivePrivateChatSessionLease is null)
            return GetResource("DashNpcRuntimeNone", "-");

        return FormatResource(
            "DashNpcRuntimeLeaseValueFormat",
            "{0}#{1}",
            snapshot.ActivePrivateChatSessionLease.Owner,
            snapshot.ActivePrivateChatSessionLease.Generation);
    }

    private static string FormatActionSummary(NpcRuntimeSnapshot snapshot)
    {
        if (snapshot.Controller.ActionSlot is null)
            return GetResource("DashNpcRuntimeNone", "-");

        return FormatResource(
            "DashNpcRuntimeActionValueFormat",
            "{0}/{1}",
            snapshot.Controller.ActionSlot.SlotName,
            snapshot.Controller.ActionSlot.CommandId ?? snapshot.Controller.ActionSlot.WorkItemId);
    }

    private static string FormatPendingSummary(NpcRuntimeSnapshot snapshot)
    {
        if (snapshot.Controller.PendingWorkItem is null)
            return GetResource("DashNpcRuntimeNone", "-");

        return FormatResource(
            "DashNpcRuntimePendingValueFormat",
            "{0} ({1})",
            snapshot.Controller.PendingWorkItem.WorkType,
            snapshot.Controller.PendingWorkItem.Status);
    }

    private static string FormatCursorSummary(GameEventCursor cursor)
    {
        if (cursor.Sequence.HasValue)
            return FormatResource("DashNpcRuntimeCursorSequenceFormat", "seq {0}", cursor.Sequence.Value);

        if (!string.IsNullOrWhiteSpace(cursor.Since))
            return cursor.Since;

        return GetResource("DashNpcRuntimeNone", "-");
    }

    private static bool IsBridgeAttached(NpcRuntimeSnapshot snapshot)
        => !string.IsNullOrWhiteSpace(snapshot.CurrentBridgeKey) &&
           !string.Equals(snapshot.PauseReason, StardewBridgeErrorCodes.BridgeUnavailable, StringComparison.OrdinalIgnoreCase) &&
           !string.Equals(snapshot.PauseReason, StardewBridgeErrorCodes.BridgeStaleDiscovery, StringComparison.OrdinalIgnoreCase);

    private static string FormatResource(string key, string fallbackFormat, params object[] args)
        => string.Format(CultureInfo.CurrentCulture, GetResource(key, fallbackFormat), args);

    private static string GetResource(string key, string fallback)
    {
        var value = ResourceLoader.GetString(key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}

internal sealed record NpcRuntimeWorkspaceSnapshot(
    IReadOnlyList<NpcRuntimeItem> Items,
    string BridgeHealth,
    string LastTraceId,
    string LastError,
    string RuntimeRoot);
