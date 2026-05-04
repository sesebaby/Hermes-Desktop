using System.Text.Json;
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
            new[] { "stardew_status", "stardew_move", "stardew_speak", "stardew_open_private_chat", "stardew_task_status" },
            tools.Select(tool => tool.Name).ToArray());

        Assert.IsFalse(tools.Any(tool => tool.Name is "agent" or "todo" or "memory" or "ask_user" or "schedule_cron"));

        var moveSchema = ((IToolSchemaProvider)tools.Single(tool => tool.Name == "stardew_move")).GetParameterSchema().GetRawText();
        Assert.IsFalse(moveSchema.Contains("npcId", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(moveSchema.Contains("saveId", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(moveSchema.Contains("traceId", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(moveSchema.Contains("idempotencyKey", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void MoveToolDescription_AllowsObservedPlaceCandidate()
    {
        var tools = StardewNpcToolFactory.CreateDefault(
            new FakeGameAdapter(new CapturingCommandService(), new FakeQueryService(), new FakeEventSource()),
            CreateDescriptor("haley"));
        var moveTool = (IToolSchemaProvider)tools.Single(tool => tool.Name == "stardew_move");
        var description = tools.Single(tool => tool.Name == "stardew_move").Description;
        var moveSchema = moveTool.GetParameterSchema().GetRawText();

        StringAssert.Contains(description, "moveCandidate");
        StringAssert.Contains(description, "placeCandidate");
        StringAssert.Contains(description, "endpoint candidate");
        StringAssert.Contains(description, "latest observation");
        StringAssert.Contains(description, "never invent coordinates");
        StringAssert.Contains(description, "route probing as bridge-owned rather than guaranteed by the tool");
        StringAssert.Contains(description, "path_blocked");
        StringAssert.Contains(description, "path_unreachable");
        StringAssert.Contains(description, "runtime binds npcId, saveId, traceId, and idempotency internally");
        Assert.IsFalse(
            description.Contains("route-guaranteed", StringComparison.OrdinalIgnoreCase),
            "placeCandidate facts are endpoint candidates; the bridge probes routes, but the tool contract must not promise route-guaranteed movement.");
        Assert.IsFalse(
            description.Contains("schedule-style", StringComparison.OrdinalIgnoreCase),
            "World-style destination semantics belong to stardew-world; the tool description should stay at the local executable contract.");

        StringAssert.Contains(GetSchemaPropertyDescription(moveSchema, "x"), "current moveCandidate or placeCandidate");
        StringAssert.Contains(GetSchemaPropertyDescription(moveSchema, "y"), "current moveCandidate or placeCandidate");
        StringAssert.Contains(GetSchemaPropertyDescription(moveSchema, "reason"), "copied from the current moveCandidate or placeCandidate");
        StringAssert.Contains(GetSchemaPropertyDescription(moveSchema, "facingDirection"), "copied from an endpoint placeCandidate");
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
                LocationName = "HaleyHouse",
                X = 15,
                Y = 8
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
            LocationName = "Town",
            X = 42,
            Y = 17,
            Reason = "inspect the town board",
            FacingDirection = 2
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
        Assert.AreEqual("Town", commands.LastAction.Target.LocationName);
        Assert.AreEqual(42, commands.LastAction.Target.Tile?.X);
        Assert.AreEqual("2", commands.LastAction.Payload?["facingDirection"]?.ToString());
        Assert.AreEqual("cmd-1", JsonDocument.Parse(result.Content).RootElement.GetProperty("commandId").GetString());
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
            LocationName = "Town",
            X = 42,
            Y = 17
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
                LocationName = "Town",
                X = 42,
                Y = 17
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
    public async Task MoveTool_WhenClaimConflicts_ReturnsBlockedWithoutSubmittingCommand()
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
                LocationName = "Town",
                X = 42,
                Y = 17
            }, CancellationToken.None);

            Assert.IsTrue(result.Success);
            Assert.IsNull(commands.LastAction);
            var snapshot = driver.Snapshot();
            Assert.IsNull(snapshot.PendingWorkItem);
            Assert.IsNull(snapshot.ActionSlot);
            Assert.IsTrue(snapshot.NextWakeAtUtc.HasValue);
            using var doc = JsonDocument.Parse(result.Content);
            Assert.AreEqual(StardewCommandStatuses.Blocked, doc.RootElement.GetProperty("status").GetString());
            Assert.AreEqual(StardewBridgeErrorCodes.CommandConflict, doc.RootElement.GetProperty("failureReason").GetString());
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

        public CapturingCommandService(IReadOnlyList<GameCommandStatus>? statusSequence = null)
        {
            _statusSequence = new Queue<GameCommandStatus>(statusSequence ?? []);
        }

        public GameAction? LastAction { get; private set; }

        public int StatusCalls { get; private set; }

        public Task<GameCommandResult> SubmitAsync(GameAction action, CancellationToken ct)
        {
            LastAction = action;
            return Task.FromResult(new GameCommandResult(true, "cmd-1", StardewCommandStatuses.Queued, null, action.TraceId));
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
        public Task<GameObservation> ObserveAsync(string npcId, CancellationToken ct)
            => Task.FromResult(new GameObservation(npcId, "stardew-valley", DateTime.UtcNow, "status", []));

        public Task<WorldSnapshot> GetWorldSnapshotAsync(string npcId, CancellationToken ct)
            => Task.FromResult(new WorldSnapshot("stardew-valley", "save-1", DateTime.UtcNow, [], []));
    }

    private sealed class FakeEventSource : IGameEventSource
    {
        public Task<IReadOnlyList<GameEventRecord>> PollAsync(GameEventCursor cursor, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<GameEventRecord>>([]);
    }
}
