/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  MMNotificationClientComAdapter.cs
  Bridges the raw COM sink (Interfaces.IMMNotificationClient) to a consumer's pure-C#
  IAudioDeviceEvents implementation: it forwards each call, converting any exception to an
  HRESULT so nothing propagates back into native code.
*/

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using AudioDeviceLib.CoreAudioApi.Interfaces;
using AudioDeviceLib.Lib;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// Internal COM sink that receives <see cref="IMMNotificationClient"/> callbacks and forwards them to
/// a consumer's <see cref="IAudioDeviceEvents"/> instance.
/// </summary>
/// <remarks>
/// THREADING: the wrapped callbacks are raised by Windows Core Audio on arbitrary, non-UI threads and
/// may arrive concurrently. The forwarded consumer must be quick and thread-safe.
/// </remarks>
internal sealed class MMNotificationClientComAdapter : IMMNotificationClient
{
    private const int S_OK = 0;

    /// <summary>The consumer implementation these COM callbacks are forwarded to.</summary>
    internal IAudioDeviceEvents Target { get; }

    public MMNotificationClientComAdapter(IAudioDeviceEvents target)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public int OnDeviceStateChanged(string deviceId, DeviceState newState)
    {
        try
        {
            Target.OnDeviceStateChanged(deviceId, newState);
            return S_OK;
        }
        catch (Exception ex)
        {
            return Marshal.GetHRForException(ex);
        }
    }

    public int OnDeviceAdded(string deviceId)
    {
        try
        {
            Target.OnDeviceAdded(deviceId);
            return S_OK;
        }
        catch (Exception ex)
        {
            return Marshal.GetHRForException(ex);
        }
    }

    public int OnDeviceRemoved(string deviceId)
    {
        try
        {
            Target.OnDeviceRemoved(deviceId);
            return S_OK;
        }
        catch (Exception ex)
        {
            return Marshal.GetHRForException(ex);
        }
    }

    public int OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        try
        {
            Target.OnDefaultDeviceChanged(flow, role, defaultDeviceId);
            return S_OK;
        }
        catch (Exception ex)
        {
            return Marshal.GetHRForException(ex);
        }
    }

    public int OnPropertyValueChanged(string deviceId, PropertyKey key)
    {
        try
        {
            Target.OnPropertyValueChanged(deviceId, key);
            return S_OK;
        }
        catch (Exception ex)
        {
            return Marshal.GetHRForException(ex);
        }
    }
}

/// <summary>
/// Token returned by <see cref="AudioController.RegisterDeviceNotification"/>. Disposing it
/// unregisters exactly the registration it represents; safe to dispose more than once.
/// </summary>
internal sealed class DeviceEventsRegistration : IDisposable
{
    private AudioController _owner;
    private readonly MMNotificationClientComAdapter _adapter;

    internal DeviceEventsRegistration(AudioController owner, MMNotificationClientComAdapter adapter)
    {
        _owner = owner;
        _adapter = adapter;
    }

    public void Dispose()
    {
        AudioController owner = Interlocked.Exchange(ref _owner, null);
        owner?.RemoveDeviceRegistration(_adapter);
    }
}

/// <summary>
/// Reference-equality comparer for <see cref="IAudioDeviceEvents"/> keys. Used instead of
/// <c>ReferenceEqualityComparer.Instance</c>, which is only available on net5+.
/// </summary>
internal sealed class AudioDeviceEventsRefComparer : IEqualityComparer<IAudioDeviceEvents>
{
    public static readonly AudioDeviceEventsRefComparer Instance = new AudioDeviceEventsRefComparer();

    public bool Equals(IAudioDeviceEvents x, IAudioDeviceEvents y) => ReferenceEquals(x, y);

    public int GetHashCode(IAudioDeviceEvents obj) => RuntimeHelpers.GetHashCode(obj);
}
