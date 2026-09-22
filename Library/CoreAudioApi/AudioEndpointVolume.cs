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
using System.Runtime.ExceptionServices;
using System.Threading;
using AudioDeviceLib.CoreAudioApi.Extensions;
using AudioDeviceLib.CoreAudioApi.Interfaces;
using JetBrains.Annotations;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// Managed wrapper over the Core Audio <c>IAudioEndpointVolume</c> interface. Provides master
/// volume, per-channel volume, mute control and volume-change notifications for an endpoint.
/// </summary>
/// <remarks>
/// <para>
/// <b>Disposal is mandatory.</b> This type registers a callback with Core Audio when it is created,
/// and native code holds a reference to that callback for as long as the registration is live. That
/// reference roots this object, so an instance that is never disposed is never collected: it leaks
/// for the lifetime of the process and keeps delivering notifications to any consumer still
/// registered with it. The garbage collector cannot recover it, and there is deliberately no
/// finalizer that pretends otherwise - a finalizer could not run while the registration is live,
/// which is precisely when it would be needed.
/// </para>
/// <para>
/// Instances reached through <see cref="AudioDeviceLib.AudioDevice.Volume"/> are owned by that
/// device and are disposed with it; only dispose this object directly if you created it directly.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class AudioEndpointVolume : IDisposable
{
    private readonly IAudioEndpointVolumeCOM _audioEndPointVolume;
    private AudioEndpointVolumeCallback? _callBack;

    // volatile: write happens under _registrationsLock, but ThrowIfDisposed reads it on every
    // public getter and both VolumeStep methods without taking that lock.
    private volatile bool _disposed;

    // Cache one token per consumer so repeated registration returns the same disposable.
    private readonly Dictionary<IAudioEndpointVolumeEvents, VolumeEventsRegistration> _registrations
        = new Dictionary<IAudioEndpointVolumeEvents, VolumeEventsRegistration>(
            AudioEndpointVolumeEventsRefComparer.Instance);

    private readonly object _registrationsLock = new object();

    // Replace the snapshot under _registrationsLock; dispatch without locking or allocating.
    // Consumer callbacks must run outside the lock to allow reentrant registration.
    private IAudioEndpointVolumeEvents[] _consumers = Array.Empty<IAudioEndpointVolumeEvents>();

    /// <summary>Gets the supported volume range (minimum, maximum and step, in decibels) for the endpoint.</summary>
    public AudioEndPointVolumeVolumeRange VolumeRange { get; }

    /// <summary>Gets the hardware functions (volume, mute, meter) natively supported by the endpoint.</summary>
    public EndpointHardwareSupport HardwareSupport { get; }

    /// <summary>Gets the number of discrete volume steps and the current step for the endpoint.</summary>
    public AudioEndpointVolumeStepInformation StepInformation { get; }

    /// <summary>Gets the collection of per-channel volume controls for the endpoint.</summary>
    public AudioEndpointVolumeChannels Channels { get; }

    /// <summary>Gets or sets the master volume level in decibels, within <see cref="VolumeRange"/>.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public float MasterVolumeLevel
    {
        get
        {
            ThrowIfDisposed();
            InteropUtils.ThrowIfFailed(_audioEndPointVolume.GetMasterVolumeLevel(out var result));
            return result;
        }
        set
        {
            ThrowIfDisposed();
            InteropUtils.ThrowIfFailed(_audioEndPointVolume.SetMasterVolumeLevel(value));
        }
    }

    /// <summary>Gets or sets the master volume as a normalized scalar in the range 0.0 to 1.0.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public float MasterVolumeLevelScalar
    {
        get
        {
            ThrowIfDisposed();
            InteropUtils.ThrowIfFailed(_audioEndPointVolume.GetMasterVolumeLevelScalar(out var result));
            return result;
        }
        set
        {
            ThrowIfDisposed();
            InteropUtils.ThrowIfFailed(_audioEndPointVolume.SetMasterVolumeLevelScalar(value));
        }
    }

    /// <summary>Gets or sets the mute state of the endpoint.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public bool Mute
    {
        get
        {
            ThrowIfDisposed();
            InteropUtils.ThrowIfFailed(_audioEndPointVolume.GetMute(out bool result));
            return result;
        }
        set
        {
            ThrowIfDisposed();
            InteropUtils.ThrowIfFailed(_audioEndPointVolume.SetMute(value));
        }
    }

    /// <summary>Increases the master volume by one hardware-defined step.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public void VolumeStepUp()
    {
        ThrowIfDisposed();
        InteropUtils.ThrowIfFailed(_audioEndPointVolume.VolumeStepUp());
    }

    /// <summary>Decreases the master volume by one hardware-defined step.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public void VolumeStepDown()
    {
        ThrowIfDisposed();
        InteropUtils.ThrowIfFailed(_audioEndPointVolume.VolumeStepDown());
    }

    internal AudioEndpointVolume(IAudioEndpointVolumeCOM audioEndpointVolume)
    {
        _audioEndPointVolume = audioEndpointVolume;
        Channels = new AudioEndpointVolumeChannels(_audioEndPointVolume);
        StepInformation = new AudioEndpointVolumeStepInformation(_audioEndPointVolume);
        InteropUtils.ThrowIfFailed(_audioEndPointVolume.QueryHardwareSupport(out var hardwareSupp));
        HardwareSupport = (EndpointHardwareSupport)hardwareSupp;
        VolumeRange = new AudioEndPointVolumeVolumeRange(_audioEndPointVolume);
        _callBack = new AudioEndpointVolumeCallback(this);
        InteropUtils.ThrowIfFailed(_audioEndPointVolume.RegisterControlChangeNotify(_callBack));
    }

    /// <summary>Registers a consumer to receive endpoint volume and mute change notifications.</summary>
    /// <param name="eventConsumer">The consumer to notify. Registering the same instance twice is a
    /// no-op that returns the token from the first registration.</param>
    /// <returns>A token that unregisters this consumer when disposed. Safe to dispose more than once.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="eventConsumer"/> is null.</exception>
    /// <exception cref="System.ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    /// <remarks>
    /// The notification callback is registered with Core Audio once, when this object is created;
    /// registering a consumer only adds it to the managed fan-out list and makes no COM call.
    /// Disposing this <see cref="AudioEndpointVolume"/> unregisters every remaining consumer.
    /// </remarks>
    public IDisposable RegisterVolumeNotification(IAudioEndpointVolumeEvents eventConsumer)
    {
        InteropUtils.RequireNotNull(eventConsumer, nameof(eventConsumer));

        lock (_registrationsLock)
        {
            ThrowIfDisposed();

            if (_registrations.TryGetValue(eventConsumer, out var existing))
            {
                // Already registered: hand back the same token for the existing registration.
                return existing;
            }

            var token = new VolumeEventsRegistration(this, eventConsumer);
            _registrations[eventConsumer] = token;
            PublishConsumers();
            return token;
        }
    }

    // Remove only the matching registration; repeated disposal and stale tokens are harmless.
    internal void RemoveRegistration(IAudioEndpointVolumeEvents eventConsumer, VolumeEventsRegistration token)
    {
        lock (_registrationsLock)
        {
            if (!_registrations.TryGetValue(eventConsumer, out var existing)
                || !ReferenceEquals(existing, token))
            {
                return;
            }

            _registrations.Remove(eventConsumer);
            PublishConsumers();
        }
    }

    // Rebuilds the dispatch snapshot. Must be called while holding _registrationsLock.
    private void PublishConsumers()
    {
        IAudioEndpointVolumeEvents[] snapshot;
        if (_registrations.Count == 0)
        {
            snapshot = Array.Empty<IAudioEndpointVolumeEvents>();
        }
        else
        {
            snapshot = new IAudioEndpointVolumeEvents[_registrations.Count];
            _registrations.Keys.CopyTo(snapshot, 0);
        }

        Volatile.Write(ref _consumers, snapshot);
    }

    internal void FireNotification(AudioVolumeNotificationData notificationData)
    {
        var consumers = Volatile.Read(ref _consumers);

        List<Exception>? failures = null;
        for (var i = 0; i < consumers.Length; i++)
        {
            try
            {
                consumers[i].OnVolumeNotification(notificationData);
            }
            catch (Exception ex)
            {
                // Notify every consumer, then propagate failures to OnNotify for HRESULT conversion.
                (failures ??= new List<Exception>()).Add(ex);
            }
        }

        if (failures == null)
        {
            return;
        }

        if (failures.Count == 1)
        {
            // Rethrow the original rather than wrapping it, so the HRESULT handed back to Core
            // Audio is the consumer's own (e.g. a COMException's) instead of a generic one.
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        throw new AggregateException(failures);
    }

    /// <summary>
    /// Unregisters every remaining volume-notification consumer and the underlying Core Audio
    /// callback. Safe to call more than once.
    /// </summary>
    /// <remarks>
    /// There is no finalizer backing this up, by design. The only resource here is a Core Audio
    /// registration held through a COM callable wrapper, not a raw handle: the interop wrapper for
    /// <c>IAudioEndpointVolume</c> is released by the runtime on its own, and unregistering is a
    /// call into another COM object rather than a resource release - something a finalizer thread
    /// must not attempt, since finalization order between this object and that wrapper is
    /// undefined. A finalizer could not help anyway, because the live registration keeps this
    /// object reachable for exactly as long as it would have had work to do.
    /// </remarks>
    public void Dispose()
    {
        AudioEndpointVolumeCallback? callback;

        lock (_registrationsLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var channel in Channels)
            {
                channel.Dispose();
            }
            StepInformation.Dispose();

            // Drop the registrations so in-flight notifications stop reaching consumers and the
            // dictionary stops rooting them. Done under the same lock RegisterVolumeNotification
            // holds, so a registration cannot slip in beside a disposal.
            _registrations.Clear();
            Volatile.Write(ref _consumers, Array.Empty<IAudioEndpointVolumeEvents>());

            // Claim the callback while still holding the lock: two threads disposing concurrently
            // would otherwise both see it non-null and unregister the same callback twice.
            callback = _callBack;
            _callBack = null;
        }

        if (callback != null)
        {
            try
            {
                // Outside the lock: never hold one across a call into Core Audio.
                InteropUtils.ThrowIfFailed(_audioEndPointVolume.UnregisterControlChangeNotify(callback));
            }
            catch (Exception cleanupException)
            {
                InteropUtils.ReportFailure(cleanupException);
                // Best-effort: never let an exception escape Dispose.
            }
        }
    }

    private void ThrowIfDisposed()
    {
        InteropUtils.RequireNotDisposed(_disposed, this);
    }
}