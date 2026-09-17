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

  The changes are summarized in MODIFICATIONS.md at the repository root; the Git history of
  this file is the authoritative record.
*/

using System;
using System.Collections.Generic;
using AudioDeviceLib.CoreAudioApi.Interfaces;
using static AudioDeviceLib.CoreAudioApi.InteropUtils;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// Managed wrapper over the Core Audio <c>IAudioSessionControl2</c> interface. Represents a single
/// audio session (typically one application) and exposes its state, metadata and volume controls.
/// </summary>
public sealed class AudioSessionControl : IDisposable
{
    private readonly IAudioSessionControl2COM _audioSessionControl;

    // Retain each consumer's COM adapter for its lifetime and for matching unregistration.
    // Reuse its token when the same consumer registers again.
    private readonly Dictionary<IAudioSessionEvents, Registration> _registrations
        = new Dictionary<IAudioSessionEvents, Registration>(AudioSessionEventsRefComparer.Instance);

    private readonly object _registrationsLock = new object();
    private volatile bool _disposed;

    // Cache immutable session identity while the COM object is healthy.
    // After disconnection, even identity getters can fail, so callbacks must use cached values.
    private readonly string? _sessionIdentifier;
    private readonly string? _sessionInstanceIdentifier;
    private readonly uint _processId;
    private readonly bool _isSystemSoundsSession;

    // Pairs the COM sink adapter with the token handed to the caller, cached together so that
    // removing the entry on dispose invalidates the cache: the next Register then creates a fresh one.
    private sealed class Registration
    {
        internal readonly AudioSessionEventsAdapter Adapter;
        internal readonly SessionEventsRegistration Token;

        internal Registration(AudioSessionEventsAdapter adapter, SessionEventsRegistration token)
        {
            Adapter = adapter;
            Token = token;
        }
    }

    /// <summary>Gets the peak-meter information for this session, or <c>null</c> if unsupported.</summary>
    private readonly AudioMeterInformation? _audioMeterInformation;
    /// <summary>Gets the session meter, or null if unsupported.</summary>
    public AudioMeterInformation? AudioMeterInformation
    {
        get
        {
            ThrowIfDisposed();
            return _audioMeterInformation;
        }
    }

    /// <summary>Gets the simple (per-session) volume and mute control, or <c>null</c> if unsupported.</summary>
    private readonly SimpleAudioVolume? _simpleAudioVolume;
    /// <summary>Gets the session volume control, or null if unsupported.</summary>
    public SimpleAudioVolume? SimpleAudioVolume
    {
        get
        {
            ThrowIfDisposed();
            return _simpleAudioVolume;
        }
    }

    internal AudioSessionControl(IAudioSessionControl2COM audioSessionControl)
    {
        // ReSharper disable once SuspiciousTypeConversion.Global
        // NOTE: This cast is safe: the meters is get by doing QueryInterface for IAudioMeterInformation
        if (audioSessionControl is IAudioMeterInformationCOM meters)
        {
            _audioMeterInformation = new AudioMeterInformation(meters);
        }

        // ReSharper disable once SuspiciousTypeConversion.Global
        // NOTE: This cast is safe: the volumne is get by doing QueryInterface for ISimpleAudioVolume
        if (audioSessionControl is ISimpleAudioVolumeCOM volume)
        {
            _simpleAudioVolume = new SimpleAudioVolume(volume);
        }

        _audioSessionControl = audioSessionControl ?? throw new ArgumentNullException(nameof(audioSessionControl));

        // Leave unreadable metadata at its default so an expired session cannot abort enumeration.
        if (HrSuccess(audioSessionControl.GetSessionIdentifier(out string sessionIdentifier)))
        {
            _sessionIdentifier = sessionIdentifier;
        }

        if (HrSuccess(audioSessionControl.GetSessionInstanceIdentifier(out string instanceIdentifier)))
        {
            _sessionInstanceIdentifier = instanceIdentifier;
        }

        // Not an S_OK comparison: a session spanning several processes returns
        // AUDCLNT_S_NO_SINGLE_PROCESS, which is a success code that still writes a usable PID.
        if (HrSuccess(audioSessionControl.GetProcessId(out uint processId)))
        {
            _processId = processId;
        }

        // Only S_OK identifies the system-sounds session; S_FALSE and failures map to false.
        // Cache this result once while the session is healthy.
        _isSystemSoundsSession = audioSessionControl.IsSystemSoundsSession() == S_OK;
    }
    
