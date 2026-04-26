using StardewModdingAPI;

namespace ContentPatcher.Framework.Migrations;

/// <summary>A migration placeholder for a version with no format changes.</summary>
internal sealed class EmptyMigration : BaseMigration
{
    /// <summary>Construct an instance.</summary>
    /// <param name="majorVersion">The major component of the format version.</param>
    /// <param name="minorVersion">The minor component of the format version.</param>
    public EmptyMigration(int majorVersion, int minorVersion)
        : base(new SemanticVersion(majorVersion, minorVersion, 0)) { }
}
