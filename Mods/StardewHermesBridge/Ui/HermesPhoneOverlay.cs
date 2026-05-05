namespace StardewHermesBridge.Ui;

using System.Text.Json.Nodes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewHermesBridge.Bridge;
using StardewHermesBridge.Logging;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

public sealed class HermesPhoneOverlay
{
    private const string PhoneIconAssetPath = "assets/phone/phone_icon.png";
    private const string PhoneShellAssetPath = "assets/phone/skins/pink.png";
    private const string PhoneBackgroundAssetPath = "assets/phone/backgrounds/hearts.png";

    private readonly HermesPhoneState _state;
    private readonly BridgeEventBuffer _events;
    private readonly SmapiBridgeLogger _logger;
    private readonly IModHelper? _helper;
    private readonly Action<string>? _privateChatSubmitted;
    private readonly TextBox _textBox;
    private Texture2D? _phoneIconTexture;
    private Texture2D? _phoneShellTexture;
    private Texture2D? _phoneBackgroundTexture;
    private bool _assetsLoaded;
    private Rectangle _phoneRect;
    private Rectangle _screenRect;
    private Rectangle _contactListRect;
    private Rectangle _chatRect;
    private Rectangle _inputRect;
    private Rectangle _indicatorRect;
    private Rectangle _closeRect;
    private int _vibrateTicks;

    public HermesPhoneOverlay(
        HermesPhoneState state,
        BridgeEventBuffer events,
        SmapiBridgeLogger logger,
        IModHelper? helper,
        Action<string>? privateChatSubmitted)
    {
        _state = state;
        _events = events;
        _logger = logger;
        _helper = helper;
        _privateChatSubmitted = privateChatSubmitted;
        _textBox = new TextBox(null, null, Game1.smallFont, Game1.textColor)
        {
            limitWidth = false,
            Selected = false
        };
        _textBox.OnEnterPressed += _ => SubmitReply();
    }

    public bool HandleButtonPressed(SButton button, IInputHelper input)
    {
        if (!Context.IsWorldReady || Game1.activeClickableMenu is not null)
            return false;

        var mouse = Game1.getMousePosition();
        RefreshLayout();

        if (button == SButton.Escape && _state.FocusOwner == HermesPhoneFocusOwner.PhoneTextInput)
        {
            CancelCurrentThread("escape");
            input.Suppress(button);
            return true;
        }

        if (button != SButton.MouseLeft)
            return false;

        if (_state.UiOwner != HermesPhoneUiOwner.PhoneOverlay)
        {
            if (_indicatorRect.Contains(mouse))
            {
                if (!_state.OpenLatestThread())
                    _state.OpenPhoneHome();

                input.Suppress(button);
                _logger.Write("phone_overlay_opened", CurrentNpcName(), "phone", "phone", null, "recorded", "indicator_clicked");
                return true;
            }

            return false;
        }

        if (_closeRect.Contains(mouse))
        {
            ClosePhone("close_button");
            input.Suppress(button);
            return true;
        }

        foreach (var (thread, rect) in EnumerateVisibleThreadRects())
        {
            if (!rect.Contains(mouse))
                continue;

            _state.OpenThread(thread.NpcName, thread.ConversationId);
            input.Suppress(button);
            _logger.Write("phone_thread_opened", thread.NpcName, "phone", "phone", null, "recorded", "conversation_clicked");
            return true;
        }

        if (_inputRect.Contains(mouse))
        {
            _state.FocusReplyInput();
            _textBox.Selected = true;
            Game1.keyboardDispatcher.Subscriber = _textBox;
            _state.KeyboardSubscriberOwnedByPhone = true;
            input.Suppress(button);
            _logger.Write("phone_input_focused", CurrentNpcName(), "phone", "phone", null, "recorded", "PhoneReplyFocusActive");
            return true;
        }

        if (_phoneRect.Contains(mouse))
        {
            input.Suppress(button);
            if (_state.FocusOwner == HermesPhoneFocusOwner.PhoneTextInput)
                ReleaseKeyboard();
            return true;
        }

        if (_state.FocusOwner == HermesPhoneFocusOwner.PhoneTextInput)
        {
            CancelCurrentThread("clicked_outside");
            return true;
        }

        return false;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        RefreshLayout();
        EnsureAssetsLoaded();

        if (_state.UiOwner != HermesPhoneUiOwner.PhoneOverlay)
        {
            DrawPhoneIndicator(spriteBatch);
            return;
        }

        DrawPhoneShell(spriteBatch);
        DrawConversationList(spriteBatch);
        DrawChatMessages(spriteBatch);
        DrawReplyInput(spriteBatch);
    }

