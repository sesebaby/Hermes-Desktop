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
                "stardew_move",
                "stardew_speak",
                "stardew_open_private_chat",
                "stardew_task_status"
            },
            tools.Select(tool => tool.Name).ToArray());

        Assert.IsFalse(tools.Any(tool => tool.Name is "agent" or "todo" or "memory" or "ask_user" or "schedule_cron"));

        var moveSchema = ((IToolSchemaProvider)tools.Single(tool => tool.Name == "stardew_move")).GetParameterSchema().GetRawText();
        Assert.IsFalse(moveSchema.Contains("npcId", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(moveSchema.Contains("saveId", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(moveSchema.Contains("traceId", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(moveSchema.Contains("idempotencyKey", StringComparison.OrdinalIgnoreCase));
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
    public void MoveToolDescription_RequiresDestinationId()
    {
        var tools = StardewNpcToolFactory.CreateDefault(
            new FakeGameAdapter(new CapturingCommandService(), new FakeQueryService(), new FakeEventSource()),
            CreateDescriptor("haley"));
        var moveTool = (IToolSchemaProvider)tools.Single(tool => tool.Name == "stardew_move");
        var description = tools.Single(tool => tool.Name == "stardew_move").Description;
        var moveSchema = moveTool.GetParameterSchema().GetRawText();

        StringAssert.Contains(description, "destination");
        StringAssert.Contains(description, "destination[n].destinationId");
        StringAssert.Contains(description, "latest observation");
        StringAssert.Contains(description, "Never invent destinations");
        StringAssert.Contains(description, "path_blocked");
        StringAssert.Contains(description, "path_unreachable");
        Assert.IsFalse(description.Contains("destination[n]." + "label", StringComparison.Ordinal));
        Assert.IsFalse(description.Contains("nearby[n]", StringComparison.Ordinal));
        Assert.IsFalse(description.Contains("tile", StringComparison.OrdinalIgnoreCase), "Move tool description must not expose tile fields as public inputs.");
        Assert.IsFalse(description.Contains("facingDirection", StringComparison.OrdinalIgnoreCase), "Move tool description must not expose facing direction as a public input.");

        using var document = JsonDocument.Parse(moveSchema);
        var properties = document.RootElement.GetProperty("properties");
        Assert.IsTrue(properties.TryGetProperty("destination", out _));
        Assert.IsFalse(properties.TryGetProperty("label", out _));
        Assert.IsFalse(properties.TryGetProperty("x", out _));
        Assert.IsFalse(properties.TryGetProperty("y", out _));
        Assert.IsFalse(properties.TryGetProperty("tile", out _));
        Assert.IsFalse(properties.TryGetProperty("facingDirection", out _));
        var destinationDescription = GetSchemaPropertyDescription(moveSchema, "destination");
        StringAssert.Contains(destinationDescription, "destination[n].destinationId");
        Assert.IsFalse(destinationDescription.Contains("destination[n]." + "label", StringComparison.Ordinal));
        Assert.IsFalse(destinationDescription.Contains("tile", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(destinationDescription.Contains("facingDirection", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(destinationDescription.Contains("coordinate", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(GetSchemaPropertyDescription(moveSchema, "reason"), "Short reason");
        StringAssert.Contains(GetSchemaPropertyDescription(moveSchema, "thought"), "overhead bubble");
    }

    [TestMethod]
    public async Task MoveTool_WithRuntimeDriver_ReleasesClaimAfterTerminalPathBlockedStatus()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-tool-path-blocked-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var commands = new CapturingCommandService(
                [
                    new GameCommandStatus("cmd-1", "haley", "move", StardewCommandStatuses.Running, 0.5, null, null),
                    new GameCommandStatus(
                        "cmd-1",
                        "haley",
                        "move",
                        StardewCommandStatuses.Failed,
                        0,
                        "path_blocked:HaleyHouse:7,7;step_tile_open_false",
                        StardewBridgeErrorCodes.PathBlocked)
                ]);
            var supervisor = new NpcRuntimeSupervisor();
            var driver = await supervisor.GetOrCreateDriverAsync(CreateDescriptor("haley"), tempDir, CancellationToken.None);
            var coordination = new WorldCoordinationService(new ResourceClaimRegistry());
            var tools = StardewNpcToolFactory.CreateDefault(
                new FakeGameAdapter(commands, new FakeQueryService(), new FakeEventSource()),
                CreateDescriptor("haley"),
                traceIdFactory: () => "trace-move",
                idempotencyKeyFactory: () => "idem-move",
                runtimeDriver: driver,
                worldCoordination: coordination);
            var moveTool = tools.Single(tool => tool.Name == "stardew_move");

            var result = await moveTool.ExecuteAsync(new StardewMoveToolParameters
            {
                Destination = "haley_house.front_door"
            }, CancellationToken.None);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, commands.StatusCalls);
            var snapshot = driver.Snapshot();
            Assert.IsNull(snapshot.PendingWorkItem);
            Assert.IsNull(snapshot.ActionSlot);
            Assert.IsTrue(
                coordination.TryClaimMove("work-2", "penny", "trace-2", new ClaimedTile("HaleyHouse", 15, 8), null, "idem-2").Accepted,
                "Terminal failed/path_blocked must release the short claim so the next action does not get action_slot_busy.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task MoveTool_BindsRuntimeIdentityAndSubmitsThroughCommandService()
    {
        var commands = new CapturingCommandService();
        var tools = StardewNpcToolFactory.CreateDefault(
            new FakeGameAdapter(commands, new FakeQueryService(), new FakeEventSource()),
            CreateDescriptor("haley"),
            traceIdFactory: () => "trace-move",
            idempotencyKeyFactory: () => "idem-move");
        var moveTool = tools.Single(tool => tool.Name == "stardew_move");

        var result = await moveTool.ExecuteAsync(new StardewMoveToolParameters
        {
            Destination = "town.fountain",
            Reason = "inspect the town board"
        }, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(commands.LastAction);
        Assert.AreEqual("haley", commands.LastAction.NpcId);
        Assert.AreEqual("Haley", commands.LastAction.BodyBinding?.TargetEntityId);
        Assert.AreEqual("Haley", commands.LastAction.BodyBinding?.SmapiName);
        Assert.AreEqual("stardew-valley", commands.LastAction.GameId);
        Assert.AreEqual(GameActionType.Move, commands.LastAction.Type);
        Assert.AreEqual("trace-move", commands.LastAction.TraceId);
        Assert.AreEqual("idem-move", commands.LastAction.IdempotencyKey);
        Assert.AreEqual("destination", commands.LastAction.Target.Kind);
        Assert.IsNull(commands.LastAction.Target.LocationName);
        Assert.IsNull(commands.LastAction.Target.Tile);
        Assert.AreEqual("town.fountain", commands.LastAction.Payload?["destinationId"]?.ToString());
        Assert.AreEqual("cmd-1", JsonDocument.Parse(result.Content).RootElement.GetProperty("commandId").GetString());
    }

    [TestMethod]
    public async Task MoveTool_WithThoughtPassesMoveThoughtToBridgePayload()
    {
        var commands = new CapturingCommandService();
        var tools = StardewNpcToolFactory.CreateDefault(
            new FakeGameAdapter(commands, new FakeQueryService(), new FakeEventSource()),
            CreateDescriptor("haley"),
            traceIdFactory: () => "trace-move",
            idempotencyKeyFactory: () => "idem-move");
        var moveTool = tools.Single(tool => tool.Name == "stardew_move");

        var result = await moveTool.ExecuteAsync(new StardewMoveToolParameters
        {
            Destination = "town.fountain",
            Reason = "inspect the fountain",
            Thought = "喷泉边的光线正好。"
        }, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(commands.LastAction);
        Assert.AreEqual("喷泉边的光线正好。", commands.LastAction.Payload?["thought"]?.ToString());
    }

    [TestMethod]
    public async Task MoveTool_WhenDestinationIdIsProvided_SubmitsWithoutObservationOrCoordinateTarget()
    {
        var commands = new CapturingCommandService();
        var queries = new FakeQueryService(
        [
            "destination[0]=label=Town fountain,locationName=Town,x=42,y=17,tags=public|photogenic,reason=stand somewhere bright and visible in town,destinationId=town.fountain,facingDirection=2"
        ]);
        var tools = StardewNpcToolFactory.CreateDefault(
            new FakeGameAdapter(commands, queries, new FakeEventSource()),
            CreateDescriptor("haley"),
            traceIdFactory: () => "trace-move",
            idempotencyKeyFactory: () => "idem-move");
        var moveTool = tools.Single(tool => tool.Name == "stardew_move");

        var result = await moveTool.ExecuteAsync(new StardewMoveToolParameters
        {
            Destination = "town.fountain",
            Reason = "inspect the fountain"
        }, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(commands.LastAction);
        Assert.AreEqual(0, queries.ObserveCalls, "Move tool must not re-observe or parse destination facts.");
        Assert.AreEqual("destination", commands.LastAction.Target.Kind);
        Assert.IsNull(commands.LastAction.Target.LocationName);
        Assert.IsNull(commands.LastAction.Target.Tile);
        Assert.AreEqual("town.fountain", commands.LastAction.Payload?["destinationId"]?.ToString());
    }

    [TestMethod]
    public async Task MoveTool_WhenLabelIsProvided_SubmitsItAsDestinationIdAndLetsBridgeReject()
    {
        var commands = new CapturingCommandService(submitResult: new GameCommandResult(
            false,
            "",
            StardewCommandStatuses.Failed,
            "invalid_destination_id",
            "trace-move"));
        var tools = StardewNpcToolFactory.CreateDefault(
            new FakeGameAdapter(commands, new FakeQueryService(), new FakeEventSource()),
            CreateDescriptor("haley"),
            traceIdFactory: () => "trace-move",
            idempotencyKeyFactory: () => "idem-move");
        var moveTool = tools.Single(tool => tool.Name == "stardew_move");

        var result = await moveTool.ExecuteAsync(new StardewMoveToolParameters
        {
            Destination = "Town fountain",
            Reason = "label should not be locally resolved"
        }, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(commands.LastAction);
        Assert.AreEqual("Town fountain", commands.LastAction.Payload?["destinationId"]?.ToString());
        Assert.IsNull(commands.LastAction.Target.LocationName);
        Assert.IsNull(commands.LastAction.Target.Tile);
        using var doc = JsonDocument.Parse(result.Content);
        Assert.AreEqual(false, doc.RootElement.GetProperty("accepted").GetBoolean());
        Assert.AreEqual(StardewCommandStatuses.Failed, doc.RootElement.GetProperty("status").GetString());
        Assert.AreEqual("invalid_destination_id", doc.RootElement.GetProperty("failureReason").GetString());
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

        StringAssert.Contains(speakTool.Description, "non-blocking");
        StringAssert.Contains(speakTool.Description, "overhead bubble");
        StringAssert.Contains(speakTool.Description, "phone message");
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
    public async Task MoveTool_PollsTaskStatusUntilTerminal()
    {
        var commands = new CapturingCommandService(
            [
                new GameCommandStatus("cmd-1", "haley", "move", StardewCommandStatuses.Running, 0.5, null, null),
                new GameCommandStatus("cmd-1", "haley", "move", StardewCommandStatuses.Completed, 1, null, null)
            ]);
        var tools = StardewNpcToolFactory.CreateDefault(
            new FakeGameAdapter(commands, new FakeQueryService(), new FakeEventSource()),
            CreateDescriptor("haley"),
            traceIdFactory: () => "trace-move",
            idempotencyKeyFactory: () => "idem-move");
        var moveTool = tools.Single(tool => tool.Name == "stardew_move");

        var result = await moveTool.ExecuteAsync(new StardewMoveToolParameters
        {
            Destination = "town.fountain"
        }, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, commands.StatusCalls);
        using var doc = JsonDocument.Parse(result.Content);
        Assert.AreEqual("cmd-1", doc.RootElement.GetProperty("commandId").GetString());
        Assert.AreEqual(StardewCommandStatuses.Completed, doc.RootElement.GetProperty("finalStatus").GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task MoveTool_WithRuntimeDriver_ReleasesClaimAfterTerminalStatus()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-tool-runtime-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var commands = new CapturingCommandService(
                [
                    new GameCommandStatus("cmd-1", "haley", "move", StardewCommandStatuses.Running, 0.5, null, null),
                    new GameCommandStatus("cmd-1", "haley", "move", StardewCommandStatuses.Completed, 1, null, null)
                ]);
            var supervisor = new NpcRuntimeSupervisor();
            var driver = await supervisor.GetOrCreateDriverAsync(CreateDescriptor("haley"), tempDir, CancellationToken.None);
            var coordination = new WorldCoordinationService(new ResourceClaimRegistry());
            var tools = StardewNpcToolFactory.CreateDefault(
                new FakeGameAdapter(commands, new FakeQueryService(), new FakeEventSource()),
                CreateDescriptor("haley"),
                traceIdFactory: () => "trace-move",
                idempotencyKeyFactory: () => "idem-move",
                runtimeDriver: driver,
                worldCoordination: coordination);
            var moveTool = tools.Single(tool => tool.Name == "stardew_move");

            var result = await moveTool.ExecuteAsync(new StardewMoveToolParameters
            {
                Destination = "town.fountain"
            }, CancellationToken.None);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, commands.StatusCalls);
            var snapshot = driver.Snapshot();
            Assert.IsNull(snapshot.PendingWorkItem);
            Assert.IsNull(snapshot.ActionSlot);
            Assert.IsTrue(
                coordination.TryClaimMove("work-2", "penny", "trace-2", new ClaimedTile("Town", 42, 17), null, "idem-2").Accepted,
                "Terminal completion must release the short claim so another NPC can use the same tile.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task MoveTool_WithRuntimeDriver_ReleasesClaimAfterInterruptedTerminalStatus()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-tool-interrupted-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var commands = new CapturingCommandService(
                [
                    new GameCommandStatus("cmd-1", "haley", "move", StardewCommandStatuses.Running, 0.5, null, null),
                    new GameCommandStatus("cmd-1", "haley", "move", "interrupted", 1, null, null)
                ]);
            var supervisor = new NpcRuntimeSupervisor();
            var driver = await supervisor.GetOrCreateDriverAsync(CreateDescriptor("haley"), tempDir, CancellationToken.None);
            var coordination = new WorldCoordinationService(new ResourceClaimRegistry());
            var tools = StardewNpcToolFactory.CreateDefault(
                new FakeGameAdapter(commands, new FakeQueryService(), new FakeEventSource()),
                CreateDescriptor("haley"),
                traceIdFactory: () => "trace-move",
                idempotencyKeyFactory: () => "idem-move",
                runtimeDriver: driver,
                worldCoordination: coordination);
            var moveTool = tools.Single(tool => tool.Name == "stardew_move");

            var result = await moveTool.ExecuteAsync(new StardewMoveToolParameters
            {
                Destination = "town.fountain"
            }, CancellationToken.None);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, commands.StatusCalls);
            var snapshot = driver.Snapshot();
            Assert.IsNull(snapshot.PendingWorkItem);
            Assert.IsNull(snapshot.ActionSlot);
            Assert.IsNotNull(snapshot.LastTerminalCommandStatus);
            Assert.AreEqual("cmd-1", snapshot.LastTerminalCommandStatus.CommandId);
            Assert.AreEqual(StardewCommandStatuses.Interrupted, snapshot.LastTerminalCommandStatus.Status);
            Assert.IsTrue(
                coordination.TryClaimMove("work-2", "penny", "trace-2", new ClaimedTile("Town", 42, 17), null, "idem-2").Accepted,
                "Interrupted terminal status must also release the short claim and clear runtime slots.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
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
    public async Task MoveTool_WithDestinationOnlyClaimDoesNotConflictWithLegacyTileClaim()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "hermes-stardew-tool-conflict-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var commands = new CapturingCommandService();
            var supervisor = new NpcRuntimeSupervisor();
            var driver = await supervisor.GetOrCreateDriverAsync(CreateDescriptor("haley"), tempDir, CancellationToken.None);
            var coordination = new WorldCoordinationService(new ResourceClaimRegistry());
            var reserved = coordination.TryClaimMove("work-existing", "penny", "trace-existing", new ClaimedTile("Town", 42, 17), null, "idem-existing");
            Assert.IsTrue(reserved.Accepted);
            var tools = StardewNpcToolFactory.CreateDefault(
                new FakeGameAdapter(commands, new FakeQueryService(), new FakeEventSource()),
                CreateDescriptor("haley"),
                traceIdFactory: () => "trace-move",
                idempotencyKeyFactory: () => "idem-move",
                runtimeDriver: driver,
                worldCoordination: coordination);
            var moveTool = tools.Single(tool => tool.Name == "stardew_move");

            var result = await moveTool.ExecuteAsync(new StardewMoveToolParameters
            {
                Destination = "town.fountain"
            }, CancellationToken.None);

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(commands.LastAction);
            var snapshot = driver.Snapshot();
            Assert.IsNotNull(snapshot.PendingWorkItem);
            Assert.IsNotNull(snapshot.ActionSlot);
            Assert.IsFalse(snapshot.NextWakeAtUtc.HasValue);
            using var doc = JsonDocument.Parse(result.Content);
            Assert.AreEqual(StardewCommandStatuses.Queued, doc.RootElement.GetProperty("status").GetString());
            Assert.IsTrue(
                coordination.TryClaimMove("work-2", "penny", "trace-2", new ClaimedTile("Town", 42, 17), null, "idem-2").Accepted is false,
                "The legacy tile claim remains reserved; destination-only move claims do not pretend to own resolved Bridge coordinates.");
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
