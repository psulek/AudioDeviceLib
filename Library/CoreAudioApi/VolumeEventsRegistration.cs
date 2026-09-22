/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  VolumeEventsRegistration.cs
  The registration token returned by AudioEndpointVolume.RegisterVolumeNotification, plus the
  reference-equality comparer used to key registered consumers.

  Unlike AudioSessionControl - which registers one COM sink per consumer - AudioEndpointVolume
  registers a single native callback in its constructor and fans out to the registered consumers
  in managed code, so a registration here is pure bookkeeping and involves no COM call.
*/

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// Token returned by <see cref="AudioEndpointVolume.RegisterVolumeNotification"/>. Disposing it
/// unregisters exactly the registration it represents; safe to dispose more than once.
/// </summary>
internal sealed class VolumeEventsRegistration : IDisposable
{
    // NOTE: owner is never null from ctor arg but will be nulled on Dispose to prevent double-unregistering, so we need to make the field nullable.
    private AudioEndpointVolume? _owner;
    private readonly IAudioEndpointVolumeEvents _consumer;

    internal VolumeEventsRegistration(AudioEndpointVolume owner, IAudioEndpointVolumeEvents consumer)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
    }

    public void Dispose()
    {
        // Exchange first: a second Dispose (or a race between two threads disposing the same
        // token) finds null and does nothing, so the removal below happens exactly once.
        var owner = Interlocked.Exchange(ref _owner, null);
        owner?.RemoveRegistration(_consumer, this);
    }
}

/// <summary>
/// Reference-equality comparer for <see cref="IAudioEndpointVolumeEvents"/> keys. Used instead of
/// <c>ReferenceEqualityComparer.Instance</c>, which is only available on net5+.
/// </summary>
internal sealed class AudioEndpointVolumeEventsRefComparer : IEqualityComparer<IAudioEndpointVolumeEvents>
{
    public static readonly AudioEndpointVolumeEventsRefComparer Instance = new AudioEndpointVolumeEventsRefComparer();

    public bool Equals(IAudioEndpointVolumeEvents? x, IAudioEndpointVolumeEvents? y) => ReferenceEquals(x, y);

    public int GetHashCode(IAudioEndpointVolumeEvents obj) => RuntimeHelpers.GetHashCode(obj);
}
