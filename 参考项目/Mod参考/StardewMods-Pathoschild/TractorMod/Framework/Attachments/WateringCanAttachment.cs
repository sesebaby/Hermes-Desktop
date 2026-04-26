using Microsoft.Xna.Framework;
using Pathoschild.Stardew.TractorMod.Framework.Config;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using SObject = StardewValley.Object;

namespace Pathoschild.Stardew.TractorMod.Framework.Attachments;

/// <summary>An attachment for the watering can.</summary>
internal class WateringCanAttachment : BaseAttachment
{
    /*********
    ** Fields
    *********/
    /// <summary>The attachment settings.</summary>
    private readonly GenericAttachmentConfig Config;

    /// <summary>An infinite watering can to apply.</summary>
    private readonly WateringCan WateringCan = new()
    {
        IsBottomless = true, // no water drain
        IsEfficient = true, // no stamina drain
        WaterLeft = 100
    };


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="config">The attachment settings.</param>
    /// <param name="modRegistry">Fetches metadata about loaded mods.</param>
    public WateringCanAttachment(GenericAttachmentConfig config, IModRegistry modRegistry)
        : base(modRegistry)
    {
        this.Config = config;
    }

    /// <inheritdoc />
    public override bool IsEnabled(Farmer player, Tool? tool, Item? item, GameLocation location)
    {
        return
            this.Config.Enable
            && tool is WateringCan;
    }

    /// <inheritdoc />
    /// <remarks>Volcano logic derived from <see cref="VolcanoDungeon.performToolAction"/>.</remarks>
    public override bool Apply(Vector2 tile, SObject? tileObj, TerrainFeature? tileFeature, Farmer player, Tool? tool, Item? item, GameLocation location)
    {
        // water dirt
        if (this.TryGetHoeDirt(tileFeature, tileObj, out HoeDirt? dirt, out _, out _) && dirt.state.Value != HoeDirt.watered)
            return this.UseWateringCanOnTile(tile, player, location);

        // cool lava
        int x = (int)tile.X;
        int y = (int)tile.Y;
        if (location is VolcanoDungeon dungeon && dungeon.isTileOnMap(x, y) && dungeon.waterTiles[x, y] && !dungeon.cooledLavaTiles.ContainsKey(tile))
            return this.UseWateringCanOnTile(tile, player, location);

        return false;
    }


    /*********
    ** Private methods
    *********/
    /// <summary>Use the watering can on a tile.</summary>
    /// <param name="tile">The tile to affect.</param>
    /// <param name="player">The current player.</param>
    /// <param name="location">The current location.</param>
    /// <returns>Returns <c>true</c> for convenience when implementing tools.</returns>
    private bool UseWateringCanOnTile(Vector2 tile, Farmer player, GameLocation location)
    {
        if (this.UseToolOnTile(this.WateringCan, tile, player, location))
        {
            if (player.CurrentTool?.PlayUseSounds is true)
                location.localSound("wateringCan"); // normally played in Tool.endUsing
            return true;
        }

        return false;
    }
}