    public void ReleaseKeyboard()
    {
        _textBox.Selected = false;
        if (ReferenceEquals(Game1.keyboardDispatcher.Subscriber, _textBox))
            Game1.keyboardDispatcher.Subscriber = null;

        _state.KeyboardSubscriberOwnedByPhone = false;
        _state.ReleaseReplyInput();
    }

    public void ClosePhone()
        => ClosePhone("lifecycle");

    public void ClosePhone(string reason)
    {
        CancelCurrentThread("phone_closed");
        ReleaseKeyboard();
        _state.ClosePhone();
        _logger.Write("phone_overlay_closed", CurrentNpcName(), "phone", "phone", null, "recorded", reason);
    }

    public void MarkReplyClosedFromPhone(string npcName, string conversationId, string source)
    {
        _events.Record(
            "private_chat_reply_closed",
            npcName,
            $"{npcName} private chat reply closed.",
            conversationId,
            new JsonObject
            {
                ["conversationId"] = conversationId,
                ["reply_closed_source"] = source
            });
        _logger.Write("private_chat_reply_closed_fact", npcName, "phone", "phone", null, "recorded", source);
    }

    private void SubmitReply()
    {
        var text = SanitizePrivateChatText(_textBox.Text);
        var thread = CurrentThread();
        if (thread is null)
            return;

        _textBox.Text = "";
        ReleaseKeyboard();

        if (string.IsNullOrWhiteSpace(text))
        {
            _events.Record(
                "player_private_message_cancelled",
                thread.NpcName,
                "Player cancelled private chat.",
                thread.ConversationId,
                new JsonObject
                {
                    ["conversationId"] = thread.ConversationId,
                    ["reason"] = "empty_submit"
                });
            _logger.Write("phone_private_chat_cancelled", thread.NpcName, "private_chat", "phone", null, "recorded", "empty_submit");
            return;
        }

        _state.AddOutgoingMessage(thread.NpcName, text, thread.ConversationId);
        _events.Record(
            "player_private_message_submitted",
            thread.NpcName,
            "Player submitted a private chat message.",
            thread.ConversationId,
            new JsonObject
            {
                ["conversationId"] = thread.ConversationId,
                ["text"] = text,
                ["submittedAtUtc"] = DateTime.UtcNow
            });
        _privateChatSubmitted?.Invoke(thread.NpcName);
        _logger.Write("private_chat_message_submitted", thread.NpcName, "private_chat", "phone", null, "recorded", null);
    }

    private void CancelCurrentThread(string reason)
    {
        var thread = CurrentThread();
        ReleaseKeyboard();
        if (thread is null)
            return;

        _events.Record(
            "player_private_message_cancelled",
            thread.NpcName,
            "Player cancelled private chat.",
            thread.ConversationId,
            new JsonObject
            {
                ["conversationId"] = thread.ConversationId,
                ["reason"] = reason
            });
        _logger.Write("phone_private_chat_cancelled", thread.NpcName, "private_chat", "phone", null, "recorded", reason);
    }

    private void RefreshLayout()
    {
        var width = Math.Min(460, Math.Max(380, Game1.uiViewport.Width / 3));
        var height = Math.Min(680, Math.Max(500, Game1.uiViewport.Height - 96));
        var x = Game1.uiViewport.Width - width - 24;
        var y = Math.Max(32, (Game1.uiViewport.Height - height) / 2);
        _phoneRect = new Rectangle(x, y, width, height);
        _screenRect = new Rectangle(x + 24, y + 52, width - 48, height - 104);
        _closeRect = new Rectangle(_screenRect.Right - 38, _screenRect.Y + 4, 32, 26);
        var listWidth = Math.Min(148, Math.Max(126, _screenRect.Width / 3));
        _contactListRect = new Rectangle(_screenRect.X, _screenRect.Y + 36, listWidth, _screenRect.Height - 36);
        _chatRect = new Rectangle(_contactListRect.Right + 10, _screenRect.Y + 36, _screenRect.Width - listWidth - 10, _screenRect.Height - 92);
        _inputRect = new Rectangle(_chatRect.X, _screenRect.Bottom - 48, _chatRect.Width, 40);
        _indicatorRect = new Rectangle(Game1.uiViewport.Width - 78, 92, 54, 78);
    }

