/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  EndpointVolumeLogger.cs
  Sample consumer of the public AudioDeviceLib IAudioEndpointVolumeEvents interface.
  Each callback writes to the console.

  NOTE: these callbacks are raised by Windows Core Audio on arbitrary, non-UI threads,
  so keep them fast and thread-safe (Console.WriteLine is fine).
*/

using System;
using AudioDeviceLib.CoreAudioApi;

namespace AudioDeviceLib.Test;

internal sealed class EndpointVolumeLogger : IAudioEndpointVolumeEvents
{
    // The notification carries no endpoint identity, so the endpoint this consumer was registered
    // on is captured here instead.
    private readonly string _endpointName;

    internal EndpointVolumeLogger(string endpointName)
    {
        _endpointName = endpointName;
    }

    public void OnVolumeNotification(AudioVolumeNotificationData data)
    {
        Console.WriteLine(
            $"[endpoint: {_endpointName}] master={data.MasterVolume:P0} muted={data.Muted} " +
            $"channels={data.Channels}, ctx: {data.EventContext}");
    }
}
