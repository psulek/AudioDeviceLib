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

  Changes from the original:
  - `MMDevice` and the AudioDeviceCmdlets-derived `AudioDevice` were merged into this single type,
    which now holds the `IMMDevice` directly. `CoreAudioApi/MMDevice.cs` no longer exists.
  - Namespace changed to `AudioDeviceLib.Lib` (file-scoped); unused `using` directives removed.
  - Reformatted to the project's C# style (full braces, modern C# syntax) and annotated with XML
    documentation comments.
  - Implements `IDisposable` with a `_disposed` flag and a `ThrowIfDisposed()` guard.
  - `FriendlyName`/`ID`/`DataFlow`/`State` became the snapshot properties `Name`/`Id`/`Kind`/`State`,
    captured once at construction rather than re-read from COM on every access.
  - `AudioEndpointVolume`/`AudioSessionManager`/`AudioMeterInformation` exposed as
    `Volume`/`SessionManager`/`Meter`.
  - `EStgmAccess`/`EDataFlow`/`EDeviceState` renamed to `StgmAccess`/`DataFlow`/`DeviceState`.
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

namespace AudioDeviceLib.Lib;

/// <summary>
/// A single Windows audio endpoint: its identity, its state, and its volume, metering and session
/// controls.
/// </summary>
/// <remarks>
/// <para>
/// Identity (<see cref="Id"/>, <see cref="Name"/>, <see cref="Kind"/>) and <see cref="State"/> are
/// captured when the instance is created and cost nothing to read afterwards. Call
/// <see cref="Refresh"/> to re-read them, or register for change notifications with
/// <see cref="AudioController.RegisterDeviceNotification"/>.
/// </para>
/// <para>
/// Dispose an instance once you are done with it. That tears down any endpoint-volume or session
/// callbacks it activated; the identity snapshot stays readable afterwards, but every other member
/// throws <see cref="ObjectDisposedException"/>.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class AudioDevice : IDisposable
{
    private static Guid IID_IAudioMeterInformation = typeof(IAudioMeterInformation).GUID;
    private static Guid IID_IAudioEndpointVolume = typeof(IAudioEndpointVolume).GUID;
    private static Guid IID_IAudioSessionManager = typeof(IAudioSessionManager2).GUID;

    private readonly IMMDevice _realDevice;

    private PropertyStore _propertyStore;
    private AudioMeterInformation _meter;
    private AudioEndpointVolume _volume;
    private AudioSessionManager _sessionManager;

    private bool _disposed;

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

    internal AudioDevice(IMMDevice realDevice, bool isDefault, bool isDefaultCommunication)
    {
        if (realDevice == null)
        {
            throw new ArgumentNullException(nameof(realDevice));
        }

        _realDevice = realDevice;
        IsDefault = isDefault;
        IsDefaultCommunication = isDefaultCommunication;

        Id = ReadId();
        Kind = ReadDataFlow() == DataFlow.Capture ? AudioDeviceKind.Recording : AudioDeviceKind.Playback;
        Name = ReadFriendlyName();
        State = ReadState();
    }

    /// <summary>Gets the volume and mute control for this endpoint (activated on first access).</summary>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    /// <exception cref="COMException">Thrown when the interface cannot be activated.</exception>
    public AudioEndpointVolume Volume
    {
        get
        {
            ThrowIfDisposed();
            if (_volume == null)
            {
                Marshal.ThrowExceptionForHR(_realDevice.Activate(ref IID_IAudioEndpointVolume, CLSCTX.ALL,
                    IntPtr.Zero, out var result));
                _volume = new AudioEndpointVolume(result as IAudioEndpointVolume);
            }

            return _volume;
        }
    }

    /// <summary>Gets the audio session manager for this endpoint (activated on first access).</summary>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    /// <exception cref="COMException">Thrown when the interface cannot be activated.</exception>
    public AudioSessionManager SessionManager
    {
        get
        {
            ThrowIfDisposed();
            if (_sessionManager == null)
            {
                Marshal.ThrowExceptionForHR(_realDevice.Activate(ref IID_IAudioSessionManager, CLSCTX.ALL,
                    IntPtr.Zero, out var result));
                _sessionManager = new AudioSessionManager(result as IAudioSessionManager2);
            }

            return _sessionManager;
        }
    }

    /// <summary>Gets the peak-meter information for this endpoint (activated on first access).</summary>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    /// <exception cref="COMException">Thrown when the interface cannot be activated.</exception>
    public AudioMeterInformation Meter
    {
        get
        {
            ThrowIfDisposed();
            if (_meter == null)
            {
                Marshal.ThrowExceptionForHR(_realDevice.Activate(ref IID_IAudioMeterInformation, CLSCTX.ALL,
                    IntPtr.Zero, out var result));
                _meter = new AudioMeterInformation(result as IAudioMeterInformation);
            }

            return _meter;
        }
    }

    /// <summary>Gets the property store for this endpoint (opened on first access).</summary>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    /// <exception cref="COMException">Thrown when the property store cannot be opened.</exception>
    public PropertyStore Properties
    {
        get
        {
            ThrowIfDisposed();
            return PropertyStoreCore;
        }
    }

    /// <summary>Re-reads <see cref="Name"/> and <see cref="State"/> from the endpoint.</summary>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
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
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    public float GetVolumePercent()
    {
        return Volume.MasterVolumeLevelScalar * 100f;
    }

    /// <summary>Sets master volume from a percentage in the range 0..100 (values are clamped).</summary>
    /// <param name="percent">
    /// The desired master volume as a percentage. Values below 0 are clamped to 0 and values above
    /// 100 are clamped to 100.
    /// </param>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
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
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    public bool IsMuted
    {
        get => Volume.Mute;
        set => Volume.Mute = value;
    }

    /// <summary>Inverts the current mute state.</summary>
    /// <returns>The mute state after the change.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    public bool ToggleMute()
    {
        AudioEndpointVolume volume = Volume;
        bool muted = !volume.Mute;
        volume.Mute = muted;
        return muted;
    }

    /// <summary>Instantaneous master peak level in the range 0..1 (0 when silent).</summary>
    /// <returns>The current master peak meter value between 0 (silent) and 1 (full scale).</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    public float GetPeakValue()
    {
        return Meter.MasterPeakValue;
    }

    /// <summary>Determines whether the given object is the same endpoint, compared by <see cref="Id"/>.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><c>true</c> if <paramref name="obj"/> is an <see cref="AudioDevice"/> with the same ID.</returns>
    public override bool Equals(object obj)
    {
        // Two wrappers for one endpoint are never reference-equal: Core Audio hands out a distinct
        // COM object per acquisition, so identity has to come from the endpoint ID.
        return obj is AudioDevice other &&
               string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Serves as the hash function, derived from <see cref="Id"/>.</summary>
    /// <returns>A hash code for this endpoint.</returns>
    public override int GetHashCode()
    {
        return Id == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Id);
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
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _volume?.Dispose();
        _volume = null;

        _sessionManager?.Dispose();
        _sessionManager = null;

        _meter = null;
        _propertyStore = null;

        // The IMMDevice RCW is deliberately left to the CLR. Releasing it here would hand a consumer
        // that disposes twice an InvalidComObjectException: the wrapper holds exactly one reference,
        // so there is no slack to absorb the mistake.
    }

    // Bypasses the disposed guard so the constructor can populate the snapshot before the guard is
    // meaningful, and so Refresh can reuse the store without re-checking.
    private PropertyStore PropertyStoreCore
    {
        get
        {
            if (_propertyStore == null)
            {
                Marshal.ThrowExceptionForHR(_realDevice.OpenPropertyStore(StgmAccess.Read, out var store));
                _propertyStore = new PropertyStore(store);
            }

            return _propertyStore;
        }
    }

    private string ReadId()
    {
        Marshal.ThrowExceptionForHR(_realDevice.GetId(out var result));
        return result;
    }

    private DeviceState ReadState()
    {
        Marshal.ThrowExceptionForHR(_realDevice.GetState(out var result));
        return result;
    }

    private DataFlow ReadDataFlow()
    {
        var endpoint = _realDevice as IMMEndpoint;
        if (endpoint == null)
        {
            throw new InvalidOperationException("The endpoint does not implement IMMEndpoint.");
        }

        endpoint.GetDataFlow(out var result);
        return result;
    }

    private string ReadFriendlyName()
    {
        bool found = PropertyStoreCore.TryGetValue(PKEY.PKEY_DeviceInterface_FriendlyName, out var value);
        return found ? (value.Value as string ?? "Unknown") : "Unknown";
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AudioDevice));
        }
    }
}
