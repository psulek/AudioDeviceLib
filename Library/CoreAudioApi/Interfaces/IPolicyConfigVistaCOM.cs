/*
  MODIFICATIONS
  -------------
  This file is an ALTERED version of the corresponding source in AudioDeviceCmdlets
  (https://github.com/frgnca/AudioDeviceCmdlets, MIT) and must not be misrepresented as being
  the original source code. Altered by Peter Šulek for AudioDeviceLib
  (https://github.com/psulek/AudioDeviceLib).

  The changes are summarized in MODIFICATIONS.md at the repository root; the Git history of
  this file is the authoritative record.
*/

using System;
using System.Runtime.InteropServices;

namespace AudioDeviceLib.CoreAudioApi.Interfaces;

// KNOWN INTEROP DEBT (deliberately not fixed):
//   * The `bool` parameters below marshal as a 2-byte VariantBool in COM interop, while the native
//     signatures take a 4-byte BOOL. Everywhere else in this layer that mismatch was resolved by
//     declaring the parameter `int`.
//   * The `string` parameters carry no [MarshalAs], so they marshal as BSTR rather than LPCWSTR.
//     It happens to work - a BSTR is a null-terminated wide string, so reading it as LPCWSTR
//     succeeds - but it allocates and copies on every call.
// Neither is fixed here because IPolicyConfig is undocumented and reverse-engineered: there is no
// contract to verify a change against, and this is the code path that switches the default device.
[Guid("568B9108-44BF-40B4-9006-86AFE5B5A620"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfigVistaCOM
{
    [PreserveSig]
    int GetMixFormat(string deviceName, IntPtr format);

    [PreserveSig]
    int GetDeviceFormat(string deviceName, bool @default, IntPtr format);

    [PreserveSig]
    int ResetDeviceFormat(string deviceName);

    [PreserveSig]
    int SetDeviceFormat(string deviceName, IntPtr endpointFormat, IntPtr mixFormat);

    [PreserveSig]
    int GetProcessingPeriod(string deviceName, bool @default, IntPtr defaultPeriod, IntPtr minimumPeriod);

    [PreserveSig]
    int SetProcessingPeriod(string deviceName, IntPtr period);

    [PreserveSig]
    int GetShareMode(string deviceName, IntPtr mode);

    [PreserveSig]
    int SetShareMode(string deviceName, IntPtr mode);

    [PreserveSig]
    int GetPropertyValue(string deviceName, bool store, IntPtr key, IntPtr pv);

    [PreserveSig]
    int SetPropertyValue(string deviceName, bool store, IntPtr key, IntPtr pv);

    [PreserveSig]
    int SetDefaultEndpoint(string deviceName, Role role);

    [PreserveSig]
    int SetEndpointVisibility(string deviceName, bool visible);
}