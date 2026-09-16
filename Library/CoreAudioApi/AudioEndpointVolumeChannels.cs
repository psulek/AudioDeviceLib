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

  Changes from the original:
  - Namespace changed to `AudioDeviceLib.CoreAudioApi` (file-scoped); unused `using`
    directives removed.
  - Reformatted to the project's C# style (full braces, modern C# syntax) and annotated with XML
    documentation comments.
*/

using System.Runtime.InteropServices;
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>The collection of per-channel volume controls for an audio endpoint.</summary>
public class AudioEndpointVolumeChannels
{
    IAudioEndpointVolume _AudioEndPointVolume;
    AudioEndpointVolumeChannel[] _Channels;

    /// <summary>Gets the number of channels exposed by the endpoint.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public int Count
    {
        get
        {
            Marshal.ThrowExceptionForHR(_AudioEndPointVolume.GetChannelCount(out var result));
            return result;
        }
    }

    /// <summary>Gets the volume control for the channel at the specified zero-based index.</summary>
    /// <param name="index">The zero-based channel index (0 to <see cref="Count"/> - 1).</param>
    /// <returns>The <see cref="AudioEndpointVolumeChannel"/> at the requested position.</returns>
    public AudioEndpointVolumeChannel this[int index] => _Channels[index];

    internal AudioEndpointVolumeChannels(IAudioEndpointVolume parent)
    {
        int ChannelCount;
        _AudioEndPointVolume = parent;

        ChannelCount = Count;
        _Channels = new AudioEndpointVolumeChannel[ChannelCount];
        for (int i = 0; i < ChannelCount; i++)
        {
            _Channels[i] = new AudioEndpointVolumeChannel(_AudioEndPointVolume, i);
        }
    }
}