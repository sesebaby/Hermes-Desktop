namespace StardewHermesBridge;

using System.Text.Json;
using StardewHermesBridge.Bridge;
using StardewHermesBridge.Logging;
using StardewHermesBridge.Ui;
using StardewModdingAPI;
using StardewModdingAPI.Events;

public sealed class ModEntry : Mod
{
    private BridgeCommandQueue _commands = null!;
    private BridgeHttpHost _httpHost = null!;
    private BridgeStatusOverlay _overlay = null!;
    private BridgeDebugMenu _debugMenu = null!;
    private SmapiBridgeLogger _bridgeLogger = null!;

    public override void Entry(IModHelper helper)
    {
        _bridgeLogger = new SmapiBridgeLogger(helper.DirectoryPath, Monitor);
        _commands = new BridgeCommandQueue(_bridgeLogger);
        _overlay = new BridgeStatusOverlay();
        _debugMenu = new BridgeDebugMenu(_overlay);
        _httpHost = new BridgeHttpHost(_commands, _bridgeLogger);

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.GameLoop.DayEnding += OnWorldDraining;
        helper.Events.GameLoop.Saving += OnWorldDraining;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.Display.RenderedHud += OnRenderedHud;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        _httpHost.Start("127.0.0.1", preferredPort: 8745);
        _overlay.SetBridgeOnline(_httpHost.Port, _httpHost.BridgeToken);
        WriteDiscoveryFile();
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        var status = _commands.PumpOneTick();
        if (status is not null)
            _overlay.SetLastRequest(status.NpcId, status.Action, status.TraceId, status.Status, status.BlockedReason);
    }

    private void OnWorldDraining(object? sender, EventArgs e)
    {
        _commands.Drain("day_transition");
        _overlay.SetBlockedReason("day_transition");
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        _commands.Clear();
        _overlay.SetBlockedReason("returned_to_title");
    }

    private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
    {
        _overlay.Draw(e.SpriteBatch);
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        _debugMenu.HandleButton(e.Button);
    }

    private void WriteDiscoveryFile()
    {
        try
        {
            var path = GetDiscoveryFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var document = new BridgeDiscoveryDocument(
                "127.0.0.1",
                _httpHost.Port,
                _httpHost.BridgeToken,
                DateTimeOffset.UtcNow,
                Environment.ProcessId);
            File.WriteAllText(path, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));
            _bridgeLogger.Write("bridge_discovery_written", null, "bridge", "bridge", null, "online", path);
        }
        catch (Exception ex)
        {
            _bridgeLogger.Write("bridge_discovery_failed", null, "bridge", "bridge", null, "failed", ex.Message);
        }
    }

    private static string GetDiscoveryFilePath()
        => Path.Combine(GetHermesHomePath(), "hermes-cs", "stardew-bridge.json");

    private static string GetHermesHomePath()
        => Environment.GetEnvironmentVariable("HERMES_HOME") is { Length: > 0 } configuredHome
            ? configuredHome
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "hermes");

    private sealed record BridgeDiscoveryDocument(
        string Host,
        int Port,
        string BridgeToken,
        DateTimeOffset StartedAtUtc,
        int ProcessId);
}
