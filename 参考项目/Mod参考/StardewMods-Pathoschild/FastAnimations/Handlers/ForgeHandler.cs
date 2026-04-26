using Pathoschild.Stardew.FastAnimations.Framework;
using StardewValley;
using StardewValley.Menus;

namespace Pathoschild.Stardew.FastAnimations.Handlers;

/// <summary>Handles the volcano forge animations.</summary>
/// <remarks>See game logic in <see cref="ForgeMenu.receiveLeftClick"/>.</remarks>
internal sealed class ForgeHandler : BaseAnimationHandler
{
    /*********
    ** Public methods
    *********/
    /// <inheritdoc />
    public ForgeHandler(float multiplier)
        : base(multiplier) { }

    /// <inheritdoc />
    public override bool TryApply(int playerAnimationId)
    {
        return
            Game1.activeClickableMenu is ForgeMenu menu
            && menu.IsBusy()
            && this.ApplySkipsWhile(() =>
            {
                menu.update(Game1.currentGameTime);

                return menu.IsBusy();
            });
    }
}
