using System.Text.Json;
using System.Text.Json.Nodes;
using Hermes.Agent.Core;
using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Hermes.Agent.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Stardew;

[TestClass]
public class StardewNpcToolFactoryTests
{
    [TestMethod]
    public void StardewNpcToolSurfacePolicy_DefaultParentSurfaceMatchesCurrentTools()
    {
        var tools = StardewNpcToolFactory.CreateDefault(
            new FakeGameAdapter(new CapturingCommandService(), new FakeQueryService(), new FakeEventSource()),
            CreateDescriptor("haley"));

        CollectionAssert.AreEqual(
            StardewNpcToolSurfacePolicy.Default.ParentToolNames.ToArray(),
            tools.Select(tool => tool.Name).ToArray());
    }

    [TestMethod]
    public void StardewNpcToolSurfacePolicy_DefaultLocalExecutorSurfaceMatchesCurrentTools()
    {
        var tools = StardewNpcToolFactory.CreateLocalExecutorTools(
            new FakeGameAdapter(new CapturingCommandService(), new FakeQueryService(), new FakeEventSource()),
            CreateDescriptor("haley"));

        CollectionAssert.AreEqual(
            StardewNpcToolSurfacePolicy.Default.LocalExecutorToolNames.ToArray(),
            tools.Select(tool => tool.Name).ToArray());
        CollectionAssert.DoesNotContain(StardewNpcToolSurfacePolicy.Default.LocalExecutorToolNames.ToArray(), "stardew_navigate_to_tile");
        CollectionAssert.DoesNotContain(StardewNpcToolSurfacePolicy.Default.LocalExecutorToolNames.ToArray(), "stardew_idle_micro_action");
        CollectionAssert.DoesNotContain(StardewNpcToolSurfacePolicy.Default.LocalExecutorToolNames.ToArray(), "stardew_speak");
        CollectionAssert.DoesNotContain(StardewNpcToolSurfacePolicy.Default.LocalExecutorToolNames.ToArray(), "stardew_open_private_chat");
    }

    [TestMethod]
    public void StardewNpcToolSurfacePolicy_OverrideControlsExecutorSurface()
    {
        var policy = StardewNpcToolSurfacePolicy.Create(
            parentToolNames: ["stardew_status"],
            localExecutorToolNames: ["stardew_status", "stardew_task_status"]);

        var parentTools = StardewNpcToolFactory.CreateDefault(
            new FakeGameAdapter(new CapturingCommandService(), new FakeQueryService(), new FakeEventSource()),
            CreateDescriptor("haley"),
            toolSurfacePolicy: policy);
        var executorTools = StardewNpcToolFactory.CreateLocalExecutorTools(
            new FakeGameAdapter(new CapturingCommandService(), new FakeQueryService(), new FakeEventSource()),
            CreateDescriptor("haley"),
            toolSurfacePolicy: policy);

        CollectionAssert.AreEqual(new[] { "stardew_status" }, parentTools.Select(tool => tool.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "stardew_status", "stardew_task_status" }, executorTools.Select(tool => tool.Name).ToArray());
        Assert.AreEqual(
            StardewNpcToolFactory.LocalExecutorToolFingerprint(),
            StardewNpcToolFactory.LocalExecutorToolFingerprint(policy));
    }

    [TestMethod]
    public void CreateDefault_ReturnsOnlyNpcSafeStardewTools()
    {
        var tools = StardewNpcToolFactory.CreateDefault(
            new FakeGameAdapter(new CapturingCommandService(), new FakeQueryService(), new FakeEventSource()),
            CreateDescriptor("haley"));

        CollectionAssert.AreEqual(
            new[]
            {
                "stardew_status",
                "stardew_player_status",
                "stardew_progress_status",
                "stardew_social_status",
                "stardew_quest_status",
                "stardew_farm_status",
                "stardew_recent_activity",
                "stardew_navigate_to_tile",
                "stardew_speak",
                "stardew_open_private_chat",
                "stardew_idle_micro_action",
                "stardew_task_status"
            },
            tools.Select(tool => tool.Name).ToArray());

        Assert.IsFalse(tools.Any(tool => tool.Name is "agent" or "todo" or "memory" or "ask_user" or "schedule_cron"));

        CollectionAssert.DoesNotContain(tools.Select(tool => tool.Name).ToArray(), "stardew_move");
    }

