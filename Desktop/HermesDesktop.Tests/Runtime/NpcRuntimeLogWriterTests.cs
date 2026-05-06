using System.Text.Json;
using Hermes.Agent.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Runtime;

[TestClass]
public class NpcRuntimeLogWriterTests
{
    [TestMethod]
    public async Task WriteAsync_AppendsJsonlRecordWithTraceAndCommand()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-runtime-log-tests", Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempDir, "runtime.jsonl");
        try
        {
            var writer = new NpcRuntimeLogWriter(logPath);

            await writer.WriteAsync(new NpcRuntimeLogRecord(
                DateTime.UtcNow,
                "trace-1",
                "haley",
                "stardew-valley",
                "session-1",
                "move",
                "Town:42,17",
                "bridge_ack",
                "queued",
                CommandId: "cmd-1"), CancellationToken.None);

            var line = File.ReadAllLines(logPath).Single();
            using var doc = JsonDocument.Parse(line);

            Assert.AreEqual("trace-1", doc.RootElement.GetProperty("traceId").GetString());
            Assert.AreEqual("cmd-1", doc.RootElement.GetProperty("commandId").GetString());
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task WriteAsync_WithExecutorMode_WritesOptionalExecutorMode()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-runtime-log-executor-mode-tests", Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(tempDir, "runtime.jsonl");
        try
        {
            var writer = new NpcRuntimeLogWriter(logPath);

            await writer.WriteAsync(new NpcRuntimeLogRecord(
                DateTime.UtcNow,
                "trace-1",
                "haley",
                "stardew-valley",
                "session-1",
                "local_executor",
                "stardew_move",
                "completed",
                "queued",
                ExecutorMode: "model_called"), CancellationToken.None);

            var line = File.ReadAllLines(logPath).Single();
            using var doc = JsonDocument.Parse(line);

            Assert.AreEqual("model_called", doc.RootElement.GetProperty("executorMode").GetString());
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public void NpcRuntimeLogRecord_DeserializesOlderJsonWithoutExecutorMode()
    {
        var oldJson =
            """
            {
              "timestampUtc": "2026-05-06T00:00:00Z",
              "traceId": "trace-old",
              "npcId": "haley",
              "gameId": "stardew-valley",
              "sessionId": "session-1",
              "actionType": "local_executor",
              "target": "stardew_move",
              "stage": "completed",
              "result": "queued"
            }
            """;

        var record = JsonSerializer.Deserialize<NpcRuntimeLogRecord>(
            oldJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.IsNotNull(record);
        Assert.AreEqual("trace-old", record.TraceId);
        Assert.IsNull(record.ExecutorMode);
    }
}
