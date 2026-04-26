using System.Collections.Generic;
using ContentPatcher.Framework.Conditions;

namespace ContentPatcher.Framework.Tokens.ValueProviders;

/// <summary>A value provider for the farm type's map asset name relative to the game's <c>Content/Maps</c> folder.</summary>
internal class FarmMapAssetValueProvider : BaseValueProvider
{
    /*********
    ** Fields
    *********/
    /// <summary>Handles reading info from the current save.</summary>
    private readonly TokenSaveReader SaveReader;

    /// <summary>The farm type seen during the previous context update.</summary>
    private string? LastFarmType;

    /// <summary>The farm map asset from the previous context update.</summary>
    private string? FarmMapAsset;


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="saveReader">Handles reading info from the current save.</param>
    public FarmMapAssetValueProvider(TokenSaveReader saveReader)
        : base(ConditionType.FarmMapAsset, mayReturnMultipleValuesForRoot: false)
    {
        this.SaveReader = saveReader;
    }

    /// <inheritdoc />
    public override bool UpdateContext(IContext context)
    {
        string farmTypeId = this.SaveReader.GetFarmType();

        if (this.LastFarmType == farmTypeId)
            return false;

        this.FarmMapAsset = this.SaveReader.GetFarmMapAssetName();
        this.LastFarmType = farmTypeId;
        return true;
    }

    /// <inheritdoc />
    public override IEnumerable<string> GetValues(IInputArguments input)
    {
        this.AssertInput(input);

        return InvariantSets.FromValue(this.FarmMapAsset ?? string.Empty);
    }
}
