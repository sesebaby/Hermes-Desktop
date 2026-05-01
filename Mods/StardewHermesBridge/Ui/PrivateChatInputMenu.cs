namespace StardewHermesBridge.Ui;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

public sealed class PrivateChatInputMenu : IClickableMenu
{
    private const int MenuPadding = 32;
    private const int MenuHeight = 240;
    private const int MinimumMenuWidth = 480;
    private const int MaximumMenuWidth = 760;

    private readonly TextBox _textBox;
    private readonly string _prompt;
    private readonly Action<string> _onSubmitted;
    private readonly Action _onCancelled;
    private bool _completed;

    public PrivateChatInputMenu(
        string npcDisplayName,
        string? prompt,
        Action<string> onSubmitted,
        Action onCancelled)
        : base(
            Math.Max(32, (Game1.uiViewport.Width - GetMenuWidth()) / 2),
            Math.Max(32, (Game1.uiViewport.Height - MenuHeight) / 2),
            GetMenuWidth(),
            MenuHeight,
            showUpperRightCloseButton: true)
    {
        _prompt = string.IsNullOrWhiteSpace(prompt)
            ? $"Say something to {npcDisplayName}."
            : prompt.Trim();
        _onSubmitted = onSubmitted;
        _onCancelled = onCancelled;
        _textBox = new TextBox(null, null, Game1.dialogueFont, Game1.textColor)
        {
            X = xPositionOnScreen + MenuPadding,
            Y = yPositionOnScreen + 108,
            Width = width - MenuPadding * 2,
            Height = 64,
            limitWidth = false,
            Selected = true
        };
        _textBox.OnEnterPressed += _ => Submit();
        Game1.keyboardDispatcher.Subscriber = _textBox;
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (upperRightCloseButton is not null && upperRightCloseButton.containsPoint(x, y))
        {
            Cancel();
            return;
        }

        if (new Rectangle(_textBox.X, _textBox.Y, _textBox.Width, _textBox.Height).Contains(x, y))
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

    public override void draw(SpriteBatch b)
    {
        IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

        var wrappedPrompt = Game1.parseText(_prompt, Game1.smallFont, width - MenuPadding * 2);
        b.DrawString(
            Game1.smallFont,
            wrappedPrompt,
            new Vector2(xPositionOnScreen + MenuPadding, yPositionOnScreen + MenuPadding),
            Game1.textColor);
        _textBox.Draw(b);
        b.DrawString(
            Game1.tinyFont,
            "Enter to send, Esc to cancel.",
            new Vector2(_textBox.X, _textBox.Y + _textBox.Height + 18),
            Game1.textColor * 0.75f);

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
        => Math.Clamp(Game1.uiViewport.Width - 128, MinimumMenuWidth, MaximumMenuWidth);
}
