using System.Runtime.CompilerServices;
using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Hermes.Agent.LLM;
using Hermes.Agent.Runtime;
using Hermes.Agent.Skills;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

namespace HermesDesktop.Tests.Stardew;

[TestClass]
public sealed class StardewNpcPrivateChatAgentRunnerTests
{
    private string _tempDir = null!;
    private string _packRoot = null!;
    private SkillManager _skillManager = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-private-chat-tests", Guid.NewGuid().ToString("N"));
        _packRoot = Path.Combine(_tempDir, "packs");
        var skillsDir = Path.Combine(_tempDir, "skills", "memory");
        Directory.CreateDirectory(skillsDir);
        File.WriteAllText(
            Path.Combine(skillsDir, "SKILL.md"),
            """
            ---
            name: npc-memory-skill
            description: Preserve recurring private-chat facts.
            ---
            Use durable memory for stable player identity details.
            """);
        _skillManager = new SkillManager(Path.Combine(_tempDir, "skills"), NullLogger<SkillManager>.Instance);
        CreatePack("haley", "Haley");
        CreatePack("penny", "Penny");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public async Task ReplyAsync_HaleyAndPennyUseSharedToolsAndFreshBuiltinMemory()
    {
        foreach (var npcId in new[] { "haley", "penny" })
        {
            var runtimeSupervisor = new NpcRuntimeSupervisor();
            var writerClient = new MemoryWriteThenFinalChatClient();
            var writer = CreateRunner(writerClient, runtimeSupervisor, [new DiscoveredNoopTool("mcp_dynamic_test")]);

            var reply = await writer.ReplyAsync(
                new NpcPrivateChatRequest(npcId, "save-1", "conversation-write", "我叫远古牛哥,你记住"),
                CancellationToken.None);

            Assert.AreEqual("我记住了，远古牛哥。", reply.Text);
            Assert.AreEqual(2, writerClient.CompleteWithToolsCalls, $"{npcId} must continue after calling memory.");
            CollectionAssert.Contains(writerClient.FirstToolNames.ToArray(), "memory");
            CollectionAssert.Contains(writerClient.FirstToolNames.ToArray(), "session_search");
            CollectionAssert.Contains(writerClient.FirstToolNames.ToArray(), "skills_list");
            CollectionAssert.Contains(writerClient.FirstToolNames.ToArray(), "skill_invoke");
            CollectionAssert.Contains(writerClient.FirstToolNames.ToArray(), "mcp_dynamic_test");

            var userMemory = Path.Combine(
                _tempDir,
                "runtime",
                "stardew",
                "games",
                "stardew-valley",
                "saves",
                "save-1",
                "npc",
                npcId,
                "profiles",
                "default",
                "memory",
                "USER.md");
            Assert.IsTrue(File.Exists(userMemory), $"{npcId} must write memory under its NPC namespace.");
            StringAssert.Contains(await File.ReadAllTextAsync(userMemory), "远古牛哥");

            var runtimeRoot = Path.Combine(
                _tempDir,
                "runtime",
                "stardew",
                "games",
                "stardew-valley",
                "saves",
                "save-1",
                "npc",
                npcId,
                "profiles",
                "default");
            var runtimeSoulPath = Path.Combine(runtimeRoot, "SOUL.md");
            var personaSoulPath = Path.Combine(runtimeRoot, "persona", "SOUL.md");
            Assert.IsTrue(File.Exists(runtimeSoulPath), $"{npcId} must seed its runtime SOUL.md from the NPC pack.");
            Assert.IsTrue(File.Exists(personaSoulPath), $"{npcId} must copy the pack into its persona directory.");
            StringAssert.Contains(await File.ReadAllTextAsync(runtimeSoulPath), $"{npcId}-pack-soul");
            StringAssert.Contains(await File.ReadAllTextAsync(personaSoulPath), $"{npcId}-pack-soul");

            var readerClient = new SnapshotAnswerChatClient("远古牛哥");
            var reader = CreateRunner(readerClient, runtimeSupervisor);

            var followup = await reader.ReplyAsync(
                new NpcPrivateChatRequest(npcId, "save-1", "conversation-fresh", "我叫什么?"),
                CancellationToken.None);

            Assert.AreEqual("你叫远古牛哥。", followup.Text);
            Assert.IsTrue(readerClient.SawExpectedMemoryInPrompt, $"{npcId} must see durable USER.md through builtin memory snapshot.");
            Assert.AreEqual(1, runtimeSupervisor.Snapshot().Count(snapshot => string.Equals(snapshot.NpcId, npcId, StringComparison.OrdinalIgnoreCase)));
        }
    }

