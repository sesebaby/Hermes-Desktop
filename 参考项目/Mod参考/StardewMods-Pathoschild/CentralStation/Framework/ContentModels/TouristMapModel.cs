using System.Collections.Generic;

namespace Pathoschild.Stardew.CentralStation.Framework.ContentModels;

/// <summary>The data for a map file containing random tourists.</summary>
internal class TouristMapModel
{
    /// <summary>The asset name of the map to load.</summary>
    public string? FromMap { get; set; }

    /// <summary>The data for each tourist in the map.</summary>
    public Dictionary<string, TouristModel>? Tourists { get; set; }
}
