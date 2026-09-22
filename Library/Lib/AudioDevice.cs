/*
  LICENSE
  -------
  Copyright (C) 2007-2010 Ray Molenkamp

  This source code is provided 'as-is', without any express or implied
  warranty.  In no event will the authors be held liable for any damages
  arising from the use of this source code or the software it produces.

  Permission is granted to anyone to use this source code for any purpose,
  including commercial applications, and to alter it and redistribute it
  freely, subject to the following restrictions:

  1. The origin of this source code must not be misrepresented; you must not
     claim that you wrote the original source code.  If you use this source code
     in a product, an acknowledgment in the product documentation would be
     appreciated but is not required.
  2. Altered source versions must be plainly marked as such, and must not be
     misrepresented as being the original source code.
  3. This notice may not be removed or altered from any source distribution.
*/

/*
  MODIFICATIONS
  -------------
  This file contains an ALTERED version of the original `MMDevice` source by Ray Molenkamp and must
  not be misrepresented as being the original source code. Altered by Peter Šulek for AudioDeviceLib
  (https://github.com/psulek/AudioDeviceLib), starting from the copy bundled in
  AudioDeviceCmdlets (https://github.com/frgnca/AudioDeviceCmdlets, MIT).

  The changes are summarized in MODIFICATIONS.md at the repository root; the Git history of
  this file is the authoritative record.
*/

/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  AudioDevice.cs
  Managed representation of a Windows audio endpoint.

  The volume/mute/peak helpers and the default-role flags derive from the AudioDevice class of
  AudioDeviceCmdlets by Francois Gendron (MIT), https://github.com/frgnca/AudioDeviceCmdlets
*/

using System;
using System.Runtime.InteropServices;
using AudioDeviceLib.CoreAudioApi;
using AudioDeviceLib.CoreAudioApi.Interfaces;
using JetBrains.Annotations;

namespace AudioDeviceLib;

