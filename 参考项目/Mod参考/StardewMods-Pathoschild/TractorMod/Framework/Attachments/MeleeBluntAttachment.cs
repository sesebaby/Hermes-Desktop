using Microsoft.Xna.Framework;
using Pathoschild.Stardew.Common;
using Pathoschild.Stardew.TractorMod.Framework.Config;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using SObject = StardewValley.Object;

namespace Pathoschild.Stardew.TractorMod.Framework.Attachments;

/// <summary>An attachment for melee blunt weapons.</summary>
internal class MeleeBluntAttachment : BaseAttachment
{
    /*********
    ** Fields
    *********/
    /// <summary>The attachment settings.</summary>
    private readonly MeleeBluntConfig Config;


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="config">The attachment settings.</param>
    /// <param name="modRegistry">Fetches metadata about loaded mods.</param>
    public MeleeBluntAttachment(MeleeBluntConfig config, IModRegistry modRegistry)
        : base(modRegistry)
    {
        this.Config = config;
    }

    /// <inheritdoc />
    public override bool IsEnabled(Farmer player, Tool? tool, Item? item, GameLocation location)
    {
        return tool is MeleeWeapon { type.Value: not (MeleeWeapon.dagger or MeleeWeapon.defenseSword) };
    }

    /// <inheritdoc />
    public override bool Apply(Vector2 tile, SObject? tileObj, TerrainFeature? tileFeature, Farmer player, Tool? tool, Item? item, GameLocation location)
    {
        tool = tool.AssertNotNull();

        // break mine containers
        if (this.Config.BreakMineContainers && this.TryBreakContainer(tile, tileObj, player, tool))
            return true;

        // attack monsters
        if (this.Config.AttackMonsters && this.UseWeaponOnTile((MeleeWeapon)tool, tile, player, location))
            return false;

        return false;
    }
}
