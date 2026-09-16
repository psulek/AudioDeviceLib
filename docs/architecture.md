# Architecture (dev reference)

Internal design notes for AudioDeviceLib. Not linked from the public README — this
is for contributors.

`AudioController` is the entry point. It enumerates endpoints through an
`MMDeviceEnumerator`, hands back `AudioDevice` facades, and sets the default device
via the undocumented `PolicyConfigClient`. Each `AudioDevice` wraps an `MMDevice`,
which lazily activates the volume, session and metering sub-interfaces. Most of the
graph implements `IDisposable`.

```mermaid
classDiagram
    direction LR

    class AudioController {
        -MMDeviceEnumerator _enumerator
        +GetDevices() IReadOnlyList~AudioDevice~
        +GetPlaybackDevices() IReadOnlyList~AudioDevice~
        +GetRecordingDevices() IReadOnlyList~AudioDevice~
        +GetDefaultPlaybackDevice(bool communications) AudioDevice
        +GetDefaultRecordingDevice(bool communications) AudioDevice
        +SetDefaultDevice(AudioDevice device, DefaultRole roles) void
        +SetDefaultDevice(string deviceId, DefaultRole roles) void
        +SetDefaultPlaybackByName(string name, DefaultRole roles) AudioDevice
        +SetDefaultRecordingByName(string name, DefaultRole roles) AudioDevice
        +RegisterDeviceNotification(IAudioDeviceEvents consumer) IDisposable
        +Dispose() void
    }

    class AudioDevice {
        +int Index
        +bool IsDefault
        +bool IsDefaultCommunication
        +AudioDeviceKind Kind
        +string Name
        +string Id
        +MMDevice Device
        +bool IsMuted
        +GetVolumePercent() float
        +SetVolumePercent(float percent) void
        +ToggleMute() void
        +GetPeakValue() float
        +Dispose() void
    }

    class MMDeviceEnumerator {
        +EnumerateAudioEndPoints(DataFlow, DeviceState) MMDeviceCollection
        +GetDefaultAudioEndpoint(DataFlow, Role) MMDevice
        +GetDevice(string ID) MMDevice
    }

    class MMDevice {
        +AudioSessionManager AudioSessionManager
        +AudioEndpointVolume AudioEndpointVolume
        +AudioMeterInformation AudioMeterInformation
        +PropertyStore Properties
        +string FriendlyName
        +string ID
        +DataFlow DataFlow
        +DeviceState State
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

    class IDisposable {
        <<interface>>
        +Dispose() void
    }

    AudioController o-- MMDeviceEnumerator : uses
    AudioController ..> AudioDevice : creates
    AudioController ..> PolicyConfigClient : sets default via
    AudioController ..> DefaultRole : parameter
    AudioDevice *-- MMDevice : wraps
    AudioDevice ..> AudioDeviceKind
    MMDeviceEnumerator ..> MMDevice : returns
    MMDevice *-- "0..1" AudioEndpointVolume
    MMDevice *-- "0..1" AudioSessionManager
    AudioSessionManager *-- SessionCollection
    SessionCollection o-- "*" AudioSessionControl

    IDisposable <|.. AudioController
    IDisposable <|.. AudioDevice
    IDisposable <|.. MMDevice
    IDisposable <|.. AudioSessionManager
    IDisposable <|.. SessionCollection
    IDisposable <|.. AudioSessionControl
    IDisposable <|.. AudioEndpointVolume
```