    private void DrawPhoneIndicator(SpriteBatch spriteBatch)
    {
        if (!Game1.displayHUD || Game1.eventUp)
            return;

        var unread = _state.Threads.Values.Sum(thread => thread.UnreadCount);
        var hasUnread = unread > 0;
        if (hasUnread)
            _vibrateTicks++;

        var offset = hasUnread && _vibrateTicks % 12 < 6 ? -2 : hasUnread ? 2 : 0;
        var rect = new Rectangle(_indicatorRect.X + offset, _indicatorRect.Y, _indicatorRect.Width, _indicatorRect.Height);
        if (_phoneIconTexture is not null)
        {
            spriteBatch.Draw(_phoneIconTexture, rect, Color.White);
        }
        else
        {
            IClickableMenu.drawTextureBox(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, Color.White);
            spriteBatch.DrawString(Game1.smallFont, "机", new Vector2(rect.X + 17, rect.Y + 25), Color.DarkSlateGray);
        }

        if (!hasUnread)
            return;

        var badge = new Rectangle(rect.Right - 18, rect.Y + 2, 24, 24);
        IClickableMenu.drawTextureBox(spriteBatch, badge.X, badge.Y, badge.Width, badge.Height, Color.White);
        spriteBatch.DrawString(Game1.smallFont, unread.ToString(), new Vector2(badge.X + 7, badge.Y + 4), Color.DeepPink);
    }

    private void DrawPhoneShell(SpriteBatch spriteBatch)
    {
        if (_phoneShellTexture is not null)
            spriteBatch.Draw(_phoneShellTexture, _phoneRect, Color.White);
        else
            IClickableMenu.drawTextureBox(spriteBatch, _phoneRect.X, _phoneRect.Y, _phoneRect.Width, _phoneRect.Height, Color.White);

        if (_phoneBackgroundTexture is not null)
            spriteBatch.Draw(_phoneBackgroundTexture, _screenRect, Color.White * 0.45f);

        DrawFilledRect(spriteBatch, _screenRect, new Color(247, 247, 247, 238));
        DrawFilledRect(spriteBatch, new Rectangle(_screenRect.X, _screenRect.Y, _screenRect.Width, 32), new Color(236, 236, 236, 250));
        spriteBatch.DrawString(Game1.smallFont, "微信", new Vector2(_screenRect.X + 12, _screenRect.Y + 8), Color.DarkSlateGray);
        DrawFilledRect(spriteBatch, _closeRect, new Color(226, 92, 92, 245));
        spriteBatch.DrawString(Game1.smallFont, "X", new Vector2(_closeRect.X + 11, _closeRect.Y + 4), Color.White);
    }

    private void DrawConversationList(SpriteBatch spriteBatch)
    {
        DrawFilledRect(spriteBatch, _contactListRect, new Color(232, 232, 232, 245));
        if (_state.Threads.Count == 0)
        {
            spriteBatch.DrawString(Game1.smallFont, "联系人", new Vector2(_contactListRect.X + 10, _contactListRect.Y + 14), Color.DimGray);
            return;
        }

        foreach (var (thread, rect) in EnumerateVisibleThreadRects())
        {
            var selected = string.Equals(thread.ThreadId, _state.VisibleThreadId, StringComparison.OrdinalIgnoreCase);
            DrawFilledRect(spriteBatch, rect, selected ? new Color(210, 232, 214, 255) : new Color(245, 245, 245, 245));
            spriteBatch.DrawString(Game1.smallFont, thread.NpcName, new Vector2(rect.X + 8, rect.Y + 8), Color.Black);
            if (thread.UnreadCount > 0)
                spriteBatch.DrawString(Game1.smallFont, thread.UnreadCount.ToString(), new Vector2(rect.Right - 22, rect.Y + 8), Color.DeepPink);
        }
    }

