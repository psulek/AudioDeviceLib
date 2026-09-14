# AudioDeviceLib

A small, dependency-free **.NET** library for Windows that lists audio endpoints
and sets the default playback/recording device, plus volume and mute control.

It replaces the need to host PowerShell and the third-party `AudioDeviceCmdlets`
module: the same Core Audio (WASAPI) interop is called directly from managed code.

- Target frameworks: `net48`, `netstandard2.0`, `net8.0-windows`
- No NuGet dependencies (pure COM interop).
- Windows only.

## Why

Switching the default output device on Windows has no public Win32 API. This library
uses the same, well-established techniques as `AudioDeviceCmdlets`:

- Enumeration via the documented Core Audio COM interfaces (`IMMDeviceEnumerator`, etc.).
- Setting the default endpoint via the **undocumented** `IPolicyConfig` interface.

## Provenance & license

- Overall library: **MIT** (see `LICENSE`).
- The `AudioController` / `AudioDevice` facade is derived from the MIT-licensed
  [`AudioDeviceCmdlets`](https://github.com/frgnca/AudioDeviceCmdlets) by Francois Gendron.
  The PowerShell cmdlet layer was **not** copied; it was replaced with a plain managed API.
- The `CoreAudioApi/` interop is the WASAPI wrapper by Ray Molenkamp (zlib-style license).

See `THIRD-PARTY-NOTICES.md` for the full required notices.

## Usage

```csharp
using AudioDeviceLib;

var audio = new AudioController();

// List active playback devices
foreach (var d in audio.GetPlaybackDevices())
    Console.WriteLine(d); // "[1] Speakers (Realtek...) (Playback) [Default]"

// Current default output
AudioDevice current = audio.GetDefaultPlaybackDevice();

// Set the default output to the first device whose name contains "Speakers".
// Returns null if nothing matches, so you can fall back or report clearly.
AudioDevice set = audio.SetDefaultPlaybackByName("Speakers");
if (set == null)
    Console.Error.WriteLine("No matching playback device found.");

// Set a specific device you already resolved (default = Multimedia + Communications)
audio.SetDefaultDevice(current);

// Or pick exactly which Windows roles to assign (flags can be combined)
audio.SetDefaultDevice(current, DefaultRole.Console | DefaultRole.Multimedia);
audio.SetDefaultDevice(current, DefaultRole.All);

// Volume / mute
current.SetVolumePercent(50f);
current.IsMuted = false;
```

## Default-role semantics

Windows tracks three independent default-device roles. `DefaultRole` is a `[Flags]`
enum, so you can combine them; each set flag maps to one native `ERole` assignment.

| Flag | Native role | Notes |
|------|-------------|-------|
| `Console` | `eConsole` | System sounds, games, voice commands |
| `Multimedia` | `eMultimedia` | Music and movies. Matches `-DefaultOnly` |
| `Communications` | `eCommunications` | Voice chat. Matches `-CommunicationOnly` |
| `Default` (= `Multimedia \| Communications`) | both | The default; matches `Set-AudioDevice` with no switch |
| `All` (= `Console \| Multimedia \| Communications`) | all three | Make this THE default for everything |

`SetDefaultDevice` defaults to `DefaultRole.Default`, which (like the original cmdlet)
does **not** set the Console role. If some applications on your target follow the
Console role, pass `DefaultRole.All` or include `DefaultRole.Console` in the combination.

## Notes for callers

- Endpoint friendly names are **localized** by Windows (e.g. "Reproduktory" on
  Czech/Slovak systems, "Lautsprecher" on German). Do not hard-code the English
  word "Speakers" when matching across a mixed fleet; match on a stable substring,
  or select by kind and default state instead.
- COM must be usable on the calling thread. In a plain console/service this works
  out of the box.

## Build

```
dotnet build
```
