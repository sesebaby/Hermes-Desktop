namespace StardewHermesBridge.Ui;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

/// <summary>
/// Focused Hermes private-chat input opened after the player clicks an NPC.
/// Remote or proactive messages use the phone overlay; this menu is the explicit
/// player-initiated private-chat entry point.
/// </summary>
public sealed class PrivateChatInputMenu : IClickableMenu
{
    private const int MenuPadding = 32;
    private const int HeaderHeight = 54;
    private const int FooterHeight = 42;
    private const int ColumnGap = 24;
    private const int PortraitPanelWidth = 220;
    private const int PortraitFrameMaxSize = 160;
    private const int InputFrameHorizontalPadding = 28;
    private const int InputFrameVerticalPadding = 24;
    private const int MinimumMenuWidth = 680;
    private const int MaximumMenuWidth = 920;
    private const int MinimumMenuHeight = 320;
    private const int MaximumMenuHeight = 380;

    private readonly NPC _npc;
    private readonly string _npcDisplayName;
    private readonly TextBox _textBox;
    private readonly string _prompt;
    private readonly Action<string> _onSubmitted;
    private readonly Action _onCancelled;
    private Rectangle _headerBounds;
    private Rectangle _portraitPanelBounds;
    private Rectangle _portraitFrameBounds;
    private Rectangle _rightPanelBounds;
    private Rectangle _inputFrameBounds;
    private Rectangle _inputTextBounds;
    private Rectangle _footerBounds;
    private bool _completed;

    public PrivateChatInputMenu(
        NPC npc,
        string? prompt,
        Action<string> onSubmitted,
        Action onCancelled)
        : base(
            Math.Max(32, (Game1.uiViewport.Width - GetMenuWidth()) / 2),
            Math.Max(32, (Game1.uiViewport.Height - GetMenuHeight()) / 2),
            GetMenuWidth(),
            GetMenuHeight(),
            showUpperRightCloseButton: true)
    {
        _npc = npc;
        _npcDisplayName = npc.displayName ?? npc.Name;
        _prompt = BuildPrompt(_npcDisplayName, prompt);
        _onSubmitted = onSubmitted;
        _onCancelled = onCancelled;
        _textBox = new TextBox(null, null, Game1.smallFont, Game1.textColor)
        {
            limitWidth = false,
            Selected = true
        };
        _textBox.OnEnterPressed += _ => Submit();
        UpdateLayout();
        Game1.keyboardDispatcher.Subscriber = _textBox;
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (upperRightCloseButton is not null && upperRightCloseButton.containsPoint(x, y))
        {
            Cancel();
            return;
        }

        if (_inputFrameBounds.Contains(x, y))
        {
            _textBox.Selected = true;
            Game1.keyboardDispatcher.Subscriber = _textBox;
            return;
        }

        base.receiveLeftClick(x, y, playSound);
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape)
        {
            Cancel();
            return;
        }

