namespace StardewHermesBridge.Logging;

using System.Text.Json;
using StardewModdingAPI;

public sealed class SmapiBridgeLogger
{
    public const string NpcClickObserved = "npc_click_observed";
    public const string NpcClickRejected = "npc_click_rejected";
    public const string OriginalDialogueObserved = "original_dialogue_observed";
    public const string OriginalDialogueCompleted = "original_dialogue_completed";
    public const string CustomDialogueQueued = "custom_dialogue_queued";
    public const string CustomDialogueDisplayed = "custom_dialogue_displayed";

    private readonly string _logPath;
    private readonly IMonitor _monitor;
    private readonly object _gate = new();

    public SmapiBridgeLogger(string modDirectory, IMonitor monitor)
    {
        _monitor = monitor;
        var logDir = Path.Combine(modDirectory, "logs");
        Directory.CreateDirectory(logDir);
        _logPath = Path.Combine(logDir, "bridge.jsonl");
    }

    public void Write(string endpoint, string? npcId, string actionType, string traceId, string? commandId, string result, string? error)
    {
        var record = new
        {
            timestampUtc = DateTime.UtcNow,
            endpoint,
            npcId,
            actionType,
            traceId,
            commandId,
            result,
            error
        };
        var json = JsonSerializer.Serialize(record);
        lock (_gate)
            File.AppendAllText(_logPath, json + Environment.NewLine);

        _monitor.Log($"{endpoint} npc={npcId ?? "-"} trace={traceId} command={commandId ?? "-"} result={result} error={error ?? "-"}", LogLevel.Info);
    }
}
