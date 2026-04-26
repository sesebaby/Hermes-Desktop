namespace Pathoschild.Stardew.CentralStation.Framework;

/// <summary>Extension methods for the <see cref="StopNetworks"/> enum.</summary>
internal static class StopNetworksExtensions
{
    /*********
    ** Public methods
    *********/
    /// <summary>Get whether a networks value has any of the given flags.</summary>
    /// <param name="networks">The networks value to check.</param>
    /// <param name="flags">The flags to match in the network value.</param>
    public static bool HasAnyFlag(this StopNetworks networks, StopNetworks flags)
    {
        return (networks & flags) != 0;
    }

    /// <summary>Get whether a networks value has multiple flags set.</summary>
    /// <param name="networks">The networks value to check.</param>
    public static bool HasMultipleFlags(this StopNetworks networks)
    {
        return
            (networks & (networks - 1)) > 0  // not a multiple of two (i.e. not one of the single values)
            || networks == (StopNetworks)~0; // or explicitly set to all
    }

    /// <summary>Get the single network which the player should follow.</summary>
    /// <param name="stopNetworks">The networks on which a stop is available.</param>
    public static StopNetworks GetPreferred(this StopNetworks stopNetworks)
    {
        // preferred order: train, bus, boat
        if (!stopNetworks.HasFlag(StopNetworks.Train))
        {
            if (stopNetworks.HasFlag(StopNetworks.Bus))
                return StopNetworks.Bus;

            if (stopNetworks.HasFlag(StopNetworks.Boat))
                return StopNetworks.Boat;
        }

        return StopNetworks.Train;
    }
}