    private void DrawChatMessages(SpriteBatch spriteBatch)
    {
        var thread = CurrentThread();
        if (thread is null)
        {
            spriteBatch.DrawString(Game1.smallFont, "暂无消息", new Vector2(_chatRect.X + 18, _chatRect.Y + 48), Color.DimGray);
            return;
        }

        spriteBatch.DrawString(Game1.smallFont, thread.NpcName, new Vector2(_chatRect.X, _screenRect.Y + 8), Color.Black);
        var y = _chatRect.Y + 8;
        foreach (var message in thread.Messages.TakeLast(8))
        {
            DrawMessageBubble(spriteBatch, message, y);
            y += 42;
        }
    }

    private void DrawReplyInput(SpriteBatch spriteBatch)
    {
        IClickableMenu.drawTextureBox(spriteBatch, _inputRect.X, _inputRect.Y, _inputRect.Width, _inputRect.Height, Color.White);
        var inputText = _state.OpenState == HermesPhoneOpenState.PhoneReplyFocusActive
            ? _textBox.Text
            : "点这里回复";
        spriteBatch.DrawString(Game1.smallFont, inputText, new Vector2(_inputRect.X + 12, _inputRect.Y + 10), Color.DimGray);
    }

    private void DrawMessageBubble(SpriteBatch spriteBatch, HermesPhoneMessage message, int y)
    {
        var maxWidth = Math.Max(80, _chatRect.Width - 38);
        var text = TruncateForBubble(message.Text, maxWidth);
        var size = Game1.smallFont.MeasureString(text);
        var bubbleWidth = Math.Min(maxWidth, (int)size.X + 22);
        var bubbleHeight = Math.Max(32, (int)size.Y + 14);
        var x = message.Incoming ? _chatRect.X + 8 : _chatRect.Right - bubbleWidth - 8;
        var color = message.Incoming ? new Color(255, 255, 255, 250) : new Color(157, 230, 116, 250);
        DrawFilledRect(spriteBatch, new Rectangle(x, y, bubbleWidth, bubbleHeight), color);
        spriteBatch.DrawString(Game1.smallFont, text, new Vector2(x + 10, y + 7), Color.Black);
    }

    private IEnumerable<(HermesPhoneThread Thread, Rectangle Rect)> EnumerateVisibleThreadRects()
    {
        var y = _contactListRect.Y + 6;
        foreach (var thread in _state.Threads.Values
                     .OrderByDescending(candidate => candidate.Messages.LastOrDefault()?.TimestampUtc ?? DateTime.MinValue)
                     .Take(8))
        {
            yield return (thread, new Rectangle(_contactListRect.X + 5, y, _contactListRect.Width - 10, 38));
            y += 42;
        }
    }

    private void EnsureAssetsLoaded()
    {
        if (_assetsLoaded || _helper is null)
            return;

        _assetsLoaded = true;
        _phoneIconTexture = TryLoadTexture(PhoneIconAssetPath);
        _phoneShellTexture = TryLoadTexture(PhoneShellAssetPath);
        _phoneBackgroundTexture = TryLoadTexture(PhoneBackgroundAssetPath);
    }

    private Texture2D? TryLoadTexture(string path)
    {
        try
        {
            return _helper?.ModContent.Load<Texture2D>(path);
        }
        catch (Exception ex)
        {
            _logger.Write("phone_asset_load_failed", "-", "phone", "phone", null, "failed", $"{path}:{ex.Message}");
            return null;
        }
    }

    private static void DrawFilledRect(SpriteBatch spriteBatch, Rectangle rect, Color color)
    {
        var texture = Game1.staminaRect;
        spriteBatch.Draw(texture, rect, color);
    }

    private static string TruncateForBubble(string value, int maxWidth)
    {
        var text = value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        return text.Length <= 28 ? text : $"{text[..27]}...";
    }

    private HermesPhoneThread? CurrentThread()
        => _state.VisibleThreadId is not null &&
           _state.Threads.TryGetValue(_state.VisibleThreadId, out var thread)
            ? thread
            : null;

    private string CurrentNpcName()
        => CurrentThread()?.NpcName ?? "-";

    private static string SanitizePrivateChatText(string value)
    {
        var normalized = value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        return normalized.Length <= 240 ? normalized : normalized[..240];
    }
}
