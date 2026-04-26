using Microsoft.Xna.Framework;
using Pathoschild.Stardew.Common;
using Pathoschild.Stardew.TractorMod.Framework.Config;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using SObject = StardewValley.Object;

namespace Pathoschild.Stardew.TractorMod.Framework.Attachments;

/// <summary>An attachment for melee dagger weapons.</summary>
internal class MeleeDaggerAttachment : BaseAttachment
{
    /*********
    ** Fields
    *********/
    /// <summary>The attachment settings.</summary>
    private readonly MeleeDaggerConfig Config;


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="config">The attachment settings.</param>
    /// <param name="modRegistry">Fetches metadata about loaded mods.</param>
    public MeleeDaggerAttachment(MeleeDaggerConfig config, IModRegistry modRegistry)
        : base(modRegistry)
    {
        this.Config = config;
    }

    /// <inheritdoc />
    public override bool IsEnabled(Farmer player, Tool? tool, Item? item, GameLocation location)
    {
        return tool is MeleeWeapon { type.Value: MeleeWeapon.dagger };
    }

    /// <inheritdoc />
    public override bool Apply(Vector2 tile, SObject? tileObj, TerrainFeature? tileFeature, Farmer player, Tool? tool, Item? item, GameLocation location)
    {
        tool = tool.AssertNotNull();

        // clear dead crops
        if (this.Config.ClearDeadCrops && this.TryClearDeadCrop(location, tile, tileFeature, player))
            return true;

        // break mine containers
        if (this.Config.BreakMineContainers && this.TryBreakContainer(tile, tileObj, player, tool))
            return true;

        // harvest grass
        if (this.Config.HarvestGrass && this.TryHarvestGrass(tileFeature as Grass, location, tile, player, tool))
            return true;

        // attack monsters
        if (this.Config.AttackMonsters && this.UseWeaponOnTile((MeleeWeapon)tool, tile, player, location))
            return false;

        return false;
    }
}
