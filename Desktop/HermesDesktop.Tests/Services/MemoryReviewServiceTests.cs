using Hermes.Agent.Core;
using Hermes.Agent.LLM;
using Hermes.Agent.Memory;
using Hermes.Agent.Plugins;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace HermesDesktop.Tests.Services;

[TestClass]
public class MemoryReviewServiceTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"hermes-memreview-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public void MarkTurnAndCheckDue_UsesConfiguredNudgeInterval()
    {
        var service = CreateService(nudgeInterval: 2);

        Assert.IsFalse(service.MarkTurnAndCheckDue("first response", interrupted: false));
        Assert.IsTrue(service.MarkTurnAndCheckDue("second response", interrupted: false));
        Assert.IsFalse(service.MarkTurnAndCheckDue("", interrupted: false));
        Assert.IsFalse(service.MarkTurnAndCheckDue("cancelled response", interrupted: true));
    }

    [TestMethod]
    public async Task ReviewConversationAsync_ExecutesMemoryToolCallAndBridge()
    {
        var chatClient = new Mock<IChatClient>(MockBehavior.Strict);
        chatClient
            .Setup(c => c.CompleteWithToolsAsync(
                It.IsAny<IEnumerable<Message>>(),
                It.IsAny<IEnumerable<ToolDefinition>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse
            {
                ToolCalls = new List<ToolCall>
                {
                    new()
                    {
                        Id = "call_1",
                        Name = "memory",
                        Arguments = "{\"action\":\"add\",\"target\":\"user\",\"content\":\"User prefers concise replies.\"}"
                    }
                }
            });

        var memoryManager = new MemoryManager(
            _tempDir,
            chatClient.Object,
            NullLogger<MemoryManager>.Instance);
        var plugin = new RecordingMemoryPlugin();
        var pluginManager = new PluginManager(NullLogger<PluginManager>.Instance);
        pluginManager.Register(plugin);
        var service = new MemoryReviewService(
            chatClient.Object,
            memoryManager,
            NullLogger<MemoryReviewService>.Instance,
            pluginManager,
            nudgeInterval: 1);

        var results = await service.ReviewConversationAsync(
            new[]
            {
                new Message { Role = "user", Content = "Please keep answers concise." },
                new Message { Role = "assistant", Content = "Noted." }
            },
            CancellationToken.None);

        Assert.AreEqual(1, results.Count);
        Assert.IsTrue(results[0].Success, results[0].Content);
        Assert.AreEqual("User prefers concise replies.", await File.ReadAllTextAsync(Path.Combine(_tempDir, "USER.md")));
        CollectionAssert.Contains(plugin.Writes, "add:user:User prefers concise replies.");
    }

    private MemoryReviewService CreateService(int nudgeInterval)
    {
        var chatClient = new Mock<IChatClient>(MockBehavior.Loose).Object;
        var memoryManager = new MemoryManager(
            _tempDir,
            chatClient,
            NullLogger<MemoryManager>.Instance);
        return new MemoryReviewService(
            chatClient,
            memoryManager,
            NullLogger<MemoryReviewService>.Instance,
            nudgeInterval: nudgeInterval);
    }

    private sealed class RecordingMemoryPlugin : PluginBase
    {
        public override string Name => "recording-memory";
        public override string Category => "memory";
        public List<string> Writes { get; } = new();

        public override Task OnMemoryWriteAsync(string action, string target, string content, CancellationToken ct)
        {
            Writes.Add($"{action}:{target}:{content}");
            return Task.CompletedTask;
        }
    }
}
