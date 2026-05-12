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
    public async Task RecentActivityProvider_WithActionChainGuard_IncludesShortChainFact()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-recent-chain-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new NpcObservationFactStore();
            var supervisor = new NpcRuntimeSupervisor();
            var descriptor = CreateDescriptor("haley");
            var driver = await supervisor.GetOrCreateDriverAsync(descriptor, tempDir, CancellationToken.None);
            await driver.SetActionChainGuardAsync(
                new NpcRuntimeActionChainGuardSnapshot(
                    "chain-activity-1",
                    "open",
                    null,
                    false,
                    "todo-1",
                    "trace-root-1",
                    new DateTime(2026, 5, 11, 7, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 5, 11, 7, 1, 0, DateTimeKind.Utc),
                    "move",
                    "move:Town:42:17",
                    ConsecutiveActions: 2,
                    ConsecutiveFailures: 1,
                    ConsecutiveSameActionFailures: 1,
                    LastTerminalStatus: StardewCommandStatuses.Blocked,
                    LastReasonCode: StardewBridgeErrorCodes.PathBlocked,
                    ClosureMissingCount: 0,
                    DeferredIngressAttempts: 0),
                CancellationToken.None);
            var provider = new StardewRecentActivityProvider(store, driver);

            var data = await provider.ReadRecentActivityAsync(descriptor, CancellationToken.None);

            var chainFact = data.Facts.Single(fact => fact.StartsWith("action_chain:", StringComparison.Ordinal));
            StringAssert.Contains(chainFact, "chainId=chain-activity-1");
            StringAssert.Contains(chainFact, "actions=2");
            StringAssert.Contains(chainFact, "failures=1");
            StringAssert.Contains(chainFact, "reason=path_blocked");
            Assert.IsFalse(chainFact.Contains("{", StringComparison.Ordinal), "Agent-visible chain facts must not expose internal JSON.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task RecentActivityProvider_WithPrivateChatLifecycleTerminal_ReportsLastInteractionNotLastAction()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-recent-interaction-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new NpcObservationFactStore();
            var supervisor = new NpcRuntimeSupervisor();
            var descriptor = CreateDescriptor("haley");
            var driver = await supervisor.GetOrCreateDriverAsync(descriptor, tempDir, CancellationToken.None);
            await driver.SetActionChainGuardAsync(
                new NpcRuntimeActionChainGuardSnapshot(
                    "chain-existing-move",
                    "open",
                    null,
                    false,
                    "go-to-beach-photo",
                    "trace-existing-move",
                    new DateTime(2026, 5, 11, 7, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 5, 11, 7, 1, 0, DateTimeKind.Utc),
                    "move",
                    "move:Beach:32:34",
                    ConsecutiveActions: 1,
                    ConsecutiveFailures: 0,
                    ConsecutiveSameActionFailures: 0,
                    LastTerminalStatus: StardewCommandStatuses.Completed,
                    LastReasonCode: null,
                    ClosureMissingCount: 0,
                    DeferredIngressAttempts: 0),
                CancellationToken.None);
            await driver.SetLastTerminalCommandStatusAsync(
                new GameCommandStatus(
                    "work_private_chat:1_haley_evt_0001_turn2",
                    "haley",
                    "open_private_chat",
                    StardewCommandStatuses.Completed,
                    1,
                    null,
                    null),
                CancellationToken.None);
            var provider = new StardewRecentActivityProvider(store, driver);

            var data = await provider.ReadRecentActivityAsync(descriptor, CancellationToken.None);

            Assert.IsTrue(
                data.Facts.Any(fact => fact.Contains("lastInteraction=open_private_chat:completed:none", StringComparison.Ordinal)),
                "Interaction lifecycle terminal should remain visible as an interaction fact.");
            var interactionFact = data.Facts.Single(fact => fact.StartsWith("lastInteraction=", StringComparison.Ordinal));
            Assert.IsFalse(
                interactionFact.Contains("rootTodoId=", StringComparison.Ordinal) ||
                interactionFact.Contains("lastTarget=", StringComparison.Ordinal),
                "Interaction lifecycle facts must not inherit old world-action chain correlation.");
            Assert.IsFalse(
                data.Facts.Any(fact => fact.Contains("lastAction=open_private_chat", StringComparison.Ordinal)),
                "Interaction lifecycle terminal must not overwrite the last real world action fact.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task RecentActivityProvider_WithDelegatedMoveChain_IncludesTodoTraceAndConversationCorrelation()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-recent-correlation-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new NpcObservationFactStore();
            var supervisor = new NpcRuntimeSupervisor();
            var descriptor = CreateDescriptor("haley");
            var driver = await supervisor.GetOrCreateDriverAsync(descriptor, tempDir, CancellationToken.None);
            var controller = new StardewRuntimeActionController(
                driver,
                null,
                actionTimeout: null,
                nowUtc: () => new DateTime(2026, 5, 11, 7, 0, 0, DateTimeKind.Utc));
            var action = new GameAction(
                "haley",
                "stardew-valley",
                GameActionType.Move,
                "trace-delegate-beach",
                "idem-delegate-beach",
                new GameActionTarget("tile", "Beach", new GameTile(32, 34)),
                "meet the player at the beach",
                new JsonObject
                {
                    ["rootTodoId"] = "meet-beach-now",
                    ["conversationId"] = "pc_evt_beach",
                    ["targetSource"] = "map-skill:stardew.navigation.poi.beach-shoreline"
                },
                descriptor.EffectiveBodyBinding);

            var prepared = await controller.TryBeginAsync(action, CancellationToken.None);
            await controller.RecordSubmitResultAsync(
                prepared,
                new GameCommandResult(true, "cmd-beach", StardewCommandStatuses.Completed, null, "trace-delegate-beach"),
                CancellationToken.None);
            var provider = new StardewRecentActivityProvider(store, driver);

            var data = await provider.ReadRecentActivityAsync(descriptor, CancellationToken.None);

            var chainFact = data.Facts.Single(fact => fact.StartsWith("action_chain:", StringComparison.Ordinal));
            StringAssert.Contains(chainFact, "rootTodoId=meet-beach-now");
            StringAssert.Contains(chainFact, "rootTraceId=trace-delegate-beach");
            StringAssert.Contains(chainFact, "lastTarget=move:Beach:32:34");
            StringAssert.Contains(chainFact, "conversationId=pc_evt_beach");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task RecentActivityProvider_WithCompletedWorldAction_IncludesTaskDoneFact()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-recent-task-done-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new NpcObservationFactStore();
            var supervisor = new NpcRuntimeSupervisor();
            var descriptor = CreateDescriptor("haley");
            var driver = await supervisor.GetOrCreateDriverAsync(descriptor, tempDir, CancellationToken.None);
            var controller = new StardewRuntimeActionController(
                driver,
                null,
                actionTimeout: null,
                nowUtc: () => new DateTime(2026, 5, 11, 7, 0, 0, DateTimeKind.Utc));
            var action = new GameAction(
                "haley",
                "stardew-valley",
                GameActionType.Move,
                "trace-beach-done",
                "idem-beach-done",
                new GameActionTarget("tile", "Beach", new GameTile(38, 22)),
                "meet the player at the beach",
                new JsonObject
                {
                    ["rootTodoId"] = "meet-player-at-beach",
                    ["conversationId"] = "pc_evt_beach"
                },
                descriptor.EffectiveBodyBinding);

            var prepared = await controller.TryBeginAsync(action, CancellationToken.None);
            await controller.RecordSubmitResultAsync(
                prepared,
                new GameCommandResult(true, "cmd-beach-done", StardewCommandStatuses.Completed, null, "trace-beach-done"),
                CancellationToken.None);
            var provider = new StardewRecentActivityProvider(store, driver);

            var data = await provider.ReadRecentActivityAsync(descriptor, CancellationToken.None);

            var taskDoneFact = data.Facts.Single(fact => fact.StartsWith("task_done:", StringComparison.Ordinal));
            StringAssert.Contains(taskDoneFact, "action=move");
            StringAssert.Contains(taskDoneFact, "commandId=cmd-beach-done");
            StringAssert.Contains(taskDoneFact, "target=move:Beach:38:22");
            StringAssert.Contains(taskDoneFact, "rootTodoId=meet-player-at-beach");
            Assert.IsFalse(taskDoneFact.Contains("{", StringComparison.Ordinal), "Agent-visible task lifecycle facts must stay short and non-JSON.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task RecentActivityProvider_WithBlockedWorldAction_IncludesTaskErrorFact()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-recent-task-error-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new NpcObservationFactStore();
            var supervisor = new NpcRuntimeSupervisor();
            var descriptor = CreateDescriptor("haley");
            var driver = await supervisor.GetOrCreateDriverAsync(descriptor, tempDir, CancellationToken.None);
            await driver.SetActionChainGuardAsync(
                new NpcRuntimeActionChainGuardSnapshot(
                    "chain-error-1",
                    "open",
                    null,
                    false,
                    "meet-player-at-beach",
                    "trace-beach-blocked",
                    new DateTime(2026, 5, 11, 7, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 5, 11, 7, 1, 0, DateTimeKind.Utc),
                    "move",
                    "move:Beach:38:22",
                    ConsecutiveActions: 1,
                    ConsecutiveFailures: 1,
                    ConsecutiveSameActionFailures: 1,
                    LastTerminalStatus: StardewCommandStatuses.Blocked,
                    LastReasonCode: StardewBridgeErrorCodes.PathBlocked,
                    ClosureMissingCount: 0,
                    DeferredIngressAttempts: 0),
                CancellationToken.None);
            await driver.SetLastTerminalCommandStatusAsync(
                new GameCommandStatus(
                    "cmd-beach-blocked",
                    "haley",
                    "move",
                    StardewCommandStatuses.Blocked,
                    1,
                    StardewBridgeErrorCodes.PathBlocked,
                    StardewBridgeErrorCodes.PathBlocked),
                CancellationToken.None);
            var provider = new StardewRecentActivityProvider(store, driver);

            var data = await provider.ReadRecentActivityAsync(descriptor, CancellationToken.None);

            var taskErrorFact = data.Facts.Single(fact => fact.StartsWith("task_error:", StringComparison.Ordinal));
            StringAssert.Contains(taskErrorFact, "action=move");
            StringAssert.Contains(taskErrorFact, "commandId=cmd-beach-blocked");
            StringAssert.Contains(taskErrorFact, "status=blocked");
            StringAssert.Contains(taskErrorFact, "reason=path_blocked");
            StringAssert.Contains(taskErrorFact, "target=move:Beach:38:22");
            StringAssert.Contains(taskErrorFact, "rootTodoId=meet-player-at-beach");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task RecentActivityProvider_WithRepeatedSameActionFailures_IncludesActionLoopFact()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-recent-loop-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new NpcObservationFactStore();
            var supervisor = new NpcRuntimeSupervisor();
            var descriptor = CreateDescriptor("haley");
            var driver = await supervisor.GetOrCreateDriverAsync(descriptor, tempDir, CancellationToken.None);
            await driver.SetActionChainGuardAsync(
                new NpcRuntimeActionChainGuardSnapshot(
                    "chain-loop-1",
                    "blocked_until_closure",
                    StardewBridgeErrorCodes.PathBlocked,
                    true,
                    "todo-1",
                    "trace-root-1",
                    new DateTime(2026, 5, 11, 7, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 5, 11, 7, 1, 0, 0, DateTimeKind.Utc),
                    "move",
                    "move:Town:42:17",
                    ConsecutiveActions: 2,
                    ConsecutiveFailures: 2,
                    ConsecutiveSameActionFailures: 2,
                    LastTerminalStatus: StardewCommandStatuses.Blocked,
                    LastReasonCode: StardewBridgeErrorCodes.PathBlocked,
                    ClosureMissingCount: 0,
                    DeferredIngressAttempts: 0),
                CancellationToken.None);
            var provider = new StardewRecentActivityProvider(store, driver);

            var data = await provider.ReadRecentActivityAsync(descriptor, CancellationToken.None);

            var loopFact = data.Facts.Single(fact => fact.StartsWith("action_loop:", StringComparison.Ordinal));
            StringAssert.Contains(loopFact, "chainId=chain-loop-1");
            StringAssert.Contains(loopFact, "action=move");
            StringAssert.Contains(loopFact, "targetKey=move:Town:42:17");
            StringAssert.Contains(loopFact, "sameActionFailures=2");
            StringAssert.Contains(loopFact, "reason=path_blocked");
            Assert.IsFalse(loopFact.Contains("{", StringComparison.Ordinal), "Agent-visible action loop facts must not expose internal JSON.");
            var stuckFact = data.Facts.Single(fact => fact.StartsWith("task_stuck:", StringComparison.Ordinal));
            StringAssert.Contains(stuckFact, "action=move");
            StringAssert.Contains(stuckFact, "target=move:Town:42:17");
            StringAssert.Contains(stuckFact, "sameActionFailures=2");
            StringAssert.Contains(stuckFact, "reason=path_blocked");
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

    [TestMethod]
    public async Task RuntimeActionController_WithOpenPrivateChat_DoesNotCreateWorldActionChain()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-runtime-open-chat-chain-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var supervisor = new NpcRuntimeSupervisor();
            var descriptor = CreateDescriptor("haley");
            var driver = await supervisor.GetOrCreateDriverAsync(descriptor, tempDir, CancellationToken.None);
            var controller = new StardewRuntimeActionController(
                driver,
                null,
                actionTimeout: null,
                nowUtc: () => new DateTime(2026, 5, 11, 7, 0, 0, DateTimeKind.Utc));
            var action = new GameAction(
                "haley",
                "stardew-valley",
                GameActionType.OpenPrivateChat,
                "trace-open-chat",
                "idem-open-chat",
                new GameActionTarget("player"),
                BodyBinding: descriptor.EffectiveBodyBinding);

            var prepared = await controller.TryBeginAsync(action, CancellationToken.None);
            Assert.IsNull(driver.Snapshot().ActionChainGuard);

            await controller.RecordSubmitResultAsync(
                prepared,
                new GameCommandResult(true, "cmd-open-chat", StardewCommandStatuses.Completed, null, "trace-open-chat"),
                CancellationToken.None);

            var snapshot = driver.Snapshot();
            Assert.AreEqual("open_private_chat", snapshot.LastTerminalCommandStatus?.Action);
            Assert.IsNull(snapshot.ActionChainGuard, "Interaction lifecycle actions must not create world-action continuity state.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task RuntimeActionController_WithOpenPrivateChatTerminal_DoesNotMutateExistingWorldActionChain()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-runtime-open-chat-existing-chain-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var supervisor = new NpcRuntimeSupervisor();
            var descriptor = CreateDescriptor("haley");
            var driver = await supervisor.GetOrCreateDriverAsync(descriptor, tempDir, CancellationToken.None);
            var existingChain = new NpcRuntimeActionChainGuardSnapshot(
                "chain-existing-move",
                "open",
                null,
                false,
                "go-to-beach-photo",
                "trace-existing-move",
                new DateTime(2026, 5, 11, 7, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 11, 7, 1, 0, DateTimeKind.Utc),
                "move",
                "move:Beach:32:34",
                ConsecutiveActions: 1,
                ConsecutiveFailures: 1,
                ConsecutiveSameActionFailures: 1,
                LastTerminalStatus: StardewCommandStatuses.Blocked,
                LastReasonCode: StardewBridgeErrorCodes.PathBlocked,
                ClosureMissingCount: 0,
                DeferredIngressAttempts: 0);
            await driver.SetActionChainGuardAsync(existingChain, CancellationToken.None);
            var controller = new StardewRuntimeActionController(
                driver,
                null,
                actionTimeout: null,
                nowUtc: () => new DateTime(2026, 5, 11, 7, 2, 0, DateTimeKind.Utc));
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
                new GameCommandResult(false, "cmd-open-chat", StardewCommandStatuses.Blocked, "private_chat_window_busy", "trace-open-chat"),
                CancellationToken.None);

            Assert.AreEqual(existingChain, driver.Snapshot().ActionChainGuard);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task RuntimeActionController_WhenSubmitResultIsTerminal_DoesNotDoubleCountAcceptedAction()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-runtime-chain-terminal-count-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var supervisor = new NpcRuntimeSupervisor();
            var descriptor = CreateDescriptor("haley");
            var driver = await supervisor.GetOrCreateDriverAsync(descriptor, tempDir, CancellationToken.None);
            var controller = new StardewRuntimeActionController(
                driver,
                null,
                actionTimeout: null,
                nowUtc: () => new DateTime(2026, 5, 11, 7, 0, 0, DateTimeKind.Utc));
            var action = new GameAction(
                "haley",
                "stardew-valley",
                GameActionType.Move,
                "trace-move",
                "idem-move",
                new GameActionTarget("tile", "Town", new GameTile(42, 17)),
                BodyBinding: descriptor.EffectiveBodyBinding);

            var prepared = await controller.TryBeginAsync(action, CancellationToken.None);
            Assert.AreEqual(1, driver.Snapshot().ActionChainGuard?.ConsecutiveActions);

            await controller.RecordSubmitResultAsync(
                prepared,
                new GameCommandResult(true, "cmd-move", StardewCommandStatuses.Completed, null, "trace-move"),
                CancellationToken.None);

            var chain = driver.Snapshot().ActionChainGuard;
            Assert.IsNotNull(chain);
            Assert.AreEqual(1, chain!.ConsecutiveActions, "Terminal status updates must not count as another accepted action.");
            Assert.AreEqual(0, chain.ConsecutiveFailures);
            Assert.AreEqual(StardewCommandStatuses.Completed, chain.LastTerminalStatus);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task RuntimeActionController_TerminalFailures_UpdateFailureCountersWithoutBlocking()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-runtime-chain-failure-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var supervisor = new NpcRuntimeSupervisor();
            var descriptor = CreateDescriptor("haley");
            var driver = await supervisor.GetOrCreateDriverAsync(descriptor, tempDir, CancellationToken.None);
            await driver.SetActionChainGuardAsync(
                new NpcRuntimeActionChainGuardSnapshot(
                    "chain-failure-1",
                    "open",
                    null,
                    false,
                    "todo-1",
                    "trace-root-1",
                    new DateTime(2026, 5, 11, 7, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 5, 11, 7, 1, 0, DateTimeKind.Utc),
                    "move",
                    "move:Town:42:17",
                    ConsecutiveActions: 1,
                    ConsecutiveFailures: 1,
                    ConsecutiveSameActionFailures: 1,
                    LastTerminalStatus: StardewCommandStatuses.Blocked,
                    LastReasonCode: StardewBridgeErrorCodes.PathBlocked,
                    ClosureMissingCount: 0,
                    DeferredIngressAttempts: 0),
                CancellationToken.None);
            var controller = new StardewRuntimeActionController(
                driver,
                null,
                actionTimeout: null,
                nowUtc: () => new DateTime(2026, 5, 11, 7, 2, 0, DateTimeKind.Utc),
                actionChainGuardOptions: new NpcActionChainGuardOptions(MaxConsecutiveFailures: 2));

            await controller.RecordStatusAsync(
                new GameCommandStatus(
                    "cmd-blocked-2",
                    "haley",
                    "move",
                    StardewCommandStatuses.Blocked,
                    1,
                    StardewBridgeErrorCodes.PathBlocked,
                    StardewBridgeErrorCodes.PathBlocked),
                CancellationToken.None);

            var chain = driver.Snapshot().ActionChainGuard;
            Assert.IsNotNull(chain);
            Assert.AreEqual(2, chain!.ConsecutiveFailures);
            Assert.AreEqual(2, chain.ConsecutiveSameActionFailures);
            Assert.AreEqual("open", chain.GuardStatus);
            Assert.IsFalse(chain.BlockedUntilClosure);
            Assert.IsNull(chain.BlockedReasonCode);
            Assert.AreEqual(StardewBridgeErrorCodes.PathBlocked, chain.LastReasonCode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task RuntimeActionController_ChainBudgetExceeded_SubmitsWithoutOverwritingTerminal()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-runtime-chain-budget-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var supervisor = new NpcRuntimeSupervisor();
            var descriptor = CreateDescriptor("haley");
            var driver = await supervisor.GetOrCreateDriverAsync(descriptor, tempDir, CancellationToken.None);
            var terminal = new GameCommandStatus(
                "cmd-previous",
                "haley",
                "move",
                StardewCommandStatuses.Completed,
                1,
                null,
                null);
            await driver.SetLastTerminalCommandStatusAsync(terminal, CancellationToken.None);
            await driver.SetActionChainGuardAsync(
                new NpcRuntimeActionChainGuardSnapshot(
                    "chain-1",
                    "open",
                    null,
                    false,
                    "todo-1",
                    "trace-root-1",
                    new DateTime(2026, 5, 11, 7, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 5, 11, 7, 1, 0, DateTimeKind.Utc),
                    "move",
                    "move:Town:42:17",
                    ConsecutiveActions: 2,
                    ConsecutiveFailures: 0,
                    ConsecutiveSameActionFailures: 0,
                    LastTerminalStatus: StardewCommandStatuses.Completed,
                    LastReasonCode: null,
                    ClosureMissingCount: 0,
                    DeferredIngressAttempts: 0),
                CancellationToken.None);
            var controller = new StardewRuntimeActionController(
                driver,
                null,
                actionTimeout: null,
                nowUtc: () => new DateTime(2026, 5, 11, 7, 2, 0, DateTimeKind.Utc),
                actionChainGuardOptions: new NpcActionChainGuardOptions(MaxActionsPerChain: 2));
            var action = new GameAction(
                "haley",
                "stardew-valley",
                GameActionType.Move,
                "trace-next",
                "idem-next",
                new GameActionTarget("tile", "Town", new GameTile(42, 18)),
                "continue walking",
                BodyBinding: descriptor.EffectiveBodyBinding);

            var prepared = await controller.TryBeginAsync(action, CancellationToken.None);

            Assert.IsNotNull(prepared);
            Assert.IsNull(prepared!.BlockedResult);
            var snapshot = driver.Snapshot();
            Assert.IsNotNull(snapshot.PendingWorkItem);
            Assert.IsNotNull(snapshot.ActionSlot);
            Assert.AreEqual("cmd-previous", snapshot.LastTerminalCommandStatus?.CommandId);
            Assert.AreEqual(StardewCommandStatuses.Completed, snapshot.LastTerminalCommandStatus?.Status);
            Assert.AreEqual("open", snapshot.ActionChainGuard?.GuardStatus);
            Assert.IsFalse(snapshot.ActionChainGuard?.BlockedUntilClosure ?? true);
            Assert.IsNull(snapshot.ActionChainGuard?.BlockedReasonCode);
            Assert.AreEqual("trace-root-1", snapshot.ActionChainGuard?.RootTraceId);
            Assert.AreEqual(3, snapshot.ActionChainGuard?.ConsecutiveActions);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task RuntimeActionController_WithLegacyBlockedUntilClosure_SubmitsNextAction()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-runtime-chain-preserve-reason-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var supervisor = new NpcRuntimeSupervisor();
            var descriptor = CreateDescriptor("haley");
            var driver = await supervisor.GetOrCreateDriverAsync(descriptor, tempDir, CancellationToken.None);
            await driver.SetActionChainGuardAsync(
                new NpcRuntimeActionChainGuardSnapshot(
                    "chain-closure-reason",
                    "blocked_until_closure",
                    "legacy_closure_missing",
                    true,
                    "todo-1",
                    "trace-root-1",
                    new DateTime(2026, 5, 11, 7, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 5, 11, 7, 1, 0, DateTimeKind.Utc),
                    "move",
                    "move:Town:42:17",
                    ConsecutiveActions: 1,
                    ConsecutiveFailures: 0,
                    ConsecutiveSameActionFailures: 0,
                    LastTerminalStatus: StardewCommandStatuses.Completed,
                    LastReasonCode: null,
                    ClosureMissingCount: 2,
                    DeferredIngressAttempts: 0),
                CancellationToken.None);
            var controller = new StardewRuntimeActionController(
                driver,
                null,
                actionTimeout: null,
                nowUtc: () => new DateTime(2026, 5, 11, 7, 2, 0, DateTimeKind.Utc),
                actionChainGuardOptions: new NpcActionChainGuardOptions(MaxActionsPerChain: 4));
            var action = new GameAction(
                "haley",
                "stardew-valley",
                GameActionType.Move,
                "trace-next",
                "idem-next",
                new GameActionTarget("tile", "Town", new GameTile(42, 18)),
                "continue walking",
                BodyBinding: descriptor.EffectiveBodyBinding);

            var prepared = await controller.TryBeginAsync(action, CancellationToken.None);

            Assert.IsNotNull(prepared);
            Assert.IsNull(prepared!.BlockedResult);
            var snapshot = driver.Snapshot();
            Assert.IsNotNull(snapshot.PendingWorkItem);
            Assert.IsNotNull(snapshot.ActionSlot);
            Assert.AreEqual("open", snapshot.ActionChainGuard?.GuardStatus);
            Assert.IsFalse(snapshot.ActionChainGuard?.BlockedUntilClosure ?? true);
            Assert.IsNull(snapshot.ActionChainGuard?.BlockedReasonCode);
            Assert.AreEqual("trace-next", snapshot.ActionChainGuard?.RootTraceId);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task RuntimeActionController_WhenSlotBusyAndChainBudgetExceeded_ReturnsBusyFirst()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-runtime-busy-first-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var supervisor = new NpcRuntimeSupervisor();
            var descriptor = CreateDescriptor("haley");
            var driver = await supervisor.GetOrCreateDriverAsync(descriptor, tempDir, CancellationToken.None);
            await driver.SetActionSlotAsync(
                new NpcRuntimeActionSlotSnapshot(
                    "action",
                    "work-active",
                    "cmd-active",
                    "trace-active",
                    new DateTime(2026, 5, 11, 7, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 5, 11, 7, 1, 0, DateTimeKind.Utc)),
                CancellationToken.None);
            await driver.SetActionChainGuardAsync(
                new NpcRuntimeActionChainGuardSnapshot(
                    "chain-1",
                    "open",
                    null,
                    false,
                    "todo-1",
                    "trace-root-1",
                    new DateTime(2026, 5, 11, 6, 55, 0, DateTimeKind.Utc),
                    new DateTime(2026, 5, 11, 7, 0, 0, DateTimeKind.Utc),
                    "move",
                    "move:Town:42:17",
                    ConsecutiveActions: 2,
                    ConsecutiveFailures: 0,
                    ConsecutiveSameActionFailures: 0,
                    LastTerminalStatus: StardewCommandStatuses.Completed,
                    LastReasonCode: null,
                    ClosureMissingCount: 0,
                    DeferredIngressAttempts: 0),
                CancellationToken.None);
            var controller = new StardewRuntimeActionController(
                driver,
                null,
                actionTimeout: null,
                nowUtc: () => new DateTime(2026, 5, 11, 7, 2, 0, DateTimeKind.Utc),
                actionChainGuardOptions: new NpcActionChainGuardOptions(MaxActionsPerChain: 2));
            var action = new GameAction(
                "haley",
                "stardew-valley",
                GameActionType.Move,
                "trace-next",
                "idem-next",
                new GameActionTarget("tile", "Town", new GameTile(42, 18)),
                "continue walking",
                BodyBinding: descriptor.EffectiveBodyBinding);

            var prepared = await controller.TryBeginAsync(action, CancellationToken.None);

            Assert.IsNotNull(prepared?.BlockedResult);
            Assert.AreEqual(StardewBridgeErrorCodes.ActionSlotBusy, prepared!.BlockedResult!.FailureReason);
            var snapshot = driver.Snapshot();
            Assert.AreEqual("cmd-active", snapshot.ActionSlot?.CommandId);
            Assert.AreEqual("open", snapshot.ActionChainGuard?.GuardStatus);
            Assert.AreEqual(2, snapshot.ActionChainGuard?.ConsecutiveActions);
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
