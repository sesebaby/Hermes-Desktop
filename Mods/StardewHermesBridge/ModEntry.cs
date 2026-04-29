namespace StardewHermesBridge;

using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewHermesBridge.Bridge;
using StardewHermesBridge.Dialogue;
using StardewHermesBridge.Logging;
using StardewHermesBridge.Ui;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

public sealed class ModEntry : Mod
{
    public const string FormalEntry = "npc_click_then_wait_original_then_custom";
    public const string DebugEntry = "npc_click_debug";

    private BridgeCommandQueue _commands = null!;
    private BridgeHttpHost _httpHost = null!;
    private BridgeStatusOverlay _overlay = null!;
    private BridgeDebugMenu _debugMenu = null!;
    private SmapiBridgeLogger _bridgeLogger = null!;
    private NpcDialogueClickRouter _clickRouter = null!;
    private NpcDialogueFlowService _dialogueFlow = null!;
    private NpcDialogueFlowState? _pendingDialogueFlow;

    public override void Entry(IModHelper helper)
    {
        _bridgeLogger = new SmapiBridgeLogger(helper.DirectoryPath, Monitor);
        _commands = new BridgeCommandQueue(_bridgeLogger);
        _overlay = new BridgeStatusOverlay();
        _debugMenu = new BridgeDebugMenu(_overlay);
        _httpHost = new BridgeHttpHost(_commands, _bridgeLogger);
        _clickRouter = new NpcDialogueClickRouter();
        _dialogueFlow = new NpcDialogueFlowService();

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

        if (_pendingDialogueFlow is null)
            return;

        var request = new NpcDialogueAdvanceRequest(
            GetActiveDialogueNpcName(),
            Game1.activeClickableMenu is DialogueBox,
            Game1.activeClickableMenu is DialogueBox { transitioning: true });
        var result = _dialogueFlow.Advance(_pendingDialogueFlow, request);
        _pendingDialogueFlow = result.State;

        if (result.OriginalDialogueObserved)
            _bridgeLogger.Write(SmapiBridgeLogger.OriginalDialogueObserved, _pendingDialogueFlow.NpcName, FormalEntry, FormalEntry, null, "observed", request.IsDialogueTransitioning ? "transitioning" : null);

        if (result.OriginalDialogueCompleted)
            _bridgeLogger.Write(SmapiBridgeLogger.OriginalDialogueCompleted, _pendingDialogueFlow.NpcName, FormalEntry, FormalEntry, null, "completed", request.IsDialogueTransitioning ? "transitioning" : null);

        if (!result.ShouldDisplayCustomDialogue)
            return;

        var npc = Game1.getCharacterFromName(_pendingDialogueFlow.NpcName, mustBeVillager: false, includeEventActors: false);
        if (npc is null)
        {
            _bridgeLogger.Write(SmapiBridgeLogger.CustomDialogueQueued, _pendingDialogueFlow.NpcName, FormalEntry, FormalEntry, null, "failed", "npc_not_found");
            _pendingDialogueFlow = null;
            return;
        }

        _bridgeLogger.Write(SmapiBridgeLogger.CustomDialogueQueued, npc.Name, FormalEntry, FormalEntry, null, "queued", null);
        Game1.DrawDialogue(npc, BuildCustomDialogueText(npc.Name));
        _bridgeLogger.Write(SmapiBridgeLogger.CustomDialogueDisplayed, npc.Name, FormalEntry, FormalEntry, null, "displayed", null);
        _pendingDialogueFlow = null;
    }

    private void OnWorldDraining(object? sender, EventArgs e)
    {
        _commands.Drain("day_transition");
        _overlay.SetBlockedReason("day_transition");
        _pendingDialogueFlow = null;
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        _commands.Clear();
        _overlay.SetBlockedReason("returned_to_title");
        _pendingDialogueFlow = null;
    }

    private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
    {
        _overlay.Draw(e.SpriteBatch);
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        _debugMenu.HandleButton(e.Button);

        if (!Context.IsWorldReady || Game1.currentLocation is null)
            return;

        var clickedNpcName = TryResolveClickedNpcName(e);
        var route = _clickRouter.Route(new NpcDialogueClickRouteRequest(
            e.Button == SButton.MouseLeft,
            clickedNpcName,
            Game1.activeClickableMenu is not null));

        if (!route.IsAccepted)
        {
            _bridgeLogger.Write(SmapiBridgeLogger.NpcClickRejected, clickedNpcName, FormalEntry, FormalEntry, null, "rejected", route.Reason);
            return;
        }

        _pendingDialogueFlow = _dialogueFlow.BeginFollowUp(route.NpcName!);
        _bridgeLogger.Write(SmapiBridgeLogger.NpcClickObserved, route.NpcName, FormalEntry, FormalEntry, null, "observed", null);
    }

    private string? TryResolveClickedNpcName(ButtonPressedEventArgs e)
    {
        if (Game1.currentLocation is null)
            return null;

        foreach (var character in Game1.currentLocation.characters)
        {
            if (!character.IsMonster && character.GetBoundingBox().Contains(e.Cursor.AbsolutePixels))
                return character.Name;
        }

        var npc = Game1.currentLocation.isCharacterAtTile(e.Cursor.Tile + new Vector2(0f, 1f))
                  ?? Game1.currentLocation.isCharacterAtTile(e.Cursor.GrabTile + new Vector2(0f, 1f));
        return npc?.Name;
    }

    private static string? GetActiveDialogueNpcName()
        => Game1.activeClickableMenu is DialogueBox && Game1.currentSpeaker is not null
            ? Game1.currentSpeaker.Name
            : null;

    private static string BuildCustomDialogueText(string npcName)
        => npcName switch
        {
            "Haley" => "Oh... you're still here? Fine. Just don't make this weird.",
            _ => $"{npcName} has something else to say."
        };

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
