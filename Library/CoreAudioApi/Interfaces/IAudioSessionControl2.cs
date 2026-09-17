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
  - Namespace changed to `AudioDeviceLib.CoreAudioApi.Interfaces` (file-scoped); unused `using`
    directives removed.
  - Reformatted to the project's C# style (full braces, modern C# syntax).
  - The `RegisterAudioSessionNotification` / `UnregisterAudioSessionNotification` parameter type
    was renamed from `IAudioSessionEvents` to `IAudioSessionEventsCOM`; the
    `IAudioSessionEvents` name now belongs to the library's own pure-C# event interface.
*/

using System;
using System.Runtime.InteropServices;

namespace AudioDeviceLib.CoreAudioApi.Interfaces;

[Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IAudioSessionControl2
{
    //IAudioSession functions
    [PreserveSig]
    int GetState(out AudioSessionState state);

    [PreserveSig]
    int GetDisplayName(out IntPtr name);

    [PreserveSig]
    int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)]string value, Guid* EventContext);
    // int SetDisplayName(string value, ref Guid EventContext);

    [PreserveSig]
    int GetIconPath(out IntPtr Path);

    [PreserveSig]
    int SetIconPath(string Value, ref Guid EventContext);

    [PreserveSig]
    int GetGroupingParam(out Guid GroupingParam);

    [PreserveSig]
    int SetGroupingParam(ref Guid Override, ref Guid Eventcontext);

    [PreserveSig]
    int RegisterAudioSessionNotification(IAudioSessionEventsCOM NewNotifications);

    [PreserveSig]
    int UnregisterAudioSessionNotification(IAudioSessionEventsCOM NewNotifications);

    //IAudioSession2 functions
    [PreserveSig]
    int GetSessionIdentifier(out IntPtr retVal);

    [PreserveSig]
    int GetSessionInstanceIdentifier(out IntPtr retVal);

    [PreserveSig]
    int GetProcessId(out UInt32 retvVal);

    [PreserveSig]
    int IsSystemSoundsSession();

    [PreserveSig]
    int SetDuckingPreference(bool optOut);
}