    [TestMethod]
    public void CreateLocalExecutorTools_ContainsOnlyAllowedMechanicalReadOnlyTools()
    {
        var tools = StardewNpcToolFactory.CreateLocalExecutorTools(
            new FakeGameAdapter(new CapturingCommandService(), new FakeQueryService(), new FakeEventSource()),
            CreateDescriptor("haley"));

        CollectionAssert.AreEqual(
            new[]
            {
                "stardew_status",
                "stardew_task_status"
            },
            tools.Select(tool => tool.Name).ToArray());
        CollectionAssert.DoesNotContain(tools.Select(tool => tool.Name).ToArray(), "stardew_navigate_to_tile");
        CollectionAssert.DoesNotContain(tools.Select(tool => tool.Name).ToArray(), "stardew_idle_micro_action");
        CollectionAssert.DoesNotContain(tools.Select(tool => tool.Name).ToArray(), "stardew_speak");
        CollectionAssert.DoesNotContain(tools.Select(tool => tool.Name).ToArray(), "stardew_open_private_chat");
    }

    [TestMethod]
    public void CreateDefault_ExposesIdleMicroActionToolForParentLifecycle()
    {
        var tools = StardewNpcToolFactory.CreateDefault(
            new FakeGameAdapter(new CapturingCommandService(), new FakeQueryService(), new FakeEventSource()),
            CreateDescriptor("haley"));

        CollectionAssert.Contains(tools.Select(tool => tool.Name).ToArray(), "stardew_idle_micro_action");
    }

    [TestMethod]
    public void CreateDefault_ExposesNavigateToTileForParentToolFeedback()
    {
        var tools = StardewNpcToolFactory.CreateDefault(
            new FakeGameAdapter(new CapturingCommandService(), new FakeQueryService(), new FakeEventSource()),
            CreateDescriptor("haley"));

        CollectionAssert.Contains(tools.Select(tool => tool.Name).ToArray(), "stardew_navigate_to_tile");
    }

