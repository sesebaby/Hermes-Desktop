using Pathoschild.Stardew.Common.Integrations.GenericModConfigMenu;
using StardewModdingAPI;

namespace Pathoschild.Stardew.HorseFluteAnywhere.Framework;

/// <summary>Registers the mod configuration with Generic Mod Config Menu.</summary>
internal class GenericModConfigMenuIntegrationForHorseFluteAnywhere : IGenericModConfigMenuIntegrationFor<ModConfig>
{
    /*********
    ** Public methods
    *********/
    /// <inheritdoc />
    public void Register(GenericModConfigMenuIntegration<ModConfig> menu, IMonitor monitor)
    {
        var defaultConfig = new ModConfig();

        menu
            .Register()
            .AddCheckbox(
                name: I18n.Config_RequireFlute_Name,
                tooltip: I18n.Config_RequireFlute_Description,
                get: config => config.RequireHorseFlute,
                set: (config, value) => config.RequireHorseFlute = value
            )
            .AddKeyBinding(
                name: I18n.Config_SummonHorseButton_Name,
                tooltip: () => I18n.Config_SummonHorseButton_Description(defaultValue: defaultConfig.SummonHorseKey.ToString()),
                get: config => config.SummonHorseKey,
                set: (config, value) => config.SummonHorseKey = value
            );
    }
}
