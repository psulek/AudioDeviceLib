# AudioDeviceLib

A .NET library for Windows audio devices. Enumerate playback and recording endpoints,
choose defaults, control volume and mute, and receive device and session notifications
from C# without hosting PowerShell.

## Get started

```sh
dotnet add package AudioDeviceLib --prerelease
```

```csharp
using AudioDeviceLib;

foreach (var device in AudioController.ListDevices(AudioDeviceKind.Playback))
{
    System.Console.WriteLine(device);
}
```

## Documentation

- [Getting started](articles/getting-started.md): requirements, installation, and a first example.
- [Working with devices](articles/devices.md): live endpoints, snapshots, volume, and defaults.
- [Notifications](articles/notifications.md): device, volume, and session callbacks.
- [Lifetime and threading](articles/lifetime.md): ownership, disposal, and callback safety.
- [API reference](api/index.md): public types and members generated from the library source.
- [Contributing](articles/contributing.md): build, test, and publish this site.

Windows is required at runtime. The package targets .NET Framework 4.8,
.NET Standard 2.0, and .NET 8 for Windows. Native AOT and trimming are unsupported.

## License

AudioDeviceLib is licensed under [MIT](https://github.com/psulek/AudioDeviceLib/blob/main/LICENSE).
See the [third-party notices](https://github.com/psulek/AudioDeviceLib/blob/main/THIRD-PARTY-NOTICES.md)
for attribution and incorporated license terms.
