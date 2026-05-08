using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StardewHermesBridge.Tests;

[TestClass]
public class RawDialogueDisplayRegressionTests
{
    [TestMethod]
    public void RawNpcDialogueText_IsNotDisplayedThroughTranslationKeyOverload()
    {
        var modEntry = ReadRepositoryFile("Mods", "StardewHermesBridge", "ModEntry.cs");
        var commandQueue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");

        Assert.IsFalse(
            modEntry.Contains("Game1.DrawDialogue(npc,", StringComparison.Ordinal),
            "Raw custom follow-up text must not use Game1.DrawDialogue(NPC,string), because that overload treats the string as a translation key.");
        Assert.IsFalse(
            commandQueue.Contains("Game1.DrawDialogue(npc,", StringComparison.Ordinal),
            "Raw debug speak text must not use Game1.DrawDialogue(NPC,string), because that overload treats the string as a translation key.");
    }

    [TestMethod]
    public void NpcClickPathObservesVanillaDialogueInsteadOfManuallyStartingOriginalDialogue()
    {
        var modEntry = ReadRepositoryFile("Mods", "StardewHermesBridge", "ModEntry.cs");

        StringAssert.Contains(
            modEntry,
            "helper.Events.Display.MenuChanged += OnMenuChanged",
            "The formal NPC dialogue path must observe Stardew's DialogueBox lifecycle instead of relying only on ButtonPressed timing.");
        Assert.IsFalse(
            modEntry.Contains("_originalDialogueStarter.TryStart(clickedNpc, e.Button)", StringComparison.Ordinal),
            "The formal NPC dialogue path must not manually replay npc.checkAction directly from ButtonPressed.");
        StringAssert.Contains(
            modEntry,
            "TryStartOriginalDialogueIfNeeded()",
            "If vanilla does not naturally open the DialogueBox after an accepted click, the bridge may perform a narrow delayed original-start retry.");
        StringAssert.Contains(
            modEntry,
            "_menuGuard.ConsumeMenuChange(oldDialogueNpcName, newDialogueNpcName)",
            "MenuChanged must consume Hermes custom-dialogue menu transitions before deciding whether a vanilla follow-up is pending.");
        StringAssert.Contains(
            modEntry,
            "if (_menuGuard.IsCustomDialogue(activeDialogueNpcName))",
            "ButtonPressed must ignore Hermes custom dialogue boxes so their close click cannot start another follow-up loop.");
    }

    [TestMethod]
    public void NpcClickPathHasTimeoutFallbackWhenVanillaDialogueIsNotObserved()
    {
        var modEntry = ReadRepositoryFile("Mods", "StardewHermesBridge", "ModEntry.cs");

        StringAssert.Contains(
            modEntry,
            "original_not_observed_timeout",
            "If Stardew never opens the original DialogueBox after an accepted NPC click, Hermes should expose a fallback instead of waiting forever.");
    }

    [TestMethod]
    public void FormalNpcClickPathRecordsFactInsteadOfDisplayingHardcodedCustomDialogue()
    {
        var modEntry = ReadRepositoryFile("Mods", "StardewHermesBridge", "ModEntry.cs");

        StringAssert.Contains(
            modEntry,
            "RecordVanillaDialogueCompleted",
            "The formal Haley click path should record a fact for the Desktop/core autonomy loop to observe.");
        Assert.IsFalse(
            modEntry.Contains("DisplayCustomDialogue(_pendingDialogueFlow.NpcName", StringComparison.Ordinal),
            "The formal Haley click path must not directly display hard-coded custom text after vanilla dialogue completion.");
    }

