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
            Assert.AreEqual(3, writerClient.CompleteWithToolsCalls, $"{npcId} must continue after calling memory and declaring no immediate world action.");
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
        Assert.IsTrue(client.SawImmediateDelegationGuidance, "Private chat prompt must explain accepted immediate world actions require a skill-grounded stardew_submit_host_task target.");
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
    public async Task ReplyAsync_WhenImmediateActionAccepted_QueuesHostTaskSubmissionIngress()
    {
        var runtimeSupervisor = new NpcRuntimeSupervisor();
        var client = new TodoThenDelegateActionThenFinalChatClient();
        var runner = CreateRunner(client, runtimeSupervisor);

        var reply = await runner.ReplyAsync(
            new NpcPrivateChatRequest("haley", "save-1", "conversation-beach", "go to the beach now"),
            CancellationToken.None);

        Assert.AreEqual("I'll head there now.", reply.Text);
        Assert.IsTrue(client.SawDelegateActionTool, "Private chat tool surface must include stardew_submit_host_task.");
        CollectionAssert.AreEqual(
            new[] { "todo", "stardew_submit_host_task" },
            client.ExecutedWorldPlanningTools,
            "Accepted immediate world commitments must first become NPC session todo, then enter the host action lifecycle.");
        var haleySnapshot = runtimeSupervisor.Snapshot().Single(snapshot => snapshot.NpcId == "haley");
        Assert.IsTrue(runtimeSupervisor.TryGetTaskView(haleySnapshot.SessionId, out var taskView));
        Assert.IsNotNull(taskView);
        Assert.AreEqual(1, taskView.ActiveSnapshot.Todos.Count);
        Assert.AreEqual("Meet player at the beach now", taskView.ActiveSnapshot.Todos[0].Content);
        Assert.AreEqual("in_progress", taskView.ActiveSnapshot.Todos[0].Status);
        var ingress = haleySnapshot.Controller.IngressWorkItems.Single();
        Assert.AreEqual("stardew_host_task_submission", ingress.WorkType);
        Assert.AreEqual("queued", ingress.Status);
        Assert.IsFalse(string.IsNullOrWhiteSpace(ingress.TraceId));
        Assert.IsFalse(string.IsNullOrWhiteSpace(ingress.WorkItemId));
        Assert.AreEqual("move", ingress.Payload?["action"]?.GetValue<string>());
        Assert.AreEqual("meet the player at the beach now", ingress.Payload?["reason"]?.GetValue<string>());
        var target = ingress.Payload?["target"]?.AsObject();
        Assert.IsNotNull(target, "Private-chat move delegation must carry the parent-resolved mechanical target.");
        Assert.AreEqual("Beach", target["locationName"]?.GetValue<string>());
        Assert.AreEqual(32, target["x"]?.GetValue<int>());
        Assert.AreEqual(34, target["y"]?.GetValue<int>());
        Assert.AreEqual("map-skill:stardew.navigation.poi.beach-shoreline", target["source"]?.GetValue<string>());
        Assert.IsNull(ingress.Payload?["destinationText"]);
        Assert.IsNull(ingress.Payload?["intentText"]);
        Assert.AreEqual("conversation-beach", ingress.Payload?["conversationId"]?.GetValue<string>());
        Assert.AreEqual("meet-beach-now", ingress.Payload?["rootTodoId"]?.GetValue<string>());
    }

    [TestMethod]
    public async Task ReplyAsync_WithReusedPrivateChatHandle_QueuesHostTaskWithCurrentConversationId()
    {
        var runtimeSupervisor = new NpcRuntimeSupervisor();
        var client = new TwoConversationImmediateActionChatClient();
        var runner = CreateRunner(client, runtimeSupervisor);

        await runner.ReplyAsync(
            new NpcPrivateChatRequest("haley", "save-1", "conversation-first", "go to the beach now"),
            CancellationToken.None);
        var reply = await runner.ReplyAsync(
            new NpcPrivateChatRequest("haley", "save-1", "conversation-second", "go to the beach now"),
            CancellationToken.None);

        Assert.AreEqual("I'll head there now.", reply.Text);
        var haleySnapshot = runtimeSupervisor.Snapshot().Single(snapshot => snapshot.NpcId == "haley");
        var ingressItems = haleySnapshot.Controller.IngressWorkItems.ToArray();
        Assert.AreEqual(2, ingressItems.Length);
        Assert.AreEqual("conversation-first", ingressItems[0].Payload?["conversationId"]?.GetValue<string>());
        Assert.AreEqual("conversation-second", ingressItems[1].Payload?["conversationId"]?.GetValue<string>());
    }

    [TestMethod]
    public async Task ReplyAsync_PrivateChatToolSurface_PrioritizesImmediateHostTaskSubmissionOverTodo()
    {
        var runtimeSupervisor = new NpcRuntimeSupervisor();
        var client = new TodoThenDelegateActionThenFinalChatClient();
        var runner = CreateRunner(client, runtimeSupervisor);

        await runner.ReplyAsync(
            new NpcPrivateChatRequest("haley", "save-1", "conversation-beach", "海莉，我们现在去海边吧。"),
            CancellationToken.None);

        var delegateIndex = client.FirstToolNames.FindIndex(name => name == "stardew_submit_host_task");
        var noWorldActionIndex = client.FirstToolNames.FindIndex(name => name == "npc_no_world_action");
        var todoIndex = client.FirstToolNames.FindIndex(name => name == "todo");
        Assert.IsTrue(delegateIndex >= 0, "私聊工具面必须包含 stardew_submit_host_task。");
        Assert.IsTrue(noWorldActionIndex >= 0, "私聊工具面必须包含 npc_no_world_action。");
        Assert.IsTrue(todoIndex >= 0, "私聊工具面必须包含 todo。");
        Assert.IsTrue(todoIndex < delegateIndex, "立即行动承诺必须先成为 NPC session todo，再委托真实动作。");
        Assert.IsTrue(noWorldActionIndex < delegateIndex, "无世界动作声明工具必须靠前，保证父层每轮都能明确闭环。");
        var todoDefinition = client.FirstToolDefinitions.Single(tool => tool.Name == "todo");
        StringAssert.Contains(todoDefinition.Description, "当前会话");
        StringAssert.Contains(todoDefinition.Description, "现在就执行");
        StringAssert.Contains(todoDefinition.Description, "stardew_submit_host_task");
        StringAssert.DoesNotMatch(todoDefinition.Description, new System.Text.RegularExpressions.Regex(@"\bManage your task list\b"));
    }

    [TestMethod]
    public async Task ReplyAsync_WhenImmediateMoveAcceptedWithoutDelegation_RetriesAndQueuesHostTaskSubmission()
    {
        var runtimeSupervisor = new NpcRuntimeSupervisor();
        var client = new FirstAcceptsWithoutDelegatingThenDelegatesChatClient();
        var runner = CreateRunner(client, runtimeSupervisor);

        var reply = await runner.ReplyAsync(
            new NpcPrivateChatRequest("haley", "save-1", "conversation-corrective", "海莉，我们现在去海边吧。"),
            CancellationToken.None);

        Assert.AreEqual("好，我现在过去。", reply.Text);
        Assert.AreEqual(3, client.CompleteWithToolsCalls, "第一轮没有任何工具调用时，应触发一次父层自检工具轮，自检轮必须同时补 todo 和委托动作。");
        Assert.IsTrue(
            client.MessagesByCall.Skip(1).Any(messages => messages.Any(message =>
                message.Role == "user" &&
                (message.Content?.Contains("自检：上一轮私聊回复没有调用 stardew_submit_host_task", StringComparison.Ordinal) ?? false) &&
                (message.Content?.Contains("请你自己判断", StringComparison.Ordinal) ?? false))),
            "自检重试只能把缺少 stardew_submit_host_task 的结构事实反馈给父层，由父层自己判断是否补工具调用。");
        var haleySnapshot = runtimeSupervisor.Snapshot().Single(snapshot => snapshot.NpcId == "haley");
        var ingress = haleySnapshot.Controller.IngressWorkItems.Single();
        Assert.AreEqual("stardew_host_task_submission", ingress.WorkType);
        Assert.AreEqual("move", ingress.Payload?["action"]?.GetValue<string>());
        var target = ingress.Payload?["target"]?.AsObject();
        Assert.IsNotNull(target, "Corrective retry must delegate the resolved mechanical target, not natural-language destinationText.");
        Assert.AreEqual("Beach", target["locationName"]?.GetValue<string>());
        Assert.AreEqual(32, target["x"]?.GetValue<int>());
        Assert.AreEqual(34, target["y"]?.GetValue<int>());
        Assert.AreEqual("map-skill:stardew.navigation.poi.beach-shoreline", target["source"]?.GetValue<string>());
        Assert.IsNull(ingress.Payload?["destinationText"]);
        Assert.IsNull(ingress.Payload?["intentText"]);
        Assert.IsTrue(runtimeSupervisor.TryGetTaskView(haleySnapshot.SessionId, out var taskView));
        Assert.IsNotNull(taskView);
        Assert.AreEqual(1, taskView.ActiveSnapshot.Todos.Count);
        Assert.AreEqual("Meet player at the beach now", taskView.ActiveSnapshot.Todos[0].Content);
        Assert.AreEqual("in_progress", taskView.ActiveSnapshot.Todos[0].Status);
    }

    [TestMethod]
    public async Task ReplyAsync_WhenSubmitHostTaskFailsValidation_AllowsNativeRetryWithoutCommitmentTodoSelfCheck()
    {
        var runtimeSupervisor = new NpcRuntimeSupervisor();
        var client = new FirstMalformedSubmitThenNativeRetryChatClient();
        var runner = CreateRunner(client, runtimeSupervisor);

        var reply = await runner.ReplyAsync(
            new NpcPrivateChatRequest("haley", "save-1", "conversation-invalid-target", "海莉，我们现在去海边吧。"),
            CancellationToken.None);

        Assert.AreEqual("好，我现在过去。", reply.Text);
        Assert.AreEqual(3, client.CompleteWithToolsCalls, "失败的 stardew_submit_host_task 应先作为工具结果回到同一 agent-native tool loop，由父层自己重试。");
        Assert.IsFalse(client.SawDelegationSelfCheck, "同一 tool loop 已经成功重试时，runner 不应再追加第二次自检。");
        Assert.IsFalse(client.SawMissingTodoSelfCheck, "参数校验失败时不能进入“只补 todo”路径，因为没有成功 host task submission。");
        var haleySnapshot = runtimeSupervisor.Snapshot().Single(snapshot => snapshot.NpcId == "haley");
        var ingress = haleySnapshot.Controller.IngressWorkItems.Single();
        Assert.AreEqual("stardew_host_task_submission", ingress.WorkType);
        Assert.AreEqual("move", ingress.Payload?["action"]?.GetValue<string>());
        var target = ingress.Payload?["target"]?.AsObject();
        Assert.IsNotNull(target);
        Assert.AreEqual("Beach", target["locationName"]?.GetValue<string>());
        Assert.AreEqual(32, target["x"]?.GetValue<int>());
        Assert.AreEqual(34, target["y"]?.GetValue<int>());
        Assert.AreEqual("map-skill:stardew.navigation.poi.beach-shoreline", target["source"]?.GetValue<string>());
    }

    [TestMethod]
    public async Task ReplyAsync_WhenSubmitHostTaskFailsValidationAndModelStops_RunsDelegationSelfCheckAndQueuesRetry()
    {
        var runtimeSupervisor = new NpcRuntimeSupervisor();
        var client = new FirstMalformedSubmitThenStopsThenSelfCheckRetryChatClient();
        var runner = CreateRunner(client, runtimeSupervisor);

        var reply = await runner.ReplyAsync(
            new NpcPrivateChatRequest("haley", "save-1", "conversation-invalid-target", "海莉，我们现在去海边吧。"),
            CancellationToken.None);

        Assert.AreEqual("好，我现在过去。", reply.Text);
        Assert.AreEqual(4, client.CompleteWithToolsCalls, "失败提交后若模型直接 final，runner 必须把“未成功提交”作为结构事实反馈给父层自检。");
        Assert.IsTrue(client.SawDelegationSelfCheck);
        Assert.IsFalse(client.SawMissingTodoSelfCheck, "失败提交不能进入只补 todo 的成功提交修复路径。");
        var haleySnapshot = runtimeSupervisor.Snapshot().Single(snapshot => snapshot.NpcId == "haley");
        var ingress = haleySnapshot.Controller.IngressWorkItems.Single();
        Assert.AreEqual("stardew_host_task_submission", ingress.WorkType);
        Assert.AreEqual("move", ingress.Payload?["action"]?.GetValue<string>());
        var target = ingress.Payload?["target"]?.AsObject();
        Assert.IsNotNull(target);
        Assert.AreEqual("Beach", target["locationName"]?.GetValue<string>());
        Assert.AreEqual(32, target["x"]?.GetValue<int>());
        Assert.AreEqual(34, target["y"]?.GetValue<int>());
        Assert.AreEqual("map-skill:stardew.navigation.poi.beach-shoreline", target["source"]?.GetValue<string>());
    }

    [TestMethod]
    public async Task ReplyAsync_WhenImmediateActionDelegatedWithoutTodo_RetriesForCommitmentTodoWithoutDuplicatingDelegation()
    {
        var runtimeSupervisor = new NpcRuntimeSupervisor();
        var client = new FirstDelegatesWithoutTodoThenWritesTodoChatClient();
        var runner = CreateRunner(client, runtimeSupervisor);

        var reply = await runner.ReplyAsync(
            new NpcPrivateChatRequest("haley", "save-1", "conversation-beach", "go to the beach now"),
            CancellationToken.None);

        Assert.AreEqual("I'll head there now.", reply.Text);
        Assert.AreEqual(4, client.CompleteWithToolsCalls, "Missing commitment todo should trigger one parent self-check turn before the final reply.");
        Assert.IsTrue(client.SawMissingTodoSelfCheck);
        var haleySnapshot = runtimeSupervisor.Snapshot().Single(snapshot => snapshot.NpcId == "haley");
        Assert.AreEqual(1, haleySnapshot.Controller.IngressWorkItems.Count, "The todo repair turn must not enqueue a duplicate delegated action.");
        Assert.IsTrue(runtimeSupervisor.TryGetTaskView(haleySnapshot.SessionId, out var taskView));
        Assert.IsNotNull(taskView);
        Assert.AreEqual(1, taskView.ActiveSnapshot.Todos.Count);
        Assert.AreEqual("Meet player at the beach now", taskView.ActiveSnapshot.Todos[0].Content);
        Assert.AreEqual("in_progress", taskView.ActiveSnapshot.Todos[0].Status);
    }

    [TestMethod]
    public async Task ReplyAsync_WhenSuccessfulHostTaskSubmissionEndsWithEmptyReply_RunsReplySelfCheckWithoutDuplicatingDelegation()
    {
        var runtimeSupervisor = new NpcRuntimeSupervisor();
        var client = new FirstDelegatesWithTodoThenEmptyReplyThenNaturalReplyChatClient();
        var runner = CreateRunner(client, runtimeSupervisor);

        var reply = await runner.ReplyAsync(
            new NpcPrivateChatRequest("haley", "save-1", "conversation-reply-repair", "海莉，我们现在去海边吧。"),
            CancellationToken.None);

        Assert.AreEqual("好，我先回你一句，然后就过去。", reply.Text);
        Assert.AreEqual(3, client.CompleteWithToolsCalls, "成功提交 host task 后如果最终回复为空，runner 必须只追加一次 bounded reply self-check。");
        Assert.IsTrue(client.SawReplySelfCheck, "reply self-check 必须把“已成功提交、只缺玩家可见回复、不要重复提交”作为结构事实反馈给父层。");
        Assert.AreEqual(1, runtimeSupervisor.Snapshot().Single(snapshot => snapshot.NpcId == "haley").Controller.IngressWorkItems.Count, "reply 修复轮不能重复排队 host task ingress。");
        Assert.AreEqual(1, runtimeSupervisor.Snapshot().Single(snapshot => snapshot.NpcId == "haley").Controller.IngressWorkItems.Count(item => item.WorkType == "stardew_host_task_submission"));
    }

    [TestMethod]
    public async Task ReplyAsync_WhenSuccessfulHostTaskSubmissionStillHasEmptyReply_BlocksQueuedIngress()
    {
        var runtimeSupervisor = new NpcRuntimeSupervisor();
        var client = new FirstDelegatesWithTodoThenAlwaysEmptyReplyChatClient();
        var runner = CreateRunner(client, runtimeSupervisor);

        var reply = await runner.ReplyAsync(
            new NpcPrivateChatRequest("haley", "save-1", "conversation-reply-fails", "海莉，我们现在去海边吧。"),
            CancellationToken.None);

        Assert.AreEqual(string.Empty, reply.Text);
        Assert.AreEqual(3, client.CompleteWithToolsCalls, "reply self-check 仍然只能追加一次，不能无限重试。");
        Assert.IsTrue(client.SawReplySelfCheck);
        var snapshot = runtimeSupervisor.Snapshot().Single(runtime => runtime.NpcId == "haley").Controller;
        Assert.AreEqual(0, snapshot.IngressWorkItems.Count, "当父层两次都不给玩家可见回复时，已排队 ingress 必须被阻断，而不是静默继续执行。");
        Assert.AreEqual(StardewCommandStatuses.Blocked, snapshot.LastTerminalCommandStatus?.Status);
        Assert.AreEqual("move", snapshot.LastTerminalCommandStatus?.Action);
        StringAssert.Contains(snapshot.LastTerminalCommandStatus?.ErrorCode ?? string.Empty, "private_chat_reply");
    }

    [TestMethod]
    public async Task ReplyAsync_WhenCasualImmediateMoveAcceptedWithoutDelegation_RetriesAndQueuesHostTaskSubmission()
    {
        var runtimeSupervisor = new NpcRuntimeSupervisor();
        var client = new FirstCasualAcceptsWithoutDelegatingThenDelegatesChatClient();
        var runner = CreateRunner(client, runtimeSupervisor);

        var reply = await runner.ReplyAsync(
            new NpcPrivateChatRequest("haley", "save-1", "conversation-casual-corrective", "走吧,我们去海边"),
            CancellationToken.None);

        Assert.AreEqual("好啦，我们走。", reply.Text);
        Assert.AreEqual(3, client.CompleteWithToolsCalls, "第一轮没有任何工具调用时，应触发一次通用父层自检；自检轮必须同时补 todo 和委托动作，宿主不能靠短语或地点判断移动意图。");
        var haleySnapshot = runtimeSupervisor.Snapshot().Single(snapshot => snapshot.NpcId == "haley");
        var ingress = haleySnapshot.Controller.IngressWorkItems.Single();
        Assert.AreEqual("stardew_host_task_submission", ingress.WorkType);
        Assert.AreEqual("move", ingress.Payload?["action"]?.GetValue<string>());
        var target = ingress.Payload?["target"]?.AsObject();
        Assert.IsNotNull(target);
        Assert.AreEqual("Beach", target["locationName"]?.GetValue<string>());
        Assert.AreEqual(32, target["x"]?.GetValue<int>());
        Assert.AreEqual(34, target["y"]?.GetValue<int>());
        Assert.AreEqual("map-skill:stardew.navigation.poi.beach-shoreline", target["source"]?.GetValue<string>());
        Assert.IsNull(ingress.Payload?["destinationText"]);
        Assert.IsTrue(runtimeSupervisor.TryGetTaskView(haleySnapshot.SessionId, out var taskView));
        Assert.IsNotNull(taskView);
        Assert.AreEqual(1, taskView.ActiveSnapshot.Todos.Count);
        Assert.AreEqual("Meet player at the beach now", taskView.ActiveSnapshot.Todos[0].Content);
        Assert.AreEqual("in_progress", taskView.ActiveSnapshot.Todos[0].Status);
    }

    [TestMethod]
    public async Task ReplyAsync_WhenFirstTurnUsesNonDelegationToolWithoutMarker_RunsDelegationSelfCheck()
    {
        var runtimeSupervisor = new NpcRuntimeSupervisor();
        var client = new FirstUsesSessionSearchThenSelfCheckChatClient();
        var runner = CreateRunner(client, runtimeSupervisor);

        var reply = await runner.ReplyAsync(
            new NpcPrivateChatRequest("haley", "save-1", "conversation-search", "你还记得我们之前聊过什么吗？"),
            CancellationToken.None);

        Assert.AreEqual("我记得，我们聊过花。", reply.Text);
        Assert.AreEqual(4, client.CompleteWithToolsCalls, "非 stardew_submit_host_task 工具不能替代世界动作提交；没有 npc_no_world_action 工具调用时仍要自检。");
    }

    [TestMethod]
    public async Task ReplyAsync_WhenNoWorldActionToolCalled_DoesNotSelfCheck()
    {
        var runtimeSupervisor = new NpcRuntimeSupervisor();
        var client = new NoWorldActionToolThenFinalChatClient();
        var runner = CreateRunner(client, runtimeSupervisor);

        var reply = await runner.ReplyAsync(
            new NpcPrivateChatRequest("haley", "save-1", "conversation-chat", "今天心情怎么样？"),
            CancellationToken.None);

        Assert.AreEqual("今天还不错。", reply.Text);
        Assert.AreEqual(2, client.CompleteWithToolsCalls, "父层已经调用 npc_no_world_action 时，不应追加自检轮。");
    }

    [TestMethod]
    public async Task ReplyAsync_WhenNoWorldActionFailsValidation_RunsDelegationSelfCheck()
    {
        var runtimeSupervisor = new NpcRuntimeSupervisor();
        var client = new FailedNoWorldActionThenSelfCheckChatClient();
        var runner = CreateRunner(client, runtimeSupervisor);

        var reply = await runner.ReplyAsync(
            new NpcPrivateChatRequest("haley", "save-1", "conversation-no-world-invalid", "今天心情怎么样？"),
            CancellationToken.None);

        Assert.AreEqual("今天还不错。", reply.Text);
        Assert.AreEqual(4, client.CompleteWithToolsCalls, "失败的 npc_no_world_action 不能压制 bounded self-check。");
        Assert.IsTrue(client.SawDelegationSelfCheck);
        Assert.IsFalse(runtimeSupervisor.Snapshot().Single(snapshot => snapshot.NpcId == "haley").Controller.IngressWorkItems.Any());
    }

    [TestMethod]
    public async Task ReplyAsync_AgentToolUsesDelegationClientWhenSupplied()
    {
        var runtimeSupervisor = new NpcRuntimeSupervisor();
        var privateChatClient = new SnapshotAnswerChatClient("unused");
        var delegationClient = new DelegationStreamingChatClient();
        var runner = CreateRunner(
            privateChatClient,
            runtimeSupervisor,
            delegationChatClient: delegationClient);

        await runner.ReplyAsync(
            new NpcPrivateChatRequest("haley", "save-1", "conversation-delegation", "你先整理一下附近情况"),
            CancellationToken.None);
        var handle = runtimeSupervisor.Snapshot().Single(snapshot => snapshot.NpcId == "haley");
        var found = runtimeSupervisor.TryGetTaskView(handle.SessionId, out _);
        Assert.IsTrue(found, "Private-chat runner must create the long-term NPC runtime before agent delegation is inspected.");

        var pack = new FileSystemNpcPackLoader().LoadPacks(_packRoot).Single(pack => pack.Manifest.NpcId == "haley");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");
        var privateHandle = await runtimeSupervisor.GetOrCreatePrivateChatHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAgentBindingRequest(
                ChannelKey: "private_chat",
                SystemPromptSupplement: "Reply directly.",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                Services: new NpcRuntimeCompositionServices(
                    privateChatClient,
                    NullLoggerFactory.Instance,
                    _skillManager,
                    new NoopCronScheduler(),
                    delegationClient),
                ToolSurface: NpcToolSurface.FromTools([])),
            CancellationToken.None);

        var result = await privateHandle.Agent.Tools["agent"].ExecuteAsync(
            new AgentParameters { AgentType = "general", Task = "Summarize nearby context." },
            CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, privateChatClient.CompleteWithToolsCalls);
        Assert.AreEqual(1, delegationClient.StructuredStreamCalls);
        Assert.IsTrue(delegationClient.LastSystemPrompt?.Contains("helpful assistant", StringComparison.OrdinalIgnoreCase));
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
        IEnumerable<ITool>? discoveredTools = null,
        IChatClient? delegationChatClient = null)
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
            maxToolIterations: 3,
            delegationChatClient: delegationChatClient);

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

    private static IReadOnlyList<string> ReadExecutedToolNames(IEnumerable<Message> messages)
        => messages
            .Where(message => string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            .SelectMany(message => message.ToolCalls ?? [])
            .Select(call => call.Name)
            .ToArray();

    private static ToolCall MalformedSubmitHostTaskToolCall()
        => new()
        {
            Id = "malformed-submit",
            Name = "stardew_submit_host_task",
            Arguments = """
            {
              "action": "move",
              "reason": "meet the player at the beach now",
              "target": "beach",
              "conversationId": "conversation-invalid-target"
            }
            """
        };

    private static ToolCall TodoAfterValidationFailureToolCall(string id)
        => new()
        {
            Id = id,
            Name = "todo",
            Arguments = "{\"todos\":[{\"id\":\"meet-beach-now\",\"content\":\"Meet player at the beach now\",\"status\":\"in_progress\"}]}"
        };

    private static ToolCall ValidSubmitHostTaskToolCall(string id)
        => new()
        {
            Id = id,
            Name = "stardew_submit_host_task",
            Arguments = """
            {
              "action": "move",
              "reason": "meet the player at the beach now",
              "target": {
                "locationName": "Beach",
                "x": 32,
                "y": 34,
                "source": "map-skill:stardew.navigation.poi.beach-shoreline"
              },
              "conversationId": "conversation-invalid-target"
            }
            """
        };

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

            if (CompleteWithToolsCalls == 2)
            {
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        new ToolCall
                        {
                            Id = "no-world-after-memory",
                            Name = "npc_no_world_action",
                            Arguments = "{\"reason\":\"只是记住玩家名字，不需要立即改变游戏世界\"}"
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
        public int CompleteWithToolsCalls { get; private set; }

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("ok");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            CompleteWithToolsCalls++;
            var snapshot = messages.ToList();
            SawExpectedMemoryInPrompt = snapshot.Any(message =>
                string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase) &&
                message.Content.Contains("USER PROFILE", StringComparison.Ordinal) &&
                message.Content.Contains(_expectedMemory, StringComparison.Ordinal));
            SawOriginalNamingTurn = snapshot.Any(message =>
                message.Content.Contains("我叫远古牛哥,你记住", StringComparison.Ordinal));

            if (CompleteWithToolsCalls == 1)
            {
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        new ToolCall
                        {
                            Id = "no-world-action",
                            Name = "npc_no_world_action",
                            Arguments = "{\"reason\":\"只是回答玩家的问题\"}"
                        }
                    ]
                });
            }

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

    private sealed class DelegationStreamingChatClient : IChatClient
    {
        public int StructuredStreamCalls { get; private set; }
        public string? LastSystemPrompt { get; private set; }

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("ok");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
            => Task.FromResult(new ChatResponse { Content = "delegation", FinishReason = "stop" });

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
            StructuredStreamCalls++;
            LastSystemPrompt = systemPrompt;
            yield return new StreamEvent.TokenDelta("delegated");
            yield return new StreamEvent.MessageComplete("stop", new UsageStats(1, 1));
        }
    }

    private sealed class TodoWriteThenFinalChatClient : IChatClient
    {
        private int _calls;

        public bool SawChineseContinuityGuidance { get; private set; }
        public bool SawImmediateDelegationGuidance { get; private set; }
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
            SawImmediateDelegationGuidance |= snapshot.Any(message =>
                string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase) &&
                message.Content.Contains("stardew_submit_host_task", StringComparison.Ordinal) &&
                message.Content.Contains("只口头答应不会让动作发生", StringComparison.Ordinal) &&
                message.Content.Contains("skill_view", StringComparison.Ordinal) &&
                message.Content.Contains("target(locationName,x,y,source)", StringComparison.Ordinal));
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

            if (_calls == 2)
            {
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        new ToolCall
                        {
                            Id = "no-world-after-todo",
                            Name = "npc_no_world_action",
                            Arguments = "{\"reason\":\"玩家约的是明天，不是当前立即改变游戏世界\"}"
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

    private sealed class TodoThenDelegateActionThenFinalChatClient : IChatClient
    {
        private int _calls;

        public bool SawDelegateActionTool { get; private set; }
        public List<string> FirstToolNames { get; } = new();
        public List<ToolDefinition> FirstToolDefinitions { get; } = new();
        public string[] ExecutedWorldPlanningTools { get; private set; } = [];

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("ok");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            _calls++;
            var toolSnapshot = tools.ToList();
            if (_calls == 1)
            {
                FirstToolNames.AddRange(toolSnapshot.Select(tool => tool.Name));
                FirstToolDefinitions.AddRange(toolSnapshot);
            }

            SawDelegateActionTool |= toolSnapshot.Any(tool => string.Equals(tool.Name, "stardew_submit_host_task", StringComparison.Ordinal));
            if (_calls == 1)
            {
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        new ToolCall
                        {
                            Id = "todo-commitment",
                            Name = "todo",
                            Arguments = "{\"todos\":[{\"id\":\"meet-beach-now\",\"content\":\"Meet player at the beach now\",\"status\":\"in_progress\"}]}"
                        },
                        new ToolCall
                        {
                            Id = "delegate-action",
                            Name = "stardew_submit_host_task",
                            Arguments = """
                            {
                              "action": "move",
                              "reason": "meet the player at the beach now",
                              "target": {
                                "locationName": "Beach",
                                "x": 32,
                                "y": 34,
                                "source": "map-skill:stardew.navigation.poi.beach-shoreline"
                              },
                              "conversationId": "conversation-beach"
                            }
                            """
                        }
                    ]
                });
            }

            if (_calls == 2)
                ExecutedWorldPlanningTools = ReadExecutedToolNames(messages)
                    .Where(name => name is "todo" or "stardew_submit_host_task")
                    .ToArray();

            return Task.FromResult(new ChatResponse { Content = "I'll head there now.", FinishReason = "stop" });
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

    private sealed class TwoConversationImmediateActionChatClient : IChatClient
    {
        private int _calls;

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("ok");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            _calls++;
            if (_calls is 1 or 3)
            {
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        new ToolCall
                        {
                            Id = $"todo-commitment-{_calls}",
                            Name = "todo",
                            Arguments = "{\"todos\":[{\"id\":\"meet-beach-now\",\"content\":\"Meet player at the beach now\",\"status\":\"in_progress\"}]}"
                        },
                        new ToolCall
                        {
                            Id = $"delegate-action-{_calls}",
                            Name = "stardew_submit_host_task",
                            Arguments = """
                            {
                              "action": "move",
                              "reason": "meet the player at the beach now",
                              "target": {
                                "locationName": "Beach",
                                "x": 32,
                                "y": 34,
                                "source": "map-skill:stardew.navigation.poi.beach-shoreline"
                              }
                            }
                            """
                        }
                    ]
                });
            }

            return Task.FromResult(new ChatResponse { Content = "I'll head there now.", FinishReason = "stop" });
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

    private sealed class FirstAcceptsWithoutDelegatingThenDelegatesChatClient : IChatClient
    {
        public int CompleteWithToolsCalls { get; private set; }
        public List<IReadOnlyList<Message>> MessagesByCall { get; } = [];

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("ok");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            CompleteWithToolsCalls++;
            MessagesByCall.Add(messages.ToArray());
            if (CompleteWithToolsCalls == 1)
            {
                return Task.FromResult(new ChatResponse
                {
                    Content = "好，我现在过去。",
                    FinishReason = "stop"
                });
            }

            if (CompleteWithToolsCalls == 2)
            {
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        new ToolCall
                        {
                            Id = "todo-after-correction",
                            Name = "todo",
                            Arguments = "{\"todos\":[{\"id\":\"meet-beach-now\",\"content\":\"Meet player at the beach now\",\"status\":\"in_progress\"}]}"
                        },
                        new ToolCall
                        {
                            Id = "delegate-after-correction",
                            Name = "stardew_submit_host_task",
                            Arguments = """
                            {
                              "action": "move",
                              "reason": "现在陪玩家去海边",
                              "target": {
                                "locationName": "Beach",
                                "x": 32,
                                "y": 34,
                                "source": "map-skill:stardew.navigation.poi.beach-shoreline"
                              },
                              "conversationId": "conversation-corrective"
                            }
                            """
                        }
                    ]
                });
            }

            return Task.FromResult(new ChatResponse { Content = "好，我现在过去。", FinishReason = "stop" });
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

    private sealed class FirstMalformedSubmitThenNativeRetryChatClient : IChatClient
    {
        public int CompleteWithToolsCalls { get; private set; }
        public bool SawDelegationSelfCheck { get; private set; }
        public bool SawMissingTodoSelfCheck { get; private set; }

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("ok");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            CompleteWithToolsCalls++;
            var snapshot = messages.ToArray();
            SawDelegationSelfCheck |= snapshot.Any(message =>
                string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase) &&
                (message.Content?.Contains("自检：上一轮私聊回复没有调用 stardew_submit_host_task", StringComparison.Ordinal) ?? false));
            SawMissingTodoSelfCheck |= snapshot.Any(message =>
                string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase) &&
                (message.Content?.Contains("缺少 todo", StringComparison.Ordinal) ?? false));

            if (CompleteWithToolsCalls == 1)
            {
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        MalformedSubmitHostTaskToolCall()
                    ]
                });
            }

            if (CompleteWithToolsCalls == 2)
            {
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        TodoAfterValidationFailureToolCall("todo-after-validation-failure"),
                        ValidSubmitHostTaskToolCall("submit-after-validation-failure")
                    ]
                });
            }

            return Task.FromResult(new ChatResponse { Content = "好，我现在过去。", FinishReason = "stop" });
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

    private sealed class FirstMalformedSubmitThenStopsThenSelfCheckRetryChatClient : IChatClient
    {
        public int CompleteWithToolsCalls { get; private set; }
        public bool SawDelegationSelfCheck { get; private set; }
        public bool SawMissingTodoSelfCheck { get; private set; }

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("ok");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            CompleteWithToolsCalls++;
            var snapshot = messages.ToArray();
            SawDelegationSelfCheck |= snapshot.Any(message =>
                string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase) &&
                (message.Content?.Contains("自检：上一轮私聊回复没有调用 stardew_submit_host_task", StringComparison.Ordinal) ?? false));
            SawMissingTodoSelfCheck |= snapshot.Any(message =>
                string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase) &&
                (message.Content?.Contains("缺少 todo", StringComparison.Ordinal) ?? false));

            if (CompleteWithToolsCalls == 1)
            {
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        MalformedSubmitHostTaskToolCall()
                    ]
                });
            }

            if (CompleteWithToolsCalls == 2)
                return Task.FromResult(new ChatResponse { Content = "好，我现在过去。", FinishReason = "stop" });

            if (CompleteWithToolsCalls == 3)
            {
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        TodoAfterValidationFailureToolCall("todo-after-self-check"),
                        ValidSubmitHostTaskToolCall("submit-after-self-check")
                    ]
                });
            }

            return Task.FromResult(new ChatResponse { Content = "好，我现在过去。", FinishReason = "stop" });
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

    private sealed class FirstDelegatesWithoutTodoThenWritesTodoChatClient : IChatClient
    {
        public int CompleteWithToolsCalls { get; private set; }
        public bool SawMissingTodoSelfCheck { get; private set; }

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("ok");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            CompleteWithToolsCalls++;
            var snapshot = messages.ToArray();
            SawMissingTodoSelfCheck |= snapshot.Any(message =>
                string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase) &&
                (message.Content?.Contains("缺少 todo", StringComparison.Ordinal) ?? false) &&
                (message.Content?.Contains("不要重复调用 stardew_submit_host_task", StringComparison.Ordinal) ?? false));
            if (CompleteWithToolsCalls == 1)
            {
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        new ToolCall
                        {
                            Id = "delegate-action",
                            Name = "stardew_submit_host_task",
                            Arguments = """
                            {
                              "action": "move",
                              "reason": "meet the player at the beach now",
                              "target": {
                                "locationName": "Beach",
                                "x": 32,
                                "y": 34,
                                "source": "map-skill:stardew.navigation.poi.beach-shoreline"
                              }
                            }
                            """
                        }
                    ]
                });
            }

            if (CompleteWithToolsCalls == 2)
            {
                return Task.FromResult(new ChatResponse { Content = "I'll head there now.", FinishReason = "stop" });
            }

            if (CompleteWithToolsCalls == 3)
            {
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        new ToolCall
                        {
                            Id = "todo-commitment-repair",
                            Name = "todo",
                            Arguments = "{\"todos\":[{\"id\":\"meet-beach-now\",\"content\":\"Meet player at the beach now\",\"status\":\"in_progress\"}]}"
                        }
                    ]
                });
            }

            return Task.FromResult(new ChatResponse { Content = "I'll head there now.", FinishReason = "stop" });
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

    private sealed class FirstDelegatesWithTodoThenEmptyReplyThenNaturalReplyChatClient : IChatClient
    {
        public int CompleteWithToolsCalls { get; private set; }
        public bool SawReplySelfCheck { get; private set; }

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("ok");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            CompleteWithToolsCalls++;
            var snapshot = messages.ToArray();
            SawReplySelfCheck |= snapshot.Any(message =>
                string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase) &&
                (message.Content?.Contains("缺少玩家可见回复", StringComparison.Ordinal) ?? false) &&
                (message.Content?.Contains("不要重复调用 stardew_submit_host_task", StringComparison.Ordinal) ?? false));
            if (CompleteWithToolsCalls == 1)
            {
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        new ToolCall
                        {
                            Id = "todo-before-empty-reply",
                            Name = "todo",
                            Arguments = "{\"todos\":[{\"id\":\"meet-beach-now\",\"content\":\"Meet player at the beach now\",\"status\":\"in_progress\"}]}"
                        },
                        new ToolCall
                        {
                            Id = "submit-before-empty-reply",
                            Name = "stardew_submit_host_task",
                            Arguments = """
                            {
                              "action": "move",
                              "reason": "meet the player at the beach now",
                              "target": {
                                "locationName": "Beach",
                                "x": 32,
                                "y": 34,
                                "source": "map-skill:stardew.navigation.poi.beach-shoreline"
                              },
                              "conversationId": "conversation-reply-repair"
                            }
                            """
                        }
                    ]
                });
            }

            if (CompleteWithToolsCalls == 2)
                return Task.FromResult(new ChatResponse { Content = "   ", FinishReason = "stop" });

            return Task.FromResult(new ChatResponse { Content = "好，我先回你一句，然后就过去。", FinishReason = "stop" });
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

    private sealed class FirstDelegatesWithTodoThenAlwaysEmptyReplyChatClient : IChatClient
    {
        public int CompleteWithToolsCalls { get; private set; }
        public bool SawReplySelfCheck { get; private set; }

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("ok");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            CompleteWithToolsCalls++;
            var snapshot = messages.ToArray();
            SawReplySelfCheck |= snapshot.Any(message =>
                string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase) &&
                (message.Content?.Contains("缺少玩家可见回复", StringComparison.Ordinal) ?? false) &&
                (message.Content?.Contains("不要重复调用 stardew_submit_host_task", StringComparison.Ordinal) ?? false));
            if (CompleteWithToolsCalls == 1)
            {
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        new ToolCall
                        {
                            Id = "todo-before-empty-reply-fail",
                            Name = "todo",
                            Arguments = "{\"todos\":[{\"id\":\"meet-beach-now\",\"content\":\"Meet player at the beach now\",\"status\":\"in_progress\"}]}"
                        },
                        new ToolCall
                        {
                            Id = "submit-before-empty-reply-fail",
                            Name = "stardew_submit_host_task",
                            Arguments = """
                            {
                              "action": "move",
                              "reason": "meet the player at the beach now",
                              "target": {
                                "locationName": "Beach",
                                "x": 32,
                                "y": 34,
                                "source": "map-skill:stardew.navigation.poi.beach-shoreline"
                              },
                              "conversationId": "conversation-reply-fails"
                            }
                            """
                        }
                    ]
                });
            }

            return Task.FromResult(new ChatResponse { Content = "", FinishReason = "stop" });
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

    private sealed class FirstCasualAcceptsWithoutDelegatingThenDelegatesChatClient : IChatClient
    {
        public int CompleteWithToolsCalls { get; private set; }

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
                return Task.FromResult(new ChatResponse
                {
                    Content = "这不已经走着呢嘛，海水应该蓝得发亮。",
                    FinishReason = "stop"
                });
            }

            if (CompleteWithToolsCalls == 2)
            {
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        new ToolCall
                        {
                            Id = "todo-after-casual-correction",
                            Name = "todo",
                            Arguments = "{\"todos\":[{\"id\":\"meet-beach-now\",\"content\":\"Meet player at the beach now\",\"status\":\"in_progress\"}]}"
                        },
                        new ToolCall
                        {
                            Id = "delegate-after-casual-correction",
                            Name = "stardew_submit_host_task",
                            Arguments = """
                            {
                              "action": "move",
                              "reason": "陪玩家去海边",
                              "target": {
                                "locationName": "Beach",
                                "x": 32,
                                "y": 34,
                                "source": "map-skill:stardew.navigation.poi.beach-shoreline"
                              },
                              "conversationId": "conversation-casual-corrective"
                            }
                            """
                        }
                    ]
                });
            }

            return Task.FromResult(new ChatResponse { Content = "好啦，我们走。", FinishReason = "stop" });
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

    private sealed class FirstUsesSessionSearchThenSelfCheckChatClient : IChatClient
    {
        public int CompleteWithToolsCalls { get; private set; }

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
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        new ToolCall
                        {
                            Id = "search-before-reply",
                            Name = "session_search",
                            Arguments = "{\"query\":\"之前聊过什么\",\"limit\":1}"
                        }
                    ]
                });
            }

            if (CompleteWithToolsCalls == 2)
            {
                return Task.FromResult(new ChatResponse { Content = "我记得，我们聊过花。", FinishReason = "stop" });
            }

            if (CompleteWithToolsCalls == 3)
            {
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        new ToolCall
                        {
                            Id = "no-world-after-self-check",
                            Name = "npc_no_world_action",
                            Arguments = "{\"reason\":\"只是回答记忆问题，不需要立即改变游戏世界\"}"
                        }
                    ]
                });
            }

            return Task.FromResult(new ChatResponse { Content = "我记得，我们聊过花。", FinishReason = "stop" });
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

    private sealed class NoWorldActionToolThenFinalChatClient : IChatClient
    {
        public int CompleteWithToolsCalls { get; private set; }

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
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        new ToolCall
                        {
                            Id = "no-world-action",
                            Name = "npc_no_world_action",
                            Arguments = "{\"reason\":\"只是闲聊\"}"
                        }
                    ]
                });
            }

            return Task.FromResult(new ChatResponse
            {
                Content = "今天还不错。",
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

    private sealed class FailedNoWorldActionThenSelfCheckChatClient : IChatClient
    {
        public int CompleteWithToolsCalls { get; private set; }
        public bool SawDelegationSelfCheck { get; private set; }

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("ok");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            CompleteWithToolsCalls++;
            var snapshot = messages.ToArray();
            SawDelegationSelfCheck |= snapshot.Any(message =>
                string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase) &&
                (message.Content?.Contains("自检：上一轮私聊回复没有调用 stardew_submit_host_task", StringComparison.Ordinal) ?? false));

            if (CompleteWithToolsCalls == 1)
            {
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        new ToolCall
                        {
                            Id = "invalid-no-world-action",
                            Name = "npc_no_world_action",
                            Arguments = "{\"reason\":\"\"}"
                        }
                    ]
                });
            }

            if (CompleteWithToolsCalls == 2)
                return Task.FromResult(new ChatResponse { Content = "今天还不错。", FinishReason = "stop" });

            if (CompleteWithToolsCalls == 3)
            {
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        new ToolCall
                        {
                            Id = "valid-no-world-action-after-self-check",
                            Name = "npc_no_world_action",
                            Arguments = "{\"reason\":\"只是闲聊，不需要立即改变游戏世界\"}"
                        }
                    ]
                });
            }

            return Task.FromResult(new ChatResponse { Content = "今天还不错。", FinishReason = "stop" });
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
