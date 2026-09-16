/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  AudioDeviceInfo.cs
  Immutable snapshot of an audio endpoint's identifying information, detached from the
  live Core Audio COM object.
*/

using AudioDeviceLib.CoreAudioApi;

namespace AudioDeviceLib.Lib;

/// <summary>
/// An immutable snapshot of an <see cref="AudioDevice"/>'s identifying information, detached from the
/// live Core Audio COM object. Create one with <see cref="AudioDevice.ToDeviceInfo"/>.
/// </summary>
/// <remarks>
/// All properties are captured when the snapshot is taken and never change, so an instance is safe to
/// read from any thread, keep, or store — including after the originating <see cref="AudioDevice"/>
/// has been disposed.
/// </remarks>
public sealed class AudioDeviceInfo
{
    /// <summary>True if this endpoint is the current default device for its kind (multimedia role).</summary>
    public bool IsDefault { get; }

    /// <summary>True if this endpoint is the current default communications device for its kind.</summary>
    public bool IsDefaultCommunication { get; }

    /// <summary>Whether this is a playback (render) or recording (capture) endpoint.</summary>
    public AudioDeviceKind Kind { get; }

    /// <summary>Friendly name, e.g. "Speakers (Realtek High Definition Audio)".</summary>
    public string Name { get; }

    /// <summary>Endpoint ID, e.g. "{0.0.0.00000000}.{c4aadd95-...}".</summary>
    public string Id { get; }
    
    /// <summary>Current state of the endpoint.</summary>
    public DeviceState State { get; }
    
    internal AudioDeviceInfo(string id, string name, AudioDeviceKind kind, DeviceState state, bool isDefault, bool isDefaultCommunication)
    {
        Id = id;
        Name = name;
        Kind = kind;
        State = state;
        IsDefault = isDefault;
        IsDefaultCommunication = isDefaultCommunication;
    }
}