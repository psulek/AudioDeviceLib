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

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// Well-known Core Audio property keys, as defined by <c>DEFINE_PROPERTYKEY</c> in the Windows SDK
/// headers (<c>mmdeviceapi.h</c> and <c>functiondiscoverykeys_devpkey.h</c>).
/// </summary>
/// <remarks>
/// Each entry is a full <see cref="PropertyKey"/> - a property-set GUID plus the PID that selects
/// one property within that set. The PID is what distinguishes the members of a set: every
/// <c>PKEY_AudioEndpoint_*</c> key below shares one fmtid and differs only in its PID, so a lookup
/// that matched on the GUID alone could not tell them apart.
/// </remarks>
public static class PKEY
{
    // The property sets these keys are drawn from.
    private static readonly Guid AudioEndpointSet =
        new Guid(0x1da5d803, 0xd492, 0x4edd, 0x8c, 0x23, 0xe0, 0xc0, 0xff, 0xee, 0x7f, 0x0e);

    private static readonly Guid AudioEngineDeviceFormatSet =
        new Guid(0xf19f064d, 0x082c, 0x4e27, 0xbc, 0x73, 0x68, 0x82, 0xa1, 0xbb, 0x8e, 0x4c);

    // PKEY_AudioEngine_OEMFormat lives in its own set, not alongside PKEY_AudioEngine_DeviceFormat.
    private static readonly Guid AudioEngineOemFormatSet =
        new Guid(0xe4870e26, 0x3cc5, 0x4cd2, 0xba, 0x46, 0xca, 0x0a, 0x9a, 0x70, 0xed, 0x04);

    private static readonly Guid DeviceSet =
        new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0);

    private static readonly Guid DeviceInterfaceSet =
        new Guid(0x026e516e, 0xb814, 0x414b, 0x83, 0xcd, 0x85, 0x6d, 0x6f, 0xef, 0x48, 0x22);

    private static PropertyKey Key(Guid fmtid, int pid) => new PropertyKey { fmtid = fmtid, pid = pid };

    /// <summary>The form factor of the endpoint, such as speakers, headphones or a digital jack.</summary>
    public static readonly PropertyKey PKEY_AudioEndpoint_FormFactor = Key(AudioEndpointSet, 0);

    /// <summary>The CLSID of the provider supplying the endpoint's control-panel page.</summary>
    public static readonly PropertyKey PKEY_AudioEndpoint_ControlPanelPageProvider = Key(AudioEndpointSet, 1);

    /// <summary>The endpoint this one is grouped with, for devices that pair render and capture.</summary>
    public static readonly PropertyKey PKEY_AudioEndpoint_Association = Key(AudioEndpointSet, 2);

    /// <summary>The channel mask describing which physical speakers the endpoint drives.</summary>
    public static readonly PropertyKey PKEY_AudioEndpoint_PhysicalSpeakers = Key(AudioEndpointSet, 3);

    /// <summary>The DirectSound device identifier corresponding to this endpoint.</summary>
    public static readonly PropertyKey PKEY_AudioEndpoint_GUID = Key(AudioEndpointSet, 4);

    /// <summary>Whether system effects are disabled for this endpoint.</summary>
    public static readonly PropertyKey PKEY_AudioEndpoint_Disable_SysFx = Key(AudioEndpointSet, 5);

    /// <summary>The channel mask describing which speakers are full-range.</summary>
    public static readonly PropertyKey PKEY_AudioEndpoint_FullRangeSpeakers = Key(AudioEndpointSet, 6);

    /// <summary>Whether the endpoint supports event-driven buffering.</summary>
    public static readonly PropertyKey PKEY_AudioEndpoint_Supports_EventDriven_Mode = Key(AudioEndpointSet, 7);

    /// <summary>The subtype of the jack the endpoint is connected through.</summary>
    public static readonly PropertyKey PKEY_AudioEndpoint_JackSubType = Key(AudioEndpointSet, 8);

    /// <summary>The device format the audio engine uses for shared-mode streams on this endpoint.</summary>
    public static readonly PropertyKey PKEY_AudioEngine_DeviceFormat = Key(AudioEngineDeviceFormatSet, 0);

    /// <summary>The format the OEM supplied for this endpoint.</summary>
    public static readonly PropertyKey PKEY_AudioEngine_OEMFormat = Key(AudioEngineOemFormatSet, 3);

    /// <summary>The endpoint's friendly display name, for example "Speakers (High Definition Audio)".</summary>
    public static readonly PropertyKey PKEY_Device_FriendlyName = Key(DeviceSet, 14);

    /// <summary>The endpoint's device description, for example "Speakers".</summary>
    public static readonly PropertyKey PKEY_Device_DeviceDesc = Key(DeviceSet, 2);

    /// <summary>
    /// The friendly name of the device <em>interface</em> - the adapter, for example
    /// "High Definition Audio Device". This is not the per-endpoint name; for that use
    /// <see cref="PKEY_Device_FriendlyName"/>.
    /// </summary>
    public static readonly PropertyKey PKEY_DeviceInterface_FriendlyName = Key(DeviceInterfaceSet, 2);
}