/// <summary>
/// A single Windows audio endpoint: its identity, its state, and its volume, metering and session
/// controls.
/// </summary>
/// <remarks>
/// <para>
/// Identity (<see cref="Id"/>, <see cref="Name"/>, <see cref="Kind"/>) and <see cref="State"/> are
/// captured when the instance is created and cost nothing to read afterward. Call
/// <see cref="Refresh"/> to re-read them or register for change notifications with
/// <see cref="AudioController.RegisterDeviceNotification"/>.
/// </para>
/// <para>
/// Dispose an instance once you are done with it. That tears down any endpoint-volume or session
/// callbacks it activated; the identity snapshot stays readable afterward, but every other member
/// throws <see cref="ObjectDisposedException"/>.
/// </para>
/// <para>
/// <see cref="Volume"/>, <see cref="SessionManager"/>, <see cref="Meter"/> and
/// <see cref="Properties"/> activate their underlying Core Audio interface on first access, and
/// doing so is thread-safe: concurrent first readers all receive the same instance, and
/// <see cref="Dispose"/> is synchronized against them. This matters because device notifications
/// are documented to arrive on arbitrary threads and concurrently, so reading a device's volume
/// from a notification callback is a normal thing to do.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class AudioDevice : IDisposable, IEquatable<AudioDevice>
{
    // ReSharper disable InconsistentNaming
    private static Guid IID_IAudioMeterInformation = typeof(IAudioMeterInformationCOM).GUID;
    private static Guid IID_IAudioEndpointVolume = typeof(IAudioEndpointVolumeCOM).GUID;
    private static Guid IID_IAudioSessionManager = typeof(IAudioSessionManager2COM).GUID;
    // ReSharper restore InconsistentNaming

    private readonly IMMDeviceCOM _device;

    private PropertyStore? _propertyStore;
    private AudioMeterInformation? _meter;
    private AudioEndpointVolume? _volume;
    private AudioSessionManager? _sessionManager;
    
    // Guards disposal state and all lazily activated members.
    // Activation occurs under the lock to avoid creating duplicate COM wrappers,
    // which could leave orphaned notification registrations.
    private readonly object _activationLock = new object();

    // Volatile ensures disposal is visible to Refresh, which reads the flag without _activationLock.
    private volatile bool _disposed;

    /// <summary>True if this endpoint is the current default device for its kind (multimedia role).</summary>
    public bool IsDefault { get; internal set; }

    /// <summary>True if this endpoint is the current default communications device for its kind.</summary>
    public bool IsDefaultCommunication { get; internal set; }

    /// <summary>Whether this is a playback (render) or recording (capture) endpoint.</summary>
    public AudioDeviceKind Kind { get; }

    /// <summary>Friendly name, e.g. "Speakers (Realtek High Definition Audio)".</summary>
    public string Name { get; private set; }

    /// <summary>Endpoint ID, e.g. "{0.0.0.00000000}.{c4aadd95-...}".</summary>
    public string Id { get; }

    /// <summary>State of the endpoint as of construction or the last <see cref="Refresh"/>.</summary>
    public DeviceState State { get; private set; }

    /// <summary>Gets whether the endpoint was active as of construction or the last <see cref="Refresh"/>.</summary>
    public bool IsActive => State == DeviceState.Active;

    internal AudioDevice(IMMDeviceCOM device, bool isDefault, bool isDefaultCommunication)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        IsDefault = isDefault;
        IsDefaultCommunication = isDefaultCommunication;

        Id = ReadId();
        Kind = ReadDataFlow() == DataFlow.Capture ? AudioDeviceKind.Recording : AudioDeviceKind.Playback;
        Name = ReadFriendlyName();
        State = ReadState();
    }

    /// <summary>Gets the volume and mute control for this endpoint (activated on first access).</summary>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed of.</exception>
    /// <exception cref="COMException">Thrown when the interface cannot be activated.</exception>
    public AudioEndpointVolume Volume
    {
        get
        {
            lock (_activationLock)
            {
                ThrowIfDisposed();
                if (_volume == null)
                {
                    InteropUtils.ThrowIfFailed(_device.Activate(ref IID_IAudioEndpointVolume, CLSCTX.ALL,
                        IntPtr.Zero, out var result));
                    _volume = new AudioEndpointVolume((result as IAudioEndpointVolumeCOM)!);
                }

                return _volume;
            }
        }
    }

    /// <summary>Gets the audio session manager for this endpoint (activated on first access).</summary>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed of.</exception>
    /// <exception cref="COMException">Thrown when the interface cannot be activated.</exception>
    public AudioSessionManager SessionManager
    {
        get
        {
            lock (_activationLock)
            {
                ThrowIfDisposed();
                if (_sessionManager == null)
                {
                    InteropUtils.ThrowIfFailed(_device.Activate(ref IID_IAudioSessionManager, CLSCTX.ALL,
                        IntPtr.Zero, out var result));
                    _sessionManager = new AudioSessionManager((result as IAudioSessionManager2COM)!);
                }

                return _sessionManager;
            }
        }
    }

    /// <summary>Gets the peak-meter information for this endpoint (activated on first access).</summary>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed of.</exception>
    /// <exception cref="COMException">Thrown when the interface cannot be activated.</exception>
    public AudioMeterInformation Meter
    {
        get
        {
            lock (_activationLock)
            {
                ThrowIfDisposed();
                if (_meter == null)
                {
                    InteropUtils.ThrowIfFailed(_device.Activate(ref IID_IAudioMeterInformation, CLSCTX.ALL,
                        IntPtr.Zero, out var result));
                    _meter = new AudioMeterInformation((result as IAudioMeterInformationCOM)!);
                }

                return _meter;
            }
        }
    }

    /// <summary>Gets the property store for this endpoint (opened on first access).</summary>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed of.</exception>
    /// <exception cref="COMException">Thrown when the property store cannot be opened.</exception>
    public PropertyStore Properties => PropertyStoreCore;

    /// <summary>Re-reads <see cref="Name"/> and <see cref="State"/> from the endpoint.</summary>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed of.</exception>
    /// <exception cref="COMException">Thrown when the underlying Core Audio call fails.</exception>
    public void Refresh()
    {
        ThrowIfDisposed();
        State = ReadState();
        Name = ReadFriendlyName();
    }

    /// <summary>Creates an immutable <see cref="AudioDeviceInfo"/> snapshot of this device's identifying data.</summary>
    /// <returns>
    /// A snapshot carrying this device's information, safe to keep after this <see cref="AudioDevice"/> is disposed of.
    /// </returns>
    public AudioDeviceInfo ToDeviceInfo() =>
        new AudioDeviceInfo(Id, Name, Kind, State, IsDefault, IsDefaultCommunication);

    /// <summary>Master volume as a percentage in the range 0..100.</summary>
    /// <returns>The current master volume scalar expressed as a percentage between 0 and 100.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed of.</exception>
    public float GetVolumePercent()
    {
        return Volume.MasterVolumeLevelScalar * 100f;
    }

    /// <summary>Sets master volume from a percentage in the range 0..100 (values are clamped).</summary>
    /// <param name="percent">
    /// The desired master volume as a percentage. Values below 0 are clamped to 0, and values above
    /// 100 are clamped to 100.
    /// </param>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed of.</exception>
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

        Volume.MasterVolumeLevelScalar = percent / 100f;
    }

    /// <summary>Gets or sets the mute state of the endpoint.</summary>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed of.</exception>
    public bool IsMuted
    {
        get => Volume.Mute;
        set => Volume.Mute = value;
    }

    /// <summary>Inverts the current mute state.</summary>
    /// <returns>The mute state after the change.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed of.</exception>
    public bool ToggleMute()
    {
        AudioEndpointVolume volume = Volume;
        bool muted = !volume.Mute;
        volume.Mute = muted;
        return muted;
    }

    /// <summary>Instantaneous master peak level in the range 0..1 (0 when silent).</summary>
    /// <returns>The current master peak meter value between 0 (silent) and 1 (full scale).</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed of.</exception>
    public float GetPeakValue()
    {
        return Meter.MasterPeakValue;
    }

    /// <summary>Determines whether the given object is the same endpoint, compared by <see cref="Id"/>.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><c>true</c> if <paramref name="obj"/> is an <see cref="AudioDevice"/> with the same ID.</returns>
    public override bool Equals(object? obj)
    {
        // Two wrappers for one endpoint are never reference-equal: Core Audio hands out a distinct
        // COM object per acquisition, so identity has to come from the endpoint ID.
        return obj is AudioDevice other &&
               string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Compares endpoint IDs without regard to case.</summary>
    /// <param name="other">The endpoint to compare.</param>
    /// <returns>Whether both instances identify the same endpoint.</returns>
    public bool Equals(AudioDevice? other) => other is not null && StringComparer.OrdinalIgnoreCase.Equals(Id, other.Id);

    /// <summary>Compares endpoint identities.</summary>
    public static bool operator ==(AudioDevice? left, AudioDevice? right) => ReferenceEquals(left, right) || (left is not null && left.Equals(right));

    /// <summary>Compares endpoint identities for inequality.</summary>
    public static bool operator !=(AudioDevice? left, AudioDevice? right) => !(left == right);

    /// <summary>Serves as the hash function, derived from <see cref="Id"/>.</summary>
    /// <returns>A hash code for this endpoint.</returns>
    public override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(Id);
    }

    /// <summary>Returns a human-readable description of this endpoint.</summary>
    /// <returns>
    /// A string in the form <c>Name (Kind)</c>, optionally suffixed with <c>[Default]</c>
    /// and/or <c>[DefaultComm]</c> when this endpoint is a current default device.
    /// </returns>
    public override string ToString()
    {
        return $"{Name} ({Kind}){(IsDefault ? " [Default]" : string.Empty)}{(IsDefaultCommunication ? " [DefaultComm]" : string.Empty)}";
    }

    /// <summary>
    /// Releases the endpoint-volume and session callbacks this instance registered, and the property
    /// store and meter it activated.
    /// </summary>
    /// <remarks>
    /// The identity snapshot (<see cref="Id"/>, <see cref="Name"/>, <see cref="Kind"/>,
    /// <see cref="State"/>, <see cref="ToDeviceInfo"/>, <see cref="ToString"/>) stays readable after
    /// disposal; every member that talks to Core Audio throws <see cref="ObjectDisposedException"/>.
    /// </remarks>
    public void Dispose()
    {
        AudioEndpointVolume? volume;
        AudioSessionManager? sessionManager;

        lock (_activationLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Detach COM-backed members under the lock, then dispose those requiring COM calls outside it
            // to avoid blocking other access while preventing reactivation after disposal.
            volume = _volume;
            _volume = null;

            sessionManager = _sessionManager;
            _sessionManager = null;

            _meter?.Dispose();
            _meter = null;
            _propertyStore?.Dispose();
            _propertyStore = null;
        }

        volume?.Dispose();
        sessionManager?.Dispose();

        // Leave the IMMDevice RCW to the CLR to avoid invalidating the shared wrapper on repeated disposal.
    }

    // Centralizes synchronized property-store access for construction, Refresh, and Properties.
    // Checks disposal under the lock to prevent a racing operation from reopening the store
    // after Dispose has cleared it.
    private PropertyStore PropertyStoreCore
    {
        get
        {
            lock (_activationLock)
            {
                ThrowIfDisposed();
                if (_propertyStore == null)
                {
                    InteropUtils.ThrowIfFailed(_device.OpenPropertyStore(StgmAccess.Read, out var store));
                    _propertyStore = new PropertyStore(store);
                }

                return _propertyStore;
            }
        }
    }

    private string ReadId()
    {
        InteropUtils.ThrowIfFailed(_device.GetId(out var result));
        return result;
    }

    private DeviceState ReadState()
    {
        InteropUtils.ThrowIfFailed(_device.GetState(out var result));
        return result;
    }

    private DataFlow ReadDataFlow()
    {
        // ReSharper disable once SuspiciousTypeConversion.Global
        // NOTE: This cast is safe: the endpoint is get by doing QueryInterface for IMMEndpoint  
        var endpoint = _device as IMMEndpointCOM;
        if (endpoint == null)
        {
            throw new InvalidOperationException("The endpoint does not implement IMMEndpoint.");
        }

        InteropUtils.ThrowIfFailed(endpoint.GetDataFlow(out var result));
        return result;
    }

    private string ReadFriendlyName()
    {
        bool found = PropertyStoreCore.TryGetValue(PKEY.DeviceFriendlyName, out var value);
        return found ? (value.Value as string ?? string.Empty) : string.Empty;
    }

    private void ThrowIfDisposed()
    {
        InteropUtils.RequireNotDisposed(_disposed, this);
    }
}