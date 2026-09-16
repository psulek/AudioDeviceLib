# Architecture (dev reference)

Internal design notes for AudioDeviceLib. Not linked from the public README — this
is for contributors.

`AudioController` is the entry point. It holds the `IMMDeviceEnumerator` directly
(created lazily), hands back `AudioDevice` instances, and sets the default device via
the undocumented `PolicyConfigClient`. `AudioDevice` is the single device type: it wraps
the `IMMDevice` itself, snapshots identity and state at construction, and lazily
activates the volume, session and metering sub-interfaces. Most of the graph implements
`IDisposable`.

Two invariants worth knowing before changing anything here:

- **Nothing is shared.** Core Audio returns a distinct COM object for every acquisition,
  on every path (`GetDevice`, `IMMDeviceCollection::Item`, `GetDefaultAudioEndpoint`), even
  for the same endpoint ID. So every `AudioDevice` owns its own RCW and disposing one can
  never strand another. This is what makes returning fresh instances safe, and it is why
  there is no device cache.
- **Deterministic COM release is limited to objects that never escape.** The enumerator,
  the `IMMDeviceCollection`, and the throwaway endpoint inside `TryGetDefaultId` are
  released with `Marshal.ReleaseComObject`. The device's own `IMMDevice` and the
  sub-interface RCWs are not: consumers can hold those, and each wrapper holds exactly one
  reference, so one stray double-release would throw `InvalidComObjectException`.

```mermaid
classDiagram
    direction LR

    class AudioController {
        -IMMDeviceEnumerator _realEnumerator
        +GetDevices(DataFlowFilter, DeviceStateFilter) IReadOnlyList~AudioDevice~
        +GetPlaybackDevices(DeviceStateFilter) IReadOnlyList~AudioDevice~
        +GetRecordingDevices(DeviceStateFilter) IReadOnlyList~AudioDevice~
        +GetDeviceById(string deviceId) AudioDevice
        +GetDeviceInfo(string deviceId) AudioDeviceInfo
        +GetDefaultPlaybackDevice(bool communications) AudioDevice
        +GetDefaultRecordingDevice(bool communications) AudioDevice
        +SetDefaultDevice(AudioDevice device, DefaultRole roles) void
        -SetDefaultDeviceById(string deviceId, DefaultRole roles) void
        +RegisterDeviceNotification(IAudioDeviceEvents consumer) IDisposable
        +Dispose() void
        +ListDevices()$ IReadOnlyList~AudioDeviceInfo~
        +ListDevices(AudioDeviceKind kind)$ IReadOnlyList~AudioDeviceInfo~
        +GetDefaultPlayback(bool communications)$ AudioDeviceInfo
        +GetDefaultRecording(bool communications)$ AudioDeviceInfo
        +SetDefaultDevice(string deviceId, DefaultRole roles)$ void
        +SetDefaultPlaybackByName(string name, DefaultRole roles)$ AudioDeviceInfo
        +SetDefaultRecordingByName(string name, DefaultRole roles)$ AudioDeviceInfo
        +GetVolume(string deviceId)$ float
        +SetVolume(string deviceId, float percent)$ void
        +IsMuted(string deviceId)$ bool
        +SetMute(string deviceId, bool mute)$ void
        +ToggleMute(string deviceId)$ bool
    }

    class AudioDeviceInfo {
        +bool IsDefault
        +bool IsDefaultCommunication
        +AudioDeviceKind Kind
        +string Name
        +string Id
        +DeviceState State
    }

    class AudioDevice {
        -IMMDevice _realDevice
        +bool IsDefault
        +bool IsDefaultCommunication
        +AudioDeviceKind Kind
        +string Name
        +string Id
        +DeviceState State
        +bool IsActive
        +AudioEndpointVolume Volume
        +AudioSessionManager SessionManager
        +AudioMeterInformation Meter
        +PropertyStore Properties
        +bool IsMuted
        +Refresh() void
        +ToDeviceInfo() AudioDeviceInfo
        +GetVolumePercent() float
        +SetVolumePercent(float percent) void
        +ToggleMute() bool
        +GetPeakValue() float
        +Dispose() void
    }

    class AudioSessionManager {
        +SessionCollection Sessions
        +Dispose() void
    }

    class SessionCollection {
        +AudioSessionControl this[int index]
        +int Count
        +Dispose() void
    }

    class AudioSessionControl {
        +AudioSessionState State
        +string DisplayName
        +string SessionIdentifier
        +uint ProcessID
        +AudioMeterInformation AudioMeterInformation
        +SimpleAudioVolume SimpleAudioVolume
        +RegisterAudioSessionNotification(IAudioSessionEvents) IDisposable
        +UnregisterAudioSessionNotification(IAudioSessionEvents) void
        +Dispose() void
    }

    class AudioEndpointVolume {
        +float MasterVolumeLevel
        +float MasterVolumeLevelScalar
        +bool Mute
        +OnVolumeNotification event
        +VolumeStepUp() void
        +VolumeStepDown() void
        +Dispose() void
    }

    class PolicyConfigClient {
        +SetDefaultEndpoint(string deviceId, Role role) void
    }

    class DefaultRole {
        <<Flags enum>>
        Console
        Multimedia
        Communications
        Default
        All
    }

    class AudioDeviceKind {
        <<enumeration>>
        Playback
        Recording
    }

    class DataFlowFilter {
        <<enumeration>>
        Render
        Capture
        All
    }

    class DataFlow {
        <<enumeration>>
        Render
        Capture
    }

    class DeviceStateFilter {
        <<Flags enum>>
        Active
        Disabled
        NotPresent
        Unplugged
        All
    }

    class DeviceState {
        <<enumeration>>
        Active
        Disabled
        NotPresent
        Unplugged
    }

    class IDisposable {
        <<interface>>
        +Dispose() void
    }

    AudioController ..> MMDeviceCollection : enumerates
    AudioController ..> AudioDevice : creates
    AudioController ..> PolicyConfigClient : sets default via
    AudioController ..> DefaultRole : parameter
    MMDeviceCollection ..> AudioDevice : yields
    AudioDevice ..> AudioDeviceKind
    AudioDevice ..> AudioDeviceInfo : ToDeviceInfo() snapshot
    AudioController ..> AudioDeviceInfo : static API returns
    AudioController ..> DataFlowFilter : query parameter
    AudioController ..> DeviceStateFilter : query parameter
    AudioController ..> DataFlow : single value
    AudioDevice ..> DeviceState : single value
    AudioDevice *-- "0..1" AudioEndpointVolume
    AudioDevice *-- "0..1" AudioSessionManager
    AudioSessionManager *-- SessionCollection
    SessionCollection o-- "*" AudioSessionControl

    IDisposable <|.. AudioController
    IDisposable <|.. AudioDevice
    IDisposable <|.. MMDeviceCollection
    IDisposable <|.. AudioSessionManager
    IDisposable <|.. SessionCollection
    IDisposable <|.. AudioSessionControl
    IDisposable <|.. AudioEndpointVolume
```
