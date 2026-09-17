/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  AudioSessionEventsAdapter.cs
  Bridges the raw COM sink (Interfaces.IAudioSessionEvents) to a consumer's
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
internal sealed unsafe class AudioSessionEventsAdapter : Interfaces.IAudioSessionEventsCOM
{
    private const int S_OK = 0;

    // Core Audio passes a null LPCGUID whenever the caller that made the change supplied no event
    // context, so every context pointer is copied out on entry and never allowed to escape: the
    // pointer is caller-owned and valid only for the duration of the callback.
    private static Guid? ToEventContext(Guid* eventContext) => eventContext != null ? *eventContext : null;

    /// <summary>The session these notifications originate from; the source for each snapshot.</summary>
    private readonly AudioSessionControl _session;

    /// <summary>The consumer implementation these COM callbacks are forwarded to.</summary>
    internal IAudioSessionEvents Target { get; }

    public AudioSessionEventsAdapter(AudioSessionControl session, IAudioSessionEvents target)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public int OnDisplayNameChanged(string newDisplayName, Guid* eventContext)
    {
        try
        {
            Target.OnDisplayNameChanged(_session.ToSessionInfo(), newDisplayName, ToEventContext(eventContext));
            return S_OK;
        }
        catch (Exception ex)
        {
            return InteropUtils.ReportFailure(ex);
        }
    }

    public int OnIconPathChanged(string newIconPath, Guid* eventContext)
    {
        try
        {
            Target.OnIconPathChanged(_session.ToSessionInfo(), newIconPath, ToEventContext(eventContext));
            return S_OK;
        }
        catch (Exception ex)
        {
            return InteropUtils.ReportFailure(ex);
        }
    }

    public int OnSimpleVolumeChanged(float newVolume, int newMute, Guid* eventContext)
    {
        try
        {
            Target.OnSimpleVolumeChanged(_session.ToSessionInfo(), newVolume, newMute != 0, ToEventContext(eventContext));
            return S_OK;
        }
        catch (Exception ex)
        {
            return InteropUtils.ReportFailure(ex);
        }
    }

    public int OnChannelVolumeChanged(uint channelCount, IntPtr newChannelVolumeArray, uint changedChannel,
        Guid* eventContext)
    {
        try
        {
            float[] volumes = Array.Empty<float>();
            if (newChannelVolumeArray != IntPtr.Zero && channelCount > 0)
            {
                volumes = new float[channelCount];
                Marshal.Copy(newChannelVolumeArray, volumes, 0, (int)channelCount);
            }

            Target.OnChannelVolumeChanged(_session.ToSessionInfo(), volumes, changedChannel, ToEventContext(eventContext));
            return S_OK;
        }
        catch (Exception ex)
        {
            return InteropUtils.ReportFailure(ex);
        }
    }

    public int OnGroupingParamChanged(Guid* newGroupingParam, Guid* eventContext)
    {
        try
        {
            Target.OnGroupingParamChanged(_session.ToSessionInfo(), 
                newGroupingParam != null ? *newGroupingParam : Guid.Empty,
                ToEventContext(eventContext));
            return S_OK;
        }
        catch (Exception ex)
        {
            return InteropUtils.ReportFailure(ex);
        }
    }

    public int OnStateChanged(AudioSessionState newState)
    {
        try
        {
            Target.OnStateChanged(_session.ToSessionInfo(), newState);
            return S_OK;
        }
        catch (Exception ex)
        {
            return InteropUtils.ReportFailure(ex);
        }
    }

    public int OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason)
    {
        try
        {
            Target.OnSessionDisconnected(_session.ToSessionBaseInfo(), disconnectReason);
            return S_OK;
        }
        catch (Exception ex)
        {
            return InteropUtils.ReportFailure(ex);
        }
    }
}

/// <summary>
/// Token returned by <see cref="AudioSessionControl.RegisterAudioSessionNotification"/>. Disposing it
/// unregisters exactly the registration it represents; safe to dispose more than once.
/// </summary>
internal sealed class SessionEventsRegistration : IDisposable
{
    // NOTE: owner is never null from ctor arg but will be nulled on Dispose to prevent double-unregistering, so we need to make the field nullable.
    private AudioSessionControl? _owner;
    private readonly AudioSessionEventsAdapter _adapter;

    internal SessionEventsRegistration(AudioSessionControl owner, AudioSessionEventsAdapter adapter)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public void Dispose()
    {
        var owner = Interlocked.Exchange(ref _owner, null);
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

    public bool Equals(IAudioSessionEvents? x, IAudioSessionEvents? y) => ReferenceEquals(x, y);

    public int GetHashCode(IAudioSessionEvents obj) => RuntimeHelpers.GetHashCode(obj);
}