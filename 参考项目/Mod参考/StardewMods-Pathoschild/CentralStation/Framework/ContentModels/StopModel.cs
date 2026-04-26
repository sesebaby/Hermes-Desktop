using Microsoft.Xna.Framework;

namespace Pathoschild.Stardew.CentralStation.Framework.ContentModels;

/// <summary>A content model which defines a destination that can be visited by the player.</summary>
internal class StopModel
{
    /*********
    ** Accessors
    *********/
    /// <inheritdoc cref="Stop.DisplayName" />
    public string? DisplayName { get; set; }

    /// <inheritdoc cref="Stop.DisplayNameInCombinedLists" />
    public string? DisplayNameInCombinedLists { get; set; }

    /// <inheritdoc cref="Stop.ToLocation" />
    public string ToLocation { get; set; } = null!; // validated on load

    /// <inheritdoc cref="Stop.ToTile" />
    public Point? ToTile { get; set; }

    /// <inheritdoc cref="Stop.ToFacingDirection" />
    public string ToFacingDirection { get; set; } = "down";

    /// <inheritdoc cref="Stop.Cost" />
    public int Cost { get; set; }

    /// <inheritdoc cref="Stop.Network" />
    public StopNetworks Network { get; set; } = StopNetworks.Train;

    /// <inheritdoc cref="Stop.Condition" />
    public string? Condition { get; set; }


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an empty instance.</summary>
    public StopModel() { }

    /// <summary>Construct an instance.</summary>
    /// <param name="displayName"><inheritdoc cref="DisplayName" path="/summary" /></param>
    /// <param name="toLocation"><inheritdoc cref="ToLocation" path="/summary" /></param>
    /// <param name="toTile"><inheritdoc cref="ToTile" path="/summary" /></param>
    /// <param name="toFacingDirection"><inheritdoc cref="ToFacingDirection" path="/summary" /></param>
    /// <param name="cost"><inheritdoc cref="Cost" path="/summary" /></param>
    /// <param name="network"><inheritdoc cref="Network" path="/summary" /></param>
    /// <param name="condition"><inheritdoc cref="Condition" path="/summary" /></param>
    public StopModel(string displayName, string toLocation, Point? toTile, string toFacingDirection, int cost, StopNetworks network, string? condition)
    {
        this.DisplayName = displayName;
        this.ToLocation = toLocation;
        this.ToTile = toTile;
        this.ToFacingDirection = toFacingDirection;
        this.Cost = cost;
        this.Network = network;
        this.Condition = condition;
    }
}
