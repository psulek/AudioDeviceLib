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

  Changes from the original:
  - Namespace changed to `AudioDeviceLib.CoreAudioApi` (file-scoped); unused `using`
    directives removed.
  - Reformatted to the project's C# style (full braces, modern C# syntax) and annotated with XML
    documentation comments.
  - `RegisterAudioSessionNotification` now accepts the library's pure-C# `IAudioSessionEvents`,
    wraps it in an `AudioSessionEventsComAdapter` and returns an `IDisposable` registration
    token. Registrations are tracked per consumer (by reference identity), so the same consumer
    yields the same token and the identical sink object is handed back to COM on unregister.
  - The class now implements `IDisposable` and unregisters every outstanding sink on disposal.
  - Added the internal `ToSessionInfo()`, which builds an immutable `AudioSessionInfo` snapshot
    tolerant of failing HRESULTs, so callbacks never receive the live COM object.
  - The COM string getters were funnelled through a shared `TryGetString` helper that frees the
    native buffer and can return `null` instead of throwing.
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
public class AudioSessionControl : IDisposable
{
    internal IAudioSessionControl2 _AudioSessionControl;
    internal AudioMeterInformation _AudioMeterInformation;
    internal SimpleAudioVolume _SimpleAudioVolume;
    private const int S_OK = 0;

    // Maps each registered consumer to its registration entry (the COM adapter plus the cached
    // token). Keeping the adapter lets Unregister pass the identical sink object back to COM and
    // keeps its CCW alive while registered; caching the token means repeated registration of the
    // same consumer hands back the same IDisposable instead of allocating a new one each time.
    private readonly Dictionary<IAudioSessionEvents, Registration> _registrations
        = new Dictionary<IAudioSessionEvents, Registration>(AudioSessionEventsRefComparer.Instance);

    private readonly object _registrationsLock = new object();
    private bool _disposed;

    // Pairs the COM sink adapter with the token handed to the caller, cached together so that
    // removing the entry on dispose invalidates the cache: the next Register then creates a fresh one.
    private sealed class Registration
    {
        internal readonly AudioSessionEventsComAdapter Adapter;
        internal readonly SessionEventsRegistration Token;

        internal Registration(AudioSessionEventsComAdapter adapter, SessionEventsRegistration token)
        {
            Adapter = adapter;
            Token = token;
        }
    }

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
    
    // Signature shared by the IAudioSessionControl2 getters that return a COM string pointer.
    private delegate int GetStringPtr(out IntPtr ptr);

    // Invokes one of the raw [PreserveSig] string getters and marshals its result: returns the
    // string on S_OK (freeing the native buffer), or null when the call fails. Used for the
    // display name, icon path and the two session identifiers, which all share this pattern.
    private static string TryGetString(GetStringPtr getter, bool throwOnError = false)
    {
        var errorCode = getter(out var ptr);
        if (errorCode == S_OK)
        {
            string value = Marshal.PtrToStringAuto(ptr);
            Marshal.FreeCoTaskMem(ptr);
            return value;
        } 
        
        if (throwOnError)
        {
            Marshal.ThrowExceptionForHR(errorCode);
        }

        return null;
    }

    // Builds an immutable snapshot of this session by reading the raw COM interface directly.
    // Each getter is [PreserveSig] returning an HRESULT, so instead of throwing we keep a value
    // only when the call returns S_OK; anything that fails is left at its default. This makes the
    // snapshot safe to build even while the session is tearing down (e.g. on disconnect).
    internal AudioSessionInfo ToSessionInfo()
    {
        string displayName = TryGetString(_AudioSessionControl.GetDisplayName);
        string iconPath = TryGetString(_AudioSessionControl.GetIconPath);
        string sessionIdentifier = TryGetString(_AudioSessionControl.GetSessionIdentifier);
        string sessionInstanceIdentifier = TryGetString(_AudioSessionControl.GetSessionInstanceIdentifier);

        AudioSessionState state = default;
        if (_AudioSessionControl.GetState(out var stateValue) == S_OK)
        {
            state = stateValue;
        }

        uint processId = 0;
        if (_AudioSessionControl.GetProcessId(out var pid) == S_OK)
        {
            processId = pid;
        }

        bool isSystemSounds = _AudioSessionControl.IsSystemSoundsSession() == S_OK;

        return new AudioSessionInfo(displayName, iconPath, state, processId, sessionIdentifier,
            sessionInstanceIdentifier, isSystemSounds);
    }

