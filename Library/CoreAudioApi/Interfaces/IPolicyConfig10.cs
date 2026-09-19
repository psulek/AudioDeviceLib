/*
  MODIFICATIONS
  -------------
  This file is an ALTERED version of the corresponding source in AudioDeviceCmdlets
  (https://github.com/frgnca/AudioDeviceCmdlets, MIT) and must not be misrepresented as being
  the original source code. Altered by Peter Šulek for AudioDeviceLib
  (https://github.com/psulek/AudioDeviceLib).

  The changes are summarised in MODIFICATIONS.md at the repository root; the Git history of
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
[Guid("00000000-0000-0000-C000-000000000046"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig10
{
    [PreserveSig]
    int GetMixFormat(string pszDeviceName, IntPtr ppFormat);

    [PreserveSig]
    int GetDeviceFormat(string pszDeviceName, bool bDefault, IntPtr ppFormat);

    [PreserveSig]
    int ResetDeviceFormat(string pszDeviceName);

    [PreserveSig]
    int SetDeviceFormat(string pszDeviceName, IntPtr pEndpointFormat, IntPtr MixFormat);

    [PreserveSig]
    int GetProcessingPeriod(string pszDeviceName, bool bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);

    [PreserveSig]
    int SetProcessingPeriod(string pszDeviceName, IntPtr pmftPeriod);

    [PreserveSig]
    int GetShareMode(string pszDeviceName, IntPtr pMode);

    [PreserveSig]
    int SetShareMode(string pszDeviceName, IntPtr mode);

    [PreserveSig]
    int GetPropertyValue(string pszDeviceName, bool bFxStore, IntPtr key, IntPtr pv);

    [PreserveSig]
    int SetPropertyValue(string pszDeviceName, bool bFxStore, IntPtr key, IntPtr pv);

    [PreserveSig]
    int SetDefaultEndpoint(string pszDeviceName, Role role);

    [PreserveSig]
    int SetEndpointVisibility(string pszDeviceName, bool bVisible);
}