using System.Diagnostics.CodeAnalysis;
using ContentPatcher.Framework.ConfigModels;
using StardewModdingAPI;

namespace ContentPatcher.Framework.Migrations;

/// <summary>Migrates patches to format version 2.5.</summary>
[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Named for clarity.")]
internal class Migration_2_5 : BaseMigration
{
    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    public Migration_2_5()
        : base(new SemanticVersion(2, 5, 0)) { }

    /// <inheritdoc />
    public override bool TryMigrate(ref PatchConfig[] patches, [NotNullWhen(false)] out string? error)
    {
        if (!base.TryMigrate(ref patches, out error))
            return false;

        // 2.5 adds the LocalTokens feature
        foreach (PatchConfig patch in patches)
        {
            if (patch.LocalTokens?.Count > 0)
            {
                error = this.GetNounPhraseError($"using {nameof(patch.LocalTokens)}");
                return false;
            }
        }

        return true;
    }
}
