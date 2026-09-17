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
public sealed class PropertyValue
{
    /// <summary>Gets whether the variant type is VT_EMPTY or VT_NULL.</summary>
    public bool IsEmpty { get; }

    /// <summary>Gets the native variant type of the snapshot.</summary>
    public VarEnum VarType { get; }

    /// <summary>Gets the managed value, or null for an empty, null, or unsupported variant type.</summary>
    /// <remarks>VarType identifies unsupported types even when IsEmpty is false. Blob values are mutable byte arrays.</remarks>
    public object Value { get; }

    internal PropertyValue(VarEnum varType, object value)
    {
        IsEmpty = varType == VarEnum.VT_EMPTY || varType == VarEnum.VT_NULL;
        VarType = varType;
        Value = value;
    }
}
