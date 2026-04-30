namespace StardewHermesBridge.Ui;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

public sealed class BridgeStatusOverlay
{
    private const int PrivateChatPendingSeconds = 30;
    private const string HaleyPrivateChatPendingText = "海莉知道你想和她聊天，\n她正在考虑是否回答你。";
    private const string HaleyPrivateChatThinkingText = "海莉正在思考怎么回答你。";

    private string _online = "offline";
    private string _lastNpcId = "-";
    private string _lastAction = "-";
    private string _lastTrace = "-";
    private string _lastResult = "-";
    private string _blockedReason = "-";
    private string? _privateChatPendingText;
    private DateTime _privateChatPendingExpiresAtUtc;

    public bool Visible { get; set; } = true;

    public void SetBridgeOnline(int port, string token)
    {
        _online = $"online 127.0.0.1:{port}";
    }

    public void SetLastRequest(string npcId, string action, string traceId, string result, string? blockedReason)
    {
        _lastNpcId = npcId;
        _lastAction = action;
        _lastTrace = traceId;
        _lastResult = result;
        _blockedReason = string.IsNullOrWhiteSpace(blockedReason) ? "-" : blockedReason;
    }

    public void SetBlockedReason(string reason)
    {
        _blockedReason = reason;
    }

    public void SetPrivateChatPending(string npcName)
    {
        _privateChatPendingText = string.Equals(npcName, "Haley", StringComparison.OrdinalIgnoreCase)
            ? HaleyPrivateChatPendingText
            : $"{npcName} knows you want to chat and is considering whether to answer.";
        _privateChatPendingExpiresAtUtc = DateTime.UtcNow.AddSeconds(PrivateChatPendingSeconds);
    }

    public void SetPrivateChatThinking(string npcName)
    {
        _privateChatPendingText = string.Equals(npcName, "Haley", StringComparison.OrdinalIgnoreCase)
            ? HaleyPrivateChatThinkingText
            : $"{npcName} is thinking about how to answer you.";
        _privateChatPendingExpiresAtUtc = DateTime.UtcNow.AddSeconds(PrivateChatPendingSeconds);
    }

    public void ClearPrivateChatPending()
    {
        _privateChatPendingText = null;
        _privateChatPendingExpiresAtUtc = DateTime.MinValue;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (Game1.smallFont is null)
            return;

        var nextY = 24f;
        if (Visible)
        {
            var text = $"Hermes Bridge: {_online}\nNPC: {_lastNpcId}  Action: {_lastAction}\nTrace: {_lastTrace}\nResult: {_lastResult}\nBlocked: {_blockedReason}";
            DrawText(spriteBatch, text, new Vector2(24, nextY), Color.White);
            nextY += 92f;
        }

        if (Game1.activeClickableMenu is null && TryGetPrivateChatPendingText(out var pendingText))
            DrawText(spriteBatch, pendingText, new Vector2(24, nextY), Color.LightPink);
    }

    private bool TryGetPrivateChatPendingText(out string text)
    {
        if (_privateChatPendingText is not null && DateTime.UtcNow <= _privateChatPendingExpiresAtUtc)
        {
            text = _privateChatPendingText;
            return true;
        }

        ClearPrivateChatPending();
        text = "";
        return false;
    }

    private static void DrawText(SpriteBatch spriteBatch, string text, Vector2 position, Color color)
    {
        spriteBatch.DrawString(Game1.smallFont, text, position + new Vector2(2, 2), Color.Black * 0.75f);
        spriteBatch.DrawString(Game1.smallFont, text, position, color);
    }
}
