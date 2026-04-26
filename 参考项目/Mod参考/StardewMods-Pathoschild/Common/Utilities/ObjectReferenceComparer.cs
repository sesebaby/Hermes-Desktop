using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Pathoschild.Stardew.Common.Utilities;

/// <summary>A comparer which considers two references equal if they point to the same instance.</summary>
/// <typeparam name="T">The value type.</typeparam>
internal class ObjectReferenceComparer<T> : IEqualityComparer<T>
{
    /*********
    ** Public methods
    *********/
    /// <inheritdoc />
    public bool Equals(T? x, T? y)
    {
        return object.ReferenceEquals(x, y);
    }

    /// <inheritdoc />
    [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1024:Compare symbols correctly", Justification = "Comparing by object reference is intended.")]
    public int GetHashCode(T obj)
    {
        return RuntimeHelpers.GetHashCode(obj);
    }
}
