using System.Collections.Generic;

namespace Pathoschild.Stardew.Common.Integrations.TrainStation;

/// <summary>The API provided by the Train Station mod.</summary>
public interface ITrainStationApi
{
    /// <summary>Get the available stops from the player's current location.</summary>
    /// <param name="isBoat">Whether to get boat stops (<c>true</c>) or train stops (<c>false</c>).</param>
    IEnumerable<ITrainStationStopModel> GetAvailableStops(bool isBoat);
}
