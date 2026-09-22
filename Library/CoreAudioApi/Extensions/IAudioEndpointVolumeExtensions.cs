using System;
using AudioDeviceLib.CoreAudioApi.Interfaces;
using static AudioDeviceLib.CoreAudioApi.InteropUtils;

namespace AudioDeviceLib.CoreAudioApi.Extensions;

// Every method here takes an optional event context and forwards it to IAudioEndpointVolume, which
// declares the parameter as `ref Guid`. An omitted context becomes Guid.Empty rather than a null
// pointer: Core Audio documents that a NULL eventContext is reported to subscribers as
// GUID_NULL, so the two are indistinguishable to anyone receiving the notification.
internal static class AudioEndpointVolumeExtensions
{
    public static int SetMasterVolumeLevelScalar(this IAudioEndpointVolumeCOM audioEndpointVolume, float level,
        Guid? eventContext = null)
    {
        Guid ctx = eventContext ?? Guid.Empty;
        return audioEndpointVolume.SetMasterVolumeLevelScalar(level, ref ctx);
    }

    public static int SetMasterVolumeLevel(this IAudioEndpointVolumeCOM audioEndpointVolume, float levelDb,
        Guid? eventContext = null)
    {
        Guid ctx = eventContext ?? Guid.Empty;
        return audioEndpointVolume.SetMasterVolumeLevel(levelDb, ref ctx);
    }

    public static int SetChannelVolumeLevel(this IAudioEndpointVolumeCOM audioEndpointVolume, uint channel,
        float levelDb, Guid? eventContext = null)
    {
        Guid ctx = eventContext ?? Guid.Empty;
        return audioEndpointVolume.SetChannelVolumeLevel(channel, levelDb, ref ctx);
    }

    public static int SetChannelVolumeLevelScalar(this IAudioEndpointVolumeCOM audioEndpointVolume, uint channel,
        float level, Guid? eventContext = null)
    {
        Guid ctx = eventContext ?? Guid.Empty;
        return audioEndpointVolume.SetChannelVolumeLevelScalar(channel, level, ref ctx);
    }

    public static int SetMute(this IAudioEndpointVolumeCOM audioEndpointVolume, bool value, Guid? eventContext = null)
    {
        Guid ctx = eventContext ?? Guid.Empty;
        return audioEndpointVolume.SetMute(value ? 1 : 0, ref ctx);
    }

    // Paired with SetMute above: the interface carries the mute flag as a blittable `int`, so the
    // bool conversion lives here. The flag is only meaningful once the HRESULT says so.
    public static int GetMute(this IAudioEndpointVolumeCOM audioEndpointVolume, out bool mute)
    {
        int hr = audioEndpointVolume.GetMute(out int nativeMute);
        mute = HrSuccess(hr) && nativeMute != 0;
        return hr;
    }

    public static int VolumeStepUp(this IAudioEndpointVolumeCOM audioEndpointVolume, Guid? eventContext = null)
    {
        Guid ctx = eventContext ?? Guid.Empty;
        return audioEndpointVolume.VolumeStepUp(ref ctx);
    }

    public static int VolumeStepDown(this IAudioEndpointVolumeCOM audioEndpointVolume, Guid? eventContext = null)
    {
        Guid ctx = eventContext ?? Guid.Empty;
        return audioEndpointVolume.VolumeStepDown(ref ctx);
    }
}