    [TestMethod]
    public void FormalNpcClickPathShowsNonBlockingPendingHudBeforeAgentOpensPrivateChat()
    {
        var modEntry = ReadRepositoryFile("Mods", "StardewHermesBridge", "ModEntry.cs");
        var commandQueue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");
        var overlay = ReadRepositoryFile("Mods", "StardewHermesBridge", "Ui", "BridgeStatusOverlay.cs");

        StringAssert.Contains(
            modEntry,
            "_overlay.SetPrivateChatPending(npcName)",
            "After vanilla dialogue completes, the bridge should immediately show a non-blocking pending HUD while the Agent decides.");
        StringAssert.Contains(
            overlay,
            "海莉知道你想和她聊天",
            "The pending HUD should tell the player Haley noticed the chat intent and is considering whether to answer.");
        StringAssert.Contains(
            modEntry,
            "_ => _overlay.ClearPrivateChatPending()",
            "The pending HUD should clear as soon as the Agent-selected private-chat input opens.");
        StringAssert.Contains(
            commandQueue,
            "_privateChatOpened?.Invoke(npc.Name)",
            "Opening the private chat input should notify the host so the waiting HUD cannot reappear after menu close.");
        Assert.IsFalse(
            modEntry.Contains("Game1.showTextEntry", StringComparison.Ordinal),
            "The vanilla completion fact path must not directly open the blocking private-chat text entry menu.");
    }

    [TestMethod]
    public void PrivateChatEventsCarryConversationPayloadAndThinkingHud()
    {
        var commandModels = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandModels.cs");
        var commandQueue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");
        var overlay = ReadRepositoryFile("Mods", "StardewHermesBridge", "Ui", "BridgeStatusOverlay.cs");
        var phoneOverlay = ReadRepositoryFile("Mods", "StardewHermesBridge", "Ui", "HermesPhoneOverlay.cs");

        StringAssert.Contains(
            commandModels,
            "OpenPrivateChatPayload(string? Prompt, string? ConversationId)",
            "Desktop/core must pass a conversation id into the SMAPI private-chat input.");
        StringAssert.Contains(
            commandModels,
            "SpeakPayload(string Text, string? Channel, string? ConversationId = null, string? Source = null)",
            "Desktop/core must pass both the active private-chat conversation id and its originating input source into private-chat replies.");
        StringAssert.Contains(
            commandModels,
            "JsonObject? Payload",
            "Bridge events must expose structured payloads so Desktop/core never parses Summary for private-chat text.");
        StringAssert.Contains(
            commandQueue,
            "\"conversationId\"",
            "Private-chat opened/submitted events must include the Desktop-created conversation id.");
        StringAssert.Contains(
            phoneOverlay,
            "\"text\"",
            "Player private-chat text must be recorded as typed payload data by the phone input owner.");
        Assert.IsFalse(
            commandQueue.Contains("$\"Player private message to {npc.Name}: {submitted}\"", StringComparison.Ordinal),
            "Raw player text must not be duplicated in the event summary.");
        StringAssert.Contains(
            overlay,
            "SetPrivateChatThinking",
            "After player submit, the HUD should switch to a non-blocking thinking state while Desktop/core waits for Haley's agent.");
        StringAssert.Contains(
            overlay,
            "海莉正在思考怎么回答你",
            "The thinking HUD should explain that Haley is considering the reply.");
        StringAssert.Contains(
            commandQueue,
            "string.Equals(channel, \"private_chat\", StringComparison.OrdinalIgnoreCase)",
            "Only private-chat replies should clear the private-chat thinking HUD.");
        StringAssert.Contains(
            commandQueue,
            "private_chat_reply_displayed",
            "Private-chat replies should record a displayed event carrying the active conversation id.");
        StringAssert.Contains(
            phoneOverlay,
            "player_private_message_submitted",
            "Phone replies must emit the event consumed by Desktop/core.");
        StringAssert.Contains(
            phoneOverlay,
            "player_private_message_cancelled",
            "Empty private-chat submissions should end the Desktop/core session instead of leaving it waiting forever.");
        StringAssert.Contains(
            commandQueue,
            "[\"source\"] = \"input_menu\"",
            "Player-click NPC private-chat input events must be tagged as input_menu so replies can use original Stardew dialogue.");
        StringAssert.Contains(
            phoneOverlay,
            "[\"source\"] = \"phone_overlay\"",
            "Phone-origin private-chat replies must remain tagged as phone_overlay so nearby NPC replies can use overhead bubbles.");
    }

    [TestMethod]
    public void PrivateChatInputCloseWithoutEnterRecordsCancellation()
    {
        var modEntry = ReadRepositoryFile("Mods", "StardewHermesBridge", "ModEntry.cs");

        StringAssert.Contains(
            modEntry,
            "_commands.RecordPrivateChatInputClosedWithoutSubmit()",
            "Closing the active Hermes private-chat input menu without submitting must emit cancellation so Desktop/core releases the private-chat turn.");
        StringAssert.Contains(
            ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs"),
            "RecordPrivateChatInputClosedWithoutSubmit",
            "BridgeCommandQueue must keep the input-menu cancellation lifecycle for player-click NPC private chat.");
    }

