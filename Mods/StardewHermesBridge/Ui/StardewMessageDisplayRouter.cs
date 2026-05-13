namespace StardewHermesBridge.Ui;

using System.Text.Json.Nodes;
using StardewHermesBridge.Bridge;
using StardewHermesBridge.Dialogue;
using StardewHermesBridge.Logging;
using StardewValley;

public sealed class StardewMessageDisplayRouter
{
    private const int NearbyTileRange = 8;

    private readonly HermesPhoneState _phoneState;
    private readonly NpcOverheadBubbleOverlay _bubbleOverlay;
    private readonly HermesPhoneOverlay _phoneOverlay;
    private readonly BridgeEventBuffer _events;
    private readonly SmapiBridgeLogger _logger;
    private readonly Action<string, string?>? _preparePrivateChatReplyDialogue;

    public StardewMessageDisplayRouter(
        HermesPhoneState phoneState,
        NpcOverheadBubbleOverlay bubbleOverlay,
        HermesPhoneOverlay phoneOverlay,
        BridgeEventBuffer events,
        SmapiBridgeLogger logger,
        Action<string, string?>? preparePrivateChatReplyDialogue = null)
    {
        _phoneState = phoneState;
        _bubbleOverlay = bubbleOverlay;
        _phoneOverlay = phoneOverlay;
        _events = events;
        _logger = logger;
        _preparePrivateChatReplyDialogue = preparePrivateChatReplyDialogue;
    }

    public StardewMessageDisplayResult Display(NPC npc, string text, string channel, string? conversationId, string? source)
    {
        var privateChat = string.Equals(channel, "private_chat", StringComparison.OrdinalIgnoreCase);
        if (privateChat && string.Equals(source, "input_menu", StringComparison.OrdinalIgnoreCase))
        {
            _phoneState.AddIncomingMessage(npc.Name, text, conversationId, openThread: false, recordOnly: true);
            _preparePrivateChatReplyDialogue?.Invoke(npc.Name, conversationId);
            NpcRawDialogueRenderer.Display(npc, text);
            _logger.Write("private_chat_reply_displayed_auto", npc.Name, channel, "dialogue", null, "displayed", conversationId);
            return new StardewMessageDisplayResult("input_menu_dialogue", "input_menu_dialogue_closed");
        }

        var nearby = IsPlayerWithinNearbyRange(npc);
        if (nearby)
        {
            _phoneState.AddIncomingMessage(npc.Name, text, conversationId, openThread: false, recordOnly: true);
            _bubbleOverlay.Show(npc, text, conversationId, privateChat);
            _logger.Write("phone_message_recorded", npc.Name, channel, "phone", null, "recorded", conversationId);
            return new StardewMessageDisplayResult("bubble", "reply_displayed_bubble");
        }

        _phoneState.AddIncomingMessage(npc.Name, text, conversationId, openThread: false);
        if (privateChat && !string.IsNullOrWhiteSpace(conversationId))
            _phoneOverlay.MarkReplyClosedFromPhone(npc.Name, conversationId, "phone_enqueued_unread");

        _logger.Write("phone_message_enqueued", npc.Name, channel, "phone", null, "queued", conversationId);
        _events.Record(
            "phone_message_enqueued",
            npc.Name,
            $"{npc.Name} message was delivered to Hermes phone.",
            conversationId,
            new JsonObject
            {
                ["conversationId"] = conversationId,
                ["channel"] = channel,
                ["route"] = "phone"
            });
        return new StardewMessageDisplayResult("phone", "phone_enqueued_unread");
    }

    private static bool IsPlayerWithinNearbyRange(NPC npc)
    {
        if (Game1.player?.currentLocation is null ||
            npc.currentLocation is null ||
            !ReferenceEquals(Game1.player.currentLocation, npc.currentLocation))
        {
            return false;
        }

        var playerTile = Game1.player.TilePoint;
        var npcTile = npc.TilePoint;
        return Math.Abs(playerTile.X - npcTile.X) <= NearbyTileRange &&
               Math.Abs(playerTile.Y - npcTile.Y) <= NearbyTileRange;
    }
}

public sealed record StardewMessageDisplayResult(string Route, string ReplyClosedSource);
