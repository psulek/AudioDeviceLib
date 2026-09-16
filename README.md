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

## Install

```
dotnet add package AudioDeviceLib --prerelease
```

The current release is `1.0.0-rc.1`, a release candidate. NuGet does not resolve prereleases by
default, so the `--prerelease` flag is required — or pin it explicitly:

```xml
<PackageReference Include="AudioDeviceLib" Version="1.0.0-rc.1" />
```

## Usage

There are two ways in: **static one-liners** for the common cases, and the **instance API**
when you need live objects or repeated operations.

### One-liners

Each static call creates and disposes its own `AudioController` and device, so you own
nothing and there is nothing to dispose. They hand back `AudioDeviceInfo`, an immutable
snapshot, rather than a live `AudioDevice`.

```csharp
using AudioDeviceLib.Lib;

// Defaults
AudioDeviceInfo playback = AudioController.GetDefaultPlayback();
AudioDeviceInfo recording = AudioController.GetDefaultRecording();
Console.WriteLine(playback.Name);

// Volume (0..100) and mute on the default playback device
float volume = AudioController.GetVolume();
AudioController.SetVolume(50f);
bool muted = AudioController.IsMuted();
AudioController.SetMute(false);
bool nowMuted = AudioController.ToggleMute();

// ...or on a specific endpoint
AudioController.SetVolume(playback.Id, 25f);

// Active endpoints, as snapshots
IReadOnlyList<AudioDeviceInfo> all = AudioController.ListDevices();
IReadOnlyList<AudioDeviceInfo> outputs = AudioController.ListDevices(AudioDeviceKind.Playback);

// Make the first device whose name contains "Speakers" the default.
// Returns null if nothing matches, so you can fall back or report clearly.
AudioDeviceInfo set = AudioController.SetDefaultPlaybackByName("Speakers");
if (set == null)
{
    Console.Error.WriteLine("No matching playback device found.");
}
```

These create a controller per call. For repeated work — polling, or several operations on
the same device — use the instance API below.

### Instance API

```csharp
using AudioDeviceLib.Lib;

// AudioController is IDisposable — wrap it in a using so it is torn down deterministically.
using var audio = new AudioController();

// List active playback devices. AudioDevice is IDisposable; dispose what you enumerate.
foreach (var d in audio.GetPlaybackDevices())
{
    Console.WriteLine(d); // "Speakers (Realtek...) (Playback) [Default]"
    d.Dispose();
}

// Current default output.
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

        // Detach an immutable snapshot you can keep after the device is disposed
        AudioDeviceInfo snapshot = current.ToDeviceInfo();

        // Volume, sessions, metering and the raw property store hang off the device
        current.Volume.VolumeStepUp();
        var sessions = current.SessionManager.Sessions;
        float peak = current.GetPeakValue();
    }
}
```

`Id`, `Name`, `Kind` and `State` are captured when the device is created, so reading them
costs nothing and still works after the device is disposed. Call `Refresh()` to re-read
`Name`/`State`, or register for device notifications (below) to be told when they change.
Devices compare by endpoint ID, so `a.Equals(b)` is `true` for two handles on the same
endpoint — reference equality never is.

`GetDevices` on the instance API also takes `DataFlowFilter` and `DeviceStateFilter`
(in `AudioDeviceLib.CoreAudioApi`) if you need endpoints that are disabled, not present
or unplugged — the static `ListDevices` returns active endpoints only.

## Disposal

Much of the object graph implements `IDisposable`, so callers should dispose what
they own with `using`:

- `AudioController` — dispose the controller itself (shown above). That unregisters any
  device-notification sinks still attached; it does **not** dispose devices it handed you.
