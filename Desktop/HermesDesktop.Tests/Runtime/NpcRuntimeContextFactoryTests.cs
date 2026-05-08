using System.Runtime.CompilerServices;
using Hermes.Agent.Core;
using Hermes.Agent.LLM;
using Hermes.Agent.Runtime;
using Hermes.Agent.Skills;
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
        var skillManager = CreateSkillManager();

        var bundle = factory.Create(ns, new FakeChatClient(), NullLoggerFactory.Instance, skillManager);
        var messages = await bundle.ContextManager.PrepareContextAsync(
            "sdv_save-1_haley_default",
            "Observe the latest Stardew facts and decide whether to act.",
            ["location=Town", "tile=42,17"],
            CancellationToken.None);

        Assert.AreEqual(Path.Combine(ns.SoulHomePath, "SOUL.md"), bundle.SoulService.SoulFilePath);
        Assert.IsTrue(bundle.PromptBuilder.SystemPrompt.Contains("Stardew Valley NPC runtime", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(bundle.PromptBuilder.SystemPrompt.Contains(SystemPrompts.RuntimeFactsGuidance, StringComparison.Ordinal));
        Assert.IsTrue(bundle.PromptBuilder.SystemPrompt.Contains("## Skills (mandatory)", StringComparison.Ordinal));
        Assert.IsTrue(bundle.PromptBuilder.SystemPrompt.Contains("npc-memory-skill", StringComparison.Ordinal));
        Assert.IsTrue(messages.Any(message =>
            message.Role == "system" &&
            message.Content.Contains("[Agent Identity]", StringComparison.Ordinal)));
        Assert.AreEqual("user", messages[^1].Role);
        Assert.AreEqual("Observe the latest Stardew facts and decide whether to act.", messages[^1].Content);
    }

    [TestMethod]
    public async Task Create_AutonomyChannel_OmitsGlobalSkillsMandatoryIndex()
    {
        var ns = new NpcNamespace(_tempDir, "stardew-valley", "save-1", "haley", "default");
        var factory = new NpcRuntimeContextFactory();
        var skillManager = CreateSkillManager();

        var bundle = factory.Create(
            ns,
            new FakeChatClient(),
            NullLoggerFactory.Instance,
            skillManager,
            channelKey: "autonomy");
        var messages = await bundle.ContextManager.PrepareContextAsync(
            "sdv_save-1_haley_default",
            "Observe and decide.",
            ["location=Town"],
            CancellationToken.None);

        Assert.IsFalse(bundle.PromptBuilder.SystemPrompt.Contains("## Skills (mandatory)", StringComparison.Ordinal));
        Assert.IsTrue(bundle.PromptBuilder.SystemPrompt.Contains("session_search", StringComparison.Ordinal));
        Assert.IsTrue(bundle.PromptBuilder.SystemPrompt.Contains("return one JSON intent contract only", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(bundle.PromptBuilder.SystemPrompt.Contains("raw JSON", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(bundle.PromptBuilder.SystemPrompt.Contains("Mechanical actions are executed by the host and local executor", StringComparison.Ordinal));
        Assert.IsFalse(bundle.PromptBuilder.SystemPrompt.Contains("Do not call tools", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(bundle.PromptBuilder.SystemPrompt.Contains(SystemPrompts.RuntimeFactsGuidance, StringComparison.Ordinal));
        Assert.IsFalse(bundle.PromptBuilder.SystemPrompt.Contains("use the registered tools available in this session", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(bundle.PromptBuilder.SystemPrompt.Contains("stardew_navigate_to_tile", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(bundle.PromptBuilder.SystemPrompt.Contains("native desktop environment", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(bundle.PromptBuilder.SystemPrompt.Contains("browser automation", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(bundle.PromptBuilder.SystemPrompt.Contains("web search tools", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(messages.Any(message =>
            message.Role == "system" &&
            message.Content.Contains("[Agent Identity]", StringComparison.Ordinal)));
    }

    private SkillManager CreateSkillManager()
    {
        var skillsDir = Path.Combine(_tempDir, "skills", "memory");
        Directory.CreateDirectory(skillsDir);
        File.WriteAllText(
            Path.Combine(skillsDir, "SKILL.md"),
            """
            ---
            name: npc-memory-skill
            description: Preserve stable NPC conversation context.
            ---
            Use durable memory for recurring player facts.
            """);
        return new SkillManager(Path.Combine(_tempDir, "skills"), NullLogger<SkillManager>.Instance);
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
