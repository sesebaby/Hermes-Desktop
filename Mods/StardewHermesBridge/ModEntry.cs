namespace StardewHermesBridge;

using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewHermesBridge.Bridge;
using StardewHermesBridge.Commands;
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
    private BridgeEventBuffer _events = null!;
    private BridgeHttpHost _httpHost = null!;
    private BridgeStatusOverlay _overlay = null!;
    private HermesPhoneState _phoneState = null!;
    private HermesPhoneOverlay _phoneOverlay = null!;
    private NpcOverheadBubbleOverlay _bubbleOverlay = null!;
    private StardewMessageDisplayRouter _messageRouter = null!;
    private BridgeDebugMenu _debugMenu = null!;
    private SmapiBridgeLogger _bridgeLogger = null!;
    private NpcDialogueClickRouter _clickRouter = null!;
    private NpcDialogueFlowService _dialogueFlow = null!;
    private NpcDialogueMenuGuard _menuGuard = null!;
    private NpcOriginalDialogueStarter _originalDialogueStarter = null!;
    private TestTeleportCommand _testTeleportCommand = null!;
    private NpcDialogueFlowState? _pendingDialogueFlow;
    private int _pendingDialogueTicks;
    private SButton? _pendingOriginalStartButton;
    private bool _originalStartRetryAttempted;
    private DateTimeOffset? _bridgeStartedAtUtc;
    private PendingPrivateChatReplyDialogue? _pendingPrivateChatReplyDialogue;

    public override void Entry(IModHelper helper)
    {
        _bridgeLogger = new SmapiBridgeLogger(helper.DirectoryPath, Monitor);
        _events = new BridgeEventBuffer();
        _overlay = new BridgeStatusOverlay();
        _phoneState = new HermesPhoneState();
        _phoneOverlay = new HermesPhoneOverlay(_phoneState, _events, _bridgeLogger, Helper, npcName => _overlay.SetPrivateChatThinking(npcName));
        _bubbleOverlay = new NpcOverheadBubbleOverlay(_events, _bridgeLogger);
        _messageRouter = new StardewMessageDisplayRouter(_phoneState, _bubbleOverlay, _phoneOverlay, _events, _bridgeLogger);
        _commands = new BridgeCommandQueue(
            _bridgeLogger,
            _events,
            _phoneState,
            _messageRouter,
            _bubbleOverlay,
            _ => _overlay.ClearPrivateChatPending(),
            npcName => _overlay.SetPrivateChatThinking(npcName),
            (npcName, conversationId, route) =>
            {
                _overlay.ClearPrivateChatPending();
                if (string.Equals(route, "dialogue", StringComparison.OrdinalIgnoreCase))
                    _pendingPrivateChatReplyDialogue = new PendingPrivateChatReplyDialogue(npcName, conversationId);
            });
        _debugMenu = new BridgeDebugMenu(_overlay);
        _httpHost = new BridgeHttpHost(_commands, _events, _bridgeLogger);
        _clickRouter = new NpcDialogueClickRouter();
        _dialogueFlow = new NpcDialogueFlowService();
        _menuGuard = new NpcDialogueMenuGuard();
        _originalDialogueStarter = new NpcOriginalDialogueStarter(helper);
        _testTeleportCommand = new TestTeleportCommand(helper, Monitor);

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.GameLoop.DayEnding += OnWorldDraining;
        helper.Events.GameLoop.Saving += OnWorldDraining;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.Display.MenuChanged += OnMenuChanged;
        helper.Events.Display.RenderedHud += OnRenderedHud;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        _httpHost.Start("127.0.0.1", preferredPort: 8745);
        _overlay.SetBridgeOnline(_httpHost.Port, _httpHost.BridgeToken);
        _bridgeStartedAtUtc = DateTimeOffset.UtcNow;
        WriteDiscoveryFile();
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        _bridgeStartedAtUtc = DateTimeOffset.UtcNow;
        WriteDiscoveryFile();
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
        => WriteDiscoveryFile();

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
            Game1.activeClickableMenu is not null,
            Game1.activeClickableMenu is DialogueBox { transitioning: true });
        var result = _dialogueFlow.Advance(_pendingDialogueFlow, request);
        _pendingDialogueFlow = result.State;
        _pendingDialogueTicks++;

        if (result.OriginalDialogueObserved)
            _bridgeLogger.Write(SmapiBridgeLogger.OriginalDialogueObserved, _pendingDialogueFlow.NpcName, FormalEntry, FormalEntry, null, "observed", request.IsDialogueTransitioning ? "transitioning" : null);

        if (result.OriginalDialogueCompleted)
            _bridgeLogger.Write(SmapiBridgeLogger.OriginalDialogueCompleted, _pendingDialogueFlow.NpcName, FormalEntry, FormalEntry, null, "completed", request.IsDialogueTransitioning ? "transitioning" : null);

        if (result.ShouldRecordVanillaDialogueCompleted)
        {
            RecordVanillaDialogueCompleted(_pendingDialogueFlow.NpcName, null);
            ClearPendingDialogueFlow();
            return;
        }

        if (!result.ShouldDisplayCustomDialogue)
        {
            if (TryStartOriginalDialogueIfNeeded())
                return;

            if (!_pendingDialogueFlow.OriginalDialogueObserved && _pendingDialogueTicks > 30 && Game1.activeClickableMenu is null)
            {
                var npcName = _pendingDialogueFlow.NpcName;
                _bridgeLogger.Write(SmapiBridgeLogger.OriginalDialogueCompleted, npcName, FormalEntry, FormalEntry, null, "timeout", "original_not_observed_timeout");
                RecordDialogueFollowUpUnavailable(npcName, "original_not_observed_timeout");
                ClearPendingDialogueFlow();
            }

            return;
        }

        RecordVanillaDialogueCompleted(_pendingDialogueFlow.NpcName, "legacy_custom_display_suppressed");
        ClearPendingDialogueFlow();
    }

    private void OnWorldDraining(object? sender, EventArgs e)
    {
        _commands.Drain("day_transition");
        _overlay.SetBlockedReason("day_transition");
        _overlay.ClearPrivateChatPending();
        _phoneOverlay.ClosePhone();
        ClearPendingDialogueFlow();
        ClearCustomDialogueGuards();
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        _commands.Clear();
        _overlay.SetBlockedReason("returned_to_title");
        _overlay.ClearPrivateChatPending();
        _phoneOverlay.ClosePhone();
        ClearPendingDialogueFlow();
        ClearCustomDialogueGuards();
        WriteDiscoveryFile();
    }

    private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
    {
        _overlay.Draw(e.SpriteBatch);
        _bubbleOverlay.Draw(e.SpriteBatch);
        _phoneOverlay.Draw(e.SpriteBatch);
    }

    private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.eventUp)
            return;

        var oldDialogueNpcName = GetDialogueNpcName(e.OldMenu);
        var newDialogueNpcName = GetDialogueNpcName(e.NewMenu);

        if (_menuGuard.ConsumeMenuChange(oldDialogueNpcName, newDialogueNpcName) is not NpcDialogueMenuGuardResult.Unhandled)
            return;

        if (_pendingPrivateChatReplyDialogue is { } pendingReply)
        {
            if (string.Equals(newDialogueNpcName, pendingReply.NpcName, StringComparison.OrdinalIgnoreCase))
            {
                _bridgeLogger.Write("private_chat_reply_dialogue_observed", pendingReply.NpcName, FormalEntry, FormalEntry, null, "observed", pendingReply.ConversationId);
                return;
            }

            if (string.Equals(oldDialogueNpcName, pendingReply.NpcName, StringComparison.OrdinalIgnoreCase) && newDialogueNpcName is null)
            {
                RecordPrivateChatReplyClosedFromInputDialogue(pendingReply);
                _pendingPrivateChatReplyDialogue = null;
                return;
            }
        }

        if (e.OldMenu is not null && e.NewMenu is null)
            _commands.RecordPrivateChatInputClosedWithoutSubmit();

        if (_pendingDialogueFlow is null)
            return;

        if (!string.Equals(newDialogueNpcName, _pendingDialogueFlow.NpcName, StringComparison.OrdinalIgnoreCase) ||
            !IsEnabledDialogueNpc(newDialogueNpcName))
        {
            return;
        }

        BeginOrObserveOriginalDialogue(newDialogueNpcName!, "menu_changed_open");
        _bridgeLogger.Write(SmapiBridgeLogger.OriginalDialogueObserved, newDialogueNpcName, FormalEntry, FormalEntry, null, "observed", "source=menu_changed_open");
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        _debugMenu.HandleButton(e.Button);
        if (_phoneOverlay.HandleButtonPressed(e.Button, Helper.Input))
            return;

        if (!Context.IsWorldReady || Game1.currentLocation is null)
            return;

        var clickedNpc = TryResolveClickedNpc(e);
        var clickedNpcName = clickedNpc?.Name;
        var isActionButton = e.Button.IsActionButton();
        var isUseToolButton = e.Button.IsUseToolButton();
        var isMouseButton = IsMouseButton(e.Button);
        var isDialogueBoxOpen = Game1.activeClickableMenu is DialogueBox;
        var activeDialogueNpcName = GetActiveDialogueNpcName();
        if (_menuGuard.IsCustomDialogue(activeDialogueNpcName))
            return;

        var route = _clickRouter.Route(new NpcDialogueClickRouteRequest(
            isActionButton,
            isUseToolButton,
            isMouseButton,
            clickedNpcName,
            Game1.activeClickableMenu is not null,
            isDialogueBoxOpen,
            activeDialogueNpcName));

        if (!route.IsAccepted)
        {
            if (ShouldLogNpcClickRejected(route.Reason))
                _bridgeLogger.Write(SmapiBridgeLogger.NpcClickRejected, clickedNpcName, FormalEntry, FormalEntry, null, "rejected", BuildInputRejectDetail(e, route.Reason, isActionButton, isUseToolButton, isMouseButton, activeDialogueNpcName));

            return;
        }

        BeginOrObserveOriginalDialogue(route.NpcName!, isDialogueBoxOpen ? "already_open" : "vanilla_pending", isDialogueBoxOpen ? null : e.Button);
        _bridgeLogger.Write(SmapiBridgeLogger.NpcClickObserved, route.NpcName, FormalEntry, FormalEntry, null, "observed", $"{BuildInputAcceptedDetail(e, isActionButton, isUseToolButton, isMouseButton)};original_start={(isDialogueBoxOpen ? "already_open" : "vanilla_pending")}");
    }

    private static string BuildInputRejectDetail(ButtonPressedEventArgs e, string reason, bool isActionButton, bool isUseToolButton, bool isMouseButton, string? activeDialogueNpcName)
        => $"{reason};button={e.Button};is_action={isActionButton};is_use_tool={isUseToolButton};is_mouse={isMouseButton};active_menu={Game1.activeClickableMenu?.GetType().Name ?? "-"};active_dialogue_npc={activeDialogueNpcName ?? "-"};action_bindings={FormatButtons(Game1.options.actionButton)};use_tool_bindings={FormatButtons(Game1.options.useToolButton)}";

    private static string BuildInputAcceptedDetail(ButtonPressedEventArgs e, bool isActionButton, bool isUseToolButton, bool isMouseButton)
        => $"button={e.Button};is_action={isActionButton};is_use_tool={isUseToolButton};is_mouse={isMouseButton}";

    private static bool ShouldLogNpcClickRejected(string reason)
        => !string.Equals(reason, "unsupported_button", StringComparison.Ordinal);

    private void DisplayCustomDialogue(string npcName, string? detail)
    {
        var npc = Game1.getCharacterFromName(npcName, mustBeVillager: false, includeEventActors: false);
        if (npc is null)
        {
            _bridgeLogger.Write(SmapiBridgeLogger.CustomDialogueQueued, npcName, FormalEntry, FormalEntry, null, "failed", AppendDetail(detail, "npc_not_found"));
            return;
        }

        _bridgeLogger.Write(SmapiBridgeLogger.CustomDialogueQueued, npc.Name, FormalEntry, FormalEntry, null, "queued", detail);
        _menuGuard.MarkCustomDialogueOpening(npc.Name);
        NpcRawDialogueRenderer.Display(npc, BuildCustomDialogueText(npc.Name));
        _bridgeLogger.Write(SmapiBridgeLogger.CustomDialogueDisplayed, npc.Name, FormalEntry, FormalEntry, null, "displayed", detail);
    }

    private void RecordVanillaDialogueCompleted(string npcName, string? detail)
    {
        var suffix = string.IsNullOrWhiteSpace(detail) ? "" : $" ({detail})";
        _overlay.SetPrivateChatPending(npcName);
        _events.Record(
            "vanilla_dialogue_completed",
            npcName,
            $"{npcName} vanilla dialogue completed{suffix}.");
        _bridgeLogger.Write("vanilla_dialogue_completed_fact", npcName, FormalEntry, FormalEntry, null, "recorded", detail);
    }

    private void RecordDialogueFollowUpUnavailable(string npcName, string detail)
    {
        _events.Record(
            "vanilla_dialogue_unavailable",
            npcName,
            $"{npcName} vanilla dialogue follow-up was unavailable ({detail}).");
        _bridgeLogger.Write("vanilla_dialogue_unavailable_fact", npcName, FormalEntry, FormalEntry, null, "recorded", detail);
    }

    private void RecordPrivateChatReplyClosedFromInputDialogue(PendingPrivateChatReplyDialogue pending)
    {
        _events.Record(
            "private_chat_reply_closed",
            pending.NpcName,
            $"{pending.NpcName} private chat reply closed.",
            pending.ConversationId,
            new System.Text.Json.Nodes.JsonObject
            {
                ["conversationId"] = pending.ConversationId,
                ["reply_closed_source"] = "input_menu_dialogue_closed"
            });
        _bridgeLogger.Write("private_chat_reply_closed_fact", pending.NpcName, FormalEntry, FormalEntry, null, "recorded", "input_menu_dialogue_closed");
    }

    private bool TryStartOriginalDialogueIfNeeded()
    {
        if (_pendingDialogueFlow is not { } pending ||
            pending.OriginalDialogueObserved ||
            _originalStartRetryAttempted ||
            _pendingOriginalStartButton is not { } triggerButton ||
            _pendingDialogueTicks < 2 ||
            Game1.activeClickableMenu is not null)
        {
            return false;
        }

        _originalStartRetryAttempted = true;
        var npc = Game1.getCharacterFromName(pending.NpcName, mustBeVillager: false, includeEventActors: false);
        if (npc is null)
        {
            _bridgeLogger.Write(SmapiBridgeLogger.NpcClickRejected, pending.NpcName, FormalEntry, FormalEntry, null, "rejected", "manual_original_start_failed;npc_not_found;fallback_custom=true");
            RecordDialogueFollowUpUnavailable(pending.NpcName, "manual_original_start_failed;npc_not_found");
            ClearPendingDialogueFlow();
            return true;
        }

        if (!_originalDialogueStarter.TryStart(npc, triggerButton))
        {
            _bridgeLogger.Write(SmapiBridgeLogger.NpcClickRejected, pending.NpcName, FormalEntry, FormalEntry, null, "rejected", "manual_original_start_failed;fallback_custom=true");
            RecordDialogueFollowUpUnavailable(pending.NpcName, "manual_original_start_failed");
            ClearPendingDialogueFlow();
            return true;
        }

        _bridgeLogger.Write(SmapiBridgeLogger.NpcClickObserved, pending.NpcName, FormalEntry, FormalEntry, null, "observed", "original_start=manual_check_action");

        if (Game1.activeClickableMenu is DialogueBox && string.Equals(GetActiveDialogueNpcName(), pending.NpcName, StringComparison.OrdinalIgnoreCase))
        {
            _pendingDialogueFlow = _dialogueFlow.BeginObservedOriginal(pending.NpcName);
            _bridgeLogger.Write(SmapiBridgeLogger.OriginalDialogueObserved, pending.NpcName, FormalEntry, FormalEntry, null, "observed", "source=manual_check_action");
        }

        return true;
    }

    private void BeginOrObserveOriginalDialogue(string npcName, string source, SButton? triggerButton = null)
    {
        if (_pendingDialogueFlow is { } pending && string.Equals(pending.NpcName, npcName, StringComparison.OrdinalIgnoreCase))
        {
            if (source is "already_open" or "menu_changed_open" && !pending.OriginalDialogueObserved)
                _pendingDialogueFlow = _dialogueFlow.BeginObservedOriginal(npcName);

            return;
        }

        _pendingDialogueFlow = source is "already_open" or "menu_changed_open"
            ? _dialogueFlow.BeginObservedOriginal(npcName)
            : _dialogueFlow.BeginFollowUp(npcName);
        _pendingDialogueTicks = 0;
        _pendingOriginalStartButton = source is "vanilla_pending" ? triggerButton : null;
        _originalStartRetryAttempted = false;
    }

    private void ClearPendingDialogueFlow()
    {
        _pendingDialogueFlow = null;
        _pendingDialogueTicks = 0;
        _pendingOriginalStartButton = null;
        _originalStartRetryAttempted = false;
    }

    private void ClearCustomDialogueGuards()
    {
        _menuGuard.Clear();
    }

    private static string? AppendDetail(string? detail, string suffix)
        => string.IsNullOrWhiteSpace(detail) ? suffix : $"{detail};{suffix}";

    private static bool IsMouseButton(SButton button)
        => button is SButton.MouseLeft or SButton.MouseRight or SButton.MouseMiddle or SButton.MouseX1 or SButton.MouseX2;

    private static string FormatButtons(IEnumerable<StardewValley.InputButton> buttons)
        => string.Join(",", buttons.Select(button => button.ToString()));

    private NPC? TryResolveClickedNpc(ButtonPressedEventArgs e)
    {
        if (Game1.currentLocation is null)
            return null;

        var grabTileNpc = Game1.currentLocation.isCharacterAtTile(Game1.player.GetGrabTile());
        if (grabTileNpc is not null && !grabTileNpc.IsMonster)
            return grabTileNpc;

        foreach (var character in Game1.currentLocation.characters)
        {
            if (!character.IsMonster && character.GetBoundingBox().Contains(e.Cursor.AbsolutePixels))
                return character;
        }

        var npc = Game1.currentLocation.isCharacterAtTile(e.Cursor.Tile + new Vector2(0f, 1f))
                  ?? Game1.currentLocation.isCharacterAtTile(e.Cursor.GrabTile + new Vector2(0f, 1f));
        return npc;
    }

    private static bool IsEnabledDialogueNpc(string? npcName)
        => !string.IsNullOrWhiteSpace(npcName);

    private static string? GetActiveDialogueNpcName()
        => GetDialogueNpcName(Game1.activeClickableMenu);

    private static string? GetDialogueNpcName(IClickableMenu? menu)
        => menu is DialogueBox dialogueBox
            ? dialogueBox.characterDialogue?.speaker?.Name ?? Game1.currentSpeaker?.Name
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
            if (!_httpHost.IsRunning)
            {
                _bridgeLogger.Write("bridge_discovery_skipped", null, "bridge", "bridge", null, "skipped", "http_bridge_not_running");
                return;
            }

            var path = GetDiscoveryFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var document = new BridgeDiscoveryDocument(
                "127.0.0.1",
                _httpHost.Port,
                _httpHost.BridgeToken,
                _bridgeStartedAtUtc ?? DateTimeOffset.UtcNow,
                Environment.ProcessId,
                GetCurrentSaveId());
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

    private static string? GetCurrentSaveId()
        => Context.IsWorldReady ? Constants.SaveFolderName : null;

    private sealed record BridgeDiscoveryDocument(
        string Host,
        int Port,
        string BridgeToken,
        DateTimeOffset StartedAtUtc,
        int ProcessId,
        string? SaveId);

    private sealed record PendingPrivateChatReplyDialogue(string NpcName, string ConversationId);
}
