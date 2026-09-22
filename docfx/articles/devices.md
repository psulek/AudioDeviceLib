# Working with devices


Use live devices when you need several operations on the same endpoint or access to
its volume controls, sessions, metering, and property store.

```csharp
using System;
using AudioDeviceLib;

using var audio = new AudioController();
using var device = audio.GetDefaultPlaybackDevice();
if (device != null)
{
    device.SetVolumePercent(50f);
    device.IsMuted = false;
    Console.WriteLine($"Peak: {device.GetPeakValue():P0}");

    var snapshot = device.ToDeviceInfo();
    Console.WriteLine(snapshot);
}
```

Device metadata is cached. Refresh it when needed, or subscribe to notifications
for changes. Snapshots can be retained after the live device is disposed. Device
equality is based on endpoint identity.

Instance enumeration supports filtering by direction and availability. Static
listing returns active endpoints. Lookups can return no result when an endpoint
is unavailable; devices may disappear between enumeration and use. Invalid input
and native failures are reported as exceptions.

Windows maintains separate defaults for different application roles. Default-device
changes target multimedia and communications unless you specify another combination.
Use `DefaultRole.All` to include the console role. Assignments are sequential: if one
fails, earlier assignments remain applied.

```csharp
// Use the ID of an endpoint selected by your application.
AudioController.SetDefaultDevice(deviceId, DefaultRole.All);
```

Name matching is also available, but friendly names are localized and may not be
unique. Prefer endpoint IDs when you need to identify a particular device.