    [TestMethod]
    public void PassivePhoneCloseRecordsCancellationForVisiblePhoneThread()
    {
        var phoneOverlay = ReadRepositoryFile("Mods", "StardewHermesBridge", "Ui", "HermesPhoneOverlay.cs");
        var closeMethodStart = phoneOverlay.IndexOf("public void ClosePhone(string reason)", StringComparison.Ordinal);
        var closeMethodEnd = phoneOverlay.IndexOf("public void MarkReplyClosedFromPhone", closeMethodStart, StringComparison.Ordinal);
        Assert.IsTrue(closeMethodStart >= 0 && closeMethodEnd > closeMethodStart, "Could not locate HermesPhoneOverlay.ClosePhone(string).");
        var closeMethod = phoneOverlay[closeMethodStart..closeMethodEnd];

        StringAssert.Contains(
            closeMethod,
            "CancelCurrentThread(\"phone_closed\")",
            "Closing a visible phone thread must emit player_private_message_cancelled when that thread owns a phone-based remote reply.");
        Assert.IsFalse(
            closeMethod.Contains("if (_state.FocusOwner == HermesPhoneFocusOwner.PhoneTextInput)", StringComparison.Ordinal),
            "ClosePhone must not cancel only while the reply input is focused.");
    }

    [TestMethod]
    public void PlayerClickedNpcPrivateChatUsesInputMenuNotPhoneOverlay()
    {
        var commandQueue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");
        var inputMenu = ReadRepositoryFile("Mods", "StardewHermesBridge", "Ui", "PrivateChatInputMenu.cs");

        StringAssert.Contains(
            commandQueue,
            "new PrivateChatInputMenu",
            "open_private_chat is the player-click NPC private-chat entry and must create the existing Hermes private-chat input window.");
        StringAssert.Contains(
            commandQueue,
            "Game1.activeClickableMenu = inputMenu",
            "The Hermes private-chat input window is an explicit focused chat menu, not the passive phone contact list.");
        StringAssert.Contains(
            commandQueue,
            "if (Game1.activeClickableMenu is not null)",
            "Because the player-click private chat uses a real Stardew menu, the bridge must not overwrite another active menu.");
        StringAssert.Contains(
            commandQueue,
            "menu_blocked",
            "A busy Stardew menu should produce a retryable open_private_chat failure instead of routing to phone or replacing the menu.");
        Assert.IsFalse(
            commandQueue.Contains("_phoneState.OpenThread(npc.Name, conversationId)", StringComparison.Ordinal),
            "open_private_chat must not route player-click NPC private chat into the phone contact thread.");
        StringAssert.Contains(
            commandQueue,
            "player_private_message_cancelled",
            "The Bridge private-chat input lifecycle must emit cancellation when the player closes or submits an empty private chat.");
        Assert.IsFalse(
            inputMenu.Contains("Retired production path", StringComparison.Ordinal),
            "PrivateChatInputMenu is not retired; it is the formal player-click NPC private-chat input window.");
        StringAssert.Contains(
            inputMenu,
            "player-initiated private-chat entry point",
            "The menu comment must preserve its intended production role so it is not removed again as phone-only legacy.");
        Assert.IsFalse(
            inputMenu.Contains("phone thread", StringComparison.Ordinal),
            "The private-chat input menu should not describe itself as the phone thread path.");
    }

    [TestMethod]
    public void BridgeUiActionsAreQueuedForGameLoopPump()
    {
        var httpHost = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeHttpHost.cs");
        var commandQueue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");

        StringAssert.Contains(
            httpHost,
            "await _commands.OpenPrivateChatAsync",
            "The HTTP listener must not execute Stardew UI calls directly on its background request thread.");
        StringAssert.Contains(
            httpHost,
            "await _commands.SpeakAsync",
            "Speak replies also display UI and must be marshalled through the game loop.");
        StringAssert.Contains(
            commandQueue,
            "_pendingUi.Enqueue",
            "Bridge UI actions should be queued for execution by UpdateTicked/PumpOneTick.");
        StringAssert.Contains(
            commandQueue,
            "TryPumpUiCommand()",
            "PumpOneTick must execute queued UI commands on the Stardew game loop.");
        StringAssert.Contains(
            commandQueue,
            "TaskCompletionSource<BridgeResponse<OpenPrivateChatData>>",
            "The HTTP path should wait for the game-loop execution result before reporting completion.");
    }

