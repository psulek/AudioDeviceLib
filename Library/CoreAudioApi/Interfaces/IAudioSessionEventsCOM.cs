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
  Derived from `SOURCE/IAudioSessionEvents.cs` upstream.

  Changes from the original:
  - Namespace changed to `AudioDeviceLib.CoreAudioApi.Interfaces` (file-scoped); unused `using`
    directives removed.
  - Reformatted to the project's C# style (full braces, modern C# syntax).
  - Renamed from `IAudioSessionEvents.cs` / `IAudioSessionEvents` to `IAudioSessionEventsCOM` and
    changed from `public` to `internal`; it is now the raw COM sink behind the library's own
    pure-C# `IAudioSessionEvents`.
*/

using System;
using System.Runtime.InteropServices;

namespace AudioDeviceLib.CoreAudioApi.Interfaces;

// Raw COM sink contract for IAudioSessionEvents (mmdeviceapi / audiopolicy).
// This is the interop-shaped interface (PreserveSig HRESULTs, MarshalAs, raw pointers) and is
// kept internal. Library consumers implement the pure-C# AudioDeviceLib.CoreAudioApi.IAudioSessionEvents
// instead; AudioSessionEventsComAdapter bridges the two.
[Guid("24918ACC-64B3-37C1-8CA9-74A66E9957A8"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionEventsCOM
{
    [PreserveSig]
    int OnDisplayNameChanged([MarshalAs(UnmanagedType.LPWStr)] string NewDisplayName, Guid EventContext);

    [PreserveSig]
    int OnIconPathChanged([MarshalAs(UnmanagedType.LPWStr)] string NewIconPath, Guid EventContext);

    [PreserveSig]
    int OnSimpleVolumeChanged(float NewVolume, bool newMute, Guid EventContext);

    [PreserveSig]
    int OnChannelVolumeChanged(UInt32 ChannelCount, IntPtr NewChannelVolumeArray, UInt32 ChangedChannel,
        Guid EventContext);

    [PreserveSig]
    int OnGroupingParamChanged(Guid NewGroupingParam, Guid EventContext);

    [PreserveSig]
    int OnStateChanged(AudioSessionState NewState);

    [PreserveSig]
    int OnSessionDisconnected(AudioSessionDisconnectReason DisconnectReason);
}