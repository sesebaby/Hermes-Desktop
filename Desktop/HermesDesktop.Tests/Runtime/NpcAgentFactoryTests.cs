using Hermes.Agent.Core;
using Hermes.Agent.LLM;
using Hermes.Agent.Runtime;
using Hermes.Agent.Skills;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.CompilerServices;

namespace HermesDesktop.Tests.Runtime;

[TestClass]
public class NpcAgentFactoryTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "hermes-npc-agent-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public void Create_WithSharedCapabilities_RegistersDesktopCapabilitySurfaceForHaleyAndPenny()
    {
        var skillManager = CreateSkillManager();

        foreach (var npcId in new[] { "haley", "penny" })
        {
            var chatClient = new MemoryWriteThenFinalChatClient();
            var npcNamespace = new NpcNamespace(_tempDir, "stardew-valley", "save-1", npcId, "default");
            var context = CreateContext(npcNamespace, chatClient);
            var agent = CreateNpcAgentWithDesktopCapabilities(chatClient, context, npcNamespace, skillManager);

            CollectionAssert.AreEqual(
                AgentCapabilityAssembler.BuiltInToolNames.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
                agent.Tools.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
                $"{npcId} must receive the same built-in tool names as the desktop agent.");
            CollectionAssert.AreEqual(
                agent.Tools.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
                context.ToolRegistry.GetAllTools().Select(tool => tool.Name).Order(StringComparer.OrdinalIgnoreCase).ToArray(),
                $"{npcId} agent and registry tool surfaces must not diverge.");

            Assert.IsTrue(agent.Tools.ContainsKey("memory"), $"{npcId} must have the reference memory tool.");
            Assert.IsTrue(agent.Tools.ContainsKey("session_search"), $"{npcId} must have transcript-backed session recall.");
            Assert.IsTrue(agent.Tools.ContainsKey("skills_list"), $"{npcId} must have skill inventory access.");
            Assert.IsTrue(agent.Tools.ContainsKey("skill_view"), $"{npcId} must have skill content access.");
            Assert.IsTrue(agent.Tools.ContainsKey("skill_manage"), $"{npcId} must have skill mutation access.");
            Assert.IsTrue(agent.Tools.ContainsKey("skill_invoke"), $"{npcId} must have skill invocation access.");
            Assert.AreEqual(2, agent.MaxToolIterations, $"{npcId} needs at least a tool call plus final reply.");
        }
    }

    [TestMethod]
    public async Task SessionSearchTool_UsesNpcScopedTranscriptStore()
    {
        var skillManager = CreateSkillManager();
        var haleyContext = CreateContext(new NpcNamespace(_tempDir, "stardew-valley", "save-1", "haley", "default"), new SnapshotAnswerChatClient("unused"));
        var pennyContext = CreateContext(new NpcNamespace(_tempDir, "stardew-valley", "save-1", "penny", "default"), new SnapshotAnswerChatClient("unused"));
        var haleyAgent = CreateNpcAgentWithDesktopCapabilities(new SnapshotAnswerChatClient("unused"), haleyContext, new NpcNamespace(_tempDir, "stardew-valley", "save-1", "haley", "default"), skillManager);
        var pennyAgent = CreateNpcAgentWithDesktopCapabilities(new SnapshotAnswerChatClient("unused"), pennyContext, new NpcNamespace(_tempDir, "stardew-valley", "save-1", "penny", "default"), skillManager);

        await haleyContext.TranscriptStore.SaveMessageAsync(
            "haley-private-old",
            new Message { Role = "user", Content = "haley-only transcript marker: sunflower bench" },
            CancellationToken.None);
        await pennyContext.TranscriptStore.SaveMessageAsync(
            "penny-private-old",
            new Message { Role = "user", Content = "penny-only transcript marker: library lesson" },
            CancellationToken.None);

        var haleyResult = await haleyAgent.Tools["session_search"].ExecuteAsync(
            new SessionSearchParameters { Limit = 5, CurrentSessionId = "haley-private-new" },
            CancellationToken.None);
        var pennyResult = await pennyAgent.Tools["session_search"].ExecuteAsync(
            new SessionSearchParameters { Limit = 5, CurrentSessionId = "penny-private-new" },
            CancellationToken.None);

        Assert.IsTrue(haleyResult.Success);
        StringAssert.Contains(haleyResult.Content, "sunflower bench");
        Assert.IsFalse(haleyResult.Content.Contains("library lesson", StringComparison.Ordinal));

        Assert.IsTrue(pennyResult.Success);
        StringAssert.Contains(pennyResult.Content, "library lesson");
        Assert.IsFalse(pennyResult.Content.Contains("sunflower bench", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ChatAsync_MemoryToolPersistsAndFreshAgentRecallsBuiltinSnapshot_ForHaleyAndPenny()
    {
        var skillManager = CreateSkillManager();

        foreach (var npcId in new[] { "haley", "penny" })
        {
            var npcNamespace = new NpcNamespace(_tempDir, "stardew-valley", "save-1", npcId, "default");

            var firstChat = new MemoryWriteThenFinalChatClient();
            var firstContext = CreateContext(npcNamespace, firstChat);
            var firstAgent = CreateNpcAgentWithDesktopCapabilities(firstChat, firstContext, npcNamespace, skillManager);

            var firstReply = await firstAgent.ChatAsync(
                "我叫远古牛哥,你记住",
                new Session { Id = $"sdv_save-1_{npcId}_default:private_chat:first", Platform = "stardew" },
                CancellationToken.None);

            Assert.AreEqual("我记住了，远古牛哥。", firstReply);
            Assert.AreEqual(2, firstChat.CompleteWithToolsCalls, $"{npcId} must continue after the memory tool call.");
            CollectionAssert.Contains(firstChat.FirstToolNames.ToArray(), "memory", $"{npcId} first model call must expose memory.");

            var userMemoryPath = Path.Combine(npcNamespace.MemoryPath, "USER.md");
            Assert.IsTrue(File.Exists(userMemoryPath), $"{npcId} must persist player identity under NPC namespace.");
            StringAssert.Contains(await File.ReadAllTextAsync(userMemoryPath), "远古牛哥");

            var followupChat = new SnapshotAnswerChatClient("远古牛哥");
            var freshContext = CreateContext(npcNamespace, followupChat);
            var freshAgent = CreateNpcAgentWithDesktopCapabilities(followupChat, freshContext, npcNamespace, skillManager);

            var followupReply = await freshAgent.ChatAsync(
                "我叫什么?",
                new Session { Id = $"sdv_save-1_{npcId}_default:private_chat:fresh", Platform = "stardew" },
                CancellationToken.None);

            Assert.AreEqual("你叫远古牛哥。", followupReply);
            Assert.IsTrue(followupChat.SawExpectedMemoryInPrompt, $"{npcId} fresh agent must receive USER.md via builtin memory snapshot.");
            Assert.IsFalse(followupChat.SawOriginalNamingTurn, $"{npcId} recall proof must not depend on recent transcript context.");
            Assert.IsFalse(followupChat.SawAutonomyTickMemory, $"{npcId} recall proof must not use autonomy tick memory.");
        }
    }

    private NpcRuntimeContextBundle CreateContext(NpcNamespace npcNamespace, IChatClient chatClient)
        => new NpcRuntimeContextFactory().Create(
            npcNamespace,
            chatClient,
            NullLoggerFactory.Instance,
            "You are an equal-capability Stardew NPC agent. Reply in character, and use durable memory when the player shares stable facts.");

    private static Hermes.Agent.Core.Agent CreateNpcAgentWithDesktopCapabilities(
        IChatClient chatClient,
        NpcRuntimeContextBundle context,
        NpcNamespace npcNamespace,
        SkillManager skillManager)
    {
        var agent = new NpcAgentFactory().Create(
            chatClient,
            context,
            Enumerable.Empty<ITool>(),
            NullLoggerFactory.Instance,
            maxToolIterations: 2);

        AgentCapabilityAssembler.RegisterBuiltInTools(
            agent,
            new AgentCapabilityServices
            {
                ChatClient = chatClient,
                ToolRegistry = context.ToolRegistry,
                TodoStore = context.TodoStore,
                CronScheduler = new NoopCronScheduler(),
                MemoryManager = context.MemoryManager,
                PluginManager = context.PluginManager,
                TranscriptRecallService = context.TranscriptRecallService,
                SkillManager = skillManager,
                CheckpointDirectory = Path.Combine(npcNamespace.RuntimeRoot, "checkpoints"),
                MemoryAvailable = true
            });

        return agent;
    }

    private SkillManager CreateSkillManager()
    {
        var skillsDir = Path.Combine(_tempDir, "skills");
        Directory.CreateDirectory(Path.Combine(skillsDir, "conversation"));
        File.WriteAllText(
            Path.Combine(skillsDir, "conversation", "SKILL.md"),
            """
            ---
            name: remember-player-details
            description: Keep stable player identity details concise and durable.
            ---
            Use the memory tool for stable player identity facts.
            """);
        return new SkillManager(skillsDir, NullLogger<SkillManager>.Instance);
    }

    private sealed class MemoryWriteThenFinalChatClient : IChatClient
    {
        public int CompleteWithToolsCalls { get; private set; }
        public List<string> FirstToolNames { get; } = new();

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("ok");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            CompleteWithToolsCalls++;
            if (CompleteWithToolsCalls == 1)
            {
                FirstToolNames.AddRange(tools.Select(tool => tool.Name));
                return Task.FromResult(new ChatResponse
                {
                    Content = null,
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        new ToolCall
                        {
                            Id = "call-memory-1",
                            Name = "memory",
                            Arguments = "{\"action\":\"add\",\"target\":\"user\",\"content\":\"玩家名字是远古牛哥。\"}"
                        }
                    ]
                });
            }

            return Task.FromResult(new ChatResponse { Content = "我记住了，远古牛哥。", FinishReason = "stop" });
        }

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

    private sealed class SnapshotAnswerChatClient : IChatClient
    {
        private readonly string _expectedMemory;

        public SnapshotAnswerChatClient(string expectedMemory)
        {
            _expectedMemory = expectedMemory;
        }

        public bool SawExpectedMemoryInPrompt { get; private set; }
        public bool SawOriginalNamingTurn { get; private set; }
        public bool SawAutonomyTickMemory { get; private set; }

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("ok");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            var snapshot = messages.ToList();
            SawExpectedMemoryInPrompt = snapshot.Any(message =>
                string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase) &&
                message.Content.Contains("USER PROFILE", StringComparison.Ordinal) &&
                message.Content.Contains(_expectedMemory, StringComparison.Ordinal));
            SawOriginalNamingTurn = snapshot.Any(message =>
                message.Content.Contains("我叫远古牛哥,你记住", StringComparison.Ordinal));
            SawAutonomyTickMemory = snapshot.Any(message =>
                message.Content.Contains("Autonomy tick", StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(new ChatResponse
            {
                Content = SawExpectedMemoryInPrompt ? "你叫远古牛哥。" : "不知道。",
                FinishReason = "stop"
            });
        }

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

    private sealed class NoopCronScheduler : ICronScheduler
    {
        public event EventHandler<CronTaskDueEventArgs>? TaskDue;

        public void Schedule(CronTask task) => TaskDue?.Invoke(this, new CronTaskDueEventArgs { Task = task, FiredAt = DateTimeOffset.UtcNow });
        public void Cancel(string taskId) { }
        public CronTask? GetTask(string taskId) => null;
        public IReadOnlyList<CronTask> GetAllTasks() => Array.Empty<CronTask>();
        public DateTimeOffset? GetNextRun(string taskId) => null;
    }
}