    [TestMethod]
    public void PhoneIndicatorIsAlwaysVisibleAsManualEntryPoint()
    {
        var phoneState = ReadRepositoryFile("Mods", "StardewHermesBridge", "Ui", "HermesPhoneState.cs");
        var phoneOverlay = ReadRepositoryFile("Mods", "StardewHermesBridge", "Ui", "HermesPhoneOverlay.cs");

        StringAssert.Contains(
            phoneState,
            "OpenPhoneHome",
            "The player needs a persistent phone entry point even before any NPC has sent an unread message.");
        Assert.IsFalse(
            phoneOverlay.Contains("if (unread <= 0)\r\n            return;", StringComparison.Ordinal) ||
            phoneOverlay.Contains("if (unread <= 0)\n            return;", StringComparison.Ordinal),
            "The phone indicator must not disappear just because there are no unread messages.");
        StringAssert.Contains(
            phoneOverlay,
            "_state.OpenPhoneHome()",
            "Clicking the persistent indicator with no thread should still open the phone home view.");
    }

    [TestMethod]
    public void HermesPhoneOverlayUsesWeChatLikePhoneShellWithCloseButtonAndLogs()
    {
        var phoneOverlay = ReadRepositoryFile("Mods", "StardewHermesBridge", "Ui", "HermesPhoneOverlay.cs");
        var modEntry = ReadRepositoryFile("Mods", "StardewHermesBridge", "ModEntry.cs");
        var notice = ReadRepositoryFile("Mods", "StardewHermesBridge", "assets", "phone", "NOTICE.md");

        StringAssert.Contains(
            phoneOverlay,
            "_closeRect",
            "The phone overlay must keep an explicit close hitbox; players need a visible way to dismiss the phone.");
        StringAssert.Contains(
            phoneOverlay,
            "phone_overlay_closed",
            "Closing the overlay must write a diagnostic log instead of silently changing state.");
        StringAssert.Contains(
            phoneOverlay,
            "DrawConversationList",
            "The phone UI should expose a WeChat-like conversation list, not only a single raw text panel.");
        StringAssert.Contains(
            phoneOverlay,
            "DrawChatMessages",
            "The phone UI should draw chat bubbles/messages separately from the contact list.");
        StringAssert.Contains(
            phoneOverlay,
            "assets/phone/skins/pink.png",
            "The bridge should reuse the authorized MobilePhone phone shell asset instead of drawing only a generic texture box.");
        StringAssert.Contains(
            phoneOverlay,
            "assets/phone/backgrounds/hearts.png",
            "The bridge should reuse the authorized MobilePhone screen background asset.");
        StringAssert.Contains(
            phoneOverlay,
            "assets/phone/phone_icon.png",
            "The bridge should reuse the authorized MobilePhone phone icon asset.");
        StringAssert.Contains(
            modEntry,
            "new HermesPhoneOverlay(_phoneState, _events, _bridgeLogger, Helper",
            "The overlay needs the SMAPI helper so it can load bundled phone assets.");
        StringAssert.Contains(
            notice,
            "Stardew-GitHub-aedenthorn-MobilePhone",
            "Bundled phone assets must retain their source note.");
    }

