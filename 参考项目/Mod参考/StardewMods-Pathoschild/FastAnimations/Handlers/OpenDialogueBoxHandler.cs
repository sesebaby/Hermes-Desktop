using Pathoschild.Stardew.FastAnimations.Framework;
using StardewValley;
using StardewValley.Menus;

namespace Pathoschild.Stardew.FastAnimations.Handlers;

/// <summary>Handles the dialogue-box-open animation.</summary>
/// <remarks>See game logic in <see cref="DialogueBox.update"/>.</remarks>
internal sealed class OpenDialogueBoxHandler : BaseAnimationHandler
{
    /*********
    ** Public methods
    *********/
    /// <inheritdoc />
    public OpenDialogueBoxHandler(float multiplier)
        : base(multiplier) { }

    /// <inheritdoc />
    public override bool TryApply(int playerAnimationId)
    {
        return
            Game1.activeClickableMenu is DialogueBox { transitioning: true } menu
            && this.ApplySkipsWhile(() =>
            {
                menu.update(Game1.currentGameTime);

                return
                    object.ReferenceEquals(Game1.activeClickableMenu, menu)
                    && menu.transitioning;
            });
    }
}
