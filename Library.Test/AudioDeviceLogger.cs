/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  DeviceLogger.cs
  Sample consumer of the public AudioDeviceLib IAudioDeviceEvents interface.
  Each callback writes to the console.

  NOTE: these callbacks are raised by Windows Core Audio on arbitrary, non-UI threads,
  so keep them fast and thread-safe (Console.WriteLine is fine).
*/

using System;
using AudioDeviceLib.CoreAudioApi;

namespace AudioDeviceLib.Test;

internal sealed class AudioDeviceLogger : IAudioDeviceEvents
{
    public void OnDeviceStateChanged(string deviceId, DeviceState newState)
    {
        Console.WriteLine($"[device] OnDeviceStateChanged: state={newState} id={deviceId}");
    }

    public void OnDeviceAdded(string deviceId)
    {
        Console.WriteLine($"[device] OnDeviceAdded: id={deviceId}");
    }

    public void OnDeviceRemoved(string deviceId)
    {
        Console.WriteLine($"[device] OnDeviceRemoved: id={deviceId}");
    }

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        Console.WriteLine($"[device] OnDefaultDeviceChanged: flow={flow} role={role} id={defaultDeviceId ?? "(none)"}");
    }

    public void OnPropertyValueChanged(string deviceId, PropertyKey key)
    {
        Console.WriteLine($"[device] OnPropertyValueChanged: property={key.Name} id={deviceId}");
    }
}
