using Hermes.Agent.Core;
using Hermes.Agent.LLM;
using Hermes.Agent.Permissions;
using Hermes.Agent.Tasks;
using Hermes.Agent.Tools;
using Hermes.Agent.Transcript;
using HermesDesktop.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Services;

[TestClass]
public sealed class HermesChatServiceTaskLoopTests
{
    private string _tempDir = "";

    [TestInitialize]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"hermes-chat-task-loop-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public async Task SendAsync_TodoToolCompletion_UpdatesSessionProjectionFromPersistedResult()
    {
        var todoStore = new SessionTodoStore();
        var projection = new SessionTaskProjectionService(todoStore);
        var transcriptStore = new TranscriptStore(Path.Combine(_tempDir, "transcripts"), messageObserver: projection);
        var client = new ToolCallingChatClient();
        var agent = new Agent(client, NullLogger<Agent>.Instance);
        agent.RegisterTool(new TodoTool(todoStore));
        var service = CreateService(agent, client, transcriptStore, projection);
        var snapshots = new List<SessionTodoSnapshot>();
        projection.SnapshotChanged += (_, e) => snapshots.Add(e.Snapshot);

        var reply = await service.SendAsync("track this", CancellationToken.None);

        Assert.AreEqual("done", reply.Response);
        Assert.IsTrue(snapshots.Any(s => s.Todos.Any(t => t.Content == "Visible task")));
        var projected = projection.GetSnapshot(reply.SessionId);
        Assert.AreEqual(1, projected.Todos.Count);
        Assert.AreEqual("Visible task", projected.Todos[0].Content);
    }

    [TestMethod]
    public async Task AgentChatAsync_WithToolSessionId_ExecutesTodoAgainstToolSessionAndPersistsTranscriptSession()
    {
        var todoStore = new SessionTodoStore();
        var projection = new SessionTaskProjectionService(todoStore);
        var transcriptStore = new TranscriptStore(Path.Combine(_tempDir, "transcripts"), messageObserver: projection);
        var client = new ToolCallingChatClient();
        var agent = new Agent(client, NullLogger<Agent>.Instance, transcripts: transcriptStore);
        agent.RegisterTool(new TodoTool(todoStore));
        var session = new Session
        {
            Id = "npc-1:private_chat:conversation-1",
            ToolSessionId = "npc-1"
        };

        var reply = await agent.ChatAsync("remember this promise", session, CancellationToken.None);

        Assert.AreEqual("done", reply);
        Assert.AreEqual(1, todoStore.Read("npc-1").Todos.Count);
        Assert.AreEqual("Visible task", todoStore.Read("npc-1").Todos[0].Content);
        var transcriptMessages = await transcriptStore.LoadSessionAsync("npc-1:private_chat:conversation-1", CancellationToken.None);
        var transcriptToolMessage = transcriptMessages.Single(message => message.Role == "tool" && message.ToolName == "todo");
        Assert.AreEqual("npc-1", transcriptToolMessage.TaskSessionId);
        Assert.AreEqual(0, todoStore.Read("npc-1:private_chat:conversation-1").Todos.Count);
        Assert.AreEqual(0, projection.GetSnapshot("npc-1:private_chat:conversation-1").Todos.Count);
    }

    [TestMethod]
    public async Task AgentChatAsync_WithoutToolSessionId_PersistsTaskSessionIdAsSessionId()
    {
        var todoStore = new SessionTodoStore();
        var projection = new SessionTaskProjectionService(todoStore);
        var transcriptStore = new TranscriptStore(Path.Combine(_tempDir, "transcripts"), messageObserver: projection);
        var client = new ToolCallingChatClient();
        var agent = new Agent(client, NullLogger<Agent>.Instance, transcripts: transcriptStore);
        agent.RegisterTool(new TodoTool(todoStore));
        var session = new Session { Id = "npc-1" };

        var reply = await agent.ChatAsync("remember root promise", session, CancellationToken.None);

        Assert.AreEqual("done", reply);
        var transcriptMessages = await transcriptStore.LoadSessionAsync("npc-1", CancellationToken.None);
        var transcriptToolMessage = transcriptMessages.Single(message => message.Role == "tool" && message.ToolName == "todo");
        Assert.AreEqual("npc-1", transcriptToolMessage.TaskSessionId);
    }

    [TestMethod]
    public async Task AgentChatAsync_WhenPermissionRuleDeniesTool_PersistsTaskSessionIdOnDenialResult()
    {
        var transcriptPath = Path.Combine(_tempDir, "transcripts");
        var transcriptStore = new TranscriptStore(transcriptPath);
        var client = new ToolCallingChatClient();
        var permissions = new PermissionManager(
            new PermissionContext
            {
                Mode = PermissionMode.Default,
                AlwaysDeny = { PermissionRule.DenyAll("todo") }
            },
            NullLogger<PermissionManager>.Instance);
        var agent = new Agent(client, NullLogger<Agent>.Instance, permissions: permissions, transcripts: transcriptStore);
        agent.RegisterTool(new TodoTool(new SessionTodoStore()));
        var session = new Session
        {
            Id = "npc-1:private_chat:conversation-1",
            ToolSessionId = "npc-1"
        };

        var reply = await agent.ChatAsync("try denied todo", session, CancellationToken.None);

        Assert.AreEqual("done", reply);
        var transcriptToolMessage = await LoadTodoToolMessageFromFreshStoreAsync(transcriptPath, session.Id);
        Assert.AreEqual("npc-1", transcriptToolMessage.TaskSessionId);
    }

