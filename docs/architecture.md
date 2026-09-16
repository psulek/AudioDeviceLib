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
        +EnumerateAudioEndPoints(DataFlowFilter, DeviceStateFilter) MMDeviceCollection
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

    AudioController o-- MMDeviceEnumerator : uses
    AudioController ..> AudioDevice : creates
    AudioController ..> PolicyConfigClient : sets default via
    AudioController ..> DefaultRole : parameter
    AudioDevice *-- MMDevice : wraps
    AudioDevice ..> AudioDeviceKind
    AudioDevice ..> AudioDeviceInfo : ToDeviceInfo() snapshot
    AudioController ..> AudioDeviceInfo : static API returns
    AudioController ..> DataFlowFilter : query parameter
    AudioController ..> DeviceStateFilter : query parameter
    MMDeviceEnumerator ..> DataFlowFilter : query parameter
    MMDeviceEnumerator ..> DeviceStateFilter : query parameter
    MMDevice ..> DataFlow : single value
    MMDevice ..> DeviceState : single value
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
