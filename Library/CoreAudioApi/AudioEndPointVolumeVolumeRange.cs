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

/// <summary>Describes the supported volume range of an audio endpoint, in decibels.</summary>
public class AudioEndPointVolumeVolumeRange
{
    float _VolumeMindB;
    float _VolumeMaxdB;
    float _VolumeIncrementdB;

    internal AudioEndPointVolumeVolumeRange(IAudioEndpointVolume parent)
    {
        Marshal.ThrowExceptionForHR(parent.GetVolumeRange(out _VolumeMindB, out _VolumeMaxdB, out _VolumeIncrementdB));
    }

    /// <summary>Gets the minimum supported volume level, in decibels.</summary>
    public float MindB => _VolumeMindB;

    /// <summary>Gets the maximum supported volume level, in decibels.</summary>
    public float MaxdB => _VolumeMaxdB;

    /// <summary>Gets the volume increment between consecutive steps, in decibels.</summary>
    public float IncrementdB => _VolumeIncrementdB;
}