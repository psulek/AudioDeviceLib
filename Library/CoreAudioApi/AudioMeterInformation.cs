/*
  LICENSE
  -------
  Copyright (C) 2007-2010 Ray Molenkamp

  This source code is provided 'as-is', without any express or implied
  warranty.  In no event will the authors be held liable for any damages
  arising from the use of this source code or the software it produces.

  Permission is granted to anyone to use this source code for any purpose,
  including commercial applications, and to alter it and redistribute it
  freely, subject to the following restrictions:

  1. The origin of this source code must not be misrepresented; you must not
     claim that you wrote the original source code.  If you use this source code
     in a product, an acknowledgment in the product documentation would be
     appreciated but is not required.
  2. Altered source versions must be plainly marked as such, and must not be
     misrepresented as being the original source code.
  3. This notice may not be removed or altered from any source distribution.
*/

using System.Runtime.InteropServices;
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// Managed wrapper over the Core Audio <c>IAudioMeterInformation</c> interface. Exposes the
/// current peak sample values (master and per-channel) for an audio endpoint.
/// </summary>
public class AudioMeterInformation
{
    private IAudioMeterInformation _AudioMeterInformation;
    private EEndpointHardwareSupport _HardwareSupport;
    private AudioMeterInformationChannels _Channels;

    internal AudioMeterInformation(IAudioMeterInformation realInterface)
    {
        _AudioMeterInformation = realInterface;
        Marshal.ThrowExceptionForHR(_AudioMeterInformation.QueryHardwareSupport(out var HardwareSupp));
        _HardwareSupport = (EEndpointHardwareSupport)HardwareSupp;
        _Channels = new AudioMeterInformationChannels(_AudioMeterInformation);
    }

    /// <summary>Gets the collection of per-channel peak meter values.</summary>
    public AudioMeterInformationChannels PeakValues => _Channels;

    /// <summary>Gets the hardware functions (volume, mute, meter) natively supported by the endpoint.</summary>
    public EEndpointHardwareSupport HardwareSupport => _HardwareSupport;

    /// <summary>Gets the current master peak sample value in the range 0.0 to 1.0.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public float MasterPeakValue
    {
        get
        {
            Marshal.ThrowExceptionForHR(_AudioMeterInformation.GetPeakValue(out var result));
            return result;
        }
    }
}