        if (key == Keys.Enter)
        {
            Submit();
            return;
        }
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        width = GetMenuWidth();
        height = GetMenuHeight();
        xPositionOnScreen = Math.Max(32, (Game1.uiViewport.Width - width) / 2);
        yPositionOnScreen = Math.Max(32, (Game1.uiViewport.Height - height) / 2);
        UpdateLayout();
    }

    public override void draw(SpriteBatch b)
    {
        IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

        DrawHeader(b);
        DrawPortraitPanel(b);
        DrawInputPrompt(b);
        DrawInputFrame(b, _inputFrameBounds);
        DrawWrappedInputText(b, _inputTextBounds);
        DrawFooter(b);

        if (shouldDrawCloseButton())
            upperRightCloseButton?.draw(b);

        drawMouse(b);
    }

    protected override void cleanupBeforeExit()
    {
        if (!_completed)
        {
            _completed = true;
            _onCancelled();
        }

        _textBox.Selected = false;
        if (ReferenceEquals(Game1.keyboardDispatcher.Subscriber, _textBox))
            Game1.keyboardDispatcher.Subscriber = null;

        base.cleanupBeforeExit();
    }

    private void UpdateLayout()
    {
        _headerBounds = new Rectangle(
            xPositionOnScreen + MenuPadding,
            yPositionOnScreen + 24,
            width - MenuPadding * 2,
            HeaderHeight);

        var bodyTop = _headerBounds.Bottom + 8;
        var bodyBottom = yPositionOnScreen + height - FooterHeight;
        var bodyHeight = Math.Max(180, bodyBottom - bodyTop);
        _portraitPanelBounds = new Rectangle(
            xPositionOnScreen + MenuPadding,
            bodyTop,
            PortraitPanelWidth,
            bodyHeight);
        _rightPanelBounds = new Rectangle(
            _portraitPanelBounds.Right + ColumnGap,
            bodyTop,
            xPositionOnScreen + width - MenuPadding - (_portraitPanelBounds.Right + ColumnGap),
            bodyHeight);

        var portraitSize = Math.Min(
            PortraitFrameMaxSize,
            Math.Min(_portraitPanelBounds.Width - 48, _portraitPanelBounds.Height - 64));
        portraitSize = Math.Max(96, portraitSize);
        _portraitFrameBounds = new Rectangle(
            _portraitPanelBounds.X + (_portraitPanelBounds.Width - portraitSize) / 2,
            _portraitPanelBounds.Y + 18,
            portraitSize,
            portraitSize);

        var inputTop = _rightPanelBounds.Y + 64;
        _inputFrameBounds = new Rectangle(
            _rightPanelBounds.X,
            inputTop,
            _rightPanelBounds.Width,
            Math.Max(126, _rightPanelBounds.Bottom - inputTop - 26));
        _inputTextBounds = new Rectangle(
            _inputFrameBounds.X + InputFrameHorizontalPadding,
            _inputFrameBounds.Y + InputFrameVerticalPadding,
            _inputFrameBounds.Width - InputFrameHorizontalPadding * 2,
            _inputFrameBounds.Height - InputFrameVerticalPadding * 2);
        _footerBounds = new Rectangle(
            _rightPanelBounds.X,
            yPositionOnScreen + height - 36,
            _rightPanelBounds.Width,
            24);

        _textBox.X = _inputTextBounds.X;
        _textBox.Y = _inputTextBounds.Y;
        _textBox.Width = _inputTextBounds.Width;
        _textBox.Height = _inputTextBounds.Height;

        PositionCloseButtonInsideMenu();
    }

    private void DrawHeader(SpriteBatch b)
    {
        var title = $"{_npcDisplayName} 的私聊";
        b.DrawString(
            Game1.smallFont,
            title,
            new Vector2(_headerBounds.X, _headerBounds.Y + 14),
            Game1.textColor);

        b.Draw(
            Game1.staminaRect,
            new Rectangle(_headerBounds.X, _headerBounds.Bottom, _headerBounds.Width, 2),
            Game1.textColor * 0.2f);
    }

    private void DrawPortraitPanel(SpriteBatch b)
    {
        DrawLightPanel(b, _portraitPanelBounds);

        DrawNpcPortrait(b, _portraitFrameBounds);

        var nameSize = Game1.smallFont.MeasureString(_npcDisplayName);
        var namePosition = new Vector2(
            _portraitPanelBounds.X + (_portraitPanelBounds.Width - nameSize.X) / 2,
            _portraitFrameBounds.Bottom + 14);
        b.DrawString(Game1.smallFont, _npcDisplayName, namePosition, Game1.textColor);
    }

    private static void DrawLightPanel(SpriteBatch b, Rectangle bounds)
    {
        b.Draw(Game1.staminaRect, bounds, Color.White * 0.12f);
        b.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), Game1.textColor * 0.18f);
        b.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Y, 2, bounds.Height), Game1.textColor * 0.18f);
        b.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2), Game1.textColor * 0.26f);
        b.Draw(Game1.staminaRect, new Rectangle(bounds.Right - 2, bounds.Y, 2, bounds.Height), Game1.textColor * 0.26f);
    }

    private void DrawNpcPortrait(SpriteBatch b, Rectangle bounds)
    {
        if (_npc.Portrait is not null)
        {
            var source = new Rectangle(0, 0, Math.Min(64, _npc.Portrait.Width), Math.Min(64, _npc.Portrait.Height));
            b.Draw(_npc.Portrait, bounds, source, Color.White);
            return;
        }

        if (_npc.Sprite?.Texture is not null)
        {
            var source = _npc.getMugShotSourceRect();
            var target = FitInto(bounds, source.Width, source.Height);
            b.Draw(_npc.Sprite.Texture, target, source, Color.White);
            return;
        }

        var fallback = "?";
        var fallbackSize = Game1.dialogueFont.MeasureString(fallback);
        b.DrawString(
            Game1.dialogueFont,
            fallback,
            new Vector2(bounds.X + (bounds.Width - fallbackSize.X) / 2, bounds.Y + (bounds.Height - fallbackSize.Y) / 2),
            Game1.textColor * 0.6f);
    }

    private void DrawInputPrompt(SpriteBatch b)
    {
        var wrappedPrompt = Game1.parseText(_prompt, Game1.smallFont, _rightPanelBounds.Width);
        b.DrawString(
            Game1.smallFont,
            wrappedPrompt,
            new Vector2(_rightPanelBounds.X, _rightPanelBounds.Y + 4),
            Game1.textColor);
    }

    private void DrawInputFrame(SpriteBatch b, Rectangle bounds)
    {
        IClickableMenu.drawTextureBox(
            b,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            Color.White);
    }

    private void DrawWrappedInputText(SpriteBatch b, Rectangle bounds)
    {
        var rawText = _textBox.Text ?? string.Empty;
        if (string.IsNullOrEmpty(rawText))
        {
            b.DrawString(
                Game1.smallFont,
                "输入你想说的话...",
                new Vector2(bounds.X, bounds.Y),
                Game1.textColor * 0.45f);
            DrawCaret(b, bounds.X, bounds.Y);
            return;
        }

        var wrappedText = Game1.parseText(rawText, Game1.smallFont, bounds.Width).Replace("\r\n", "\n");
        var lines = wrappedText.Split('\n');
        var visibleLineCount = Math.Max(1, bounds.Height / Game1.smallFont.LineSpacing);
        var firstLine = Math.Max(0, lines.Length - visibleLineCount);
        var y = bounds.Y;
        var lastVisibleLine = string.Empty;

        for (var i = firstLine; i < lines.Length; i++)
        {
            lastVisibleLine = lines[i];
            b.DrawString(Game1.smallFont, lines[i], new Vector2(bounds.X, y), Game1.textColor);
            y += Game1.smallFont.LineSpacing;
        }

        var caretX = bounds.X + Math.Min(
            bounds.Width - 6,
            (int)Game1.smallFont.MeasureString(lastVisibleLine).X + 3);
        var caretY = bounds.Y + (Math.Min(lines.Length - firstLine, visibleLineCount) - 1) * Game1.smallFont.LineSpacing;
        DrawCaret(b, caretX, caretY);
    }

    private void DrawFooter(SpriteBatch b)
    {
        var hint = "回车发送    ESC取消";
        var hintSize = Game1.smallFont.MeasureString(hint);
        b.DrawString(
            Game1.smallFont,
            hint,
            new Vector2(_footerBounds.Right - hintSize.X, _footerBounds.Y),
            Game1.textColor * 0.75f);
    }

    private void PositionCloseButtonInsideMenu()
    {
        initializeUpperRightCloseButton();
        if (upperRightCloseButton is null)
            return;

        upperRightCloseButton.bounds = new Rectangle(
            xPositionOnScreen + width - 60,
            yPositionOnScreen + 10,
            48,
            48);
    }

    private static void DrawCaret(SpriteBatch b, int x, int y)
    {
        if (DateTime.UtcNow.Millisecond >= 500)
            return;

        b.Draw(Game1.staminaRect, new Rectangle(x, y + 2, 3, Math.Max(18, Game1.smallFont.LineSpacing - 4)), Game1.textColor);
    }

    private static Rectangle FitInto(Rectangle target, int sourceWidth, int sourceHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
            return target;

        var scale = Math.Min(target.Width / (float)sourceWidth, target.Height / (float)sourceHeight);
        var width = Math.Max(1, (int)(sourceWidth * scale));
        var height = Math.Max(1, (int)(sourceHeight * scale));
        return new Rectangle(
            target.X + (target.Width - width) / 2,
            target.Y + (target.Height - height) / 2,
            width,
            height);
    }

    private static string BuildPrompt(string npcDisplayName, string? prompt)
    {
        var defaultPrompt = $"你想悄悄对{npcDisplayName}说什么？";
        if (string.IsNullOrWhiteSpace(prompt))
            return defaultPrompt;

        var trimmed = prompt.Trim();
        return ContainsAsciiLetter(trimmed) ? defaultPrompt : trimmed;
    }

    private static bool ContainsAsciiLetter(string value)
    {
        foreach (var ch in value)
        {
            if ((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z'))
                return true;
        }

        return false;
    }

    private void Submit()
    {
        if (_completed)
            return;

        _completed = true;
        _onSubmitted(_textBox.Text);
        exitThisMenu(playSound: false);
    }

    private void Cancel()
    {
        if (_completed)
            return;

        _completed = true;
        _onCancelled();
        exitThisMenu();
    }

    private static int GetMenuWidth()
        => Math.Clamp(Game1.uiViewport.Width - 96, MinimumMenuWidth, MaximumMenuWidth);

    private static int GetMenuHeight()
        => Math.Clamp(Game1.uiViewport.Height - 112, MinimumMenuHeight, MaximumMenuHeight);
}
