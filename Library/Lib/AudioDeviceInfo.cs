// namespace AudioDeviceLib.Lib;
//
// /// <summary>
// /// An immutable snapshot of an <see cref="AudioDevice"/>'s identifying information, detached from the
// /// live Core Audio COM object. Create one with <see cref="AudioDevice.ToDeviceInfo"/>.
// /// </summary>
// /// <remarks>
// /// All properties are captured when the snapshot is taken and never change, so an instance is safe to
// /// read from any thread, keep, or store — including after the originating <see cref="AudioDevice"/>
// /// has been disposed.
// /// </remarks>
// public sealed class AudioDeviceInfo
// {
//     /// <summary>1-based position in the enumeration of all active endpoints.</summary>
//     public int Index { get; }
//
//     /// <summary>True if this endpoint is the current default device for its kind (multimedia role).</summary>
//     public bool IsDefault { get; }
//
//     /// <summary>True if this endpoint is the current default communications device for its kind.</summary>
//     public bool IsDefaultCommunication { get; }
//
//     /// <summary>Whether this is a playback (render) or recording (capture) endpoint.</summary>
//     public AudioDeviceKind Kind { get; }
//
//     /// <summary>Friendly name, e.g. "Speakers (Realtek High Definition Audio)".</summary>
//     public string Name { get; }
//
//     /// <summary>Endpoint ID, e.g. "{0.0.0.00000000}.{c4aadd95-...}".</summary>
//     public string Id { get; }
//     
//     internal AudioDeviceInfo(int index, bool isDefault, bool isDefaultCommunication, AudioDeviceKind kind, string name, string id)
//     {
//         Index = index;
//         IsDefault = isDefault;
//         IsDefaultCommunication = isDefaultCommunication;
//         Kind = kind;
//         Name = name;
//         Id = id;
//     }
// }