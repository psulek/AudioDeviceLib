/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  AudioDevice.cs
  Managed representation of a Windows audio endpoint.

  Derived from the AudioDevice class of AudioDeviceCmdlets by Francois Gendron (MIT),
  https://github.com/frgnca/AudioDeviceCmdlets
*/

using System;
using AudioDeviceLib.CoreAudioApi;

namespace AudioDeviceLib.Lib;

/// <summary>
/// A single active Windows audio endpoint, wrapping the underlying <see cref="MMDevice"/>
/// and exposing volume/mute helpers.
/// </summary>
public sealed class AudioDevice
{
    /// <summary>1-based position in the enumeration of all active endpoints.</summary>
    public int Index { get; }

    /// <summary>True if this endpoint is the current default device for its kind (multimedia role).</summary>
    public bool IsDefault { get; internal set; }

    /// <summary>True if this endpoint is the current default communications device for its kind.</summary>
    public bool IsDefaultCommunication { get; internal set; }

    /// <summary>Whether this is a playback (render) or recording (capture) endpoint.</summary>
    public AudioDeviceKind Kind { get; }

    /// <summary>Friendly name, e.g. "Speakers (Realtek High Definition Audio)".</summary>
    public string Name { get; }

    /// <summary>Endpoint ID, e.g. "{0.0.0.00000000}.{c4aadd95-...}".</summary>
    public string Id { get; }

    /// <summary>The underlying Core Audio device, for advanced scenarios.</summary>
    public MMDevice Device { get; }

    internal AudioDevice(int index, MMDevice baseDevice, bool isDefault, bool isDefaultCommunication)
    {
        if (baseDevice == null)
            throw new ArgumentNullException(nameof(baseDevice));

        Index = index;
        IsDefault = isDefault;
        IsDefaultCommunication = isDefaultCommunication;
        Kind = baseDevice.DataFlow == EDataFlow.eCapture ? AudioDeviceKind.Recording : AudioDeviceKind.Playback;
        Name = baseDevice.FriendlyName;
        Id = baseDevice.ID;
        Device = baseDevice;
    }

    /// <summary>Master volume as a percentage in the range 0..100.</summary>
    public float GetVolumePercent()
    {
        return Device.AudioEndpointVolume.MasterVolumeLevelScalar * 100f;
    }

    /// <summary>Sets master volume from a percentage in the range 0..100 (values are clamped).</summary>
    public void SetVolumePercent(float percent)
    {
        if (percent < 0f) percent = 0f;
        if (percent > 100f) percent = 100f;
        Device.AudioEndpointVolume.MasterVolumeLevelScalar = percent / 100f;
    }

    /// <summary>Gets or sets the mute state of the endpoint.</summary>
    public bool IsMuted
    {
        get { return Device.AudioEndpointVolume.Mute; }
        set { Device.AudioEndpointVolume.Mute = value; }
    }

    /// <summary>Inverts the current mute state.</summary>
    public void ToggleMute()
    {
        Device.AudioEndpointVolume.Mute = !Device.AudioEndpointVolume.Mute;
    }

    /// <summary>Instantaneous master peak level in the range 0..1 (0 when silent).</summary>
    public float GetPeakValue()
    {
        return Device.AudioMeterInformation.MasterPeakValue;
    }

    public override string ToString()
    {
        return string.Format("[{0}] {1} ({2}){3}{4}",
            Index, Name, Kind,
            IsDefault ? " [Default]" : string.Empty,
            IsDefaultCommunication ? " [DefaultComm]" : string.Empty);
    }
}