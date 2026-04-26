using Pathoschild.Stardew.FastAnimations.Framework;
using StardewValley;
using StardewValley.Tools;

namespace Pathoschild.Stardew.FastAnimations.Handlers;

/// <summary>Handles the tool swinging animation.</summary>
internal sealed class WeaponSwingHandler : BaseAnimationHandler
{
    /*********
    ** Public methods
    *********/
    /// <inheritdoc />
    public WeaponSwingHandler(float multiplier)
        : base(multiplier) { }

    /// <inheritdoc />
    public override bool TryApply(int playerAnimationId)
    {
        return
            Game1.player.UsingTool
            && Game1.player.CurrentTool is MeleeWeapon weapon
            && !weapon.isScythe()
            && (!weapon.isOnSpecial || weapon.type.Value != MeleeWeapon.defenseSword) // don't speed up blocking animation
            && this.SpeedUpPlayer(until: () => !Game1.player.UsingTool);
    }
}
