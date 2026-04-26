using StardewModdingAPI.Events;

namespace Pathoschild.Stardew.FastAnimations.Framework;

/// <summary>An implementation of <see cref="IAnimationHandler"/> that needs to be updated when the object list changes.</summary>
internal interface IAnimationHandlerWithObjectList : IAnimationHandler
{
    /// <summary>Perform any updates needed when the object list in the current location changes.</summary>
    /// <param name="e">The event args.</param>
    void OnObjectListChanged(ObjectListChangedEventArgs e);
}
