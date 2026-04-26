using System;
using Pathoschild.Stardew.Common.Integrations.GenericModConfigMenu;
using StardewModdingAPI;

namespace Pathoschild.Stardew.SkipIntro.Framework;

/// <summary>Registers the mod configuration with Generic Mod Config Menu.</summary>
internal class GenericModConfigMenuIntegrationForSkipIntro : IGenericModConfigMenuIntegrationFor<ModConfig>
{
    /*********
    ** Public methods
    *********/
    /// <inheritdoc />
    public void Register(GenericModConfigMenuIntegration<ModConfig> menu, IMonitor monitor)
    {
        menu
            .Register()
            .AddDropdown(
                name: I18n.Config_SkipTo_Name,
                tooltip: I18n.Config_SkipTo_Tooltip,
                allowedValues: Enum.GetNames(typeof(Screen)),
                formatAllowedValue: this.TranslateScreen,
                get: config => config.SkipTo.ToString(),
                set: (config, value) => config.SkipTo = (Screen)Enum.Parse(typeof(Screen), value)
            );
    }


    /*********
    ** Private methods
    *********/
    /// <summary>Get the translated text for a screen value.</summary>
    /// <param name="rawScreen">The raw screen value.</param>
    private string TranslateScreen(string rawScreen)
    {
        if (!Enum.TryParse(rawScreen, out Screen screen))
            return rawScreen;

        return screen switch
        {
            Screen.Title => I18n.Config_SkipTo_Values_TitleMenu(),
            Screen.Load => I18n.Config_SkipTo_Values_LoadMenu(),
            Screen.JoinCoop => I18n.Config_SkipTo_Values_JoinCoop(),
            Screen.HostCoop => I18n.Config_SkipTo_Values_HostCoop(),
            _ => screen.ToString()
        };
    }
}
