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
using System.Runtime.InteropServices;

namespace AudioDeviceLib.CoreAudioApi.Interfaces;

[Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface ISimpleAudioVolumeCOM
{
    // `EventContext` stays a raw pointer because NULL is meaningful on this interface: "If the
    // caller supplies a NULL pointer for this parameter, the client's notification method receives
    // a NULL context pointer" (ISimpleAudioVolume::SetMasterVolume). Contrast IAudioEndpointVolume,
    // where a NULL context is documented to reach subscribers as GUID_NULL and `ref Guid` is
    // therefore lossless. Callers go through SimpleAudioVolumeExtensions, which takes a `Guid?`.
    [PreserveSig]
    int SetMasterVolume(float level, Guid* eventContext);

    [PreserveSig]
    int GetMasterVolume(out float level);

    [PreserveSig]
    int SetMute(int mute, Guid* eventContext);

    [PreserveSig]
    int GetMute(out int mute);
}