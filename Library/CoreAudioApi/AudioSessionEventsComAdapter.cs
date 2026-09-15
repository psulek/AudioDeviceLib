/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  AudioSessionEventsComAdapter.cs
  Bridges the raw COM sink (Interfaces.IAudioSessionEventsCOM) to a consumer's
  pure-C# IAudioSessionEvents implementation: it marshals the native arguments to
  natural .NET types and forwards each call, converting any exception to an HRESULT
  so nothing propagates back into native code.
*/

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// Internal COM sink that receives <see cref="IAudioSessionEventsCOM"/> callbacks and
/// forwards them to a consumer's <see cref="IAudioSessionEvents"/> instance.
/// </summary>
/// <remarks>
/// THREADING: the wrapped callbacks are raised by Windows Core Audio on arbitrary, non-UI
/// threads and may arrive concurrently. The forwarded consumer must be quick and thread-safe.
/// </remarks>
internal sealed class AudioSessionEventsComAdapter : IAudioSessionEventsCOM
{
    private const int S_OK = 0;

    /// <summary>The session these notifications originate from; the source for each snapshot.</summary>
    private readonly AudioSessionControl _session;

    /// <summary>The consumer implementation these COM callbacks are forwarded to.</summary>
    internal IAudioSessionEvents Target { get; }

    public AudioSessionEventsComAdapter(AudioSessionControl session, IAudioSessionEvents target)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public int OnDisplayNameChanged(string NewDisplayName, Guid EventContext)
    {
        try
        {
            Target.OnDisplayNameChanged(_session.ToSessionInfo(), NewDisplayName, EventContext);
            return S_OK;
        }
        catch (Exception ex)
        {
            return Marshal.GetHRForException(ex);
        }
    }

    public int OnIconPathChanged(string NewIconPath, Guid EventContext)
    {
        try
        {
            Target.OnIconPathChanged(_session.ToSessionInfo(), NewIconPath, EventContext);
            return S_OK;
        }
        catch (Exception ex)
        {
            return Marshal.GetHRForException(ex);
        }
    }

    public int OnSimpleVolumeChanged(float NewVolume, bool newMute, Guid EventContext)
    {
        try
        {
            Target.OnSimpleVolumeChanged(_session.ToSessionInfo(), NewVolume, newMute, EventContext);
            return S_OK;
        }
        catch (Exception ex)
        {
            return Marshal.GetHRForException(ex);
        }
    }

    public int OnChannelVolumeChanged(uint ChannelCount, IntPtr NewChannelVolumeArray, uint ChangedChannel,
        Guid EventContext)
    {
        try
        {
            float[] volumes;
            if (NewChannelVolumeArray != IntPtr.Zero && ChannelCount > 0)
            {
                volumes = new float[ChannelCount];
                Marshal.Copy(NewChannelVolumeArray, volumes, 0, (int)ChannelCount);
            }
            else
            {
                volumes = new float[0];
            }

            Target.OnChannelVolumeChanged(_session.ToSessionInfo(), volumes, ChangedChannel, EventContext);
            return S_OK;
        }
        catch (Exception ex)
        {
            return Marshal.GetHRForException(ex);
        }
    }

    public int OnGroupingParamChanged(Guid NewGroupingParam, Guid EventContext)
    {
        try
        {
            Target.OnGroupingParamChanged(_session.ToSessionInfo(), NewGroupingParam, EventContext);
            return S_OK;
        }
        catch (Exception ex)
        {
            return Marshal.GetHRForException(ex);
        }
    }

    public int OnStateChanged(AudioSessionState NewState)
    {
        try
        {
            Target.OnStateChanged(_session.ToSessionInfo(), NewState);
            return S_OK;
        }
        catch (Exception ex)
        {
            return Marshal.GetHRForException(ex);
        }
    }

    public int OnSessionDisconnected(AudioSessionDisconnectReason DisconnectReason)
    {
        try
        {
            Target.OnSessionDisconnected(_session.ToSessionInfo(), DisconnectReason);
            return S_OK;
        }
        catch (Exception ex)
        {
            return Marshal.GetHRForException(ex);
        }
    }
}

/// <summary>
/// Token returned by <see cref="AudioSessionControl.RegisterAudioSessionNotification"/>. Disposing it
/// unregisters exactly the registration it represents; safe to dispose more than once.
/// </summary>
internal sealed class SessionEventsRegistration : IDisposable
{
    private AudioSessionControl _owner;
    private readonly AudioSessionEventsComAdapter _adapter;

    internal SessionEventsRegistration(AudioSessionControl owner, AudioSessionEventsComAdapter adapter)
    {
        _owner = owner;
        _adapter = adapter;
    }

    public void Dispose()
    {
        AudioSessionControl owner = Interlocked.Exchange(ref _owner, null);
        owner?.RemoveRegistration(_adapter);
    }
}

/// <summary>
/// Reference-equality comparer for <see cref="IAudioSessionEvents"/> keys. Used instead of
/// <c>ReferenceEqualityComparer.Instance</c>, which is only available on net5+.
/// </summary>
internal sealed class AudioSessionEventsRefComparer : IEqualityComparer<IAudioSessionEvents>
{
    public static readonly AudioSessionEventsRefComparer Instance = new AudioSessionEventsRefComparer();

    public bool Equals(IAudioSessionEvents x, IAudioSessionEvents y) => ReferenceEquals(x, y);

    public int GetHashCode(IAudioSessionEvents obj) => RuntimeHelpers.GetHashCode(obj);
}