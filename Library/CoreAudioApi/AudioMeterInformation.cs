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

  The changes are summarized in MODIFICATIONS.md at the repository root; the Git history of
  this file is the authoritative record.
*/

using System;
using AudioDeviceLib.CoreAudioApi.Extensions;
using AudioDeviceLib.CoreAudioApi.Interfaces;
using JetBrains.Annotations;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// Managed wrapper over the Core Audio <c>IAudioMeterInformation</c> interface. Exposes the
/// current peak sample values (master and per-channel) for an audio endpoint.
/// </summary>
[PublicAPI]
public sealed class AudioMeterInformation : IDisposable
{
    private IAudioMeterInformationCOM _audioMeterInformation;

    internal AudioMeterInformation(IAudioMeterInformationCOM audioMeterInformation)
    {
        _audioMeterInformation = audioMeterInformation ?? throw new ArgumentNullException(nameof(audioMeterInformation));
        InteropUtils.ThrowIfFailed(_audioMeterInformation.QueryHardwareSupport(out var hardwareSupp));
        HardwareSupport = (EndpointHardwareSupport)hardwareSupp;
    }

    /// <summary>Gets the hardware functions (volume, mute, meter) natively supported by the endpoint.</summary>
    public EndpointHardwareSupport HardwareSupport { get; }

    /// <summary>Reads the current peak scalar for each channel.</summary>
    /// <returns>A new array of channel peaks.</returns>
    public float[] GetChannelsPeakValues()
    {
        ThrowIfDisposed();
        InteropUtils.ThrowIfFailed(_audioMeterInformation.GetChannelsPeakValues(out var peakValues));
        return peakValues;
    }

    /// <summary>Gets the number of channels available for metering.</summary>
    public uint MeteringChannelCount
    {
        get
        {
            ThrowIfDisposed();
            InteropUtils.ThrowIfFailed(_audioMeterInformation.GetMeteringChannelCount(out var channelCount));
            return channelCount;
        }
    }

    /// <summary>Gets the current master peak sample value in the range 0.0 to 1.0.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public float MasterPeakValue
    {
        get
        {
            ThrowIfDisposed();
            InteropUtils.ThrowIfFailed(_audioMeterInformation.GetPeakValue(out var result));
            return result;
        }
    }
    private volatile bool _disposed;

    /// <summary>Invalidates this wrapper without releasing externally held COM references.</summary>
    public void Dispose() => _disposed = true;

    private void ThrowIfDisposed()
    {
        InteropUtils.RequireNotDisposed(_disposed, this);
    }
}