    [TestMethod]
    public async Task AgentChatAsync_WhenPermissionPromptDeniesTool_PersistsTaskSessionIdOnDenialResult()
    {
        var transcriptPath = Path.Combine(_tempDir, "transcripts");
        var transcriptStore = new TranscriptStore(transcriptPath);
        var client = new ToolCallingChatClient();
        var permissions = new PermissionManager(
            new PermissionContext { Mode = PermissionMode.Default },
            NullLogger<PermissionManager>.Instance);
        var agent = new Agent(client, NullLogger<Agent>.Instance, permissions: permissions, transcripts: transcriptStore);
        agent.RegisterTool(new TodoTool(new SessionTodoStore()));
        agent.PermissionPromptCallback = (_, _, _) => Task.FromResult(false);
        var session = new Session { Id = "npc-1" };

        var reply = await agent.ChatAsync("try user denied todo", session, CancellationToken.None);

        Assert.AreEqual("done", reply);
        var transcriptToolMessage = await LoadTodoToolMessageFromFreshStoreAsync(transcriptPath, session.Id);
        Assert.AreEqual("npc-1", transcriptToolMessage.TaskSessionId);
    }

    [TestMethod]
    public async Task LoadSessionAsync_HydratesTodoProjectionFromTranscriptHistory()
    {
        var transcriptStore = new TranscriptStore(Path.Combine(_tempDir, "transcripts"));
        const string sessionId = "load-task-session";
        await transcriptStore.SaveMessageAsync(sessionId, new Message
        {
            Role = "tool",
            ToolName = "todo",
            ToolCallId = "call-1",
            Content = "{\"todos\":[{\"id\":\"1\",\"content\":\"Hydrated task\",\"status\":\"in_progress\"}]}"
        }, CancellationToken.None);
        var projection = new SessionTaskProjectionService(new SessionTodoStore());
        var client = new ToolCallingChatClient();
        var service = CreateService(new Agent(client, NullLogger<Agent>.Instance), client, transcriptStore, projection);

        await service.LoadSessionAsync(sessionId, CancellationToken.None);

        Assert.AreEqual(sessionId, service.CurrentSessionId);
        var snapshot = projection.GetSnapshot(sessionId);
        Assert.AreEqual(1, snapshot.Todos.Count);
        Assert.AreEqual("Hydrated task", snapshot.Todos[0].Content);
    }

    [TestMethod]
    public async Task ResetConversationAsync_ClearsCurrentSessionTaskProjection()
    {
        var todoStore = new SessionTodoStore();
        var projection = new SessionTaskProjectionService(todoStore);
        var client = new ToolCallingChatClient();
        var service = CreateService(
            new Agent(client, NullLogger<Agent>.Instance),
            client,
            new TranscriptStore(Path.Combine(_tempDir, "transcripts")),
            projection);
        service.EnsureSession();
        var sessionId = service.CurrentSessionId!;
        todoStore.Write(sessionId, new[] { new SessionTodoInput("1", "Clear me", "pending") });

        await service.ResetConversationAsync(CancellationToken.None);

        Assert.IsNull(service.CurrentSessionId);
        Assert.AreEqual(0, projection.GetSnapshot(sessionId).Todos.Count);
    }

    private HermesChatService CreateService(
        Agent agent,
        IChatClient client,
        TranscriptStore transcriptStore,
        SessionTaskProjectionService projection)
    {
        var permissionRoot = Path.Combine(_tempDir, "permissions");
        return new HermesChatService(
            agent,
            client,
            transcriptStore,
            new PermissionManager(new PermissionContext { Mode = PermissionMode.BypassPermissions }, NullLogger<PermissionManager>.Instance),
            new WorkspacePermissionRuleStore(permissionRoot, _tempDir, NullLogger<WorkspacePermissionRuleStore>.Instance),
            new InMemoryCronScheduler(),
            projection,
            NullLogger<HermesChatService>.Instance);
    }

    private static async Task<Message> LoadTodoToolMessageFromFreshStoreAsync(string transcriptPath, string sessionId)
    {
        var reloadedTranscriptStore = new TranscriptStore(transcriptPath);
        var transcriptMessages = await reloadedTranscriptStore.LoadSessionAsync(sessionId, CancellationToken.None);
        return transcriptMessages.Single(message => message.Role == "tool" && message.ToolName == "todo");
    }

    private sealed class ToolCallingChatClient : IChatClient
    {
        private int _calls;

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("done");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            _calls++;
            if (_calls == 1)
            {
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls =
                    [
                        new ToolCall
                        {
                            Id = "call-1",
                            Name = "todo",
                            Arguments = "{\"todos\":[{\"id\":\"1\",\"content\":\"Visible task\",\"status\":\"pending\"}]}"
                        }
                    ]
                });
            }

            return Task.FromResult(new ChatResponse { Content = "done", FinishReason = "stop" });
        }

        public async IAsyncEnumerable<string> StreamAsync(
            IEnumerable<Message> messages,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<StreamEvent> StreamAsync(
            string? systemPrompt,
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition>? tools = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
