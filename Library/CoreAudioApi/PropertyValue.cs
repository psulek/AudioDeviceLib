/*
  Copyright (c) 2026 Peter Šulek
  MIT License
*/

using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// Represents a property value read from a Core Audio <see cref="PropertyStore"/>.
/// This class encapsulates the property type and its value, and indicates whether the property is empty or null.
/// The snapshot holds no unmanaged resources and requires no cleanup.
/// </summary>
[PublicAPI]
public sealed record PropertyValue
{
    /// <summary>Gets whether the variant type is VT_EMPTY or VT_NULL.</summary>
    public bool IsEmpty { get; }

    /// <summary>
    /// Gets whether <see cref="VarType"/> is a variant type this library converts to a managed value.
    /// </summary>
    /// <remarks>
    /// <c>false</c> means the property exists but carries a type (a vector, an array, a stream) that
    /// is not read, so <see cref="Value"/> is <c>null</c> for a reason other than emptiness. Check
    /// this before treating a <c>null</c> <see cref="Value"/> as the property's actual value.
    /// </remarks>
    public bool IsSupported { get; }

    /// <summary>Gets the native variant type of the snapshot.</summary>
    public VarEnum VarType { get; }

    /// <summary>Gets the managed value, or null for an empty, null, or unsupported variant type.</summary>
    /// <remarks>
    /// <see cref="IsEmpty"/> and <see cref="IsSupported"/> tell the two null cases apart. Blob values
    /// are mutable byte arrays.
    /// </remarks>
    public object? Value { get; }

    internal PropertyValue(VarEnum varType, object? value, bool isSupported = true)
    {
        IsEmpty = varType is VarEnum.VT_EMPTY or VarEnum.VT_NULL;
        IsSupported = isSupported;
        VarType = varType;
        Value = value;
    }
}
