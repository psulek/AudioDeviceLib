/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  AudioDeviceInfo.cs
  Immutable snapshot of an audio endpoint's identifying information, detached from the
  live Core Audio COM object.
*/

using System;
using AudioDeviceLib.CoreAudioApi;

namespace AudioDeviceLib;

/// <summary>
/// An immutable snapshot of an <see cref="AudioDevice"/>'s identifying information, detached from the
/// live Core Audio COM object. Create one with <see cref="AudioDevice.ToDeviceInfo"/>.
/// </summary>
/// <remarks>
/// All properties are captured when the snapshot is taken and never change, so an instance is safe to
/// read from any thread, keep, or store - including after the originating <see cref="AudioDevice"/>
/// has been disposed.
/// </remarks>
public sealed record AudioDeviceInfo
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
        // ReSharper disable NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        Id = id ?? string.Empty;
        Name = name ?? string.Empty;
        // ReSharper restore NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        Kind = kind;
        State = state;
        IsDefault = isDefault;
        IsDefaultCommunication = isDefaultCommunication;
    }

    /// <summary>Compares endpoint IDs without regard to case.</summary>
    /// <param name="other">The endpoint to compare.</param>
    /// <returns>Whether both instances identify the same endpoint.</returns>
    public bool Equals(AudioDeviceInfo? other) => other is not null && StringComparer.OrdinalIgnoreCase.Equals(Id, other.Id);

    /// <summary>Serves as the hash function, derived from <see cref="Id"/>.</summary>
    /// <returns>A hash code for this endpoint snapshot.</returns>
    public override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(Id);
    }

    /// <summary>Returns a human-readable description of this endpoint.</summary>
    /// <returns>A string in the form <c>Name (Kind)</c>, with default-role markers when applicable.</returns>
    public override string ToString()
    {
        return $"{Name} ({Kind}){(IsDefault ? " [Default]" : string.Empty)}{(IsDefaultCommunication ? " [DefaultComm]" : string.Empty)}";
    }
}