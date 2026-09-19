using System;
using AudioDeviceLib.CoreAudioApi.Interfaces;
using JetBrains.Annotations;
using static AudioDeviceLib.CoreAudioApi.InteropUtils;

namespace AudioDeviceLib.CoreAudioApi.Extensions;

// Wrappers over the raw ISimpleAudioVolume entry points: they confine `unsafe` to this file and
// convert the interop-shaped parameters (a blittable `int` mute flag, a nullable event-context
// pointer) to natural .NET types.
//
// An omitted event context passes a null pointer rather than Guid.Empty, unlike the
// IAudioEndpointVolume wrappers. Core Audio delivers that null through to session subscribers as a
// null LPCGUID, so the two are distinguishable here and collapsing them would lose information.
[PublicAPI]
internal static class SimpleAudioVolumeExtensions
{
    public static int SetMasterVolume(this ISimpleAudioVolume simpleAudioVolume, float fLevel,
        Guid? eventContext = null)
    {
        unsafe
        {
            return eventContext is { } ctx
                ? simpleAudioVolume.SetMasterVolume(fLevel, &ctx)
                : simpleAudioVolume.SetMasterVolume(fLevel, null);
        }
    }

    public static int SetMute(this ISimpleAudioVolume simpleAudioVolume, bool value,
        Guid? eventContext = null)
    {
        int mute = value ? 1 : 0;
        unsafe
        {
            return eventContext is { } ctx
                ? simpleAudioVolume.SetMute(mute, &ctx)
                : simpleAudioVolume.SetMute(mute, null);
        }
    }

    // Paired with SetMute above: the interface carries the mute flag as a blittable `int`, so the
    // bool conversion lives here. The flag is only meaningful once the HRESULT says so.
    public static int GetMute(this ISimpleAudioVolume simpleAudioVolume, out bool mute)
    {
        int hr = simpleAudioVolume.GetMute(out int nativeMute);
        mute = HrSuccess(hr) && nativeMute != 0;
        return hr;
    }
}
