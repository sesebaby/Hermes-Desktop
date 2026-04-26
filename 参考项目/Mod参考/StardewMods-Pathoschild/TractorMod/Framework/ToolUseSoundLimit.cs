namespace Pathoschild.Stardew.TractorMod.Framework;

/// <summary>The limit to set on tool sounds.</summary>
internal enum ToolUseSoundLimit
{
    /// <summary>Apply the default behavior (currently <see cref="OncePerTick"/>).</summary>
    Default,

    /// <summary>Only play a single tool sound per tick.</summary>
    OncePerTick,

    /// <summary>Play every tool use sound.</summary>
    /// <remarks>This may lead to <c>InstancePlayLimitException</c> errors when the default tractor radius is increased.</remarks>
    Unlimited
}
