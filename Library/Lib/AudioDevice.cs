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
using JetBrains.Annotations;

namespace AudioDeviceLib.Lib;

/// <summary>
/// A single active Windows audio endpoint, wrapping the underlying <see cref="MMDevice"/>
/// and exposing volume/mute helpers.
/// </summary>
/// <remarks>
/// Dispose an <see cref="AudioDevice"/> once you are done with it if you used its volume helpers
/// or accessed <see cref="Device"/> for sessions/endpoint volume; that deterministically tears
/// down any registered Core Audio callbacks. Enumeration-only instances hold nothing registered.
/// </remarks>
[PublicAPI]
public sealed class AudioDevice : IDisposable
{
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

    internal AudioDevice(MMDevice baseDevice, bool isDefault, bool isDefaultCommunication)
    {
        if (baseDevice == null)
        {
            throw new ArgumentNullException(nameof(baseDevice));
        }

        IsDefault = isDefault;
        IsDefaultCommunication = isDefaultCommunication;
        Kind = baseDevice.DataFlow == DataFlow.Capture ? AudioDeviceKind.Recording : AudioDeviceKind.Playback;
        Name = baseDevice.FriendlyName;
        Id = baseDevice.ID;
        Device = baseDevice;
    }
    
    /// <summary>Creates an immutable <see cref="AudioDeviceInfo"/> snapshot of this device's identifying data.</summary>
    /// <returns>
    /// A snapshot carrying this device's information, safe to keep after this <see cref="AudioDevice"/> is disposed of.
    /// </returns>
    public AudioDeviceInfo ToDeviceInfo() => new AudioDeviceInfo(Id, Name, Kind, State, IsDefault, IsDefaultCommunication);

    /// <summary>Master volume as a percentage in the range 0..100.</summary>
    /// <returns>The current master volume scalar expressed as a percentage between 0 and 100.</returns>
    public float GetVolumePercent()
    {
        return Device.AudioEndpointVolume.MasterVolumeLevelScalar * 100f;
    }

    /// <summary>Sets master volume from a percentage in the range 0..100 (values are clamped).</summary>
    /// <param name="percent">
    /// The desired master volume as a percentage. Values below 0 are clamped to 0 and values above
    /// 100 are clamped to 100.
    /// </param>
    public void SetVolumePercent(float percent)
    {
        if (percent < 0f)
        {
            percent = 0f;
        }

        if (percent > 100f)
        {
            percent = 100f;
        }

        Device.AudioEndpointVolume.MasterVolumeLevelScalar = percent / 100f;
    }

    /// <summary>Gets or sets the mute state of the endpoint.</summary>
    public bool IsMuted
    {
        get => Device.AudioEndpointVolume.Mute;
        set => Device.AudioEndpointVolume.Mute = value;
    }
    
    /// <summary>
    /// Gets the current state of the endpoint.
    /// </summary>
    public DeviceState State => Device.State;
    
    /// <summary>Gets whether the endpoint is active and available for use.</summary>
    public bool IsActive => State == DeviceState.Active;

    /// <summary>Inverts the current mute state.</summary>
    public void ToggleMute()
    {
        Device.AudioEndpointVolume.Mute = !Device.AudioEndpointVolume.Mute;
    }

    /// <summary>Instantaneous master peak level in the range 0..1 (0 when silent).</summary>
    /// <returns>The current master peak meter value between 0 (silent) and 1 (full scale).</returns>
    public float GetPeakValue()
    {
        return Device.AudioMeterInformation.MasterPeakValue;
    }

    /// <summary>Returns a human-readable description of this endpoint.</summary>
    /// <returns>
    /// A string in the form <c>[Index] Name (Kind)</c>, optionally suffixed with <c>[Default]</c>
    /// and/or <c>[DefaultComm]</c> when this endpoint is a current default device.
    /// </returns>
    public override string ToString()
    {
        return $"{Name} ({Kind}){(IsDefault ? " [Default]" : string.Empty)}{(IsDefaultCommunication ? " [DefaultComm]" : string.Empty)}";
    }

    /// <summary>Disposes the underlying <see cref="MMDevice"/>, releasing any Core Audio callbacks it holds.</summary>
    public void Dispose()
    {
        Device?.Dispose();
    }
}