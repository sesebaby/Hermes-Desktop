namespace StardewHermesBridge.Ui;

using StardewModdingAPI;

public sealed class BridgeDebugMenu
{
    private readonly BridgeStatusOverlay _overlay;

    public BridgeDebugMenu(BridgeStatusOverlay overlay)
    {
        _overlay = overlay;
    }

    public void HandleButton(SButton button)
    {
        if (button == SButton.F8)
            _overlay.Visible = !_overlay.Visible;
    }
}