- `AudioDevice` — dispose every device you obtain. Disposal tears down the Core Audio
  callbacks and activations that device made, and afterwards every member that talks to
  Core Audio (`Volume`, `SessionManager`, `Meter`, `Properties`, `Refresh`, the volume and
  mute helpers) throws `ObjectDisposedException`. The identity snapshot (`Id`, `Name`,
  `Kind`, `State`, `ToDeviceInfo()`, `ToString()`, `Equals()`) stays readable. Disposing
  twice is a no-op, and disposing one device never affects another — each one wraps its
  own Core Audio endpoint object.
- Session notifications — `RegisterAudioSessionNotification` returns an `IDisposable`
  token; dispose it (or the owning device) to unregister the sink.
- The **static** methods own and dispose everything they create, and return
  `AudioDeviceInfo` snapshots rather than live devices — there is nothing for you to
  dispose.

By convention the code in this repo also always uses **full braces** for `if`, `foreach`
and other control-flow blocks — even single-statement bodies — for clarity and to avoid
accidental scope bugs; the examples above follow that style.

## Session notifications

`RegisterAudioSessionNotification` returns an `IDisposable` token; wrap it in a `using`
so the callback is unregistered deterministically when you are done listening.

> **Threading:** callbacks are raised by Windows Core Audio on arbitrary, non-UI threads
> and may arrive concurrently. Keep each handler fast and thread-safe.

```csharp
using AudioDeviceLib.Lib;
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
        SessionCollection sessions = device.SessionManager.Sessions;

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

## Device notifications

To be told when endpoints are added/removed, change state, or when the **default device**
(including the default **communications** device) changes, register an `IAudioDeviceEvents`
consumer on the controller. Like session notifications, registration returns an `IDisposable`
token; disposing it (or the controller) unregisters.

```csharp
using AudioDeviceLib.Lib;
using AudioDeviceLib.CoreAudioApi;

sealed class DeviceLogger : IAudioDeviceEvents
{
    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        // role distinguishes the communications default from the console/multimedia default;
        // defaultDeviceId is null when there is no longer a default for this flow/role.
        Console.WriteLine($"default {flow}/{role} -> {defaultDeviceId ?? "(none)"}");
    }

    // The remaining IAudioDeviceEvents members can be left as no-ops.
    public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }
    public void OnDeviceAdded(string deviceId) { }
    public void OnDeviceRemoved(string deviceId) { }
    public void OnPropertyValueChanged(string deviceId, PropertyKey key) { }
}
```

```csharp
using var audio = new AudioController();

using (IDisposable token = audio.RegisterDeviceNotification(new DeviceLogger()))
{
    // ...callbacks fire here (on arbitrary, non-UI threads)...
} // token.Dispose() unregisters
```

> **Threading:** callbacks arrive on arbitrary, non-UI threads and may be concurrent. Keep
> handlers fast and thread-safe, and don't register/unregister or dispose the controller from
> inside a callback.

## Default-role semantics

Windows tracks three independent default-device roles. `DefaultRole` is a `[Flags]`
enum, so you can combine them; each set flag maps to one `Role` assignment.

| Flag | Maps to (`Role`) | Notes |
|------|------------------|-------|
| `Console` | `Role.Console` | System sounds, games, voice commands |
| `Multimedia` | `Role.Multimedia` | Music and movies. Matches `-DefaultOnly` |
| `Communications` | `Role.Communications` | Voice chat. Matches `-CommunicationOnly` |
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

## Tests

```
dotnet test Library.UnitTests/Library.UnitTests.csproj -c Release
```

The suite runs three times, once per shipped asset: `net48`, `net7.0-windows` (which is how the
`netstandard2.0` build gets executed — netstandard cannot be targeted directly) and
`net8.0-windows`. One test asserts which asset each leg actually loaded, so a silent collapse onto a
single build would fail rather than pass quietly.

Tests are read-only: nothing changes volume, mute or the default device. Tests that need a real
endpoint skip themselves on a machine with no audio hardware, so the suite is still meaningful on a
headless CI runner — what it covers there is COM activation, enumeration and the deterministic
`Marshal.ReleaseComObject` paths.
