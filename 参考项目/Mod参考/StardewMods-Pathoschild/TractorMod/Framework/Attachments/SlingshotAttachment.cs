using Microsoft.Xna.Framework;
using Pathoschild.Stardew.Common;
using Pathoschild.Stardew.TractorMod.Framework.Config;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using SObject = StardewValley.Object;

namespace Pathoschild.Stardew.TractorMod.Framework.Attachments;

/// <summary>An attachment for slingshots.</summary>
internal class SlingshotAttachment : BaseAttachment
{
    /*********
    ** Fields
    *********/
    /// <summary>The attachment settings.</summary>
    private readonly GenericAttachmentConfig Config;


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="config">The attachment settings.</param>
    /// <param name="modRegistry">Fetches metadata about loaded mods.</param>
    public SlingshotAttachment(GenericAttachmentConfig config, IModRegistry modRegistry)
        : base(modRegistry, rateLimit: 60)
    {
        this.Config = config;
    }

    /// <inheritdoc />
    public override bool IsEnabled(Farmer player, Tool? tool, Item? item, GameLocation location)
    {
        return
            this.Config.Enable
            && tool is Slingshot;
    }

    /// <inheritdoc />
    public override bool Apply(Vector2 tile, SObject? tileObj, TerrainFeature? tileFeature, Farmer player, Tool? tool, Item? item, GameLocation location)
    {
        Slingshot slingshot = (Slingshot)tool.AssertNotNull();

        slingshot.canPlaySound = false;
        return this.UseToolOnTile(slingshot, tile, player, location);
    }
}
