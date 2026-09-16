/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  AudioDeviceInfo.cs
  Immutable snapshot of an audio endpoint's identifying information, detached from the
  live Core Audio COM object.
*/

using System;
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

    /// <summary>Determines whether the given object describes the same endpoint, compared by <see cref="Id"/>.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><c>true</c> if <paramref name="obj"/> is an <see cref="AudioDeviceInfo"/> with the same ID.</returns>
    public override bool Equals(object obj)
    {
        return obj is AudioDeviceInfo other &&
               string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Serves as the hash function, derived from <see cref="Id"/>.</summary>
    /// <returns>A hash code for this endpoint snapshot.</returns>
    public override int GetHashCode()
    {
        return Id == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Id);
    }

    /// <summary>Returns a human-readable description of this endpoint.</summary>
    /// <returns>A string in the form <c>Name (Kind)</c>, with default-role markers when applicable.</returns>
    public override string ToString()
    {
        return $"{Name} ({Kind}){(IsDefault ? " [Default]" : string.Empty)}{(IsDefaultCommunication ? " [DefaultComm]" : string.Empty)}";
    }
}