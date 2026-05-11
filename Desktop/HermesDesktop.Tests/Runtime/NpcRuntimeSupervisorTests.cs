using System.Runtime.CompilerServices;
using System.Text.Json;
using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.LLM;
using Hermes.Agent.Runtime;
using Hermes.Agent.Skills;
using Hermes.Agent.Tasks;
using Hermes.Agent.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Runtime;

[TestClass]
public class NpcRuntimeSupervisorTests
{
    private string _tempDir = null!;
    private string _packRoot = null!;
    private SkillManager _skillManager = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "hermes-runtime-supervisor-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _packRoot = Path.Combine(_tempDir, "packs");
        Directory.CreateDirectory(_packRoot);
        _skillManager = CreateSkillManager();
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public async Task StartAsync_RegistersRunningInstanceAndCreatesNamespace()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var descriptor = CreateDescriptor("haley");

        await supervisor.StartAsync(descriptor, _tempDir, CancellationToken.None);

        var snapshot = supervisor.Snapshot().Single();
        Assert.AreEqual("haley", snapshot.NpcId);
        Assert.AreEqual(NpcRuntimeState.Running, snapshot.State);
        Assert.IsTrue(Directory.Exists(Path.Combine(_tempDir, "runtime", "stardew", "games", "stardew-valley", "saves", "save-1", "npc", "haley", "profiles", "default")));
    }

    [TestMethod]
    public async Task StartAsync_RejectsDuplicateNpcRuntimeForSameProfile()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var descriptor = CreateDescriptor("haley");
        await supervisor.StartAsync(descriptor, _tempDir, CancellationToken.None);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            await supervisor.StartAsync(descriptor, _tempDir, CancellationToken.None));
    }

    [TestMethod]
    public async Task GetOrStartAsync_ReusesExistingRuntimeForSameProfile()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var descriptor = CreateDescriptor("penny");

        var first = await supervisor.GetOrStartAsync(descriptor, _tempDir, CancellationToken.None);
        var second = await supervisor.GetOrStartAsync(descriptor, _tempDir, CancellationToken.None);

        Assert.AreSame(first, second);
        Assert.AreEqual(1, supervisor.Snapshot().Count);
        Assert.AreEqual(NpcRuntimeState.Running, supervisor.Snapshot().Single().State);
    }

    [TestMethod]
    public async Task GetOrStartAsync_HydratesTasksBeforeAnyHandleExists()
    {
        var hydrator = new WritingTaskHydrator("Recovered beach promise");
        var supervisor = new NpcRuntimeSupervisor(hydrator);
        var descriptor = CreateDescriptor("haley");

        await supervisor.GetOrStartAsync(descriptor, _tempDir, CancellationToken.None);

        Assert.AreEqual(1, hydrator.CallCount);
        Assert.IsTrue(supervisor.TryGetTaskView(descriptor.SessionId, out var taskView));
        Assert.IsNotNull(taskView);
        Assert.AreEqual(1, taskView.ActiveSnapshot.Todos.Count);
        Assert.AreEqual("Recovered beach promise", taskView.ActiveSnapshot.Todos[0].Content);
    }

    [TestMethod]
    public async Task GetOrStartAsync_AndPrivateChatHandleCreation_ShareSingleFlightHydration()
    {
        var hydrator = new BlockingTaskHydrator();
        var supervisor = new NpcRuntimeSupervisor(hydrator);
        var pack = CreatePack("haley", "Haley");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");
        var services = new NpcRuntimeCompositionServices(
            new FakeChatClient(),
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler());

        var startTask = supervisor.GetOrStartAsync(descriptor, _tempDir, CancellationToken.None);
        await hydrator.WaitUntilStartedAsync();
        var handleTask = supervisor.GetOrCreatePrivateChatHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAgentBindingRequest(
                ChannelKey: "private_chat",
                SystemPromptSupplement: "Reply directly.",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")])),
            CancellationToken.None);

        await Task.Delay(50);
        Assert.AreEqual(1, hydrator.CallCount);

        hydrator.Release();
        await Task.WhenAll(startTask, handleTask);

        Assert.AreEqual(1, hydrator.CallCount);
    }

    [TestMethod]
    public async Task GetOrStartAsync_CancelledWaiterDoesNotCancelSharedHydration()
    {
        var hydrator = new BlockingTaskHydrator();
        var supervisor = new NpcRuntimeSupervisor(hydrator);
        var descriptor = CreateDescriptor("haley");
        using var waiterCts = new CancellationTokenSource();

        var cancelledWaiter = supervisor.GetOrStartAsync(descriptor, _tempDir, waiterCts.Token);
        await hydrator.WaitUntilStartedAsync();
        await waiterCts.CancelAsync();

        try
        {
            await cancelledWaiter;
            Assert.Fail("Cancelled waiter should observe cancellation.");
        }
        catch (OperationCanceledException)
        {
        }
        Assert.AreEqual(1, hydrator.CallCount);

        hydrator.Release();
        await supervisor.GetOrStartAsync(descriptor, _tempDir, CancellationToken.None);

        Assert.AreEqual(1, hydrator.CallCount);
        Assert.IsTrue(supervisor.TryGetTaskView(descriptor.SessionId, out var taskView));
        Assert.IsNotNull(taskView);
        Assert.AreEqual("Hydrated after cancellation", taskView.ActiveSnapshot.Todos[0].Content);
    }

    [TestMethod]
    public async Task GetOrStartAsync_RetriesHydrationAfterFailure()
    {
        var hydrator = new FailingThenWritingTaskHydrator();
        var supervisor = new NpcRuntimeSupervisor(hydrator);
        var descriptor = CreateDescriptor("haley");

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            await supervisor.GetOrStartAsync(descriptor, _tempDir, CancellationToken.None));

        await supervisor.GetOrStartAsync(descriptor, _tempDir, CancellationToken.None);

        Assert.AreEqual(2, hydrator.CallCount);
        Assert.IsTrue(supervisor.TryGetTaskView(descriptor.SessionId, out var taskView));
        Assert.IsNotNull(taskView);
        Assert.AreEqual("Recovered after retry", taskView.ActiveSnapshot.Todos[0].Content);
    }

    [TestMethod]
    public async Task GetOrCreatePrivateChatHandleAsync_ReusesHandleUntilToolSurfaceChanges()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var pack = CreatePack("penny", "Penny");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");
        var services = new NpcRuntimeCompositionServices(
            new FakeChatClient(),
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler());

        var first = await supervisor.GetOrCreatePrivateChatHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAgentBindingRequest(
                ChannelKey: "private_chat",
                SystemPromptSupplement: "Reply directly.",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")])),
            CancellationToken.None);
        var second = await supervisor.GetOrCreatePrivateChatHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAgentBindingRequest(
                ChannelKey: "private_chat",
                SystemPromptSupplement: "Reply directly.",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")])),
            CancellationToken.None);
        var third = await supervisor.GetOrCreatePrivateChatHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAgentBindingRequest(
                ChannelKey: "private_chat",
                SystemPromptSupplement: "Reply directly.",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_b")])),
            CancellationToken.None);

        Assert.AreSame(first, second);
        Assert.AreNotSame(first, third);
        Assert.AreEqual(2, supervisor.Snapshot().Single().PrivateChatRebindGeneration);
    }

    [TestMethod]
    public async Task GetOrCreatePrivateChatHandleAsync_RebindsWhenToolSnapshotVersionChangesEvenIfFingerprintIsStable()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var pack = CreatePack("penny", "Penny");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");
        var services = new NpcRuntimeCompositionServices(
            new FakeChatClient(),
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler());

        var first = await supervisor.GetOrCreatePrivateChatHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAgentBindingRequest(
                ChannelKey: "private_chat",
                SystemPromptSupplement: "Reply directly.",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                ToolSurfaceSnapshotVersion: 1),
            CancellationToken.None);
        var second = await supervisor.GetOrCreatePrivateChatHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAgentBindingRequest(
                ChannelKey: "private_chat",
                SystemPromptSupplement: "Reply directly.",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                ToolSurfaceSnapshotVersion: 1),
            CancellationToken.None);
        var third = await supervisor.GetOrCreatePrivateChatHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAgentBindingRequest(
                ChannelKey: "private_chat",
                SystemPromptSupplement: "Reply directly.",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                ToolSurfaceSnapshotVersion: 2),
            CancellationToken.None);

        Assert.AreSame(first, second);
        Assert.AreNotSame(first, third);
        Assert.AreEqual(2, supervisor.Snapshot().Single().PrivateChatRebindGeneration);
    }

    [TestMethod]
    public async Task GetOrCreatePrivateChatHandleAsync_AgentToolUsesDelegationClientWhenSupplied()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var pack = CreatePack("penny", "Penny");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");
        var parentClient = new DelegationCapturingChatClient("parent");
        var delegationClient = new DelegationCapturingChatClient("delegation");
        var services = new NpcRuntimeCompositionServices(
            parentClient,
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            DelegationChatClient: delegationClient);
        var handle = await supervisor.GetOrCreatePrivateChatHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAgentBindingRequest(
                ChannelKey: "private_chat",
                SystemPromptSupplement: "Reply directly.",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("session_search")])),
            CancellationToken.None);

        var result = await handle.Agent.Tools["agent"].ExecuteAsync(
            new AgentParameters { AgentType = "general", Task = "Summarize nearby context." },
            CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, parentClient.StructuredStreamCalls);
        Assert.AreEqual(1, delegationClient.StructuredStreamCalls);
        Assert.AreEqual("delegation", delegationClient.Name);
        Assert.IsTrue(delegationClient.LastSystemPrompt?.Contains("helpful assistant", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task GetOrCreateHandles_PrivateChatAndAutonomyCanUseDifferentParentClients()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var pack = CreatePack("haley", "Haley");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");
        var privateChatClient = new DelegationCapturingChatClient("private_chat");
        var autonomyClient = new DelegationCapturingChatClient("autonomy");

        var privateHandle = await supervisor.GetOrCreatePrivateChatHandleAsync(
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
                    new NoopCronScheduler()),
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")])),
            CancellationToken.None);
        var autonomyHandle = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () => new FakeGameAdapter(),
                GameToolFactory: adapter => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                Services: new NpcRuntimeCompositionServices(
                    autonomyClient,
                    NullLoggerFactory.Instance,
                    _skillManager,
                    new NoopCronScheduler()),
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")])),
            CancellationToken.None);

        await privateHandle.Agent.ChatAsync(
            "reply",
            new Session { Id = $"{descriptor.SessionId}:private_chat:test" },
            CancellationToken.None);
        await autonomyHandle.AgentHandle.Agent.ChatAsync(
            "decide",
            new Session { Id = $"{descriptor.SessionId}:autonomy:test" },
            CancellationToken.None);

        Assert.AreEqual(1, privateChatClient.CompleteWithToolsCalls);
        Assert.AreEqual(0, privateChatClient.StructuredStreamCalls);
        Assert.AreEqual(0, autonomyClient.CompleteCalls);
        Assert.AreEqual(1, autonomyClient.CompleteWithToolsCalls);
        Assert.AreEqual(0, autonomyClient.StructuredStreamCalls);
    }

    [TestMethod]
    public async Task GetOrCreateAutonomyHandleAsync_ParentUsesRestrictedStardewAndSkillTools()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var pack = CreatePack("haley", "Haley");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");
        var autonomyClient = new DelegationCapturingChatClient("autonomy");
        var services = new NpcRuntimeCompositionServices(
            autonomyClient,
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            DelegationChatClient: new DelegationCapturingChatClient("delegation"));

        var handle = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () => new FakeGameAdapter(),
                GameToolFactory: (adapter, factStore) =>
                [
                    new FakeTool("stardew_status"),
                    new FakeTool("stardew_navigate_to_tile"),
                    new FakeTool("stardew_task_status"),
                    new FakeTool("stardew_speak")
                ],
                LocalExecutorGameToolFactory: (adapter, factStore) =>
                [
                    new FakeTool("stardew_navigate_to_tile"),
                    new FakeTool("stardew_task_status")
                ],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                LocalExecutorToolFingerprint: NpcToolSurface.FromTools(
                [
                    new FakeTool("stardew_navigate_to_tile"),
                    new FakeTool("stardew_task_status")
                ]).Fingerprint),
            CancellationToken.None);

        await handle.AgentHandle.Agent.ChatAsync(
            "decide",
            new Session { Id = $"{descriptor.SessionId}:autonomy:test" },
            CancellationToken.None);

        Assert.AreEqual(0, autonomyClient.CompleteCalls);
        Assert.AreEqual(1, autonomyClient.CompleteWithToolsCalls);
        CollectionAssert.Contains(autonomyClient.LastToolNames.ToArray(), "skill_view");
        CollectionAssert.Contains(autonomyClient.LastToolNames.ToArray(), "stardew_status");
        CollectionAssert.Contains(autonomyClient.LastToolNames.ToArray(), "stardew_speak");
        CollectionAssert.DoesNotContain(autonomyClient.LastToolNames.ToArray(), "mcp_tool_a");
        CollectionAssert.Contains(autonomyClient.LastToolNames.ToArray(), "stardew_navigate_to_tile");
        CollectionAssert.DoesNotContain(autonomyClient.LastToolNames.ToArray(), "agent");
        CollectionAssert.DoesNotContain(autonomyClient.LastToolNames.ToArray(), "todo");
        CollectionAssert.DoesNotContain(autonomyClient.LastToolNames.ToArray(), "memory");
    }

    [TestMethod]
    public async Task GetOrCreateAutonomyHandleAsync_JsonIntentBlocksDelegatedWriteAction()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var pack = CreatePack("haley", "Haley");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");
        var parentContract =
            """
            {
              "action": "move",
              "reason": "meet player",
              "destinationText": "beach",
              "escalate": false
            }
            """;
        var autonomyClient = new ParentIntentChatClient(parentContract);
        var delegationClient = new DelegationToolCallChatClient(
            new StreamEvent.ToolUseComplete(
                "call-skill",
                "skill_view",
                Json("""{"name":"stardew-navigation","file_path":"references/poi/beach-shoreline.md"}""")),
            new StreamEvent.ToolUseComplete(
                "call-nav",
                "stardew_navigate_to_tile",
                Json("""{"locationName":"Beach","x":32,"y":34,"source":"map-skill:stardew.navigation.poi.beach-shoreline","reason":"meet player"}""")));
        var skillTool = new RecordingTool(
            "skill_view",
            typeof(SkillViewParameters),
            ToolResult.Ok("""{"success":true,"content":"`target(locationName=Beach,x=32,y=34,source=map-skill:stardew.navigation.poi.beach-shoreline)`"}"""));
        var navigateTool = new RecordingTool(
            "stardew_navigate_to_tile",
            typeof(NavigateToTileParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-nav-1","status":"queued"}"""));
        var services = new NpcRuntimeCompositionServices(
            autonomyClient,
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            DelegationChatClient: delegationClient);

        var handle = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () => new FakeGameAdapter(),
                GameToolFactory: (adapter, factStore) =>
                [
                    new FakeTool("stardew_status"),
                    new FakeTool("stardew_navigate_to_tile")
                ],
                LocalExecutorGameToolFactory: (adapter, factStore) => [navigateTool],
                LocalExecutorRuntimeToolFactory: services => [skillTool],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                LocalExecutorToolFingerprint: NpcToolSurface.FromTools([skillTool, navigateTool]).Fingerprint),
            CancellationToken.None);

        var result = await handle.Loop.RunDelegatedIntentAsync(
            handle.Instance,
            "trace-delegated-json",
            parentContract,
            CancellationToken.None);

        Assert.AreEqual("local_executor_blocked:local_executor_write_action_disabled", result.DecisionResponse);
        Assert.AreEqual(0, autonomyClient.CompleteCalls);
        Assert.AreEqual(0, autonomyClient.CompleteWithToolsCalls);
        CollectionAssert.DoesNotContain(autonomyClient.LastToolNames.ToArray(), "stardew_move");
        Assert.AreEqual(0, delegationClient.StructuredStreamCalls);
        CollectionAssert.DoesNotContain(delegationClient.LastToolNames.ToArray(), "stardew_navigate_to_tile");
        Assert.IsFalse(delegationClient.ToolNamesByCall.Any(names => names.Contains("skill_view")));
        Assert.AreEqual(0, navigateTool.ExecuteCalls);
    }

    [TestMethod]
    public async Task GetOrCreateAutonomyHandleAsync_MoveToolResultStaysInParentTranscriptWithoutDelegation()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var pack = CreatePack("haley", "Haley");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");
        var autonomyClient = new ToolCallingParentChatClient(
            new ToolCall
            {
                Id = "call-skill",
                Name = "skill_view",
                Arguments = """{"name":"stardew-navigation","file_path":"references/poi/beach-shoreline.md"}"""
            },
            new ToolCall
            {
                Id = "call-nav",
                Name = "stardew_navigate_to_tile",
                Arguments = """{"locationName":"Beach","x":32,"y":34,"source":"map-skill:stardew.navigation.poi.beach-shoreline","reason":"meet player"}"""
            });
        var delegationClient = new DelegationToolCallChatClient(
            new StreamEvent.ToolUseComplete(
                "unexpected",
                "stardew_navigate_to_tile",
                Json("""{"locationName":"Town","x":1,"y":1,"source":"unexpected"}""")));
        var skillTool = new RecordingTool(
            "skill_view",
            typeof(SkillViewParameters),
            ToolResult.Ok("""{"success":true,"content":"`target(locationName=Beach,x=32,y=34,source=map-skill:stardew.navigation.poi.beach-shoreline)`"}"""));
        var navigateTool = new RecordingTool(
            "stardew_navigate_to_tile",
            typeof(NavigateToTileParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-nav-1","status":"queued"}"""));
        var services = new NpcRuntimeCompositionServices(
            autonomyClient,
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            DelegationChatClient: delegationClient);

        var handle = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 3,
                AdapterFactory: () => new FakeGameAdapter(),
                GameToolFactory: (adapter, factStore) => [skillTool, navigateTool],
                LocalExecutorGameToolFactory: (adapter, factStore) => [new FakeTool("stardew_navigate_to_tile")],
                LocalExecutorRuntimeToolFactory: services => [new FakeTool("skill_view")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                LocalExecutorToolFingerprint: NpcToolSurface.FromTools([new FakeTool("skill_view"), new FakeTool("stardew_navigate_to_tile")]).Fingerprint),
            CancellationToken.None);

        var result = await handle.Loop.RunOneTickAsync(
            descriptor,
            new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley wants to meet the player at the beach.",
                ["location=Town"]),
            new GameEventBatch([], new GameEventCursor()),
            CancellationToken.None);

        Assert.AreEqual("move submitted", result.DecisionResponse);
        Assert.AreEqual(3, autonomyClient.CompleteWithToolsCalls);
        Assert.IsTrue(autonomyClient.ToolNamesByCall.Any(names => names.Contains("skill_view")));
        Assert.IsTrue(autonomyClient.ToolNamesByCall.Any(names => names.Contains("stardew_navigate_to_tile")));
        Assert.AreEqual(0, delegationClient.StructuredStreamCalls);
        Assert.AreEqual(1, skillTool.ExecuteCalls);
        Assert.AreEqual(1, navigateTool.ExecuteCalls);
        CollectionAssert.Contains(
            autonomyClient.ToolResultNames.ToArray(),
            "stardew_navigate_to_tile",
            "The navigation tool result must be appended to the parent autonomy transcript.");
    }

    [TestMethod]
    public async Task GetOrCreateAutonomyHandleAsync_WhenDelegationClientMissing_BlocksLocalExecutorWithoutParentFallback()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var pack = CreatePack("haley", "Haley");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");
        var autonomyClient = new ParentIntentChatClient(
            """
            {
              "action": "move",
              "reason": "meet player",
              "destinationText": "beach",
              "escalate": false
            }
            """);
        var navigateTool = new RecordingTool(
            "stardew_navigate_to_tile",
            typeof(NavigateToTileParameters),
            ToolResult.Ok("""{"accepted":true,"commandId":"cmd-move-1","status":"queued"}"""));
        var services = new NpcRuntimeCompositionServices(
            autonomyClient,
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler());

        var handle = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () => new FakeGameAdapter(),
                GameToolFactory: (adapter, factStore) =>
                [
                    new FakeTool("stardew_status"),
                    new FakeTool("stardew_navigate_to_tile")
                ],
                LocalExecutorGameToolFactory: (adapter, factStore) => [navigateTool],
                LocalExecutorRuntimeToolFactory: services => [new RecordingTool("skill_view", typeof(SkillViewParameters), ToolResult.Ok("{}"))],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([]),
                LocalExecutorToolFingerprint: NpcToolSurface.FromTools([new FakeTool("skill_view"), navigateTool]).Fingerprint),
            CancellationToken.None);

        var result = await handle.Loop.RunOneTickAsync(
            descriptor,
            new GameObservation(
                "haley",
                "stardew-valley",
                DateTime.UtcNow,
                "Haley can move to Pierre.",
                ["location=Town"]),
            new GameEventBatch([], new GameEventCursor()),
            CancellationToken.None);

        Assert.AreEqual("local_executor_blocked:local_executor_write_action_disabled", result.DecisionResponse);
        CollectionAssert.DoesNotContain(autonomyClient.LastToolNames.ToArray(), "stardew_move");
        Assert.AreEqual(0, navigateTool.ExecuteCalls);
    }

    [TestMethod]
    public async Task GetOrCreateAutonomyHandleAsync_RebindsWhenLocalExecutorToolSurfaceChanges()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var pack = CreatePack("haley", "Haley");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");
        var services = new NpcRuntimeCompositionServices(
            new FakeChatClient(),
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler(),
            DelegationChatClient: new FakeChatClient());
        var adapterFactoryCalls = 0;

        var first = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () =>
                {
                    adapterFactoryCalls++;
                    return new FakeGameAdapter();
                },
                GameToolFactory: (adapter, factStore) => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                LocalExecutorGameToolFactory: (adapter, factStore) => [new FakeTool("stardew_move")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                LocalExecutorToolFingerprint: NpcToolSurface.FromTools([new FakeTool("stardew_move")]).Fingerprint),
            CancellationToken.None);
        var second = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () =>
                {
                    adapterFactoryCalls++;
                    return new FakeGameAdapter();
                },
                GameToolFactory: (adapter, factStore) => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                LocalExecutorGameToolFactory: (adapter, factStore) => [new FakeTool("stardew_move")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                LocalExecutorToolFingerprint: NpcToolSurface.FromTools([new FakeTool("stardew_move")]).Fingerprint),
            CancellationToken.None);
        var third = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () =>
                {
                    adapterFactoryCalls++;
                    return new FakeGameAdapter();
                },
                GameToolFactory: (adapter, factStore) => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                LocalExecutorGameToolFactory: (adapter, factStore) =>
                [
                    new FakeTool("stardew_move"),
                    new FakeTool("stardew_task_status")
                ],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                LocalExecutorToolFingerprint: NpcToolSurface.FromTools(
                [
                    new FakeTool("stardew_move"),
                    new FakeTool("stardew_task_status")
                ]).Fingerprint),
            CancellationToken.None);

        Assert.AreSame(first, second);
        Assert.AreNotSame(first, third);
        Assert.AreEqual(2, adapterFactoryCalls);
        Assert.AreEqual(2, supervisor.Snapshot().Single().AutonomyRebindGeneration);
    }

    [TestMethod]
    public async Task TryGetTaskView_AfterPrivateChatHandle_ReturnsReadOnlyLongTermTaskSnapshot()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var pack = CreatePack("haley", "Haley");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");
        var services = new NpcRuntimeCompositionServices(
            new FakeChatClient(),
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler());
        var handle = await supervisor.GetOrCreatePrivateChatHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAgentBindingRequest(
                ChannelKey: "private_chat",
                SystemPromptSupplement: "Reply directly.",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")])),
            CancellationToken.None);
        handle.Context.TodoStore.Write(
            descriptor.SessionId,
            [
                new SessionTodoInput("1", "Meet player at the beach", "pending"),
                new SessionTodoInput("2", "Reach the pier", "blocked", "festival_active")
            ]);

        var found = supervisor.TryGetTaskView(descriptor.SessionId, out var taskView);

        Assert.IsTrue(found);
        Assert.IsNotNull(taskView);
        Assert.AreEqual(descriptor.SessionId, taskView.SessionId);
        Assert.AreEqual(2, taskView.ActiveSnapshot.Todos.Count);
        Assert.AreEqual(1, taskView.ActiveSnapshot.Summary.Pending);
        Assert.AreEqual(1, taskView.ActiveSnapshot.Summary.Blocked);
        Assert.AreEqual("festival_active", taskView.ActiveSnapshot.Todos[1].Reason);
    }

    [TestMethod]
    public async Task TryGetTaskView_WithMultipleInstances_ReturnsMatchingNpcSnapshot()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var haleyPack = CreatePack("haley", "Haley");
        var pennyPack = CreatePack("penny", "Penny");
        var haleyDescriptor = NpcRuntimeDescriptorFactory.Create(haleyPack, "save-1");
        var pennyDescriptor = NpcRuntimeDescriptorFactory.Create(pennyPack, "save-1");
        var services = new NpcRuntimeCompositionServices(
            new FakeChatClient(),
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler());
        await supervisor.GetOrCreatePrivateChatHandleAsync(
            haleyDescriptor,
            haleyPack,
            _tempDir,
            new NpcRuntimeAgentBindingRequest(
                ChannelKey: "private_chat",
                SystemPromptSupplement: "Reply directly.",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")])),
            CancellationToken.None);
        var pennyHandle = await supervisor.GetOrCreatePrivateChatHandleAsync(
            pennyDescriptor,
            pennyPack,
            _tempDir,
            new NpcRuntimeAgentBindingRequest(
                ChannelKey: "private_chat",
                SystemPromptSupplement: "Reply directly.",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")])),
            CancellationToken.None);
        pennyHandle.Context.TodoStore.Write(
            pennyDescriptor.SessionId,
            [new SessionTodoInput("1", "Penny task", "pending")]);

        var found = supervisor.TryGetTaskView(pennyDescriptor.SessionId, out var taskView);

        Assert.IsTrue(found);
        Assert.IsNotNull(taskView);
        Assert.AreEqual("Penny task", taskView.ActiveSnapshot.Todos.Single().Content);
    }

    [TestMethod]
    public async Task GetOrCreateAutonomyHandleAsync_AfterPrivateChatTodo_SharesLongTermTaskStore()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var pack = CreatePack("haley", "Haley");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");
        var services = new NpcRuntimeCompositionServices(
            new FakeChatClient(),
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler());
        var privateChatHandle = await supervisor.GetOrCreatePrivateChatHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAgentBindingRequest(
                ChannelKey: "private_chat",
                SystemPromptSupplement: "Reply directly.",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")])),
            CancellationToken.None);
        privateChatHandle.Context.TodoStore.Write(
            descriptor.SessionId,
            [new SessionTodoInput("1", "Meet player at the beach", "pending")]);

        var autonomyHandle = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () => new FakeGameAdapter(),
                GameToolFactory: adapter => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")])),
            CancellationToken.None);

        var autonomySnapshot = autonomyHandle.AgentHandle.Context.TodoStore.Read(descriptor.SessionId);
        Assert.AreEqual(1, autonomySnapshot.Todos.Count);
        Assert.AreEqual("Meet player at the beach", autonomySnapshot.Todos[0].Content);
    }

    [TestMethod]
    public async Task RunOneTickAsync_AfterPrivateChatTodo_InjectsLongTermActiveTaskIntoAutonomyPrompt()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var pack = CreatePack("haley", "Haley");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");
        var chatClient = new ActiveTaskCapturingChatClient("Meet player at the beach");
        var services = new NpcRuntimeCompositionServices(
            chatClient,
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler());
        var privateChatHandle = await supervisor.GetOrCreatePrivateChatHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAgentBindingRequest(
                ChannelKey: "private_chat",
                SystemPromptSupplement: "Reply directly.",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")])),
            CancellationToken.None);
        privateChatHandle.Context.TodoStore.Write(
            descriptor.SessionId,
            [new SessionTodoInput("1", "Meet player at the beach", "pending")]);
        var autonomyHandle = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () => new FakeGameAdapter(),
                GameToolFactory: adapter => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")])),
            CancellationToken.None);

        await autonomyHandle.Loop.RunOneTickAsync(autonomyHandle.Instance, new GameEventCursor(), CancellationToken.None);

        Assert.IsTrue(
            chatClient.SawExpectedActiveTask,
            "Autonomy prompt must inject the NPC long-term active todo, not only share the backing store.");
    }

    [TestMethod]
    public async Task GetOrCreateAutonomyHandleAsync_ReusesLoopUntilAdapterKeyChanges()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var pack = CreatePack("haley", "Haley");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");
        var services = new NpcRuntimeCompositionServices(
            new FakeChatClient(),
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler());
        var adapterFactoryCalls = 0;

        var first = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () =>
                {
                    adapterFactoryCalls++;
                    return new FakeGameAdapter();
                },
                GameToolFactory: adapter => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")])),
            CancellationToken.None);
        var second = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () =>
                {
                    adapterFactoryCalls++;
                    return new FakeGameAdapter();
                },
                GameToolFactory: adapter => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")])),
            CancellationToken.None);
        var third = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-b",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () =>
                {
                    adapterFactoryCalls++;
                    return new FakeGameAdapter();
                },
                GameToolFactory: adapter => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")])),
            CancellationToken.None);

        Assert.AreSame(first, second);
        Assert.AreNotSame(first, third);
        Assert.AreEqual(2, adapterFactoryCalls);
        Assert.AreEqual(2, supervisor.Snapshot().Single().AutonomyRebindGeneration);
    }

    [TestMethod]
    public async Task GetOrCreateAutonomyHandleAsync_RebindsWhenToolSnapshotVersionChangesEvenIfAdapterKeyIsStable()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var pack = CreatePack("haley", "Haley");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");
        var services = new NpcRuntimeCompositionServices(
            new FakeChatClient(),
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler());
        var adapterFactoryCalls = 0;

        var first = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () =>
                {
                    adapterFactoryCalls++;
                    return new FakeGameAdapter();
                },
                GameToolFactory: adapter => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                ToolSurfaceSnapshotVersion: 1),
            CancellationToken.None);
        var second = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () =>
                {
                    adapterFactoryCalls++;
                    return new FakeGameAdapter();
                },
                GameToolFactory: adapter => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                ToolSurfaceSnapshotVersion: 1),
            CancellationToken.None);
        var third = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () =>
                {
                    adapterFactoryCalls++;
                    return new FakeGameAdapter();
                },
                GameToolFactory: adapter => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                ToolSurfaceSnapshotVersion: 2),
            CancellationToken.None);

        Assert.AreSame(first, second);
        Assert.AreNotSame(first, third);
        Assert.AreEqual(2, adapterFactoryCalls);
        Assert.AreEqual(2, supervisor.Snapshot().Single().AutonomyRebindGeneration);
    }

    [TestMethod]
    public async Task GetOrCreateAutonomyHandleAsync_RebindsWhenSystemPromptSupplementChanges()
    {
        var supervisor = new NpcRuntimeSupervisor();
        var pack = CreatePack("haley", "Haley");
        var descriptor = NpcRuntimeDescriptorFactory.Create(pack, "save-1");
        var services = new NpcRuntimeCompositionServices(
            new FakeChatClient(),
            NullLoggerFactory.Instance,
            _skillManager,
            new NoopCronScheduler());
        var adapterFactoryCalls = 0;

        var first = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                SystemPromptSupplement: "Persona facts v1",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () =>
                {
                    adapterFactoryCalls++;
                    return new FakeGameAdapter();
                },
                GameToolFactory: adapter => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                ToolSurfaceSnapshotVersion: 1),
            CancellationToken.None);
        var second = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                SystemPromptSupplement: "Persona facts v1",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () =>
                {
                    adapterFactoryCalls++;
                    return new FakeGameAdapter();
                },
                GameToolFactory: adapter => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                ToolSurfaceSnapshotVersion: 1),
            CancellationToken.None);
        var third = await supervisor.GetOrCreateAutonomyHandleAsync(
            descriptor,
            pack,
            _tempDir,
            new NpcRuntimeAutonomyBindingRequest(
                ChannelKey: "autonomy",
                AdapterKey: "bridge-a",
                SystemPromptSupplement: "Persona facts v2",
                IncludeMemory: true,
                IncludeUser: true,
                MaxToolIterations: 2,
                AdapterFactory: () =>
                {
                    adapterFactoryCalls++;
                    return new FakeGameAdapter();
                },
                GameToolFactory: adapter => [new FakeTool("stardew_status"), new FakeTool("stardew_move")],
                Services: services,
                ToolSurface: NpcToolSurface.FromTools([new FakeTool("mcp_tool_a")]),
                ToolSurfaceSnapshotVersion: 1),
            CancellationToken.None);

        Assert.AreSame(first, second);
        Assert.AreNotSame(first, third);
        Assert.AreEqual(2, adapterFactoryCalls);
        Assert.AreEqual(2, supervisor.Snapshot().Single().AutonomyRebindGeneration);
    }

    private static NpcRuntimeDescriptor CreateDescriptor(string npcId)
        => new(
            npcId,
            npcId,
            "stardew-valley",
            "save-1",
            "default",
            "stardew",
            "pack-root",
            $"sdv_save-1_{npcId}_default");

    private SkillManager CreateSkillManager()
    {
        var skillsDir = Path.Combine(_tempDir, "skills", "memory");
        Directory.CreateDirectory(skillsDir);
        File.WriteAllText(
            Path.Combine(skillsDir, "SKILL.md"),
            """
            ---
            name: supervisor-test-skill
            description: Test skill.
            ---
            Use memory carefully.
            """);
        return new SkillManager(Path.Combine(_tempDir, "skills"), NullLogger<SkillManager>.Instance);
    }

    private NpcPack CreatePack(string npcId, string displayName)
    {
        var root = Path.Combine(_packRoot, npcId, "default");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "SOUL.md"), $"# {displayName}\n\n{npcId}-pack-soul");
        File.WriteAllText(Path.Combine(root, "facts.md"), $"{displayName} facts");
        File.WriteAllText(Path.Combine(root, "voice.md"), $"{displayName} voice");
        File.WriteAllText(Path.Combine(root, "boundaries.md"), $"{displayName} boundaries");
        File.WriteAllText(Path.Combine(root, "skills.json"), """{"required":[],"optional":[]}""");

        return new NpcPack(
            new NpcPackManifest
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
            },
            root);
    }

    private sealed class FakeChatClient : IChatClient
    {
        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("ok");

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

    private sealed class ActiveTaskCapturingChatClient(string expectedTask) : IChatClient
    {
        public bool SawExpectedActiveTask { get; private set; }

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
        {
            CaptureActiveTask(messages);
            return Task.FromResult(
                """
                {
                  "action": "wait",
                  "reason": "continue later",
                  "waitReason": "waiting",
                  "escalate": false
                }
                """);
        }

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            CaptureActiveTask(messages);

            return Task.FromResult(new ChatResponse { Content = "I will continue the task.", FinishReason = "stop" });
        }

        private void CaptureActiveTask(IEnumerable<Message> messages)
        {
            SawExpectedActiveTask = messages.Any(message =>
                string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase) &&
                message.Content.Contains("active task list", StringComparison.OrdinalIgnoreCase) &&
                message.Content.Contains(expectedTask, StringComparison.Ordinal));
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

    private sealed class DelegationCapturingChatClient(string name) : IChatClient
    {
        public string Name { get; } = name;
        public int CompleteCalls { get; private set; }
        public int CompleteWithToolsCalls { get; private set; }
        public int StructuredStreamCalls { get; private set; }
        public string? LastSystemPrompt { get; private set; }
        public List<string> LastToolNames { get; } = new();

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
        {
            CompleteCalls++;
            LastToolNames.Clear();
            return Task.FromResult("ok");
        }

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            CompleteWithToolsCalls++;
            LastToolNames.Clear();
            LastToolNames.AddRange(tools.Select(tool => tool.Name));
            return Task.FromResult(new ChatResponse { Content = $"{Name}: ok", FinishReason = "stop" });
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
            StructuredStreamCalls++;
            LastSystemPrompt = systemPrompt;
            yield return new StreamEvent.TokenDelta($"{Name}: done");
            yield return new StreamEvent.MessageComplete("stop", new UsageStats(1, 1));
        }
    }

    private sealed class ParentIntentChatClient(string response) : IChatClient
    {
        public int CompleteCalls { get; private set; }
        public int CompleteWithToolsCalls { get; private set; }
        public List<string> LastToolNames { get; } = new();

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
        {
            CompleteCalls++;
            LastToolNames.Clear();
            return Task.FromResult(response);
        }

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            CompleteWithToolsCalls++;
            LastToolNames.Clear();
            LastToolNames.AddRange(tools.Select(tool => tool.Name));
            return Task.FromResult(new ChatResponse { Content = response, FinishReason = "stop" });
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

    private sealed class DelegationToolCallChatClient(params StreamEvent.ToolUseComplete[] toolUses) : IChatClient
    {
        public int StructuredStreamCalls { get; private set; }
        public List<string> LastToolNames { get; } = new();
        public List<IReadOnlyList<string>> ToolNamesByCall { get; } = [];

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("unused");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
            => Task.FromResult(new ChatResponse { Content = "unused", FinishReason = "stop" });

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
            StructuredStreamCalls++;
            LastToolNames.Clear();
            LastToolNames.AddRange((tools ?? []).Select(tool => tool.Name));
            ToolNamesByCall.Add(LastToolNames.ToArray());
            await Task.Yield();
            yield return toolUses[Math.Min(StructuredStreamCalls - 1, toolUses.Length - 1)];
            yield return new StreamEvent.MessageComplete("stop");
        }
    }

    private sealed class ToolCallingParentChatClient(params ToolCall[] toolCalls) : IChatClient
    {
        private int _completeWithToolsCalls;

        public int CompleteWithToolsCalls => _completeWithToolsCalls;
        public List<IReadOnlyList<string>> ToolNamesByCall { get; } = [];
        public List<string> ToolResultNames { get; } = [];

        public Task<string> CompleteAsync(IEnumerable<Message> messages, CancellationToken ct)
            => Task.FromResult("unused");

        public Task<ChatResponse> CompleteWithToolsAsync(
            IEnumerable<Message> messages,
            IEnumerable<ToolDefinition> tools,
            CancellationToken ct)
        {
            _completeWithToolsCalls++;
            ToolNamesByCall.Add(tools.Select(tool => tool.Name).ToArray());
            ToolResultNames.AddRange(messages
                .Where(message => string.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase))
                .Select(message => message.ToolName ?? ""));

            if (_completeWithToolsCalls <= toolCalls.Length)
            {
                return Task.FromResult(new ChatResponse
                {
                    FinishReason = "tool_calls",
                    ToolCalls = [toolCalls[_completeWithToolsCalls - 1]]
                });
            }

            return Task.FromResult(new ChatResponse { Content = "move submitted", FinishReason = "stop" });
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

    private sealed class FakeTool(string name) : ITool
    {
        public string Name { get; } = name;
        public string Description => "test tool";
        public Type ParametersType => typeof(NoopParameters);
        public Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
            => Task.FromResult(ToolResult.Ok("ok"));
    }

    private sealed class RecordingTool : ITool, IToolSchemaProvider
    {
        private readonly ToolResult _result;

        public RecordingTool(string name, Type parametersType, ToolResult result)
        {
            Name = name;
            ParametersType = parametersType;
            _result = result;
        }

        public string Name { get; }
        public string Description => "test recording tool";
        public Type ParametersType { get; }
        public int ExecuteCalls { get; private set; }

        public JsonElement GetParameterSchema()
            => JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new
                {
                    destination = new { type = "string" },
                    reason = new { type = "string" }
                },
                required = new[] { "destination" }
            });

        public Task<ToolResult> ExecuteAsync(object parameters, CancellationToken ct)
        {
            ExecuteCalls++;
            return Task.FromResult(_result);
        }
    }

    private sealed class NoopParameters
    {
    }

    private sealed class MoveParameters
    {
        public required string Destination { get; init; }
        public string? Reason { get; init; }
    }

    private sealed class NavigateToTileParameters
    {
        public required string LocationName { get; init; }
        public required int X { get; init; }
        public required int Y { get; init; }
        public string? Reason { get; init; }
    }

    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private sealed class WritingTaskHydrator(string content) : INpcRuntimeTaskHydrator
    {
        public int CallCount { get; private set; }

        public Task HydrateAsync(NpcRuntimeInstance instance, CancellationToken ct)
        {
            CallCount++;
            instance.TodoStore.Write(instance.Descriptor.SessionId, [new SessionTodoInput("1", content, "pending")]);
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingTaskHydrator : INpcRuntimeTaskHydrator
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        public async Task HydrateAsync(NpcRuntimeInstance instance, CancellationToken ct)
        {
            CallCount++;
            _started.TrySetResult();
            await _release.Task;
            instance.TodoStore.Write(
                instance.Descriptor.SessionId,
                [new SessionTodoInput("1", "Hydrated after cancellation", "pending")]);
        }

        public Task WaitUntilStartedAsync() => _started.Task;

        public void Release() => _release.TrySetResult();
    }

    private sealed class FailingThenWritingTaskHydrator : INpcRuntimeTaskHydrator
    {
        public int CallCount { get; private set; }

        public Task HydrateAsync(NpcRuntimeInstance instance, CancellationToken ct)
        {
            CallCount++;
            if (CallCount == 1)
                throw new InvalidOperationException("first hydration fails");

            instance.TodoStore.Write(
                instance.Descriptor.SessionId,
                [new SessionTodoInput("1", "Recovered after retry", "pending")]);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGameAdapter : IGameAdapter
    {
        public string AdapterId => "stardew";
        public IGameCommandService Commands { get; } = new FakeCommandService();
        public IGameQueryService Queries { get; } = new FakeQueryService();
        public IGameEventSource Events { get; } = new FakeEventSource();
    }

    private sealed class FakeCommandService : IGameCommandService
    {
        public Task<GameCommandResult> SubmitAsync(GameAction action, CancellationToken ct)
            => Task.FromResult(new GameCommandResult(true, "cmd-1", "completed", null, action.TraceId));

        public Task<GameCommandStatus> GetStatusAsync(string commandId, CancellationToken ct)
            => Task.FromResult(new GameCommandStatus(commandId, "Haley", "autonomy", "completed", 1, null, null));

        public Task<GameCommandStatus> CancelAsync(string commandId, string reason, CancellationToken ct)
            => Task.FromResult(new GameCommandStatus(commandId, "Haley", "autonomy", "cancelled", 1, reason, null));
    }

    private sealed class FakeQueryService : IGameQueryService
    {
        public Task<GameObservation> ObserveAsync(string npcId, CancellationToken ct)
            => Task.FromResult(new GameObservation(npcId, "stardew-valley", DateTime.UtcNow, "standing still", ["location=Town"]));

        public Task<WorldSnapshot> GetWorldSnapshotAsync(string npcId, CancellationToken ct)
            => Task.FromResult(new WorldSnapshot("stardew-valley", "save-1", DateTime.UtcNow, [], []));
    }

    private sealed class FakeEventSource : IGameEventSource
    {
        public Task<IReadOnlyList<GameEventRecord>> PollAsync(GameEventCursor cursor, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<GameEventRecord>>(Array.Empty<GameEventRecord>());
    }
}
