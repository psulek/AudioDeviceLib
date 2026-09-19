using System;
using AudioDeviceLib.CoreAudioApi.Interfaces;
using JetBrains.Annotations;
using static AudioDeviceLib.CoreAudioApi.InteropUtils;

namespace AudioDeviceLib.CoreAudioApi.Extensions;

// Every method here takes an optional event context and forwards it to IAudioEndpointVolume, which
// declares the parameter as `ref Guid`. An omitted context becomes Guid.Empty rather than a null
// pointer: Core Audio documents that a NULL pguidEventContext is reported to subscribers as
// GUID_NULL, so the two are indistinguishable to anyone receiving the notification.
[PublicAPI]
internal static class AudioEndpointVolumeExtensions
{
    public static int SetMasterVolumeLevelScalar(this IAudioEndpointVolume audioEndpointVolume, float fLevel,
        Guid? eventContext = null)
    {
        Guid ctx = eventContext ?? Guid.Empty;
        return audioEndpointVolume.SetMasterVolumeLevelScalar(fLevel, ref ctx);
    }

    public static int SetMasterVolumeLevel(this IAudioEndpointVolume audioEndpointVolume, float levelDb,
        Guid? eventContext = null)
    {
        Guid ctx = eventContext ?? Guid.Empty;
        return audioEndpointVolume.SetMasterVolumeLevel(levelDb, ref ctx);
    }

    public static int SetChannelVolumeLevel(this IAudioEndpointVolume audioEndpointVolume, uint nChannel,
        float levelDb, Guid? eventContext = null)
    {
        Guid ctx = eventContext ?? Guid.Empty;
        return audioEndpointVolume.SetChannelVolumeLevel(nChannel, levelDb, ref ctx);
    }

    public static int SetChannelVolumeLevelScalar(this IAudioEndpointVolume audioEndpointVolume, uint nChannel,
        float fLevel, Guid? eventContext = null)
    {
        Guid ctx = eventContext ?? Guid.Empty;
        return audioEndpointVolume.SetChannelVolumeLevelScalar(nChannel, fLevel, ref ctx);
    }

    public static int SetMute(this IAudioEndpointVolume audioEndpointVolume, bool value, Guid? eventContext = null)
    {
        Guid ctx = eventContext ?? Guid.Empty;
        return audioEndpointVolume.SetMute(value ? 1 : 0, ref ctx);
    }

    // Paired with SetMute above: the interface carries the mute flag as a blittable `int`, so the
    // bool conversion lives here. The flag is only meaningful once the HRESULT says so.
    public static int GetMute(this IAudioEndpointVolume audioEndpointVolume, out bool mute)
    {
        int hr = audioEndpointVolume.GetMute(out int nativeMute);
        mute = HrSuccess(hr) && nativeMute != 0;
        return hr;
    }

    public static int VolumeStepUp(this IAudioEndpointVolume audioEndpointVolume, Guid? eventContext = null)
    {
        Guid ctx = eventContext ?? Guid.Empty;
        return audioEndpointVolume.VolumeStepUp(ref ctx);
    }

    public static int VolumeStepDown(this IAudioEndpointVolume audioEndpointVolume, Guid? eventContext = null)
    {
        Guid ctx = eventContext ?? Guid.Empty;
        return audioEndpointVolume.VolumeStepDown(ref ctx);
    }
}