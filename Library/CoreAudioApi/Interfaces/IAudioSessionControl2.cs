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
using JetBrains.Annotations;

namespace AudioDeviceLib.CoreAudioApi.Interfaces;

[Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[PublicAPI]
internal interface IAudioSessionControl2
{
    [PreserveSig]
    int GetState(out AudioSessionState state);

    [PreserveSig]
    int GetDisplayName(out IntPtr name);

    // DECLARATION ORDER IS THE VTABLE ORDER. audiopolicy.h lays IAudioSessionControl out as
    // GetState, GetDisplayName, SetDisplayName, GetIconPath, SetIconPath, GetGroupingParam,
    // SetGroupingParam, Register..., Unregister..., and IAudioSessionControl2 appends its five.
    // Grouping the getters together here would bind GetIconPath to SetDisplayName's slot and vice
    // versa, so keep each getter/setter pair interleaved exactly as the header has them.
    //
    // `value` is a native LPCWSTR the callee only reads, so an explicitly marshalled string is
    // both correct and simpler than pinning a char* at every call site. The attribute is not
    // optional: an un-attributed `string` in COM interop marshals as BSTR.
    //
    // `pguidEventContext` is an LPCGUID the docs describe as nullable, but nothing here needs to
    // send a null: SessionEventContextTests observed a null context arriving at subscribers as
    // GUID_NULL, indistinguishable from an explicitly supplied Guid.Empty. Sending GUID_NULL
    // instead costs nothing observable and keeps this interface free of pointers.
    //
    // The receiving side is NOT symmetric. IAudioSessionEventsCOM keeps `Guid*` because the value
    // arriving there comes from whichever process made the change, and the docs say it may be
    // null - see the comment on that interface.
    [PreserveSig]
    int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid pguidEventContext);

    [PreserveSig]
    int GetIconPath(out IntPtr path);

    [PreserveSig]
    int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid pguidEventContext);
    
    [PreserveSig]
    int GetGroupingParam(out Guid groupingParam);

    // `@override` is `ref Guid` on documented grounds - the native contract says it "must be a
    // valid, non-NULL pointer to a grouping-parameter GUID". `pguidEventContext` is `ref Guid` for
    // the reason given above.
    [PreserveSig]
    int SetGroupingParam(ref Guid @override, ref Guid pguidEventContext);

    [PreserveSig]
    int RegisterAudioSessionNotification([MarshalAs(UnmanagedType.Interface)] IAudioSessionEventsCOM newNotifications);

    [PreserveSig]
    int UnregisterAudioSessionNotification([MarshalAs(UnmanagedType.Interface)] IAudioSessionEventsCOM newNotifications);

    [PreserveSig]
    int GetSessionIdentifier(out IntPtr retVal);

    [PreserveSig]
    int GetSessionInstanceIdentifier(out IntPtr retVal);

    [PreserveSig]
    int GetProcessId(out UInt32 retvVal);

    [PreserveSig]
    int IsSystemSoundsSession();

    [PreserveSig]
    int SetDuckingPreference(int optOut);
}