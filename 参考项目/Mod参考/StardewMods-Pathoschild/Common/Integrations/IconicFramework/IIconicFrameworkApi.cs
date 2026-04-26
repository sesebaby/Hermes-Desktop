using System;
using Microsoft.Xna.Framework;

namespace Pathoschild.Stardew.Common.Integrations.IconicFramework;

/// <summary>The API provided by the Iconic Framework mod.</summary>
public interface IIconicFrameworkApi
{
    /*********
    ** Methods
    *********/
    /// <summary>Add a single icon with a default identifier and onClick action.</summary>
    /// <param name="texturePath">The path to the texture icon.</param>
    /// <param name="sourceRect">The source rectangle of the icon.</param>
    /// <param name="getTitle">Text to appear as the title in the Radial Menu.</param>
    /// <param name="getDescription">Text to appear when hovering over the icon.</param>
    /// <param name="onClick">An action to perform when the icon is pressed.</param>
    /// <param name="onRightClick">An optional secondary action to perform when the icon is pressed.</param>
    public void AddToolbarIcon(string texturePath, Rectangle? sourceRect, Func<string>? getTitle, Func<string>? getDescription, Action onClick, Action? onRightClick = null);
}