    [TestMethod]
    public void ProactiveNpcMessagesRouteToBubbleOrPhoneWithoutRawDialogue()
    {
        var commandQueue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");
        var router = ReadRepositoryFile("Mods", "StardewHermesBridge", "Ui", "StardewMessageDisplayRouter.cs");
        var bubbleOverlay = ReadRepositoryFile("Mods", "StardewHermesBridge", "Ui", "NpcOverheadBubbleOverlay.cs");

        Assert.IsFalse(
            commandQueue.Contains("NpcRawDialogueRenderer.Display(npc, envelope.Payload.Text)", StringComparison.Ordinal),
            "Hermes/NPC proactive speak must not open a global DialogueBox.");
        StringAssert.Contains(
            commandQueue,
            "_messageRouter.Display",
            "ExecuteSpeak should use the shared message display router.");
        StringAssert.Contains(
            router,
            "string.Equals(source, \"input_menu\", StringComparison.OrdinalIgnoreCase)",
            "Only replies sourced from the player-click PrivateChatInputMenu may use the original Stardew DialogueBox path.");
        StringAssert.Contains(
            router,
            "NpcRawDialogueRenderer.Display(npc, text)",
            "Player-click private-chat replies must display through the original-style Stardew dialogue window.");
        StringAssert.Contains(
            router,
            "return new StardewMessageDisplayResult(\"dialogue\", \"input_menu_dialogue_closed\")",
            "The input-menu dialogue route must wait for DialogueBox close before reopening the next private-chat turn.");
        StringAssert.Contains(
            router,
            "const int NearbyTileRange = 8",
            "NPC-initiated private messages use the accepted 8-tile range for bubble routing.");
        StringAssert.Contains(
            router,
            "_bubbleOverlay.Show",
            "Nearby same-map NPC-initiated or phone-origin messages should show a non-blocking overhead bubble.");
        StringAssert.Contains(
            router,
            "_phoneState.AddIncomingMessage",
            "Far or cross-map NPC messages should enter the phone thread.");
        StringAssert.Contains(
            router,
            "recordOnly: true",
            "Nearby bubble messages must still be recorded in the phone history so opening the phone does not look empty after a visible NPC message.");
        StringAssert.Contains(
            bubbleOverlay,
            "private_chat_reply_closed",
            "Bubble expiry must emit the close event that releases private-chat flow without DialogueBox lifecycle.");
    }

    [TestMethod]
    public void MoveThoughtBubbleDoesNotEmitPrivateChatReplyClosed()
    {
        var bubbleOverlay = ReadRepositoryFile("Mods", "StardewHermesBridge", "Ui", "NpcOverheadBubbleOverlay.cs");

        StringAssert.Contains(
            bubbleOverlay,
            "ShowMoveThought",
            "Movement thoughts should use a named helper instead of pretending to be private-chat replies.");
        StringAssert.Contains(
            bubbleOverlay,
            "move_thought",
            "Movement thought bubbles need their own diagnostic channel.");
        StringAssert.Contains(
            bubbleOverlay,
            "string.Equals(entry.Channel, PrivateChatChannel",
            "Only explicit private_chat bubbles may emit private_chat_reply_closed when they expire.");
    }

    [TestMethod]
    public void PrivateChatInputUsesPortraitShellAndWrappedInputArea()
    {
        var inputMenu = ReadRepositoryFile("Mods", "StardewHermesBridge", "Ui", "PrivateChatInputMenu.cs");

        StringAssert.Contains(
            inputMenu,
            "PortraitPanelWidth",
            "The private-chat menu should reserve a stable left column for the NPC portrait.");
        StringAssert.Contains(
            inputMenu,
            "DrawNpcPortrait",
            "The private-chat menu should draw a large NPC portrait or a clear fallback in the left column.");
        StringAssert.Contains(
            inputMenu,
            "DrawWrappedInputText",
            "The private-chat menu should render the current message in a multi-line visual input area.");
        StringAssert.Contains(
            inputMenu,
            "DrawInputFrame",
            "The private-chat menu should draw its own multi-line input frame instead of relying on the one-line textbox skin.");
        StringAssert.Contains(
            inputMenu,
            "gameWindowSizeChanged",
            "The private-chat menu should recompute its shell layout when the UI viewport changes.");
    }

