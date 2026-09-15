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

// AudioController is IDisposable — wrap it in a using so it is torn down deterministically.
using var audio = new AudioController();

// List active playback devices.
// These enumeration-only devices hold no COM callbacks, so they are safe to leave to GC.
foreach (var d in audio.GetPlaybackDevices())
{
    Console.WriteLine(d); // "[1] Speakers (Realtek...) (Playback) [Default]"
}

// Current default output. AudioDevice is IDisposable — once you touch its volume/mute
// (or its sessions) it registers Core Audio callbacks, so dispose it with a using.
using (AudioDevice current = audio.GetDefaultPlaybackDevice())
{
    if (current != null)
    {
        // Volume / mute
        current.SetVolumePercent(50f);
        current.IsMuted = false;

        // Set a specific device you already resolved (default = Multimedia + Communications)
        audio.SetDefaultDevice(current);

        // Or pick exactly which Windows roles to assign (flags can be combined)
        audio.SetDefaultDevice(current, DefaultRole.Console | DefaultRole.Multimedia);
        audio.SetDefaultDevice(current, DefaultRole.All);
    }
}

// Set the default output to the first device whose name contains "Speakers".
// Returns null if nothing matches, so you can fall back or report clearly.
using (AudioDevice set = audio.SetDefaultPlaybackByName("Speakers"))
{
    if (set == null)
    {
        Console.Error.WriteLine("No matching playback device found.");
    }
}
```

## Disposal

Much of the object graph implements `IDisposable`, so callers should dispose what
they own with `using`:

- `AudioController` — dispose the controller itself (shown above).
- `AudioDevice` — dispose any device you read volume/mute from or used for sessions;
  it deterministically tears down the registered Core Audio callbacks. Enumeration-only
  devices you only printed hold nothing registered and can be left to GC.
- Session notifications — `RegisterAudioSessionNotification` returns an `IDisposable`
  token; dispose it (or the owning device) to unregister the sink.

By convention the code in this repo also always uses **full braces** for `if`, `foreach`
and other control-flow blocks — even single-statement bodies — for clarity and to avoid
accidental scope bugs; the examples above follow that style.

## Session notifications

`RegisterAudioSessionNotification` returns an `IDisposable` token; wrap it in a `using`
so the callback is unregistered deterministically when you are done listening.

> **Threading:** callbacks are raised by Windows Core Audio on arbitrary, non-UI threads
> and may arrive concurrently. Keep each handler fast and thread-safe.

```csharp
using AudioDeviceLib;
using AudioDeviceLib.CoreAudioApi;

// Implement the attribute-free contract for the events you care about. Every callback receives an
// immutable AudioSessionInfo snapshot of the source session (identifiers, display name, icon path,
// state), so one consumer can serve many sessions — and there is no live COM object to misuse from
// the notification thread.
sealed class SessionLogger : IAudioSessionEvents
{
    public void OnSimpleVolumeChanged(AudioSessionInfo session, float newVolume, bool newMute, Guid eventContext)
    {
        Console.WriteLine($"pid={session.ProcessID} volume={newVolume:P0} muted={newMute}");
    }

    public void OnStateChanged(AudioSessionInfo session, AudioSessionState newState)
    {
        Console.WriteLine($"pid={session.ProcessID} state={newState}");
    }

    // The remaining IAudioSessionEvents members can be left as no-ops.
    public void OnDisplayNameChanged(AudioSessionInfo session, string newDisplayName, Guid eventContext) { }
    public void OnIconPathChanged(AudioSessionInfo session, string newIconPath, Guid eventContext) { }
    public void OnChannelVolumeChanged(AudioSessionInfo session, float[] newChannelVolumes, uint changedChannel, Guid eventContext) { }
    public void OnGroupingParamChanged(AudioSessionInfo session, Guid newGroupingParam, Guid eventContext) { }
    public void OnSessionDisconnected(AudioSessionInfo session, AudioSessionDisconnectReason disconnectReason) { }
}
```

```csharp
using var audio = new AudioController();
var logger = new SessionLogger();

using (AudioDevice device = audio.GetDefaultPlaybackDevice())
{
    if (device != null)
    {
        SessionCollection sessions = device.Device.AudioSessionManager.Sessions;

        // Register on every current session; keep the tokens so they can be disposed.
        var tokens = new List<IDisposable>();
        for (int i = 0; i < sessions.Count; i++)
        {
            tokens.Add(sessions[i].RegisterAudioSessionNotification(logger));
        }

        try
        {
            Console.WriteLine("Listening... press Enter to stop.");
            Console.ReadLine();
        }
        finally
        {
            // Dispose each token to unregister the callbacks.
            foreach (IDisposable token in tokens)
            {
                token.Dispose();
            }
        }
    }
}
```

For a single session you can inline the `using`:

```csharp
using (IDisposable token = session.RegisterAudioSessionNotification(logger))
{
    // ...callbacks fire here...
} // token.Dispose() unregisters the callback
```

Disposing the owning `AudioDevice` (or `AudioSessionControl`) also unregisters any
still-active callbacks as a safety net, so the tokens are the deterministic path and
disposal is the backstop.

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
