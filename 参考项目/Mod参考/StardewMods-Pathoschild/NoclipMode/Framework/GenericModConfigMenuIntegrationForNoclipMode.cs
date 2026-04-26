using Pathoschild.Stardew.Common.Integrations.GenericModConfigMenu;
using StardewModdingAPI;

namespace Pathoschild.Stardew.NoclipMode.Framework;

/// <summary>Registers the mod configuration with Generic Mod Config Menu.</summary>
internal class GenericModConfigMenuIntegrationForNoclipMode : IGenericModConfigMenuIntegrationFor<ModConfig>
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
                name: I18n.Config_EnabledMessage_Name,
                tooltip: I18n.Config_EnabledMessage_Desc,
                get: config => config.ShowEnabledMessage,
                set: (config, value) => config.ShowEnabledMessage = value
            )
            .AddCheckbox(
                name: I18n.Config_DisabledMessage_Name,
                tooltip: I18n.Config_DisabledMessage_Desc,
                get: config => config.ShowDisabledMessage,
                set: (config, value) => config.ShowDisabledMessage = value
            )

            .AddSectionTitle(I18n.Config_Title_Controls)
            .AddKeyBinding(
                name: I18n.Config_ToggleKey_Name,
                tooltip: I18n.Config_ToggleKey_Desc,
                get: config => config.ToggleKey,
                set: (config, value) => config.ToggleKey = value
            );
    }
}
