using System.Runtime.CompilerServices;
using Hermes.Agent.Core;
using Hermes.Agent.LLM;
using Hermes.Agent.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Runtime;

[TestClass]
public class NpcRuntimeContextFactoryTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "hermes-npc-context-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public async Task Create_BuildsRuntimeLocalContextManagerAndPromptBuilder()
    {
        var ns = new NpcNamespace(_tempDir, "stardew-valley", "save-1", "haley", "default");
        var factory = new NpcRuntimeContextFactory();

        var bundle = factory.Create(ns, new FakeChatClient(), NullLoggerFactory.Instance);
        var messages = await bundle.ContextManager.PrepareContextAsync(
            "sdv_save-1_haley_default",
            "Observe the latest Stardew facts and decide whether to act.",
            ["location=Town", "tile=42,17"],
            CancellationToken.None);

        Assert.AreEqual(Path.Combine(ns.SoulHomePath, "SOUL.md"), bundle.SoulService.SoulFilePath);
        Assert.IsTrue(bundle.PromptBuilder.SystemPrompt.Contains("autonomous NPC", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(messages.Any(message =>
            message.Role == "system" &&
            message.Content.Contains("[Agent Identity]", StringComparison.Ordinal)));
        Assert.AreEqual("user", messages[^1].Role);
        Assert.AreEqual("Observe the latest Stardew facts and decide whether to act.", messages[^1].Content);
    }

    private sealed class FakeChatClient : IChatClient
    {
        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("summary");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
            => Task.FromResult(new ChatResponse { Content = "ok", FinishReason = "stop" });

        public async IAsyncEnumerable<string> StreamAsync(IEnumerable<Message> messages, [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<StreamEvent> StreamAsync(
            string? systemPrompt,
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition>? tools = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
