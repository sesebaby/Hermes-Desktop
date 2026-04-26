using StardewValley;

namespace Pathoschild.Stardew.FastAnimations.Framework;

/// <summary>Takes care of skipping or accelerating an animation.</summary>
internal interface IAnimationHandler
{
    /// <summary>Perform any logic needed on update if the animation is active.</summary>
    /// <param name="playerAnimationId">The player's current animation ID.</param>
    /// <returns>Returns whether the animation is active.</returns>
    bool TryApply(int playerAnimationId);

    /// <summary>Perform any updates needed when the player enters a new location.</summary>
    /// <param name="location">The new location.</param>
    void OnNewLocation(GameLocation location);
}
