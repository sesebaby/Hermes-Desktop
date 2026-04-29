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
}
