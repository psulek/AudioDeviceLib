using System;
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.CoreAudioApi.Extensions;

internal static class AudioEndpointVolumeExtensions
{
    public static int SetMasterVolumeLevelScalar(this IAudioEndpointVolume audioEndpointVolume, float fLevel,
        Guid? eventContext = null)
    {
        unsafe
        {
            return eventContext is { } ctx
                ? audioEndpointVolume.SetMasterVolumeLevelScalar(fLevel, &ctx)
                : audioEndpointVolume.SetMasterVolumeLevelScalar(fLevel, null);
        }
    }

    public static int SetMasterVolumeLevel(this IAudioEndpointVolume audioEndpointVolume, float fLevelDB,
        Guid? eventContext = null)
    {
        unsafe
        {
            return eventContext is { } ctx
                ? audioEndpointVolume.SetMasterVolumeLevel(fLevelDB, &ctx)
                : audioEndpointVolume.SetMasterVolumeLevel(fLevelDB, null);
        }
    }

    public static int SetChannelVolumeLevel(this IAudioEndpointVolume audioEndpointVolume, uint nChannel,
        float fLevelDB, Guid? eventContext = null)
    {
        unsafe
        {
            return eventContext is { } ctx
                ? audioEndpointVolume.SetChannelVolumeLevel(nChannel, fLevelDB, &ctx)
                : audioEndpointVolume.SetChannelVolumeLevel(nChannel, fLevelDB, null);
        }
    }

    public static int SetChannelVolumeLevelScalar(this IAudioEndpointVolume audioEndpointVolume, uint nChannel,
        float fLevel, Guid? eventContext = null)
    {
        unsafe
        {
            return eventContext is { } ctx
                ? audioEndpointVolume.SetChannelVolumeLevelScalar(nChannel, fLevel, &ctx)
                : audioEndpointVolume.SetChannelVolumeLevelScalar(nChannel, fLevel, null);
        }
    }

    public static int SetMute(this IAudioEndpointVolume audioEndpointVolume, bool bMute, Guid? eventContext = null)
    {
        unsafe
        {
            return eventContext is { } ctx
                ? audioEndpointVolume.SetMute(bMute, &ctx)
                : audioEndpointVolume.SetMute(bMute, null);
        }
    }

    public static int VolumeStepUp(this IAudioEndpointVolume audioEndpointVolume, Guid? eventContext = null)
    {
        unsafe
        {
            return eventContext is { } ctx
                ? audioEndpointVolume.VolumeStepUp(&ctx)
                : audioEndpointVolume.VolumeStepUp(null);
        }
    }

    public static int VolumeStepDown(this IAudioEndpointVolume audioEndpointVolume, Guid? eventContext = null)
    {
        unsafe
        {
            return eventContext is { } ctx
                ? audioEndpointVolume.VolumeStepDown(&ctx)
                : audioEndpointVolume.VolumeStepDown(null);
        }
    }
}