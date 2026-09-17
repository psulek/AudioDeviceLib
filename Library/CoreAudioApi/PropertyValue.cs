using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// Represents a property value read from a Core Audio <see cref="PropertyStore"/>.
/// This class encapsulates the property type and its value, and indicates whether the property is empty or null.
/// This is snapshot of COM PROPVARIANT value, and does not hold any unmanaged resources, so it does not need to be disposed.
/// </summary>
[PublicAPI]
public sealed class PropertyValue
{
    public bool IsEmpty { get; }

    public VarEnum VarType { get; }

    public object Value { get; }

    internal PropertyValue(VarEnum varType, object value)
    {
        IsEmpty = varType == VarEnum.VT_EMPTY || varType == VarEnum.VT_NULL;
        VarType = varType;
        Value = value;
    }
}