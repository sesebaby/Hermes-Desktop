namespace HermesDesktop.Tests.Runtime;

using Hermes.Agent.Core;
using Hermes.Agent.Runtime;
using Hermes.Agent.Tasks;
using Hermes.Agent.Transcript;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class NpcDeveloperInspectorServiceTests
{
    private string _tempDir = "";

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "hermes-npc-inspector-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public async Task InspectAsync_WithRuntimeFilesTranscriptTraceAndTodos_ProjectsReadOnlyDebugView()
    {
        var descriptor = CreateDescriptor();
        var npcNamespace = new NpcNamespace(_tempDir, descriptor.GameId, descriptor.SaveId, descriptor.NpcId, descriptor.ProfileId);
        npcNamespace.EnsureDirectories();
        await File.WriteAllTextAsync(npcNamespace.SoulFilePath, "# Haley Soul\nBe direct.");
        await File.WriteAllTextAsync(Path.Combine(npcNamespace.MemoryPath, "MEMORY.md"), "Beach is south.");
        await File.WriteAllTextAsync(Path.Combine(npcNamespace.MemoryPath, "USER.md"), "Player wants Chinese UI.");
        await File.WriteAllLinesAsync(
            Path.Combine(npcNamespace.ActivityPath, "runtime.jsonl"),
            [
                """{"timestampUtc":"2026-05-07T02:00:00Z","traceId":"trace-a","npcId":"haley","gameId":"stardew-valley","sessionId":"npc-session","actionType":"observation","target":"world","stage":"captured","result":"location=Town;tile=52,74"}""",
                """not-json""",
                """{"timestampUtc":"2026-05-07T02:00:01Z","traceId":"trace-a","npcId":"haley","gameId":"stardew-valley","sessionId":"npc-session","actionType":"diagnostic","target":"local_executor","stage":"selected","result":"action=move;lane=delegation"}""",
                """{"timestampUtc":"2026-05-07T02:00:02Z","traceId":"trace-a","npcId":"haley","gameId":"stardew-valley","sessionId":"npc-session","actionType":"local_executor","target":"stardew_move","stage":"completed","result":"task_move_enqueued","commandId":"cmd-1","executorMode":"model_called"}""",
                """{"timestampUtc":"2026-05-07T02:00:03Z","traceId":"trace-a","npcId":"haley","gameId":"stardew-valley","sessionId":"npc-session","actionType":"task_continuity","target":"command_terminal","stage":"terminal","result":"task_completed","commandId":"cmd-1"}""",
                """{"timestampUtc":"2026-05-07T02:00:04Z","traceId":"trace-b","npcId":"penny","gameId":"stardew-valley","sessionId":"other","actionType":"diagnostic","target":"ignored","stage":"ignored","result":"ignored"}"""
            ]);

        var transcriptStore = npcNamespace.CreateTranscriptStore(Microsoft.Extensions.Logging.Abstractions.NullLogger<global::Hermes.Agent.Search.SessionSearchIndex>.Instance);
        await transcriptStore.SaveMessageAsync(
            descriptor.SessionId,
            new Message
            {
                Role = "assistant",
                Content = "我会去海边。",
                ReasoningContent = "需要选择 Beach 的明确坐标。",
                ToolCalls =
                [
                    new ToolCall
                    {
                        Id = "call-1",
                        Name = "stardew_move",
                        Arguments = """{"location":"Beach","tile":{"x":20,"y":15}}"""
                    },
                    new ToolCall
                    {
                        Id = "call-2",
                        Name = "agent",
                        Arguments = """{"task":"选择 Beach 坐标"}"""
                    }
                ]
            },
            CancellationToken.None);
        await transcriptStore.SaveMessageAsync(
            descriptor.SessionId,
            new Message
            {
                Role = "tool",
                Content = """{"status":"accepted","commandId":"cmd-1"}""",
                ToolName = "stardew_move",
                ToolCallId = "call-1"
            },
            CancellationToken.None);
        await transcriptStore.SaveMessageAsync(
            descriptor.SessionId,
            new Message
            {
                Role = "tool",
                Content = "子代理建议坐标 Beach 20,15。",
                ToolName = "agent",
                ToolCallId = "call-2"
            },
            CancellationToken.None);

        var todoStore = new SessionTodoStore();
        todoStore.Write(
            descriptor.SessionId,
            [
                new SessionTodoInput("1", "去海边，不要瞬移", "in_progress", null),
                new SessionTodoInput("2", "等待桥接结果", "blocked", "等待 cmd-1")
            ]);
        var runtimeSnapshot = new NpcRuntimeSnapshot(
            descriptor.NpcId,
            descriptor.DisplayName,
            descriptor.GameId,
            descriptor.SaveId,
            descriptor.ProfileId,
            descriptor.SessionId,
            NpcRuntimeState.Running,
            "trace-a",
            null,
            0,
            1,
            NpcAutonomyLoopState.Running,
            null,
            DateTime.UtcNow,
            "bridge-key",
            1,
            0,
            null,
            NpcRuntimeControllerSnapshot.Empty);
        var service = new NpcDeveloperInspectorService(
            new NpcDeveloperInspectorOptions
            {
                PreviewCharacterLimit = 200,
                RuntimeLogMaxLines = 20
            },
            transcriptFactory: _ => transcriptStore,
            todoStoreFactory: _ => todoStore);

        var view = await service.InspectAsync(runtimeSnapshot, _tempDir, CancellationToken.None);

        Assert.AreEqual("haley", view.NpcId);
        Assert.AreEqual("海莉", view.DisplayName);
        Assert.AreEqual("npc-session", view.SessionId);
        Assert.AreEqual("stardew_autonomy", view.MainChannel);
        Assert.AreEqual("delegation", view.DelegationChannel);
        CollectionAssert.AreEqual(
            new[] { "SOUL.md", "MEMORY.md", "USER.md" },
            view.Documents.Select(document => document.Name).ToArray());
        StringAssert.Contains(view.Documents[0].Content, "Haley Soul");
        StringAssert.Contains(view.Documents[1].Content, "Beach is south.");
        StringAssert.Contains(view.Documents[2].Content, "Chinese UI");
        Assert.IsTrue(view.TraceDiagnostics.Any(item => item.Contains("第 2 行", StringComparison.Ordinal)));
        CollectionAssert.AreEqual(
            new[] { "观察事实", "本地执行", "工具调用", "执行结果" },
            view.TraceEvents.Select(item => item.Kind).ToArray());
        Assert.AreEqual(1, view.ModelReplies.Count);
        Assert.AreEqual("我会去海边。", view.ModelReplies[0].Content);
        Assert.AreEqual("需要选择 Beach 的明确坐标。", view.ModelReplies[0].Reasoning);
        Assert.AreEqual(2, view.ToolCalls.Count);
        Assert.AreEqual("stardew_move", view.ToolCalls[0].Name);
        StringAssert.Contains(view.ToolCalls[0].Arguments, "Beach");
        StringAssert.Contains(view.ToolCalls[0].Result, "cmd-1");
        Assert.AreEqual(1, view.Delegations.Count);
        StringAssert.Contains(view.Delegations[0].Result, "Beach 20,15");
        Assert.AreEqual(2, view.Todos.Count);
        Assert.AreEqual("in_progress", view.Todos[0].Status);
        Assert.AreEqual("blocked", view.Todos[1].Status);
    }

    [TestMethod]
    public async Task InspectAsync_WithMissingFilesAndTranscript_ReturnsChineseMissingStates()
    {
        var descriptor = CreateDescriptor();
        var snapshot = new NpcRuntimeSnapshot(
            descriptor.NpcId,
            descriptor.DisplayName,
            descriptor.GameId,
            descriptor.SaveId,
            descriptor.ProfileId,
            descriptor.SessionId,
            NpcRuntimeState.Created,
            null,
            null,
            0,
            0,
            NpcAutonomyLoopState.NotStarted,
            null,
            null,
            null,
            0,
            0,
            null,
            NpcRuntimeControllerSnapshot.Empty);
        var service = new NpcDeveloperInspectorService(
            new NpcDeveloperInspectorOptions
            {
                PreviewCharacterLimit = 200,
                RuntimeLogMaxLines = 20
            },
            transcriptFactory: ns => ns.CreateTranscriptStore(Microsoft.Extensions.Logging.Abstractions.NullLogger<global::Hermes.Agent.Search.SessionSearchIndex>.Instance),
            todoStoreFactory: _ => new SessionTodoStore());

        var view = await service.InspectAsync(snapshot, _tempDir, CancellationToken.None);

        Assert.AreEqual(3, view.Documents.Count);
        Assert.IsTrue(view.Documents.All(document => !document.Exists));
        Assert.IsTrue(view.Documents.All(document => document.Status.Contains("未找到", StringComparison.Ordinal)));
        Assert.AreEqual("当前会话还没有保存模型回复。", view.ModelReplyEmptyState);
        Assert.AreEqual("当前记录未包含工具调用。", view.ToolCallEmptyState);
        Assert.AreEqual("本次追踪未发现委托。", view.DelegationEmptyState);
        Assert.AreEqual("当前会话没有待办事项。", view.TodoEmptyState);
        Assert.AreEqual("未找到运行时活动日志。", view.TraceEmptyState);
    }

    private static NpcRuntimeDescriptor CreateDescriptor()
        => new(
            "haley",
            "海莉",
            "stardew-valley",
            "save-1",
            "default",
            "stardew",
            "pack-root",
            "npc-session");
}
