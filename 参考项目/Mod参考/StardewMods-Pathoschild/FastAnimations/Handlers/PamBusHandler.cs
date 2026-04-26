using Pathoschild.Stardew.FastAnimations.Framework;
using StardewValley;
using StardewValley.Locations;

namespace Pathoschild.Stardew.FastAnimations.Handlers;

/// <summary>Handles Pam's bus arriving/departing animation.</summary>
/// <remarks>See game logic in <see cref="BusStop.UpdateWhenCurrentLocation"/> and <see cref="Desert.UpdateWhenCurrentLocation"/>.</remarks>
internal sealed class PamBusHandler : BaseAnimationHandler
{
    /*********
    ** Public methods
    *********/
    /// <inheritdoc />
    public PamBusHandler(float multiplier)
        : base(multiplier) { }

    /// <inheritdoc />
    public override bool TryApply(int playerAnimationId)
    {
        return
            this.IsAnimating()
            && this.ApplySkipsWhile(() =>
            {
                Game1.currentLocation.UpdateWhenCurrentLocation(Game1.currentGameTime);

                return this.IsAnimating();
            });
    }


    /*********
    ** Private methods
    *********/
    /// <summary>Get whether the animation is playing now.</summary>
    private bool IsAnimating()
    {
        return Game1.currentLocation switch
        {
            BusStop stop => stop.drivingOff || stop.drivingBack,
            Desert desert => desert.drivingOff || desert.drivingBack,
            _ => false
        };
    }
}
