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
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// Managed wrapper over the Core Audio <c>IAudioSessionControl2</c> interface. Represents a single
/// audio session (typically one application) and exposes its state, metadata and volume controls.
/// </summary>
public class AudioSessionControl 
{
    internal IAudioSessionControl2 _AudioSessionControl;
    internal AudioMeterInformation _AudioMeterInformation;
    internal SimpleAudioVolume _SimpleAudioVolume;

    // Maps each registered consumer to the COM adapter created for it, so Unregister can pass the
    // identical sink object back to COM and so the adapter's CCW stays alive while registered.
    private readonly Dictionary<IAudioSessionEvents, AudioSessionEventsComAdapter> _adapters
        = new Dictionary<IAudioSessionEvents, AudioSessionEventsComAdapter>(AudioSessionEventsRefComparer.Instance);
    private readonly object _adaptersLock = new object();

    /// <summary>Gets the peak-meter information for this session, or <c>null</c> if unsupported.</summary>
    public AudioMeterInformation AudioMeterInformation => _AudioMeterInformation;

    /// <summary>Gets the simple (per-session) volume and mute control, or <c>null</c> if unsupported.</summary>
    public SimpleAudioVolume SimpleAudioVolume => _SimpleAudioVolume;
    
    internal AudioSessionControl(IAudioSessionControl2 realAudioSessionControl)
    {
        IAudioMeterInformation _meters = realAudioSessionControl as IAudioMeterInformation;
        ISimpleAudioVolume _volume = realAudioSessionControl as ISimpleAudioVolume; 
        if (_meters != null)
        {
            _AudioMeterInformation = new CoreAudioApi.AudioMeterInformation(_meters);
        }

        if (_volume != null)
        {
            _SimpleAudioVolume = new SimpleAudioVolume(_volume);
        }

        _AudioSessionControl = realAudioSessionControl;
    }

    /// <summary>Registers a callback to receive session change notifications.</summary>
    /// <param name="eventConsumer">The consumer that will receive <see cref="IAudioSessionEvents"/> callbacks.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="eventConsumer"/> is null.</exception>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    /// <remarks>
    /// THREADING: the consumer's callbacks are raised by Windows Core Audio on arbitrary, non-UI
    /// threads and may arrive concurrently, so the implementation must be fast and thread-safe.
    /// Pass the same instance to <see cref="UnregisterAudioSessionNotification"/> to stop
    /// receiving notifications; keep a reference to it until then.
    /// </remarks>
    public void RegisterAudioSessionNotification(IAudioSessionEvents eventConsumer)
    {
        if (eventConsumer == null)
        {
            throw new ArgumentNullException(nameof(eventConsumer));
        }

        AudioSessionEventsComAdapter adapter;
        lock (_adaptersLock)
        {
            if (!_adapters.TryGetValue(eventConsumer, out adapter))
            {
                adapter = new AudioSessionEventsComAdapter(eventConsumer);
                _adapters[eventConsumer] = adapter;
            }
        }

        Marshal.ThrowExceptionForHR(_AudioSessionControl.RegisterAudioSessionNotification(adapter));
    }

    /// <summary>Unregisters a previously registered session change callback.</summary>
    /// <param name="eventConsumer">The same consumer instance that was passed to <see cref="RegisterAudioSessionNotification"/>.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="eventConsumer"/> is null.</exception>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    /// <remarks>Has no effect if the instance was not previously registered on this session.</remarks>
    public void UnregisterAudioSessionNotification(IAudioSessionEvents eventConsumer)
    {
        if (eventConsumer == null)
        {
            throw new ArgumentNullException(nameof(eventConsumer));
        }

        AudioSessionEventsComAdapter adapter;
        lock (_adaptersLock)
        {
            if (!_adapters.TryGetValue(eventConsumer, out adapter))
            {
                return;
            }

            _adapters.Remove(eventConsumer);
        }

        Marshal.ThrowExceptionForHR(_AudioSessionControl.UnregisterAudioSessionNotification(adapter));
    }

    /// <summary>Gets the current activity state of the session (inactive, active or expired).</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public AudioSessionState State
    {
        get
        {
            AudioSessionState res;
            Marshal.ThrowExceptionForHR(_AudioSessionControl.GetState(out res));
            return res;
        }
    }

    /// <summary>Gets the display name reported by the session, if any.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public string DisplayName
    {
        get
        {
            IntPtr NamePtr;
            Marshal.ThrowExceptionForHR(_AudioSessionControl.GetDisplayName(out NamePtr));
            string res = Marshal.PtrToStringAuto(NamePtr);
            Marshal.FreeCoTaskMem(NamePtr);
            return res;
        }
    }

    /// <summary>Gets the path of the icon reported by the session, if any.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public string IconPath
    {
        get
        {
            IntPtr NamePtr;
            Marshal.ThrowExceptionForHR(_AudioSessionControl.GetIconPath(out NamePtr));
            string res = Marshal.PtrToStringAuto(NamePtr);
            Marshal.FreeCoTaskMem(NamePtr);
            return res;
        }
    }

    /// <summary>Gets the session identifier string, shared by all instances of the same session.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public string SessionIdentifier
    {
        get
        {
            IntPtr NamePtr;
            Marshal.ThrowExceptionForHR(_AudioSessionControl.GetSessionIdentifier(out NamePtr));
            string res = Marshal.PtrToStringAuto(NamePtr);
            Marshal.FreeCoTaskMem(NamePtr);
            return res;
        }
    }

    /// <summary>Gets the identifier that uniquely distinguishes this session instance.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public string SessionInstanceIdentifier
    {
        get
        {
            IntPtr NamePtr;
            Marshal.ThrowExceptionForHR(_AudioSessionControl.GetSessionInstanceIdentifier(out NamePtr));
            string res = Marshal.PtrToStringAuto(NamePtr);
            Marshal.FreeCoTaskMem(NamePtr);
            return res;
        }
    }

    /// <summary>Gets the process identifier (PID) that owns the session.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public uint ProcessID
    {
        get
        {
            uint pid;
            Marshal.ThrowExceptionForHR(_AudioSessionControl.GetProcessId(out pid));
            return pid;
        }
    }

    /// <summary>Gets a value indicating whether this session is the reserved system-sounds session.</summary>
    public bool IsSystemIsSystemSoundsSession => (_AudioSessionControl.IsSystemSoundsSession() == 0); //S_OK
}