    [TestMethod]
    public async Task NavigateToTileTool_BindsRuntimeIdentityAndSubmitsTargetMove()
    {
        var commands = new CapturingCommandService();
        var tools = StardewNpcToolFactory.CreateDefault(
            new FakeGameAdapter(commands, new FakeQueryService(), new FakeEventSource()),
            CreateDescriptor("haley"),
            traceIdFactory: () => "trace-nav",
            idempotencyKeyFactory: () => "idem-nav");
        var navigateTool = tools.Single(tool => tool.Name == "stardew_navigate_to_tile");

        var result = await navigateTool.ExecuteAsync(new StardewNavigateToTileToolParameters
        {
            LocationName = "Beach",
            X = 20,
            Y = 35,
            FacingDirection = 2,
            Reason = "go to the beach",
            Thought = "海风应该很舒服。"
        }, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(commands.LastAction);
        Assert.AreEqual("haley", commands.LastAction.NpcId);
        Assert.AreEqual("Haley", commands.LastAction.BodyBinding?.TargetEntityId);
        Assert.AreEqual(GameActionType.Move, commands.LastAction.Type);
        Assert.AreEqual("trace-nav", commands.LastAction.TraceId);
        Assert.AreEqual("idem-nav", commands.LastAction.IdempotencyKey);
        Assert.AreEqual("tile", commands.LastAction.Target.Kind);
        Assert.AreEqual("Beach", commands.LastAction.Target.LocationName);
        Assert.IsNotNull(commands.LastAction.Target.Tile);
        Assert.AreEqual(20, commands.LastAction.Target.Tile.X);
        Assert.AreEqual(35, commands.LastAction.Target.Tile.Y);
        Assert.AreEqual(2, (int?)commands.LastAction.Payload?["facingDirection"]);
        Assert.AreEqual("海风应该很舒服。", commands.LastAction.Payload?["thought"]?.ToString());
        Assert.IsFalse(commands.LastAction.Payload?.ContainsKey("destinationId") is true);
    }

    [TestMethod]
    public async Task IdleMicroActionTool_BindsRuntimeIdentityAndSubmitsParentLifecycleAction()
    {
        var commands = new CapturingCommandService();
        var tools = StardewNpcToolFactory.CreateDefault(
            new FakeGameAdapter(commands, new FakeQueryService(), new FakeEventSource()),
            CreateDescriptor("haley"),
            traceIdFactory: () => "trace-idle",
            idempotencyKeyFactory: () => "idem-idle");
        var idleTool = tools.Single(tool => tool.Name == "stardew_idle_micro_action");

        var result = await idleTool.ExecuteAsync(new StardewIdleMicroActionToolParameters
        {
            Kind = "look_around",
            Intensity = "light",
            TtlSeconds = 4
        }, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(commands.LastAction);
        Assert.AreEqual("haley", commands.LastAction.NpcId);
        Assert.AreEqual("Haley", commands.LastAction.BodyBinding?.TargetEntityId);
        Assert.AreEqual(GameActionType.IdleMicroAction, commands.LastAction.Type);
        Assert.AreEqual("trace-idle", commands.LastAction.TraceId);
        Assert.AreEqual("idem-idle", commands.LastAction.IdempotencyKey);
        Assert.AreEqual("self", commands.LastAction.Target.Kind);
        Assert.AreEqual("look_around", commands.LastAction.Payload?["kind"]?.ToString());
        Assert.AreEqual("light", commands.LastAction.Payload?["intensity"]?.ToString());
        Assert.AreEqual(4, (int?)commands.LastAction.Payload?["ttlSeconds"]);
    }

    [TestMethod]
    public async Task RecentActivityTool_UsesProviderWhenAvailable()
    {
        var provider = new FakeRecentActivityProvider(new StardewStatusFactResponseData(
            "最近有 1 条连续性记录。",
            ["recent[0]=observation:Haley is in Town"],
            "completed",
            []));
        var tools = StardewNpcToolFactory.CreateDefault(
            new FakeGameAdapter(new CapturingCommandService(), new FakeQueryService(), new FakeEventSource()),
            CreateDescriptor("haley"),
            recentActivityProvider: provider);
        var tool = tools.Single(tool => tool.Name == "stardew_recent_activity");

        var result = await tool.ExecuteAsync(new StardewStatusToolParameters(), CancellationToken.None);

        Assert.IsTrue(result.Success);
        using var document = JsonDocument.Parse(result.Content);
        Assert.AreEqual("最近有 1 条连续性记录。", document.RootElement.GetProperty("summary").GetString());
        Assert.AreEqual("haley", provider.LastDescriptor?.NpcId);
    }

    [TestMethod]
    public async Task RecentActivityProvider_KeepsObservationFactsSeparatedBySession()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-recent-session-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new NpcObservationFactStore();
            var supervisor = new NpcRuntimeSupervisor();
            var autonomyDescriptor = CreateDescriptor("haley");
            var privateChatDescriptor = autonomyDescriptor with { SessionId = autonomyDescriptor.SessionId + ":private_chat:chat-1" };
            var driver = await supervisor.GetOrCreateDriverAsync(autonomyDescriptor, tempDir, CancellationToken.None);
            store.RecordObservation(
                autonomyDescriptor,
                new GameObservation(
                    "haley",
                    "stardew-valley",
                    DateTime.UtcNow,
                    "Autonomy fact.",
                    ["source=autonomy"]));
            store.RecordObservation(
                privateChatDescriptor,
                new GameObservation(
                    "haley",
                    "stardew-valley",
                    DateTime.UtcNow,
                    "Private chat fact.",
                    ["source=private_chat"]));
            var provider = new StardewRecentActivityProvider(store, driver);

            var data = await provider.ReadRecentActivityAsync(autonomyDescriptor, CancellationToken.None);

            Assert.AreEqual("completed", data.Status);
            Assert.IsTrue(data.Facts.Any(fact => fact.Contains("source=autonomy", StringComparison.Ordinal)));
            Assert.IsFalse(data.Facts.Any(fact => fact.Contains("source=private_chat", StringComparison.Ordinal)));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task RecentActivityProvider_KeepsObservationFactsSeparatedByProfile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-recent-profile-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new NpcObservationFactStore();
            var supervisor = new NpcRuntimeSupervisor();
            var defaultDescriptor = CreateDescriptor("haley");
            var otherProfileDescriptor = defaultDescriptor with { ProfileId = "profile-2", SessionId = "sdv_save-1_haley_profile-2" };
            var driver = await supervisor.GetOrCreateDriverAsync(defaultDescriptor, tempDir, CancellationToken.None);
            store.RecordObservation(
                defaultDescriptor,
                new GameObservation("haley", "stardew-valley", DateTime.UtcNow, "Default profile.", ["profile=default"]));
            store.RecordObservation(
                otherProfileDescriptor,
                new GameObservation("haley", "stardew-valley", DateTime.UtcNow, "Other profile.", ["profile=profile-2"]));
            var provider = new StardewRecentActivityProvider(store, driver);

            var data = await provider.ReadRecentActivityAsync(defaultDescriptor, CancellationToken.None);

            Assert.IsTrue(data.Facts.Any(fact => fact.Contains("profile=default", StringComparison.Ordinal)));
            Assert.IsFalse(data.Facts.Any(fact => fact.Contains("profile=profile-2", StringComparison.Ordinal)));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task RecentActivityProvider_KeepsObservationFactsSeparatedByNpc()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-recent-npc-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new NpcObservationFactStore();
            var supervisor = new NpcRuntimeSupervisor();
            var haley = CreateDescriptor("haley");
            var penny = CreateDescriptor("penny") with { BodyBinding = new NpcBodyBinding("penny", "Penny", "Penny", "Penny", "stardew") };
            var driver = await supervisor.GetOrCreateDriverAsync(haley, tempDir, CancellationToken.None);
            store.RecordObservation(haley, new GameObservation("haley", "stardew-valley", DateTime.UtcNow, "Haley fact.", ["npc=haley"]));
            store.RecordObservation(penny, new GameObservation("penny", "stardew-valley", DateTime.UtcNow, "Penny fact.", ["npc=penny"]));
            var provider = new StardewRecentActivityProvider(store, driver);

            var data = await provider.ReadRecentActivityAsync(haley, CancellationToken.None);

            Assert.IsTrue(data.Facts.Any(fact => fact.Contains("npc=haley", StringComparison.Ordinal)));
            Assert.IsFalse(data.Facts.Any(fact => fact.Contains("npc=penny", StringComparison.Ordinal)));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task SpeakTool_BindsRuntimeIdentityAndSubmitsThroughCommandService()
    {
        var commands = new CapturingCommandService();
        var tools = StardewNpcToolFactory.CreateDefault(
            new FakeGameAdapter(commands, new FakeQueryService(), new FakeEventSource()),
            CreateDescriptor("haley"),
            traceIdFactory: () => "trace-speak",
            idempotencyKeyFactory: () => "idem-speak");
        var speakTool = tools.Single(tool => tool.Name == "stardew_speak");

        var result = await speakTool.ExecuteAsync(new StardewSpeakToolParameters
        {
            Text = "Hi.",
            Channel = "player"
        }, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(commands.LastAction);
        Assert.AreEqual("haley", commands.LastAction.NpcId);
        Assert.AreEqual("Haley", commands.LastAction.BodyBinding?.TargetEntityId);
        Assert.AreEqual(GameActionType.Speak, commands.LastAction.Type);
        Assert.AreEqual("trace-speak", commands.LastAction.TraceId);
        Assert.AreEqual("idem-speak", commands.LastAction.IdempotencyKey);
        Assert.AreEqual("Hi.", commands.LastAction.Payload?["text"]?.ToString());
        Assert.AreEqual("player", commands.LastAction.Payload?["channel"]?.ToString());
        Assert.AreEqual("cmd-1", JsonDocument.Parse(result.Content).RootElement.GetProperty("commandId").GetString());
    }

    [TestMethod]
    public void SpeakToolDescriptionExplainsNonBlockingBubbleAndPhoneDelivery()
    {
        var tools = StardewNpcToolFactory.CreateDefault(
            new FakeGameAdapter(new CapturingCommandService(), new FakeQueryService(), new FakeEventSource()),
            CreateDescriptor("haley"));
        var speakTool = tools.Single(tool => tool.Name == "stardew_speak");

        StringAssert.Contains(speakTool.Description, "非阻塞");
        StringAssert.Contains(speakTool.Description, "头顶气泡");
        StringAssert.Contains(speakTool.Description, "手机消息");
    }

    [TestMethod]
    public async Task OpenPrivateChatTool_BindsRuntimeIdentityAndSubmitsThroughCommandService()
    {
        var commands = new CapturingCommandService();
        var tools = StardewNpcToolFactory.CreateDefault(
            new FakeGameAdapter(commands, new FakeQueryService(), new FakeEventSource()),
            CreateDescriptor("haley"),
            traceIdFactory: () => "trace-private-chat",
            idempotencyKeyFactory: () => "idem-private-chat");
        var tool = tools.Single(tool => tool.Name == "stardew_open_private_chat");

        var result = await tool.ExecuteAsync(new StardewOpenPrivateChatToolParameters
        {
            Prompt = "Want to keep chatting?"
        }, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(commands.LastAction);
        Assert.AreEqual("haley", commands.LastAction.NpcId);
        Assert.AreEqual("Haley", commands.LastAction.BodyBinding?.TargetEntityId);
        Assert.AreEqual(GameActionType.OpenPrivateChat, commands.LastAction.Type);
        Assert.AreEqual("trace-private-chat", commands.LastAction.TraceId);
        Assert.AreEqual("idem-private-chat", commands.LastAction.IdempotencyKey);
        Assert.AreEqual("Want to keep chatting?", commands.LastAction.Payload?["prompt"]?.ToString());
    }

    [TestMethod]
    public async Task RuntimeActionController_WithDestinationIdOnlyMove_CreatesMoveClaim()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-runtime-claim-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var supervisor = new NpcRuntimeSupervisor();
            var descriptor = CreateDescriptor("haley");
            var driver = await supervisor.GetOrCreateDriverAsync(descriptor, tempDir, CancellationToken.None);
            var registry = new ResourceClaimRegistry();
            var coordination = new WorldCoordinationService(registry);
            var controller = new StardewRuntimeActionController(driver, coordination, actionTimeout: null, nowUtc: () => new DateTime(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc));
            var action = new GameAction(
                "haley",
                "stardew-valley",
                GameActionType.Move,
                "trace-move",
                "idem-move",
                new GameActionTarget("destination"),
                "inspect fountain",
                Payload: new JsonObject
                {
                    ["destinationId"] = "town.fountain"
                },
                BodyBinding: descriptor.EffectiveBodyBinding);

            var prepared = await controller.TryBeginAsync(action, CancellationToken.None);

            Assert.IsNotNull(prepared);
            Assert.IsNull(prepared?.BlockedResult);
            var claims = registry.Snapshot();
            Assert.AreEqual(1, claims.Count, "Destination-only moves should still create a Desktop runtime claim.");
            Assert.AreEqual("work_idem-move", claims[0].CommandId);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task RuntimeActionController_AfterAcceptedMove_RekeysClaimToCommandId()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-runtime-claim-rekey-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var supervisor = new NpcRuntimeSupervisor();
            var descriptor = CreateDescriptor("haley");
            var driver = await supervisor.GetOrCreateDriverAsync(descriptor, tempDir, CancellationToken.None);
            var registry = new ResourceClaimRegistry();
            var coordination = new WorldCoordinationService(registry);
            var controller = new StardewRuntimeActionController(driver, coordination, actionTimeout: null, nowUtc: () => new DateTime(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc));
            var action = new GameAction(
                "haley",
                "stardew-valley",
                GameActionType.Move,
                "trace-move",
                "idem-move",
                new GameActionTarget("destination"),
                "inspect fountain",
                Payload: new JsonObject
                {
                    ["destinationId"] = "town.fountain"
                },
                BodyBinding: descriptor.EffectiveBodyBinding);

            var prepared = await controller.TryBeginAsync(action, CancellationToken.None);
            await controller.RecordSubmitResultAsync(prepared, new GameCommandResult(true, "cmd-1", StardewCommandStatuses.Queued, null, "trace-move"), CancellationToken.None);

            var claims = registry.Snapshot();
            Assert.AreEqual(1, claims.Count, "Accepted move should keep exactly one claim after rekey.");
            Assert.AreEqual("cmd-1", claims[0].CommandId, "Claim identity should be upgraded from work item id to command id after accept.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task RuntimeActionController_WhenSubmitResultIsTerminal_RecordsLastTerminalStatus()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-runtime-submit-terminal-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var supervisor = new NpcRuntimeSupervisor();
            var descriptor = CreateDescriptor("haley");
            var driver = await supervisor.GetOrCreateDriverAsync(descriptor, tempDir, CancellationToken.None);
            var controller = new StardewRuntimeActionController(driver, null, actionTimeout: null, nowUtc: () => new DateTime(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc));
            var action = new GameAction(
                "haley",
                "stardew-valley",
                GameActionType.OpenPrivateChat,
                "trace-open-chat",
                "idem-open-chat",
                new GameActionTarget("player"),
                BodyBinding: descriptor.EffectiveBodyBinding);

            var prepared = await controller.TryBeginAsync(action, CancellationToken.None);
            await controller.RecordSubmitResultAsync(
                prepared,
                new GameCommandResult(true, "cmd-open-chat", StardewCommandStatuses.Completed, null, "trace-open-chat"),
                CancellationToken.None);

            var snapshot = driver.Snapshot();
            Assert.IsNull(snapshot.PendingWorkItem);
            Assert.IsNull(snapshot.ActionSlot);
            Assert.AreEqual("cmd-open-chat", snapshot.LastTerminalCommandStatus?.CommandId);
            Assert.AreEqual("open_private_chat", snapshot.LastTerminalCommandStatus?.Action);
            Assert.AreEqual(StardewCommandStatuses.Completed, snapshot.LastTerminalCommandStatus?.Status);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
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
            $"sdv_save-1_{npcId}_default",
            new NpcBodyBinding(npcId, "Haley", "Haley", "Haley", "stardew"));

    private static string GetSchemaPropertyDescription(string schema, string propertyName)
    {
        using var document = JsonDocument.Parse(schema);
        return document.RootElement
            .GetProperty("properties")
            .GetProperty(propertyName)
            .GetProperty("description")
            .GetString() ?? string.Empty;
    }

    private sealed class FakeGameAdapter : IGameAdapter
    {
        public FakeGameAdapter(IGameCommandService commands, IGameQueryService queries, IGameEventSource events)
        {
            Commands = commands;
            Queries = queries;
            Events = events;
        }

        public string AdapterId => "stardew";

        public IGameCommandService Commands { get; }

        public IGameQueryService Queries { get; }

        public IGameEventSource Events { get; }
    }

    private sealed class CapturingCommandService : IGameCommandService
    {
        private readonly Queue<GameCommandStatus> _statusSequence;

        private readonly GameCommandResult? _submitResult;

        public CapturingCommandService(IReadOnlyList<GameCommandStatus>? statusSequence = null, GameCommandResult? submitResult = null)
        {
            _statusSequence = new Queue<GameCommandStatus>(statusSequence ?? []);
            _submitResult = submitResult;
        }

        public GameAction? LastAction { get; private set; }

        public int StatusCalls { get; private set; }

        public Task<GameCommandResult> SubmitAsync(GameAction action, CancellationToken ct)
        {
            LastAction = action;
            return Task.FromResult(_submitResult ?? new GameCommandResult(true, "cmd-1", StardewCommandStatuses.Queued, null, action.TraceId));
        }

        public Task<GameCommandStatus> GetStatusAsync(string commandId, CancellationToken ct)
        {
            StatusCalls++;
            return Task.FromResult(_statusSequence.Count > 0
                ? _statusSequence.Dequeue()
                : new GameCommandStatus(commandId, "haley", "move", StardewCommandStatuses.Running, 0.5, null, null));
        }

        public Task<GameCommandStatus> CancelAsync(string commandId, string reason, CancellationToken ct)
            => Task.FromResult(new GameCommandStatus(commandId, "haley", "move", StardewCommandStatuses.Cancelled, 0, reason, null));
    }

    private sealed class FakeQueryService : IGameQueryService
    {
        private readonly IReadOnlyList<string> _facts;

        public FakeQueryService(IReadOnlyList<string>? facts = null)
        {
            _facts = facts ??
            [
                "destination[0]=label=Town fountain,locationName=Town,x=42,y=17,tags=public|photogenic,reason=stand somewhere bright and visible in town,destinationId=town.fountain,facingDirection=2",
                "destination[1]=label=Front door,locationName=HaleyHouse,x=15,y=8,tags=transition|outdoor,reason=consider going outside,destinationId=haley_house.front_door,facingDirection=2",
                "nearby[0]=locationName=Town,x=41,y=17,reason=same_location_safe_reposition"
            ];
        }

        public Task<GameObservation> ObserveAsync(string npcId, CancellationToken ct)
        {
            ObserveCalls++;
            return Task.FromResult(new GameObservation(npcId, "stardew-valley", DateTime.UtcNow, "status", _facts));
        }

        public int ObserveCalls { get; private set; }

        public Task<WorldSnapshot> GetWorldSnapshotAsync(string npcId, CancellationToken ct)
            => Task.FromResult(new WorldSnapshot("stardew-valley", "save-1", DateTime.UtcNow, [], []));
    }

    private sealed class FakeEventSource : IGameEventSource
    {
        public Task<IReadOnlyList<GameEventRecord>> PollAsync(GameEventCursor cursor, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<GameEventRecord>>([]);
    }

    private sealed class FakeRecentActivityProvider : IStardewRecentActivityProvider
    {
        private readonly StardewStatusFactResponseData _response;

        public FakeRecentActivityProvider(StardewStatusFactResponseData response)
        {
            _response = response;
        }

        public NpcRuntimeDescriptor? LastDescriptor { get; private set; }

        public Task<StardewStatusFactResponseData> ReadRecentActivityAsync(NpcRuntimeDescriptor descriptor, CancellationToken ct)
        {
            LastDescriptor = descriptor;
            return Task.FromResult(_response);
        }
    }
}
