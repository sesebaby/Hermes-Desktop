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
              "processId": 123
            }
            """);

        var discovery = new FileStardewBridgeDiscovery(path);

        var ok = discovery.TryReadLatest(out var snapshot, out var failure);

        Assert.IsTrue(ok, failure);
        Assert.IsNotNull(snapshot);
        Assert.AreEqual("127.0.0.1", snapshot.Options.Host);
        Assert.AreEqual(8746, snapshot.Options.Port);
        Assert.AreEqual("token-1", snapshot.Options.BridgeToken);
        Assert.AreEqual(123, snapshot.ProcessId);
        Assert.IsTrue(snapshot.Options.IsLoopbackOnly());
    }

    [TestMethod]
    public async Task SpeakAsync_SubmitsTypedSpeakActionThroughDiscoveredCommandService()
    {
        var discovery = new FakeDiscovery(new StardewBridgeDiscoverySnapshot(
            new StardewBridgeOptions { Host = "127.0.0.1", Port = 8745, BridgeToken = "token-2" },
            DateTimeOffset.Parse("2026-04-29T08:00:00Z"),
            456));
        var commandService = new FakeGameCommandService();
        StardewBridgeOptions? capturedOptions = null;
        var service = new StardewNpcDebugActionService(
            discovery,
            options =>
            {
                capturedOptions = options;
                return commandService;
            });

        var result = await service.SpeakAsync("Haley", "Hello from Hermes.", CancellationToken.None);

        Assert.IsTrue(result.Accepted);
        Assert.AreEqual("token-2", capturedOptions?.BridgeToken);
        Assert.IsNotNull(commandService.LastAction);
        Assert.AreEqual(GameActionType.Speak, commandService.LastAction.Type);
        Assert.AreEqual("Haley", commandService.LastAction.NpcId);
        Assert.AreEqual("stardew-valley", commandService.LastAction.GameId);
        Assert.AreEqual("player", commandService.LastAction.Target.Kind);
        Assert.AreEqual("Hello from Hermes.", commandService.LastAction.Payload?["text"]?.GetValue<string>());
        Assert.AreEqual("manual_debug", commandService.LastAction.Payload?["channel"]?.GetValue<string>());
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
