using Pathoschild.Stardew.Common.Integrations.GenericModConfigMenu;
using StardewModdingAPI;

namespace Pathoschild.Stardew.DebugMode.Framework;

/// <summary>Registers the mod configuration with Generic Mod Config Menu.</summary>
internal class GenericModConfigMenuIntegrationForDebugMode : IGenericModConfigMenuIntegrationFor<ModConfig>
{
    /*********
    ** Public methods
    *********/
    /// <inheritdoc />
    public void Register(GenericModConfigMenuIntegration<ModConfig> menu, IMonitor monitor)
    {
        menu
            .Register()
            .AddSectionTitle(I18n.Config_Title_GeneralOptions)
            .AddCheckbox(
                name: I18n.Config_EnableGameDebug_Name,
                tooltip: I18n.Config_EnableGameDebug_Desc,
                get: config => config.AllowGameDebug,
                set: (config, value) => config.AllowGameDebug = value
            )
            .AddCheckbox(
                name: I18n.Config_EnableDangerousHotkeys_Name,
                tooltip: I18n.Config_EnableDangerousHotkeys_Desc,
                get: config => config.AllowDangerousCommands,
                set: (config, value) => config.AllowDangerousCommands = value
            )

            .AddSectionTitle(I18n.Config_Title_Controls)
            .AddKeyBinding(
                name: I18n.Config_ToggleDebugKey_Name,
                tooltip: I18n.Config_ToggleDebugKey_Desc,
                get: config => config.Controls.ToggleDebug,
                set: (config, value) => config.Controls.ToggleDebug = value
            );
    }
}
