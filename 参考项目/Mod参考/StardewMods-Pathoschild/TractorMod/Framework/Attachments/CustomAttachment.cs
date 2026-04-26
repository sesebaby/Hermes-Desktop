using Microsoft.Xna.Framework;
using Pathoschild.Stardew.Common.Utilities;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using SObject = StardewValley.Object;

namespace Pathoschild.Stardew.TractorMod.Framework.Attachments;

/// <summary>An attachment for any configured custom tools.</summary>
internal class CustomAttachment : BaseAttachment
{
    /*********
    ** Fields
    *********/
    /// <summary>The enabled custom tool or item names.</summary>
    private readonly InvariantSet CustomNames;


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="customAttachments">The enabled custom tool or item names.</param>
    /// <param name="modRegistry">Fetches metadata about loaded mods.</param>
    public CustomAttachment(string[] customAttachments, IModRegistry modRegistry)
        : base(modRegistry)
    {
        this.CustomNames = new InvariantSet(customAttachments);
    }

    /// <inheritdoc />
    public override bool IsEnabled(Farmer player, Tool? tool, Item? item, GameLocation location)
    {
        return
            (tool != null && this.CustomNames.Contains(tool.Name))
            || (item != null && this.CustomNames.Contains(item.Name));
    }

    /// <inheritdoc />
    public override bool Apply(Vector2 tile, SObject? tileObj, TerrainFeature? tileFeature, Farmer player, Tool? tool, Item? item, GameLocation location)
    {
        // apply melee weapon
        if (tool is MeleeWeapon weapon)
            return this.UseWeaponOnTile(weapon, tile, player, location);

        // apply tool
        if (tool != null && this.CustomNames.Contains(tool.Name))
            return this.UseToolOnTile(tool, tile, player, location);

        // apply item
        if (item is { Stack: > 0 } && this.CustomNames.Contains(item.Name))
        {
            if (item is SObject obj && obj.isPlaceable() && obj.canBePlacedHere(location, tile) && obj.placementAction(location, (int)(tile.X * Game1.tileSize), (int)(tile.Y * Game1.tileSize), player))
            {
                this.ConsumeItem(player, item);
                return true;
            }
        }

        return false;
    }
}
