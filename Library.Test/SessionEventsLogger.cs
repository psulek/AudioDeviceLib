/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  SessionEventsLogger.cs
  Sample consumer of the public AudioDeviceLib IAudioSessionEvents interface.
  Each callback simply writes to the debug output.

  NOTE: these callbacks are raised by Windows Core Audio on arbitrary, non-UI threads,
  so keep them fast and thread-safe (Console.WriteLine is fine).
*/

using System;
using System.Diagnostics;
using AudioDeviceLib.CoreAudioApi;

namespace AudioDeviceLib.Test;

internal sealed class SessionEventsLogger(string tag) : IAudioSessionEvents
{
    public void OnDisplayNameChanged(string newDisplayName, Guid eventContext)
    {
        Console.WriteLine($"[{tag}] OnDisplayNameChanged: '{newDisplayName}' (ctx {eventContext})");
    }

    public void OnIconPathChanged(string newIconPath, Guid eventContext)
    {
        Console.WriteLine($"[{tag}] OnIconPathChanged: '{newIconPath}' (ctx {eventContext})");
    }

    public void OnSimpleVolumeChanged(float newVolume, bool newMute, Guid eventContext)
    {
        Console.WriteLine($"[{tag}] OnSimpleVolumeChanged: volume={newVolume:P0} mute={newMute} (ctx {eventContext})");
    }

    public void OnChannelVolumeChanged(float[] newChannelVolumes, uint changedChannel, Guid eventContext)
    {
        int count = newChannelVolumes != null ? newChannelVolumes.Length : 0;
        string values = newChannelVolumes != null ? string.Join(", ", newChannelVolumes) : "(null)";
        Console.WriteLine($"[{tag}] OnChannelVolumeChanged: {count} channel(s) [{values}] changed={changedChannel} (ctx {eventContext})");
    }

    public void OnGroupingParamChanged(Guid newGroupingParam, Guid eventContext)
    {
        Console.WriteLine($"[{tag}] OnGroupingParamChanged: group={newGroupingParam} (ctx {eventContext})");
    }

    public void OnStateChanged(AudioSessionState newState)
    {
        Console.WriteLine($"[{tag}] OnStateChanged: {newState}");
    }

    public void OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason)
    {
        Console.WriteLine($"[{tag}] OnSessionDisconnected: {disconnectReason}");
    }
}