    [TestMethod]
    public void PrivateChatInputUsesLocalizedPolishedTextAndInsetCloseButton()
    {
        var inputMenu = ReadRepositoryFile("Mods", "StardewHermesBridge", "Ui", "PrivateChatInputMenu.cs");

        Assert.IsFalse(
            inputMenu.Contains("Private chat", StringComparison.Ordinal),
            "Visible private-chat menu chrome should not include English subtitles.");
        Assert.IsFalse(
            inputMenu.Contains("Say something", StringComparison.Ordinal),
            "Visible private-chat prompt should be localized by the bridge menu.");
        StringAssert.Contains(
            inputMenu,
            "悄悄对",
            "The default private-chat prompt should be Chinese.");
        StringAssert.Contains(
            inputMenu,
            "回车发送    ESC取消",
            "The footer should use the requested Chinese send label and ESC cancel label without unsupported separators.");
        StringAssert.Contains(
            inputMenu,
            "Game1.smallFont.MeasureString(hint)",
            "The footer contains Chinese text, so it should use the same Chinese-capable font as the rest of the private-chat menu.");
        StringAssert.Contains(
            inputMenu,
            "PositionCloseButtonInsideMenu",
            "The close button should be explicitly inset so it is not clipped by the game viewport edge.");
    }

    [TestMethod]
    public void PrivateChatReplyCloseIsRecordedBySourceSpecificUiOwner()
    {
        var modEntry = ReadRepositoryFile("Mods", "StardewHermesBridge", "ModEntry.cs");
        var commandQueue = ReadRepositoryFile("Mods", "StardewHermesBridge", "Bridge", "BridgeCommandQueue.cs");
        var phoneOverlay = ReadRepositoryFile("Mods", "StardewHermesBridge", "Ui", "HermesPhoneOverlay.cs");
        var bubbleOverlay = ReadRepositoryFile("Mods", "StardewHermesBridge", "Ui", "NpcOverheadBubbleOverlay.cs");

        StringAssert.Contains(
            modEntry,
            "_pendingPrivateChatReplyDialogue = new PendingPrivateChatReplyDialogue(npcName, conversationId)",
            "Input-menu-sourced replies use a Stardew DialogueBox, so ModEntry must track that dialogue lifecycle.");
        StringAssert.Contains(
            modEntry,
            "RecordPrivateChatReplyClosedFromInputDialogue",
            "Closing the original-style reply dialogue must emit private_chat_reply_closed for the private-chat loop.");
        StringAssert.Contains(
            modEntry,
            "input_menu_dialogue_closed",
            "The close event must identify the input-menu dialogue source, not phone or bubble.");
        var preDisplayCallback = commandQueue.IndexOf("_privateChatReplyDisplayed?.Invoke(npc.Name, conversationId, \"dialogue\")", StringComparison.Ordinal);
        var displayCall = commandQueue.IndexOf("_messageRouter.Display", StringComparison.Ordinal);
        Assert.IsTrue(
            preDisplayCallback >= 0 && displayCall > preDisplayCallback,
            "BridgeCommandQueue must register input-menu dialogue lifecycle before calling the display router, otherwise MenuChanged can race past the pending reply marker.");
        StringAssert.Contains(
            phoneOverlay,
            "private_chat_reply_closed",
            "Phone messages should emit a reply-closed event after visible/enqueued reply handling.");
        StringAssert.Contains(
            bubbleOverlay,
            "private_chat_reply_closed",
            "Bubble expiry should emit a reply-closed event for nearby replies.");
    }

    [TestMethod]
    public void FallbackPathsDoNotEmitVanillaDialogueCompletedFacts()
    {
        var modEntry = ReadRepositoryFile("Mods", "StardewHermesBridge", "ModEntry.cs");

        StringAssert.Contains(
            modEntry,
            "RecordDialogueFollowUpUnavailable",
            "Fallbacks should record their real failure fact instead of pretending vanilla dialogue completed.");
        Assert.IsFalse(
            modEntry.Contains("RecordVanillaDialogueCompleted(npcName, \"original_not_observed_timeout\")", StringComparison.Ordinal),
            "Timeout fallback must not emit a vanilla_dialogue_completed fact when original dialogue was not observed.");
        Assert.IsFalse(
            modEntry.Contains("RecordVanillaDialogueCompleted(pending.NpcName, \"manual_original_start_failed", StringComparison.Ordinal),
            "Manual original-start failure must not emit a vanilla_dialogue_completed fact.");
    }

    private static string ReadRepositoryFile(params string[] relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(relativePath).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            directory = directory.Parent;
        }

        Assert.Fail($"Could not find repository file: {Path.Combine(relativePath)}");
        return string.Empty;
    }
}