    [TestMethod]
    public async Task ReplyAsync_TodoUsesLongTermNpcSessionAndPromptUsesChineseContinuityGuidance()
    {
        var runtimeSupervisor = new NpcRuntimeSupervisor();
        var client = new TodoWriteThenFinalChatClient();
        var runner = CreateRunner(client, runtimeSupervisor);

        var reply = await runner.ReplyAsync(
            new NpcPrivateChatRequest("haley", "save-1", "conversation-promise", "明天陪我去海边好吗？"),
            CancellationToken.None);

        Assert.AreEqual("我记着，明天见。", reply.Text);
        Assert.IsTrue(client.SawChineseContinuityGuidance, "Private chat prompt must use Chinese plain-language continuity guidance.");
        Assert.IsTrue(client.SawDirectPlayerReplyGuidance, "Private chat prompt must force a direct reply to the player, not inner monologue.");
        var haleySnapshot = runtimeSupervisor.Snapshot().Single(snapshot => snapshot.NpcId == "haley");
        Assert.IsTrue(runtimeSupervisor.TryGetTaskView(haleySnapshot.SessionId, out var longTermTaskView));
        Assert.IsNotNull(longTermTaskView);
        Assert.AreEqual(1, longTermTaskView.ActiveSnapshot.Todos.Count);
        Assert.AreEqual("明天陪玩家去海边", longTermTaskView.ActiveSnapshot.Todos[0].Content);
        Assert.IsTrue(runtimeSupervisor.TryGetTaskView($"{haleySnapshot.SessionId}:private_chat:conversation-promise", out var privateChatTaskView));
        Assert.IsNotNull(privateChatTaskView);
        Assert.AreEqual(0, privateChatTaskView.ActiveSnapshot.Todos.Count);
    }

    [TestMethod]
    public async Task ReplyAsync_AfterSupervisorRestart_HydratesPendingTodoBeforeNewMessage()
    {
        var firstSupervisor = new NpcRuntimeSupervisor();
        var writer = CreateRunner(new TodoWriteThenFinalChatClient(), firstSupervisor);

        await writer.ReplyAsync(
            new NpcPrivateChatRequest("haley", "save-1", "conversation-promise", "明天陪我去海边好吗？"),
            CancellationToken.None);

        var secondSupervisor = new NpcRuntimeSupervisor();
        var reader = CreateRunner(new SnapshotAnswerChatClient("unused"), secondSupervisor);
        var pack = new FileSystemNpcPackLoader().LoadPacks(_packRoot).Single(pack => pack.Manifest.NpcId == "haley");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");

        await secondSupervisor.GetOrStartAsync(descriptor, _tempDir, CancellationToken.None);

        Assert.IsTrue(secondSupervisor.TryGetTaskView(descriptor.SessionId, out var taskView));
        Assert.IsNotNull(taskView);
        Assert.AreEqual(1, taskView.ActiveSnapshot.Todos.Count);
        Assert.AreEqual("明天陪玩家去海边", taskView.ActiveSnapshot.Todos[0].Content);
        Assert.AreEqual("pending", taskView.ActiveSnapshot.Todos[0].Status);
        _ = reader;
    }

