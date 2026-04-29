namespace StardewHermesBridge.Logging;

using System.Text.Json;
using StardewModdingAPI;

public sealed class SmapiBridgeLogger
{
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
