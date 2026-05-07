using System.Text.Json.Nodes;
using Hermes.Agent.Game;
using Hermes.Agent.Games.Stardew;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Stardew;

[TestClass]
public sealed class StardewManualActionServiceTests
{
    [TestMethod]
    public void TryReadLatest_MapsDiscoveryFileToLoopbackBridgeOptions()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stardew-bridge-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            {
              "host": "127.0.0.1",
              "port": 8746,
              "bridgeToken": "token-1",
              "startedAtUtc": "2026-04-29T08:00:00Z",
              "processId": %PROCESS_ID%,
              "saveId": "farm-main"
            }
            """.Replace("%PROCESS_ID%", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        var discovery = new FileStardewBridgeDiscovery(path);

        var ok = discovery.TryReadLatest(out var snapshot, out var failure);

        Assert.IsTrue(ok, failure);
        Assert.IsNotNull(snapshot);
        Assert.AreEqual("127.0.0.1", snapshot.Options.Host);
        Assert.AreEqual(8746, snapshot.Options.Port);
        Assert.AreEqual("token-1", snapshot.Options.BridgeToken);
        Assert.AreEqual(Environment.ProcessId, snapshot.ProcessId);
        Assert.AreEqual("farm-main", snapshot.SaveId);
        Assert.IsTrue(snapshot.Options.IsLoopbackOnly());
    }

    [TestMethod]
    public void TryReadLatest_WhenDiscoveryProcessIdIsDead_ReturnsStaleDiscovery()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stardew-bridge-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            {
              "host": "127.0.0.1",
              "port": 8746,
              "bridgeToken": "token-1",
              "startedAtUtc": "2026-04-29T08:00:00Z",
              "processId": 2147483647,
              "saveId": "farm-main"
            }
            """);

        var discovery = new FileStardewBridgeDiscovery(path);

        var ok = discovery.TryReadLatest(out var snapshot, out var failure);

        Assert.IsFalse(ok);
        Assert.IsNull(snapshot);
        Assert.AreEqual(StardewBridgeErrorCodes.BridgeStaleDiscovery, failure);
    }

    [TestMethod]
    public async Task SpeakAsync_SubmitsTypedSpeakActionThroughDiscoveredCommandService()
    {
        var discovery = new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
            new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token-2" },
            DateTimeOffset.Parse("2026-04-29T08:00:00Z"),
            456,
            "save-9"));
        var commandService = new FakeGameCommandService();
        StardewBridgeDiscoverySnapshot? capturedSnapshot = null;
        var service = new StardewNpcDebugActionService(
            discovery,
            snapshot =>
            {
                capturedSnapshot = snapshot;
                return commandService;
            });

        var result = await service.SpeakAsync("Haley", "Hello from Hermes.", CancellationToken.None);

        Assert.IsTrue(result.Accepted);
        Assert.AreEqual("token-2", capturedSnapshot?.Options.BridgeToken);
        Assert.AreEqual("save-9", capturedSnapshot?.SaveId);
        Assert.IsNotNull(commandService.LastAction);
        Assert.AreEqual(GameActionType.Speak, commandService.LastAction.Type);
        Assert.AreEqual("Haley", commandService.LastAction.NpcId);
        Assert.AreEqual("stardew-valley", commandService.LastAction.GameId);
        Assert.AreEqual("player", commandService.LastAction.Target.Kind);
        Assert.AreEqual("Hello from Hermes.", commandService.LastAction.Payload?["text"]?.GetValue<string>());
        Assert.AreEqual("manual_debug", commandService.LastAction.Payload?["channel"]?.GetValue<string>());
    }

    [TestMethod]
    public async Task SpeakAsync_WithBodyBinding_SubmitsTargetEntityIdToBridgeAction()
    {
        var discovery = new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
            new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token-2" },
            DateTimeOffset.Parse("2026-04-29T08:00:00Z"),
            456,
            "save-9"));
        var commandService = new FakeGameCommandService();
        var service = new StardewNpcDebugActionService(discovery, _ => commandService);
        var bodyBinding = new NpcBodyBinding("haley", "Haley", "Haley", "海莉", "stardew");

        var result = await service.SpeakAsync(bodyBinding, "Hello from Hermes.", CancellationToken.None);

        Assert.IsTrue(result.Accepted);
        Assert.IsNotNull(commandService.LastAction);
        Assert.AreEqual("haley", commandService.LastAction.NpcId);
        Assert.AreEqual("Haley", commandService.LastAction.BodyBinding?.TargetEntityId);
    }

    [TestMethod]
    public async Task SpeakAsync_MissingSaveIdInDiscoveryFailsInsteadOfGuessingManualDebug()
    {
        var discovery = new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
            new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token-2" },
            DateTimeOffset.Parse("2026-04-29T08:00:00Z"),
            456,
            null));
        var service = new StardewNpcDebugActionService(discovery, _ => throw new AssertFailedException("Factory should not run without saveId."));

        var result = await service.SpeakAsync("Haley", "Hello from Hermes.", CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual(StardewBridgeErrorCodes.BridgeStaleDiscovery, result.FailureReason);
    }

    [TestMethod]
    public async Task RepositionToTownAsync_WithValidDiscovery_SubmitsDebugRepositionAction()
    {
        var discovery = new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
            new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token-2" },
            DateTimeOffset.Parse("2026-04-29T08:00:00Z"),
            456,
            "save-9"));
        var commandService = new FakeGameCommandService();
        var service = new StardewNpcDebugActionService(discovery, _ => commandService);

        var result = await service.RepositionToTownAsync("haley", CancellationToken.None);

        Assert.IsTrue(result.Accepted);
        Assert.IsNotNull(commandService.LastAction);
        Assert.AreEqual(GameActionType.DebugReposition, commandService.LastAction.Type);
        Assert.AreEqual("haley", commandService.LastAction.NpcId);
        Assert.AreEqual("stardew-valley", commandService.LastAction.GameId);
        Assert.AreEqual("debug_reposition", commandService.LastAction.Target.Kind);
        Assert.AreEqual("town", commandService.LastAction.Payload?["target"]?.GetValue<string>());
    }

    [TestMethod]
    public async Task RepositionToTownAsync_WithBodyBinding_SubmitsTargetEntityIdToBridgeAction()
    {
        var discovery = new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
            new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token-2" },
            DateTimeOffset.Parse("2026-04-29T08:00:00Z"),
            456,
            "save-9"));
        var commandService = new FakeGameCommandService();
        var service = new StardewNpcDebugActionService(discovery, _ => commandService);
        var bodyBinding = new NpcBodyBinding("haley", "Haley", "Haley", "海莉", "stardew");

        var result = await service.RepositionToTownAsync(bodyBinding, CancellationToken.None);

        Assert.IsTrue(result.Accepted);
        Assert.IsNotNull(commandService.LastAction);
        Assert.AreEqual(GameActionType.DebugReposition, commandService.LastAction.Type);
        Assert.AreEqual("haley", commandService.LastAction.NpcId);
        Assert.AreEqual("Haley", commandService.LastAction.BodyBinding?.TargetEntityId);
    }

    [TestMethod]
    public async Task SendHaleyToBeachAsync_SubmitsSkillBeachTargetForHaley()
    {
        var discovery = new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
            new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token-2" },
            DateTimeOffset.Parse("2026-04-29T08:00:00Z"),
            456,
            "save-9"));
        var commandService = new FakeGameCommandService();
        var service = new StardewNpcDebugActionService(discovery, _ => commandService);
        var result = await service.SendHaleyToBeachAsync(CancellationToken.None);

        Assert.IsTrue(result.Accepted);
        Assert.IsNotNull(commandService.LastAction);
        Assert.AreEqual(GameActionType.Move, commandService.LastAction.Type);
        Assert.AreEqual("haley", commandService.LastAction.NpcId);
        Assert.AreEqual("Haley", commandService.LastAction.BodyBinding?.TargetEntityId);
        Assert.AreEqual("tile", commandService.LastAction.Target.Kind);
        Assert.AreEqual("Beach", commandService.LastAction.Target.LocationName);
        Assert.IsNotNull(commandService.LastAction.Target.Tile);
        Assert.AreEqual(20, commandService.LastAction.Target.Tile.X);
        Assert.AreEqual(35, commandService.LastAction.Target.Tile.Y);
        Assert.AreEqual(2, (int?)commandService.LastAction.Payload?["facingDirection"]);
        Assert.IsFalse(commandService.LastAction.Payload?.ContainsKey("destinationId") is true);
    }

    [TestMethod]
    public async Task RepositionToTownAsync_WhenBridgeUnavailable_ReturnsDiscoveryFailure()
    {
        var service = new StardewNpcDebugActionService(
            new FakeDiscovery(null),
            _ => throw new AssertFailedException("Factory should not run without bridge discovery."));

        var result = await service.RepositionToTownAsync("haley", CancellationToken.None);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual("missing", result.FailureReason);
    }

    private sealed class FakeDiscovery : IStardewBridgeDiscovery
    {
        private readonly StardewBridgeDiscoverySnapshot? _snapshot;

        public FakeDiscovery(StardewBridgeDiscoverySnapshot? snapshot)
        {
            _snapshot = snapshot;
        }

        public string DiscoveryFilePath => "fake-discovery.json";

        public bool TryReadLatest(out StardewBridgeDiscoverySnapshot? snapshot, out string? failureReason)
        {
            snapshot = _snapshot;
            failureReason = _snapshot is null ? "missing" : null;
            return _snapshot is not null;
        }
    }

    private sealed class FakeGameCommandService : IGameCommandService
    {
        public GameAction? LastAction { get; private set; }

        public Task<GameCommandResult> SubmitAsync(GameAction action, CancellationToken ct)
        {
            LastAction = action;
            return Task.FromResult(new GameCommandResult(
                true,
                "cmd-speak",
                StardewCommandStatuses.Completed,
                null,
                action.TraceId));
        }

        public Task<GameCommandStatus> GetStatusAsync(string commandId, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<GameCommandStatus> CancelAsync(string commandId, string reason, CancellationToken ct)
            => throw new NotSupportedException();
    }
}
