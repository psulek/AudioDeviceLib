using System;
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.CoreAudioApi.Extensions;

// Adapt managed setter arguments to COM signatures and return their HRESULTs unchanged.
// Omitted event contexts are passed as Guid.Empty.
internal static class AudioSessionControl2Extensions
{
    public static int SetDisplayName(this IAudioSessionControl2COM audioSessionControl, string value,
        Guid? eventContext = null)
    {
        InteropUtils.RequireNotNull(value, nameof(value));

        Guid ctx = eventContext ?? Guid.Empty;
        return audioSessionControl.SetDisplayName(value, ref ctx);
    }

    public static int SetIconPath(this IAudioSessionControl2COM audioSessionControl, string value,
        Guid? eventContext = null)
    {
        InteropUtils.RequireNotNull(value, nameof(value));

        Guid ctx = eventContext ?? Guid.Empty;
        return audioSessionControl.SetIconPath(value, ref ctx);
    }

    public static int SetGroupingParam(this IAudioSessionControl2COM audioSessionControl, Guid @override,
        Guid? eventContext = null)
    {
        Guid ctx = eventContext ?? Guid.Empty;
        return audioSessionControl.SetGroupingParam(ref @override, ref ctx);
    }
    
    public static int SetDuckingPreference(this IAudioSessionControl2COM audioSessionControl, bool value)
    {
        return audioSessionControl.SetDuckingPreference(value ? 1 : 0);
    }
}