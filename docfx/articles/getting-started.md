# Getting started

## Requirements

- Windows with Core Audio support.
- A compatible .NET runtime. The package targets `net48`, `netstandard2.0`, and
  `net8.0-windows`; targeting .NET Standard does not make it cross-platform.
- No runtime NuGet dependencies. Audio access uses Windows COM interop.

Use a normal JIT deployment. Native AOT and trimming are not supported.


## Installation

Install from [NuGet](https://www.nuget.org/packages/AudioDeviceLib):

```sh
dotnet add package AudioDeviceLib --prerelease
```

The command includes prerelease versions. Import `AudioDeviceLib` for the main API
and `AudioDeviceLib.CoreAudioApi` for volume, session, property, and event types.


## Quick start

Static methods handle resource cleanup and return managed results. They are useful
for occasional operations; use a controller instance for repeated work.

```csharp
using System;
using AudioDeviceLib;

foreach (var device in AudioController.ListDevices(AudioDeviceKind.Playback))
{
    Console.WriteLine(device);
}

var playback = AudioController.GetDefaultPlayback();
if (playback != null)
{
    AudioController.SetVolume(playback.Id, 50f);
    AudioController.SetMute(playback.Id, false);
}
```

Convenience methods without a device ID operate on the default playback endpoint.
Volume percentages use a range of 0–100; lower-level scalar APIs use 0–1.