    /// <summary>Registers a callback to receive session change notifications.</summary>
    /// <param name="eventConsumer">The consumer that will receive <see cref="IAudioSessionEvents"/> callbacks.</param>
    /// <returns>
    /// A token that unregisters the callback when disposed. Disposing the token is the foolproof way
    /// to stop notifications; you can also call <see cref="UnregisterAudioSessionNotification"/> with
    /// the same consumer instance.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="eventConsumer"/> is null.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown when this session has been disposed.</exception>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    /// <remarks>
    /// THREADING: the consumer's callbacks are raised by Windows Core Audio on arbitrary, non-UI
    /// threads and may arrive concurrently, so the implementation must be fast and thread-safe.
    /// Registering the same consumer again returns the same token for the existing registration
    /// without registering twice.
    /// </remarks>
    public IDisposable RegisterAudioSessionNotification(IAudioSessionEvents eventConsumer)
    {
        if (eventConsumer == null)
        {
            throw new ArgumentNullException(nameof(eventConsumer));
        }

        AudioSessionEventsComAdapter adapter;
        SessionEventsRegistration token;
        lock (_registrationsLock)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AudioSessionControl));
            }

            if (_registrations.TryGetValue(eventConsumer, out var existing))
            {
                // Already registered: hand back the same token for the existing registration.
                return existing.Token;
            }

            adapter = new AudioSessionEventsComAdapter(this, eventConsumer);
            token = new SessionEventsRegistration(this, adapter);
            _registrations[eventConsumer] = new Registration(adapter, token);
        }

        Marshal.ThrowExceptionForHR(_AudioSessionControl.RegisterAudioSessionNotification(adapter));
        return token;
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
        lock (_registrationsLock)
        {
            if (!_registrations.TryGetValue(eventConsumer, out var existing))
            {
                return;
            }

            adapter = existing.Adapter;
            _registrations.Remove(eventConsumer);
        }

        Marshal.ThrowExceptionForHR(_AudioSessionControl.UnregisterAudioSessionNotification(adapter));
    }

    // Called by a registration token to undo exactly one registration. Idempotent: a no-op if the
    // adapter was already removed (e.g. by Unregister, Dispose, or a second token).
    internal void RemoveRegistration(AudioSessionEventsComAdapter adapter)
    {
        if (adapter == null)
        {
            return;
        }

        lock (_registrationsLock)
        {
            if (!_registrations.TryGetValue(adapter.Target, out var existing) || !ReferenceEquals(existing.Adapter, adapter))
            {
                return;
            }

            _registrations.Remove(adapter.Target);
        }

        Marshal.ThrowExceptionForHR(_AudioSessionControl.UnregisterAudioSessionNotification(adapter));
    }

    /// <summary>Unregisters any remaining session-event callbacks registered on this session.</summary>
    public void Dispose()
    {
        List<AudioSessionEventsComAdapter> toUnregister;
        lock (_registrationsLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            toUnregister = new List<AudioSessionEventsComAdapter>(_registrations.Count);
            foreach (Registration reg in _registrations.Values)
            {
                toUnregister.Add(reg.Adapter);
            }

            _registrations.Clear();
        }

        foreach (AudioSessionEventsComAdapter adapter in toUnregister)
        {
            try
            {
                Marshal.ThrowExceptionForHR(_AudioSessionControl.UnregisterAudioSessionNotification(adapter));
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    /// <summary>Gets the current activity state of the session (inactive, active or expired).</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public AudioSessionState State
    {
        get
        {
            Marshal.ThrowExceptionForHR(_AudioSessionControl.GetState(out AudioSessionState res));
            return res;
        }
    }

    /// <summary>Gets the display name reported by the session, if any.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public string DisplayName => TryGetString(_AudioSessionControl.GetDisplayName, true);

    /// <summary>Gets the path of the icon reported by the session, if any.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public string IconPath => TryGetString(_AudioSessionControl.GetIconPath, true);

    /// <summary>Gets the session identifier string, shared by all instances of the same session.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public string SessionIdentifier => TryGetString(_AudioSessionControl.GetSessionIdentifier, true);

    /// <summary>Gets the identifier that uniquely distinguishes this session instance.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public string SessionInstanceIdentifier => TryGetString(_AudioSessionControl.GetSessionInstanceIdentifier, true);

    /// <summary>Gets the process identifier (PID) that owns the session.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public uint ProcessID
    {
        get
        {
            Marshal.ThrowExceptionForHR(_AudioSessionControl.GetProcessId(out var pid));
            return pid;
        }
    }

    /// <summary>Gets a value indicating whether this session is the reserved system-sounds session.</summary>
    public bool IsSystemIsSystemSoundsSession => (_AudioSessionControl.IsSystemSoundsSession() == 0); //S_OK
}