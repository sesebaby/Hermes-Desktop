namespace Hermes.Agent.Runtime;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class NpcRuntimeLogWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _logPath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public NpcRuntimeLogWriter(string logPath)
    {
        if (string.IsNullOrWhiteSpace(logPath))
            throw new ArgumentException("logPath is required.", nameof(logPath));

        _logPath = logPath;
        Directory.CreateDirectory(Path.GetDirectoryName(_logPath) ?? ".");
    }

    public async Task WriteAsync(NpcRuntimeLogRecord record, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            await File.AppendAllTextAsync(
                _logPath,
                JsonSerializer.Serialize(record, JsonOptions) + "\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}

public sealed record NpcRuntimeLogRecord(
    DateTime TimestampUtc,
    string TraceId,
    string NpcId,
    string GameId,
    string? SessionId,
    string ActionType,
    string? Target,
    string Stage,
    string Result,
    long LatencyMs = 0,
    string? CommandId = null,
    string? Error = null);
