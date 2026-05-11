namespace StardewHermesBridge.Ui;

using System.Text.Json.Nodes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewHermesBridge.Bridge;
using StardewHermesBridge.Logging;
using StardewValley;
using StardewValley.Menus;

public sealed class NpcOverheadBubbleOverlay
{
    private static readonly TimeSpan BubbleLifetime = TimeSpan.FromSeconds(5);
    private const string PlayerDialogueChannel = "player_dialogue";
    private const string PrivateChatChannel = "private_chat";
    private const string MoveThoughtChannel = "move_thought";
    private const string IdleMicroChannel = "idle_micro";

    private readonly BridgeEventBuffer _events;
    private readonly SmapiBridgeLogger _logger;
    private readonly Dictionary<string, BubbleEntry> _bubbles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PendingClickReply> _pendingClickReplies = new(StringComparer.OrdinalIgnoreCase);

    public NpcOverheadBubbleOverlay(BridgeEventBuffer events, SmapiBridgeLogger logger)
    {
        _events = events;
        _logger = logger;
    }

    public void Show(NPC npc, string text, string? conversationId, bool privateChat)
    {
        var channel = privateChat ? PrivateChatChannel : PlayerDialogueChannel;
        ShowCore(npc, text, conversationId, channel, "text");
    }

    public void ShowMoveThought(NPC npc, string text, string? commandId)
    {
        ShowCore(npc, text, commandId, MoveThoughtChannel, "text");
    }

    public void ShowIdleMicro(NPC npc, string text, string? commandId, string kind)
    {
        ShowCore(npc, text, commandId, IdleMicroChannel, kind);
    }

    public void AddPendingClickReply(string npcName, string text, string? conversationId)
    {
        if (string.IsNullOrWhiteSpace(npcName))
            return;

        _pendingClickReplies[npcName] = new PendingClickReply(npcName, text, conversationId);
        _logger.Write("private_chat_reply_pending_click", npcName, PrivateChatChannel, "reply_pending_click", conversationId, "queued", null);
    }

    public bool TryConsumePendingClickReply(string npcName, out PendingClickReply reply)
    {
        if (_pendingClickReplies.TryGetValue(npcName, out reply!))
        {
            _pendingClickReplies.Remove(npcName);
            return true;
        }

        reply = default!;
        return false;
    }

    public void ClearPendingClickReplies()
        => _pendingClickReplies.Clear();

    public void Draw(SpriteBatch spriteBatch)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in _bubbles.Values.ToList())
        {
            if (now > entry.ExpiresAtUtc)
            {
                _bubbles.Remove(entry.NpcName);
                if (string.Equals(entry.Channel, PrivateChatChannel, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(entry.ConversationId))
                {
                    _events.Record(
                        "private_chat_reply_closed",
                        entry.NpcName,
                        $"{entry.NpcName} private chat reply closed.",
                        entry.ConversationId,
                        new JsonObject
                        {
                            ["conversationId"] = entry.ConversationId,
                            ["reply_closed_source"] = "bubble_expired"
                        });
                    _logger.Write("private_chat_reply_closed_fact", entry.NpcName, "bubble", "bubble", null, "recorded", "bubble_expired");
                }

                continue;
            }

            var npc = Game1.getCharacterFromName(entry.NpcName, mustBeVillager: false, includeEventActors: false);
            if (npc?.currentLocation is null || !ReferenceEquals(npc.currentLocation, Game1.currentLocation))
                continue;

            var screen = Game1.GlobalToLocal(Game1.viewport, npc.Position);
            var position = new Vector2(screen.X - 32, screen.Y - 72);
            DrawBubble(spriteBatch, entry.Text, position);
        }
    }

    private static void DrawBubble(SpriteBatch spriteBatch, string text, Vector2 position)
    {
        if (Game1.smallFont is null)
            return;

        var measured = Game1.smallFont.MeasureString(text);
        var width = Math.Min(320, Math.Max(120, (int)measured.X + 28));
        var height = Math.Max(52, (int)measured.Y + 24);
        IClickableMenu.drawTextureBox(spriteBatch, (int)position.X, (int)position.Y, width, height, Color.White);
        spriteBatch.DrawString(Game1.smallFont, text, position + new Vector2(14, 12), Color.Black);
    }

    private void ShowCore(NPC npc, string text, string? correlationId, string channel, string kind)
    {
        _bubbles[npc.Name] = new BubbleEntry(
            npc.Name,
            text,
            correlationId,
            channel,
            kind,
            DateTime.UtcNow.Add(BubbleLifetime));
        _logger.Write("message_bubble_displayed", npc.Name, channel, "bubble", correlationId, "displayed", kind);
    }

    private sealed record BubbleEntry(
        string NpcName,
        string Text,
        string? ConversationId,
        string Channel,
        string Kind,
        DateTime ExpiresAtUtc);

    public sealed record PendingClickReply(
        string NpcName,
        string Text,
        string? ConversationId);
}
