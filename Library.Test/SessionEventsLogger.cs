/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  SessionEventsLogger.cs
  Sample consumer of the public AudioDeviceLib IAudioSessionEvents interface.
  Each callback simply writes to the console, identifying the source session from the
  AudioSessionInfo snapshot it receives.

  NOTE: these callbacks are raised by Windows Core Audio on arbitrary, non-UI threads,
  so keep them fast and thread-safe (Console.WriteLine is fine).
*/

using System;
using AudioDeviceLib.CoreAudioApi;

namespace AudioDeviceLib.Test;

internal sealed class SessionEventsLogger : IAudioSessionEvents
{
    // Builds a short prefix identifying the session, using the info carried by the notification.
    private static string Describe(AudioSessionInfo session)
    {
        string name = !string.IsNullOrEmpty(session.DisplayName)
            ? session.DisplayName
            : session.IsSystemSoundsSession ? "System Sounds" : "session";
        return $"{name} pid={session.ProcessID}";
    }

    // A null context means the caller that made the change supplied none - render it distinctly,
    // because a null Guid? interpolates to the empty string and would read as a blank context.
    private static string Describe(Guid? eventContext) =>
        eventContext?.ToString() ?? "<null>";

    public void OnDisplayNameChanged(AudioSessionInfo session, string newDisplayName, Guid? eventContext)
    {
        Console.WriteLine($"[{Describe(session)}] OnDisplayNameChanged: '{newDisplayName}' (ctx {Describe(eventContext)})");
    }

    public void OnIconPathChanged(AudioSessionInfo session, string newIconPath, Guid? eventContext)
    {
        Console.WriteLine($"[{Describe(session)}] OnIconPathChanged: '{newIconPath}' (ctx {Describe(eventContext)})");
    }

    public void OnSimpleVolumeChanged(AudioSessionInfo session, float newVolume, bool newMute, Guid? eventContext)
    {
        Console.WriteLine($"[{Describe(session)}] OnSimpleVolumeChanged: volume={newVolume:P0} mute={newMute} (ctx {Describe(eventContext)})");
    }

    public void OnChannelVolumeChanged(AudioSessionInfo session, float[] newChannelVolumes, uint changedChannel, Guid? eventContext)
    {
        int count = newChannelVolumes != null ? newChannelVolumes.Length : 0;
        string values = newChannelVolumes != null ? string.Join(", ", newChannelVolumes) : "(null)";
        Console.WriteLine($"[{Describe(session)}] OnChannelVolumeChanged: {count} channel(s) [{values}] changed={changedChannel} (ctx {Describe(eventContext)})");
    }

    public void OnGroupingParamChanged(AudioSessionInfo session, Guid newGroupingParam, Guid? eventContext)
    {
        Console.WriteLine($"[{Describe(session)}] OnGroupingParamChanged: group={newGroupingParam} (ctx {Describe(eventContext)})");
    }

    public void OnStateChanged(AudioSessionInfo session, AudioSessionState newState)
    {
        Console.WriteLine($"[{Describe(session)}] OnStateChanged: {newState}");
    }

    public void OnSessionDisconnected(AudioSessionInfo session, AudioSessionDisconnectReason disconnectReason)
    {
        Console.WriteLine($"[{Describe(session)}] OnSessionDisconnected: {disconnectReason}");
    }
}
