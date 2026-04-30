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

        StringAssert.Contains(
            commandModels,
            "OpenPrivateChatPayload(string? Prompt, string? ConversationId)",
            "Desktop/core must pass a conversation id into the SMAPI private-chat input.");
        StringAssert.Contains(
            commandModels,
            "SpeakPayload(string Text, string? Channel, string? ConversationId = null)",
            "Desktop/core must pass the active private-chat conversation id into private-chat replies.");
        StringAssert.Contains(
            commandModels,
            "JsonObject? Payload",
            "Bridge events must expose structured payloads so Desktop/core never parses Summary for private-chat text.");
        StringAssert.Contains(
            commandQueue,
            "\"conversationId\"",
            "Private-chat opened/submitted events must include the Desktop-created conversation id.");
        StringAssert.Contains(
            commandQueue,
            "\"text\"",
            "Player private-chat text must be recorded as typed payload data.");
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
            commandQueue,
            "player_private_message_cancelled",
            "Empty private-chat submissions should end the Desktop/core session instead of leaving it waiting forever.");
    }

    [TestMethod]
    public void PrivateChatReplyCloseIsRecordedBeforeOptionalReopen()
    {
        var modEntry = ReadRepositoryFile("Mods", "StardewHermesBridge", "ModEntry.cs");

        StringAssert.Contains(
            modEntry,
            "MarkPrivateChatReplyDisplayed",
            "The bridge should remember which private-chat reply dialogue is currently visible.");
        StringAssert.Contains(
            modEntry,
            "TryRecordPrivateChatReplyClosed",
            "The bridge should emit a close event after the Stardew reply dialogue leaves the screen.");
        StringAssert.Contains(
            modEntry,
            "private_chat_reply_closed",
            "Desktop/core should wait for this event before reopening the next private-chat input.");
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
