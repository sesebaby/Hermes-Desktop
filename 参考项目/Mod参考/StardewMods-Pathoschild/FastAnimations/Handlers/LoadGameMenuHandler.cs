using Pathoschild.Stardew.FastAnimations.Framework;
using StardewValley;
using StardewValley.Menus;

namespace Pathoschild.Stardew.FastAnimations.Handlers;

/// <summary>Handles the load game menu animations.</summary>
/// <remarks>See game logic in <see cref="LoadGameMenu"/>.</remarks>
internal sealed class LoadGameMenuHandler : BaseAnimationHandler
{
    /*********
    ** Public methods
    *********/
    /// <inheritdoc />
    public LoadGameMenuHandler(float multiplier)
        : base(multiplier) { }

    /// <inheritdoc />
    public override bool TryApply(int playerAnimationId)
    {
        return
            Game1.activeClickableMenu is TitleMenu
            && TitleMenu.subMenu is LoadGameMenu { timerToLoad: > 0 } loadMenu
            && this.ApplySkipsWhile(() =>
            {
                loadMenu.update(Game1.currentGameTime);

                return loadMenu.timerToLoad > 0;
            });
    }
}
