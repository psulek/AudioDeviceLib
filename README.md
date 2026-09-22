# AudioDeviceLib

[![CI](https://github.com/psulek/AudioDeviceLib/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/psulek/AudioDeviceLib/actions/workflows/ci.yml)

[![NuGet](https://img.shields.io/nuget/vpre/AudioDeviceLib.svg)](https://www.nuget.org/packages/AudioDeviceLib)
[![Downloads](https://img.shields.io/nuget/dt/AudioDeviceLib.svg)](https://www.nuget.org/packages/AudioDeviceLib)

A small .NET library for Windows audio devices. Enumerate playback and recording
endpoints, choose defaults, control volume and mute, and receive device and session
notifications directly from C#.

[Documentation and API reference](https://psulek.github.io/AudioDeviceLib/)

## Table of contents

- [Requirements](#requirements)
- [Installation](#installation)
- [Quick start](#quick-start)
- [Working with devices](#working-with-devices)
- [Notifications](#notifications)
- [Lifetime and threading](#lifetime-and-threading)
- [API](#api)
- [Compatibility and diagnostics](#compatibility-and-diagnostics)
- [Build and test](#build-and-test)
- [Documentation](#documentation)
- [Contributing](#contributing)
- [License and credits](#license-and-credits)

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
Volume percentages use a range of 0-100; lower-level scalar APIs use 0-1.

## Working with devices

Use a controller instance for repeated operations on a live device:

```csharp
using AudioDeviceLib;

using var audio = new AudioController();
using var device = audio.GetDefaultPlaybackDevice();
if (device != null)
{
    device.SetVolumePercent(50f);
    device.IsMuted = false;
}
```

See [working with devices](https://psulek.github.io/AudioDeviceLib/articles/devices.html)
for enumeration, snapshots, and default-device selection.

## Notifications

Subscribe to device, volume, and session changes. See the
[notifications guide](https://psulek.github.io/AudioDeviceLib/articles/notifications.html)
for registration and examples.

## Lifetime and threading

Dispose live audio objects and handle callbacks safely. See
[lifetime and threading](https://psulek.github.io/AudioDeviceLib/articles/lifetime.html)
for ownership, cleanup, and callback rules.

## API

See the [full API reference](https://psulek.github.io/AudioDeviceLib/api/index.html)
for public types, method signatures, overloads, and member documentation.

The [quick start](#quick-start) demonstrates static methods, while
[working with devices](#working-with-devices) demonstrates public instance methods.
For more guidance, visit the [documentation website](https://psulek.github.io/AudioDeviceLib/).

## Compatibility and diagnostics

Device enumeration and audio controls use Windows Core Audio. Changing the default
device depends on the undocumented policy-configuration interface and may be
unsupported on some Windows configurations. COM must be available on the calling thread.

Callback and cleanup failures are reported through `System.Diagnostics.Trace`.
Configure a trace listener when you need diagnostic output.

## Build and test

Run from the repository root on Windows:

```sh
dotnet build AudioDeviceLib.slnx -c Release
dotnet test Library.UnitTests/Library.UnitTests.csproj -c Release -m:1
```

Tests cover the three shipped assets through .NET Framework 4.8, .NET 7, and .NET 8
hosts. The .NET 7 host exercises the .NET Standard asset. Run targets serially because
hardware tests share the machine's audio devices.

Some integration tests change volume or mute and restore the previous settings.
Session metadata tests create their own silent session. Tests that require an audio
endpoint skip when none is available. Explicit race tests run separately:

```sh
dotnet test Library.UnitTests/Library.UnitTests.csproj -c Release -m:1 --filter FullyQualifiedName~ControllerDisposeRaceTests
```

Enable the repository's pre-commit hook in each clone:

```sh
git config core.hooksPath .githooks
```

The hook runs the Slopwatch code-quality check before committing.

Optional code-quality and coverage checks:

```sh
dotnet tool restore
dotnet slopwatch analyze --fail-on warning
dotnet test Library.UnitTests/Library.UnitTests.csproj -c Release -m:1 --settings coverage.runsettings --collect:"XPlat Code Coverage" --results-directory TestResults
dotnet reportgenerator -reports:"TestResults/**/coverage.opencover.xml" -targetdir:coverage -reporttypes:"Html;TextSummary"
```

## Documentation

The [documentation website](https://psulek.github.io/AudioDeviceLib/) includes usage
guides and a generated public API reference, using DocFX's modern theme.

With the repository SDK installed, build and preview from the repository root:

```sh
dotnet tool restore
dotnet docfx docfx/docfx.json --warningsAsErrors
dotnet docfx serve _site
```

Open <http://localhost:8080>. See the [documentation contributor guide](docfx/articles/contributing.md)
for editing and GitHub Pages setup. In **Settings > Pages**, select **GitHub Actions**;
`.github/workflows/docs.yml` validates pull requests and deploys from `main`.

## Contributing

See the [contribution guide](https://psulek.github.io/AudioDeviceLib/articles/contributing.html)
for build, test, documentation, and release publishing notes.

## License and credits

AudioDeviceLib is licensed under [MIT](LICENSE).

The managed device API derives from
[AudioDeviceCmdlets](https://github.com/frgnca/AudioDeviceCmdlets) by Francois Gendron
(MIT). Its PowerShell layer was replaced with a plain managed API. The Core Audio
interop incorporates Ray Molenkamp's wrapper under its zlib-style license.

See [third-party notices](THIRD-PARTY-NOTICES.md) for attribution and license terms,
and [modifications](MODIFICATIONS.md) for the changes to incorporated code.
