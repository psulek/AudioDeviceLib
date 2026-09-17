using System;
using AudioDeviceLib.CoreAudioApi.Interfaces;
using JetBrains.Annotations;

namespace AudioDeviceLib.CoreAudioApi.Extensions;

[PublicAPI]
internal static class IAudioSessionControl2Extensions
{
    public static int SetDisplayName(this IAudioSessionControl2 audioSessionControl, string value, Guid? eventContext)
    {
        unsafe
        {
            if (eventContext is { } ctx)
                return audioSessionControl.SetDisplayName(value, &ctx);

            return audioSessionControl.SetDisplayName(value, null);
        }
    }
}