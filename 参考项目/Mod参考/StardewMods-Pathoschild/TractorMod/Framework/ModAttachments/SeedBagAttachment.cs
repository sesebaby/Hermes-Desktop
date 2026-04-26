using Microsoft.Xna.Framework;
using Pathoschild.Stardew.Common;
using Pathoschild.Stardew.TractorMod.Framework.Config;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using SObject = StardewValley.Object;

namespace Pathoschild.Stardew.TractorMod.Framework.ModAttachments;

/// <summary>An attachment for the Seed Bag mod.</summary>
internal class SeedBagAttachment : BaseAttachment
{
    /*********
    ** Fields
    *********/
    /// <summary>The attachment settings.</summary>
    private readonly GenericAttachmentConfig Config;


    /*********
    ** Accessors
    *********/
    /// <summary>The unique ID for the Seed Bag mod.</summary>
    internal const string ModId = "Platonymous.SeedBag";


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="config">The attachment settings.</param>
    /// <param name="modRegistry">Fetches metadata about loaded mods.</param>
    public SeedBagAttachment(GenericAttachmentConfig config, IModRegistry modRegistry)
        : base(modRegistry)
    {
        this.Config = config;
    }

    /// <inheritdoc />
    public override bool IsEnabled(Farmer player, Tool? tool, Item? item, GameLocation location)
    {
        return
            this.Config.Enable
            && tool is { Name: "Seed Bag" };
    }

    /// <inheritdoc />
    public override bool Apply(Vector2 tile, SObject? tileObj, TerrainFeature? tileFeature, Farmer player, Tool? tool, Item? item, GameLocation location)
    {
        tool = tool.AssertNotNull();

        // apply to plain dirt
        if (tileFeature is HoeDirt { crop: null })
            return this.UseToolOnTile(tool, tile, player, location);

        return false;
    }
}
