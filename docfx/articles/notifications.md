# Notifications


Implement the appropriate event interface, register a consumer, and retain the
returned token for as long as you want notifications. For example, this complete
console example listens for endpoint-volume changes:

```csharp
using System;
using AudioDeviceLib;
using AudioDeviceLib.CoreAudioApi;

using var audio = new AudioController();
using var device = audio.GetDefaultPlaybackDevice();
if (device != null)
{
    using var subscription = device.Volume.RegisterVolumeNotification(new VolumeLogger());
    Console.WriteLine("Listening for volume changes. Press Enter to stop.");
    Console.ReadLine();
}

sealed class VolumeLogger : IAudioEndpointVolumeEvents
{
    public void OnVolumeNotification(AudioVolumeNotificationData data)
    {
        Console.WriteLine($"Volume: {data.MasterVolume:P0}; muted: {data.Muted}");
    }
}
```

Device notifications report endpoint and default-device changes. Session notifications
report changes within individual audio sessions and include managed snapshots of the
source session. Register separately on each session you want to observe.

Session enumeration is a snapshot. Refreshing the session manager replaces that
snapshot and disposes its previous session controls and subscriptions.

See the [sample application](https://github.com/psulek/AudioDeviceLib/tree/main/Library.Test) for device and session event consumers.

