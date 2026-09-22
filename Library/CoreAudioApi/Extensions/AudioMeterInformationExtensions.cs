using System;
using AudioDeviceLib.CoreAudioApi.Interfaces;
using static AudioDeviceLib.CoreAudioApi.InteropUtils;

namespace AudioDeviceLib.CoreAudioApi.Extensions;

internal static class AudioMeterInformationExtensions
{
    /// <summary>Reads the per-channel peak meter values for the endpoint.</summary>
    /// <param name="audioMeterInformation"> The audio meter information. </param>
    /// <param name="peakValues">
    /// The per-channel peak values on success; <see cref="Array.Empty{T}"/> when the call fails or the
    /// endpoint reports no metering channels. Never <c>null</c>, so check the HRESULT, not the array.
    /// </param>
    /// <returns>S_OK on success, otherwise the failing HRESULT.</returns>
    public static int GetChannelsPeakValues(this IAudioMeterInformationCOM audioMeterInformation, out float[] peakValues)
    {
        peakValues = Array.Empty<float>();
        int hr = audioMeterInformation.GetMeteringChannelCount(out uint channelCount);
        if (HrFailed(hr) || channelCount == 0)
        {
            return hr;
        }
        
        var values = new float[channelCount];

        unsafe
        {
            fixed (float* peakValuesPtr = values)
            {
                hr = audioMeterInformation.GetChannelsPeakValues(channelCount, peakValuesPtr);
            }
        }

        if (HrSuccess(hr))
        {
            peakValues = values;
        }
                
        return hr;
    }    
}