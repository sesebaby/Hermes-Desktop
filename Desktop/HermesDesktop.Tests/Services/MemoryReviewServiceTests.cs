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
    public void MemoryReviewDefaults_AreFiveForDesktopFallbacks()
    {
        Assert.AreEqual(5, MemoryReviewDefaults.NudgeInterval);
        Assert.AreEqual(5, MemoryReviewDefaults.SkillCreationNudgeInterval);
    }

    [TestMethod]
    public void MarkTurnAndCheckDue_DefaultConstructorUsesFiveTurnNudgeInterval()
    {
        var chatClient = new Mock<IChatClient>(MockBehavior.Loose).Object;
        var memoryManager = new MemoryManager(
            _tempDir,
            chatClient,
            NullLogger<MemoryManager>.Instance);
        var service = new MemoryReviewService(
            chatClient,
            memoryManager,
            NullLogger<MemoryReviewService>.Instance);

        for (var i = 0; i < 4; i++)
            Assert.IsFalse(service.MarkTurnAndCheckDue($"response {i}", interrupted: false));

        Assert.IsTrue(service.MarkTurnAndCheckDue("response 5", interrupted: false));
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

    [TestMethod]
    public async Task ReviewConversationAsync_ReplaysReasoningFieldsAfterReviewToolCall()
    {
        List<Message>? secondCallMessages = null;
        var chatClient = new Mock<IChatClient>(MockBehavior.Strict);
        var responses = new Queue<ChatResponse>([
            new ChatResponse
            {
                Content = "",
                ReasoningContent = "I should inspect memory before deciding.",
                Reasoning = "{\"summary\":\"memory check\"}",
                ReasoningDetails = "[{\"type\":\"summary_text\",\"text\":\"memory check\"}]",
                CodexReasoningItems = "[{\"id\":\"rs_mem\",\"type\":\"reasoning\"}]",
                ToolCalls = new List<ToolCall>
                {
                    new()
                    {
                        Id = "call_1",
                        Name = "memory",
                        Arguments = "{\"action\":\"add\",\"target\":\"user\",\"content\":\"User prefers concise replies.\"}"
                    }
                }
            },
            new ChatResponse { Content = "Nothing to save." }
        ]);
        chatClient
            .Setup(c => c.CompleteWithToolsAsync(
                It.IsAny<IEnumerable<Message>>(),
                It.IsAny<IEnumerable<ToolDefinition>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Message>, IEnumerable<ToolDefinition>, CancellationToken>((messages, _, _) =>
            {
                if (messages.Any(message => message.Role == "tool"))
                    secondCallMessages = messages.ToList();
            })
            .ReturnsAsync(() => responses.Dequeue());

        var memoryManager = new MemoryManager(
            _tempDir,
            chatClient.Object,
            NullLogger<MemoryManager>.Instance);
        var service = new MemoryReviewService(
            chatClient.Object,
            memoryManager,
            NullLogger<MemoryReviewService>.Instance,
            nudgeInterval: 1);

        await service.ReviewConversationAsync(
            new[] { new Message { Role = "user", Content = "Please keep answers concise." } },
            reviewMemory: true,
            reviewSkills: false,
            CancellationToken.None);

        Assert.IsNotNull(secondCallMessages, "The review loop should make a second LLM call after executing the review tool.");
        var assistantToolMessage = secondCallMessages!.Single(message => message.Role == "assistant" && message.ToolCalls is { Count: > 0 });
        Assert.AreEqual("I should inspect memory before deciding.", assistantToolMessage.ReasoningContent);
        Assert.AreEqual("{\"summary\":\"memory check\"}", assistantToolMessage.Reasoning);
        Assert.AreEqual("[{\"type\":\"summary_text\",\"text\":\"memory check\"}]", assistantToolMessage.ReasoningDetails);
        Assert.AreEqual("[{\"id\":\"rs_mem\",\"type\":\"reasoning\"}]", assistantToolMessage.CodexReasoningItems);
    }

    [TestMethod]
    public async Task ReviewConversationAsync_UsesPythonMemoryToolDescriptionAndSchema()
    {
        ToolDefinition? captured = null;
        var chatClient = new Mock<IChatClient>(MockBehavior.Strict);
        chatClient
            .Setup(c => c.CompleteWithToolsAsync(
                It.IsAny<IEnumerable<Message>>(),
                It.IsAny<IEnumerable<ToolDefinition>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Message>, IEnumerable<ToolDefinition>, CancellationToken>(
                (_, tools, _) => captured = tools.Single())
            .ReturnsAsync(new ChatResponse { Content = "Nothing to save." });

        var memoryManager = new MemoryManager(
            _tempDir,
            chatClient.Object,
            NullLogger<MemoryManager>.Instance);
        var service = new MemoryReviewService(
            chatClient.Object,
            memoryManager,
            NullLogger<MemoryReviewService>.Instance,
            nudgeInterval: 1);

        await service.ReviewConversationAsync(
            new[] { new Message { Role = "user", Content = "I prefer concise replies." } },
            CancellationToken.None);

        Assert.IsNotNull(captured);
        Assert.AreEqual("memory", captured!.Name);
        StringAssert.Contains(captured.Description, "Write memories as declarative facts");
        CollectionAssert.AreEqual(
            new[] { "action", "target" },
            captured.Parameters.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToArray());
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