    [TestMethod]
    public async Task ReplyAsync_MissingSaveIdFailsInsteadOfUsingManualDebug()
    {
        var runner = CreateRunner(new SnapshotAnswerChatClient("unused"), new NpcRuntimeSupervisor());

        await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
            await runner.ReplyAsync(
                new NpcPrivateChatRequest("penny", "", "conversation-fresh", "我叫什么?"),
                CancellationToken.None));
    }

    private StardewNpcPrivateChatAgentRunner CreateRunner(
        IChatClient chatClient,
        NpcRuntimeSupervisor runtimeSupervisor,
        IEnumerable<ITool>? discoveredTools = null)
        => new(
            chatClient,
            NullLoggerFactory.Instance,
            _tempDir,
            runtimeSupervisor,
            _skillManager,
            new NoopCronScheduler(),
            new StardewNpcRuntimeBindingResolver(new FileSystemNpcPackLoader(), _packRoot),
            includeMemory: true,
            includeUser: true,
            discoveredToolProvider: () => discoveredTools ?? Enumerable.Empty<ITool>(),
            maxToolIterations: 2);

    private void CreatePack(string npcId, string displayName)
    {
        var root = Path.Combine(_packRoot, npcId, "default");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "SOUL.md"), $"# {displayName}\n\n{npcId}-pack-soul");
        File.WriteAllText(Path.Combine(root, "facts.md"), $"{displayName} facts");
        File.WriteAllText(Path.Combine(root, "voice.md"), $"{displayName} voice");
        File.WriteAllText(Path.Combine(root, "boundaries.md"), $"{displayName} boundaries");
        File.WriteAllText(Path.Combine(root, "skills.json"), """{"required":[],"optional":[]}""");

        var manifest = new NpcPackManifest
        {
            SchemaVersion = 1,
            NpcId = npcId,
            GameId = "stardew-valley",
            ProfileId = "default",
            DefaultProfileId = "default",
            DisplayName = displayName,
            SmapiName = displayName,
            Aliases = [npcId, displayName],
            TargetEntityId = npcId,
            AdapterId = "stardew",
            SoulFile = "SOUL.md",
            FactsFile = "facts.md",
            VoiceFile = "voice.md",
            BoundariesFile = "boundaries.md",
            SkillsFile = "skills.json",
            Capabilities = ["move", "speak"]
        };
        File.WriteAllText(
            Path.Combine(root, FileSystemNpcPackLoader.ManifestFileName),
            JsonSerializer.Serialize(manifest));
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
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        new ToolCall
                        {
                            Id = "memory-write",
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

    private sealed class TodoWriteThenFinalChatClient : IChatClient
    {
        private int _calls;

        public bool SawChineseContinuityGuidance { get; private set; }
        public bool SawDirectPlayerReplyGuidance { get; private set; }

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("ok");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            _calls++;
            var snapshot = messages.ToList();
            SawChineseContinuityGuidance |= snapshot.Any(message =>
                string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase) &&
                message.Content.Contains("玩家找你说话时", StringComparison.Ordinal) &&
                message.Content.Contains("todo", StringComparison.Ordinal) &&
                message.Content.Contains("不要把工具过程讲给玩家听", StringComparison.Ordinal));
            SawDirectPlayerReplyGuidance |= snapshot.Any(message =>
                string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase) &&
                message.Content.Contains("最终回复会显示在玩家手机私聊里", StringComparison.Ordinal) &&
                message.Content.Contains("必须直接对玩家说话", StringComparison.Ordinal) &&
                message.Content.Contains("不要写内心独白、旁白", StringComparison.Ordinal));

            if (_calls == 1)
            {
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        new ToolCall
                        {
                            Id = "todo-write",
                            Name = "todo",
                            Arguments = "{\"todos\":[{\"id\":\"1\",\"content\":\"明天陪玩家去海边\",\"status\":\"pending\"}]}"
                        }
                    ]
                });
            }

            return Task.FromResult(new ChatResponse { Content = "我记着，明天见。", FinishReason = "stop" });
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

    private sealed class DiscoveredNoopTool : ITool
    {
        public DiscoveredNoopTool(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public string Description => "Dynamic discovered test tool";
        public Type ParametersType => typeof(NoopParameters);
        public Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
            => Task.FromResult(ToolResult.Ok("ok"));
    }

    private sealed class NoopParameters
    {
    }
}
