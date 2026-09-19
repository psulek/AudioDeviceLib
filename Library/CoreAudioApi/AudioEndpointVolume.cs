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
  This file is an ALTERED version of the original source by Ray Molenkamp and must not be
  misrepresented as being the original source code. Altered by Peter Šulek for AudioDeviceLib
  (https://github.com/psulek/AudioDeviceLib), starting from the copy bundled in
  AudioDeviceCmdlets (https://github.com/frgnca/AudioDeviceCmdlets, MIT).

  The changes are summarised in MODIFICATIONS.md at the repository root; the Git history of
  this file is the authoritative record.
*/

using System;
using System.Runtime.InteropServices;
using AudioDeviceLib.CoreAudioApi.Extensions;
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// Managed wrapper over the Core Audio <c>IAudioEndpointVolume</c> interface. Provides master
/// volume, per-channel volume, mute control and volume-change notifications for an endpoint.
/// </summary>
public class AudioEndpointVolume : IDisposable
{
    private readonly IAudioEndpointVolume _audioEndPointVolume;
    private readonly AudioEndpointVolumeChannels _channels;
    private readonly AudioEndpointVolumeStepInformation _stepInformation;
    private readonly AudioEndPointVolumeVolumeRange _volumeRange;
    private readonly EndpointHardwareSupport _hardwareSupport;
    private AudioEndpointVolumeCallback _callBack;
    private bool _disposed;

    /// <summary>Raised when the endpoint volume or mute state changes.</summary>
    public event AudioEndpointVolumeNotificationDelegate OnVolumeNotification;

    // The underlying COM interface. The wrapped members below cover the common cases; callers that
    // need the raw entry points (the event-context overloads in AudioEndpointVolumeExtensions) go
    // through here.
    internal IAudioEndpointVolume Interface
    {
        get
        {
            ThrowIfDisposed();
            return _audioEndPointVolume;
        }
    }

    /// <summary>Gets the supported volume range (minimum, maximum and step, in decibels) for the endpoint.</summary>
    public AudioEndPointVolumeVolumeRange VolumeRange => _volumeRange;

    /// <summary>Gets the hardware functions (volume, mute, meter) natively supported by the endpoint.</summary>
    public EndpointHardwareSupport HardwareSupport => _hardwareSupport;

    /// <summary>Gets the number of discrete volume steps and the current step for the endpoint.</summary>
    public AudioEndpointVolumeStepInformation StepInformation => _stepInformation;

    /// <summary>Gets the collection of per-channel volume controls for the endpoint.</summary>
    public AudioEndpointVolumeChannels Channels => _channels;

    /// <summary>Gets or sets the master volume level in decibels, within <see cref="VolumeRange"/>.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public float MasterVolumeLevel
    {
        get
        {
            ThrowIfDisposed();
            Marshal.ThrowExceptionForHR(_audioEndPointVolume.GetMasterVolumeLevel(out var result));
            return result;
        }
        set
        {
            ThrowIfDisposed();
            Marshal.ThrowExceptionForHR(_audioEndPointVolume.SetMasterVolumeLevel(value));
        }
    }

    /// <summary>Gets or sets the master volume as a normalized scalar in the range 0.0 to 1.0.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public float MasterVolumeLevelScalar
    {
        get
        {
            ThrowIfDisposed();
            Marshal.ThrowExceptionForHR(_audioEndPointVolume.GetMasterVolumeLevelScalar(out var result));
            return result;
        }
        set
        {
            ThrowIfDisposed();
            Marshal.ThrowExceptionForHR(_audioEndPointVolume.SetMasterVolumeLevelScalar(value));
        }
    }

    /// <summary>Gets or sets the mute state of the endpoint.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public bool Mute
    {
        get
        {
            ThrowIfDisposed();
            Marshal.ThrowExceptionForHR(_audioEndPointVolume.GetMute(out bool result));
            return result;
        }
        set
        {
            ThrowIfDisposed();
            Marshal.ThrowExceptionForHR(_audioEndPointVolume.SetMute(value));
        }
    }

    /// <summary>Increases the master volume by one hardware-defined step.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public void VolumeStepUp()
    {
        ThrowIfDisposed();
        Marshal.ThrowExceptionForHR(_audioEndPointVolume.VolumeStepUp());
    }

    /// <summary>Decreases the master volume by one hardware-defined step.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public void VolumeStepDown()
    {
        ThrowIfDisposed();
        Marshal.ThrowExceptionForHR(_audioEndPointVolume.VolumeStepDown());
    }

    internal AudioEndpointVolume(IAudioEndpointVolume realEndpointVolume)
    {
        _audioEndPointVolume = realEndpointVolume;
        _channels = new AudioEndpointVolumeChannels(_audioEndPointVolume);
        _stepInformation = new AudioEndpointVolumeStepInformation(_audioEndPointVolume);
        Marshal.ThrowExceptionForHR(_audioEndPointVolume.QueryHardwareSupport(out var hardwareSupp));
        _hardwareSupport = (EndpointHardwareSupport)hardwareSupp;
        _volumeRange = new AudioEndPointVolumeVolumeRange(_audioEndPointVolume);
        _callBack = new AudioEndpointVolumeCallback(this);
        Marshal.ThrowExceptionForHR(_audioEndPointVolume.RegisterControlChangeNotify(_callBack));
    }

    internal void FireNotification(AudioVolumeNotificationData notificationData)
    {
        AudioEndpointVolumeNotificationDelegate del = OnVolumeNotification;
        if (del != null)
        {
            del(notificationData);
        }
    }

    /// <summary>Unregisters the volume-change notification callback. Safe to call more than once.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        _disposed = true;

        if (_callBack != null)
        {
            try
            {
                _audioEndPointVolume.UnregisterControlChangeNotify(_callBack);
            }
            catch
            {
                // Best-effort: never let an exception escape Dispose (and never throw on the
                // finalizer thread, which would crash the process).
            }

            _callBack = null;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AudioEndpointVolume));
        }
    }

    /// <summary>
    /// Finalizer safety net: unregisters the volume-change callback if <see cref="Dispose()"/> was
    /// never called. Prefer disposing deterministically.
    /// </summary>
    ~AudioEndpointVolume()
    {
        Dispose(false);
    }
}