namespace StardewHermesBridge.Ui;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

public sealed class BridgeStatusOverlay
{
    private string _online = "offline";
    private string _lastNpcId = "-";
    private string _lastAction = "-";
    private string _lastTrace = "-";
    private string _lastResult = "-";
    private string _blockedReason = "-";

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

    public void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible || Game1.smallFont is null)
            return;

        var text = $"Hermes Bridge: {_online}\nNPC: {_lastNpcId}  Action: {_lastAction}\nTrace: {_lastTrace}\nResult: {_lastResult}\nBlocked: {_blockedReason}";
        spriteBatch.DrawString(Game1.smallFont, text, new Vector2(24, 24), Color.White);
    }
}
