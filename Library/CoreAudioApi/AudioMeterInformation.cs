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

/*
  MODIFICATIONS
  -------------
  This file is an ALTERED version of the original source by Ray Molenkamp and must not be
  misrepresented as being the original source code. Altered by Peter Šulek for AudioDeviceLib
  (https://github.com/psulek/AudioDeviceLib), starting from the copy bundled in
  AudioDeviceCmdlets (https://github.com/frgnca/AudioDeviceCmdlets, MIT).

  The changes are summarised in MODIFICATIONS.md at the repository root; the Git history of
  this file is the authoritative record.
*/

using System;
using System.Runtime.InteropServices;
using AudioDeviceLib.CoreAudioApi.Extensions;
using AudioDeviceLib.CoreAudioApi.Interfaces;
using JetBrains.Annotations;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// Managed wrapper over the Core Audio <c>IAudioMeterInformation</c> interface. Exposes the
/// current peak sample values (master and per-channel) for an audio endpoint.
/// </summary>
[PublicAPI]
public class AudioMeterInformation
{
    private IAudioMeterInformation _audioMeterInformation;

    internal AudioMeterInformation(IAudioMeterInformation realInterface)
    {
        _audioMeterInformation = realInterface;
        Marshal.ThrowExceptionForHR(_audioMeterInformation.QueryHardwareSupport(out var hardwareSupp));
        HardwareSupport = (EndpointHardwareSupport)hardwareSupp;
    }

    /// <summary>Gets the hardware functions (volume, mute, meter) natively supported by the endpoint.</summary>
    public EndpointHardwareSupport HardwareSupport { get; }

    public float[] GetChannelsPeakValues()
    {
        Marshal.ThrowExceptionForHR(_audioMeterInformation.GetChannelsPeakValues(out var afPeakValues));
        return afPeakValues;
    }

    public uint MeteringChannelCount
    {
        get
        {
            Marshal.ThrowExceptionForHR(_audioMeterInformation.GetMeteringChannelCount(out var channelCount));
            return channelCount;
        }
    }

    /// <summary>Gets the current master peak sample value in the range 0.0 to 1.0.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public float MasterPeakValue
    {
        get
        {
            Marshal.ThrowExceptionForHR(_audioMeterInformation.GetPeakValue(out var result));
            return result;
        }
    }
}