    // Build snapshots from cached identity and best-effort reads of mutable metadata.
    // Failed HRESULTs leave defaults, allowing snapshots during session teardown.
    internal AudioSessionBaseInfo ToSessionBaseInfo() => new AudioSessionBaseInfo(
        _processId, _sessionIdentifier ?? string.Empty, _sessionInstanceIdentifier ?? string.Empty,
        _isSystemSoundsSession);

    internal AudioSessionInfo ToSessionInfo()
    {
        if (HrFailed(_audioSessionControl.GetDisplayName(out string displayName)))
        {
            displayName = string.Empty;
        }
        if (HrFailed(_audioSessionControl.GetIconPath(out string iconPath)))
        {
            iconPath = string.Empty;
        }

        AudioSessionState state = default;
        if (HrSuccess(_audioSessionControl.GetState(out var stateValue)))
        {
            state = stateValue;
        }

        return new AudioSessionInfo(displayName, iconPath, state, _processId, 
            _sessionIdentifier ?? string.Empty,
            _sessionInstanceIdentifier ?? string.Empty,
            _isSystemSoundsSession);
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
        RequireNotNull(eventConsumer, nameof(eventConsumer));

        lock (_registrationsLock)
        {
            ThrowIfDisposed();

            if (_registrations.TryGetValue(eventConsumer, out var existing))
            {
                // Already registered: hand back the same token for the existing registration.
                return existing.Token;
            }

            var adapter = new AudioSessionEventsAdapter(this, eventConsumer);
            var token = new SessionEventsRegistration(this, adapter);
            // Serialize native registration with disposal; publish only after success.
            ThrowIfFailed(_audioSessionControl.RegisterAudioSessionNotification(adapter));
            _registrations[eventConsumer] = new Registration(adapter, token);
            return token;
        }
    }

    /// <summary>Unregisters a previously registered session change callback.</summary>
    /// <param name="eventConsumer">The same consumer instance that was passed to <see cref="RegisterAudioSessionNotification"/>.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="eventConsumer"/> is null.</exception>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    /// <remarks>Has no effect if the instance was not previously registered on this session.</remarks>
    public void UnregisterAudioSessionNotification(IAudioSessionEvents eventConsumer)
    {
        RequireNotNull(eventConsumer, nameof(eventConsumer));

        lock (_registrationsLock)
        {
            if (!_registrations.TryGetValue(eventConsumer, out var existing))
            {
                return;
            }

            AudioSessionEventsAdapter adapter = existing.Adapter;
            ThrowIfFailed(_audioSessionControl.UnregisterAudioSessionNotification(adapter));
            _registrations.Remove(eventConsumer);
        }
    }

    // Called by a registration token to undo exactly one registration. Idempotent: a no-op if the
    // adapter was already removed (e.g. by Unregister, Dispose, or a second token).
    internal void RemoveRegistration(AudioSessionEventsAdapter adapter)
    {
        lock (_registrationsLock)
        {
            if (!_registrations.TryGetValue(adapter.Target, out var existing) || !ReferenceEquals(existing.Adapter, adapter))
            {
                return;
            }

            ThrowIfFailed(_audioSessionControl.UnregisterAudioSessionNotification(adapter));
            _registrations.Remove(adapter.Target);
        }
    }

    /// <summary>Unregisters any remaining session-event callbacks registered on this session.</summary>
    public void Dispose()
    {
        List<AudioSessionEventsAdapter> toUnregister;
        lock (_registrationsLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _audioMeterInformation?.Dispose();
            _simpleAudioVolume?.Dispose();
            toUnregister = new List<AudioSessionEventsAdapter>(_registrations.Count);
            foreach (Registration reg in _registrations.Values)
            {
                toUnregister.Add(reg.Adapter);
            }

            _registrations.Clear();
        }

        foreach (AudioSessionEventsAdapter adapter in toUnregister)
        {
            try
            {
                ThrowIfFailed(_audioSessionControl.UnregisterAudioSessionNotification(adapter));
            }
            catch (Exception cleanupException)
            {
                ReportFailure(cleanupException);
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
            ThrowIfDisposed();
            ThrowIfFailed(_audioSessionControl.GetState(out AudioSessionState res));
            return res;
        }
    }

    // Volatile reads also guard live public getters outside the registration lock.
    private void ThrowIfDisposed()
    {
        RequireNotDisposed(_disposed, this);
    }
}