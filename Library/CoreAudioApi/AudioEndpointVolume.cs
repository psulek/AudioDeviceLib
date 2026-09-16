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

using System;
using System.Runtime.InteropServices;
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// Managed wrapper over the Core Audio <c>IAudioEndpointVolume</c> interface. Provides master
/// volume, per-channel volume, mute control and volume-change notifications for an endpoint.
/// </summary>
public class AudioEndpointVolume : IDisposable
{
    private IAudioEndpointVolume _AudioEndPointVolume;
    private AudioEndpointVolumeChannels _Channels;
    private AudioEndpointVolumeStepInformation _StepInformation;
    private AudioEndPointVolumeVolumeRange _VolumeRange;
    private EndpointHardwareSupport _HardwareSupport;
    private AudioEndpointVolumeCallback _CallBack;

    /// <summary>Raised when the endpoint volume or mute state changes.</summary>
    public event AudioEndpointVolumeNotificationDelegate OnVolumeNotification;

    /// <summary>Gets the supported volume range (minimum, maximum and step, in decibels) for the endpoint.</summary>
    public AudioEndPointVolumeVolumeRange VolumeRange => _VolumeRange;

    /// <summary>Gets the hardware functions (volume, mute, meter) natively supported by the endpoint.</summary>
    public EndpointHardwareSupport HardwareSupport => _HardwareSupport;

    /// <summary>Gets the number of discrete volume steps and the current step for the endpoint.</summary>
    public AudioEndpointVolumeStepInformation StepInformation => _StepInformation;

    /// <summary>Gets the collection of per-channel volume controls for the endpoint.</summary>
    public AudioEndpointVolumeChannels Channels => _Channels;

    /// <summary>Gets or sets the master volume level in decibels, within <see cref="VolumeRange"/>.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public float MasterVolumeLevel
    {
        get
        {
            Marshal.ThrowExceptionForHR(_AudioEndPointVolume.GetMasterVolumeLevel(out var result));
            return result;
        }
        set => Marshal.ThrowExceptionForHR(_AudioEndPointVolume.SetMasterVolumeLevel(value, Guid.Empty));
    }

    /// <summary>Gets or sets the master volume as a normalized scalar in the range 0.0 to 1.0.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public float MasterVolumeLevelScalar
    {
        get
        {
            Marshal.ThrowExceptionForHR(_AudioEndPointVolume.GetMasterVolumeLevelScalar(out var result));
            return result;
        }
        set => Marshal.ThrowExceptionForHR(_AudioEndPointVolume.SetMasterVolumeLevelScalar(value, Guid.Empty));
    }

    /// <summary>Gets or sets the mute state of the endpoint.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public bool Mute
    {
        get
        {
            Marshal.ThrowExceptionForHR(_AudioEndPointVolume.GetMute(out var result));
            return result;
        }
        set => Marshal.ThrowExceptionForHR(_AudioEndPointVolume.SetMute(value, Guid.Empty));
    }

    /// <summary>Increases the master volume by one hardware-defined step.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public void VolumeStepUp()
    {
        Marshal.ThrowExceptionForHR(_AudioEndPointVolume.VolumeStepUp(Guid.Empty));
    }

    /// <summary>Decreases the master volume by one hardware-defined step.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public void VolumeStepDown()
    {
        Marshal.ThrowExceptionForHR(_AudioEndPointVolume.VolumeStepDown(Guid.Empty));
    }

    internal AudioEndpointVolume(IAudioEndpointVolume realEndpointVolume)
    {
        _AudioEndPointVolume = realEndpointVolume;
        _Channels = new AudioEndpointVolumeChannels(_AudioEndPointVolume);
        _StepInformation = new AudioEndpointVolumeStepInformation(_AudioEndPointVolume);
        Marshal.ThrowExceptionForHR(_AudioEndPointVolume.QueryHardwareSupport(out var HardwareSupp));
        _HardwareSupport = (EndpointHardwareSupport)HardwareSupp;
        _VolumeRange = new AudioEndPointVolumeVolumeRange(_AudioEndPointVolume);
        _CallBack = new AudioEndpointVolumeCallback(this);
        Marshal.ThrowExceptionForHR(_AudioEndPointVolume.RegisterControlChangeNotify(_CallBack));
    }

    internal void FireNotification(AudioVolumeNotificationData NotificationData)
    {
        AudioEndpointVolumeNotificationDelegate del = OnVolumeNotification;
        if (del != null)
        {
            del(NotificationData);
        }
    }

    #region IDisposable Members

    /// <summary>Unregisters the volume-change notification callback. Safe to call more than once.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_CallBack != null)
        {
            try
            {
                _AudioEndPointVolume.UnregisterControlChangeNotify(_CallBack);
            }
            catch
            {
                // Best-effort: never let an exception escape Dispose (and never throw on the
                // finalizer thread, which would crash the process).
            }

            _CallBack = null;
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

    #endregion
}