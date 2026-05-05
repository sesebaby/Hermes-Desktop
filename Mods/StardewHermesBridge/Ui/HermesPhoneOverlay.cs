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
    private readonly HermesPhoneState _state;
    private readonly BridgeEventBuffer _events;
    private readonly SmapiBridgeLogger _logger;
    private readonly Action<string>? _privateChatSubmitted;
    private readonly TextBox _textBox;
    private Rectangle _phoneRect;
    private Rectangle _screenRect;
    private Rectangle _inputRect;
    private Rectangle _indicatorRect;
    private int _vibrateTicks;

    public HermesPhoneOverlay(
        HermesPhoneState state,
        BridgeEventBuffer events,
        SmapiBridgeLogger logger,
        Action<string>? privateChatSubmitted)
    {
        _state = state;
        _events = events;
        _logger = logger;
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
                _logger.Write("phone_thread_opened", CurrentNpcName(), "phone", "phone", null, "recorded", "indicator_clicked");
                return true;
            }

            return false;
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

        if (_state.UiOwner != HermesPhoneUiOwner.PhoneOverlay)
        {
            DrawPhoneIndicator(spriteBatch);
            return;
        }

        DrawPhoneShell(spriteBatch);
        DrawThread(spriteBatch);
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
    {
        if (_state.FocusOwner == HermesPhoneFocusOwner.PhoneTextInput)
            CancelCurrentThread("phone_closed");
        ReleaseKeyboard();
        _state.ClosePhone();
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
        var width = Math.Min(360, Math.Max(280, Game1.uiViewport.Width / 4));
        var height = Math.Min(620, Math.Max(420, Game1.uiViewport.Height - 120));
        var x = Game1.uiViewport.Width - width - 24;
        var y = Math.Max(48, (Game1.uiViewport.Height - height) / 2);
        _phoneRect = new Rectangle(x, y, width, height);
        _screenRect = new Rectangle(x + 22, y + 48, width - 44, height - 96);
        _inputRect = new Rectangle(_screenRect.X + 12, _screenRect.Bottom - 52, _screenRect.Width - 24, 40);
        _indicatorRect = new Rectangle(Game1.uiViewport.Width - 74, 96, 48, 72);
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
        IClickableMenu.drawTextureBox(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, Color.White);
        var label = hasUnread ? unread.ToString() : "机";
        spriteBatch.DrawString(Game1.smallFont, label, new Vector2(rect.X + 15, rect.Y + 22), hasUnread ? Color.DeepPink : Color.DarkSlateGray);
    }

    private void DrawPhoneShell(SpriteBatch spriteBatch)
    {
        IClickableMenu.drawTextureBox(spriteBatch, _phoneRect.X, _phoneRect.Y, _phoneRect.Width, _phoneRect.Height, Color.White);
        spriteBatch.DrawString(Game1.smallFont, "Hermes", new Vector2(_screenRect.X, _phoneRect.Y + 20), Color.DarkSlateGray);
    }

    private void DrawThread(SpriteBatch spriteBatch)
    {
        var thread = CurrentThread();
        if (thread is null)
        {
            spriteBatch.DrawString(Game1.smallFont, "Hermes 手机", new Vector2(_screenRect.X, _screenRect.Y), Color.Black);
            spriteBatch.DrawString(Game1.smallFont, "暂无消息", new Vector2(_screenRect.X, _screenRect.Y + 44), Color.DimGray);
            return;
        }

        spriteBatch.DrawString(Game1.smallFont, thread.NpcName, new Vector2(_screenRect.X, _screenRect.Y), Color.Black);
        var y = _screenRect.Y + 34;
        foreach (var message in thread.Messages.TakeLast(8))
        {
            var prefix = message.Incoming ? thread.NpcName : "你";
            var text = $"{prefix}: {message.Text}";
            spriteBatch.DrawString(Game1.smallFont, text, new Vector2(_screenRect.X, y), message.Incoming ? Color.DarkSlateBlue : Color.DarkGreen);
            y += 34;
        }

        IClickableMenu.drawTextureBox(spriteBatch, _inputRect.X, _inputRect.Y, _inputRect.Width, _inputRect.Height, Color.White);
        var inputText = _state.OpenState == HermesPhoneOpenState.PhoneReplyFocusActive
            ? _textBox.Text
            : "点这里回复";
        spriteBatch.DrawString(Game1.smallFont, inputText, new Vector2(_inputRect.X + 12, _inputRect.Y + 10), Color.DimGray);
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
