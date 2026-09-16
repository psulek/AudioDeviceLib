/*
  MODIFICATIONS
  -------------
  This file is an ALTERED version of the corresponding source in AudioDeviceCmdlets
  (https://github.com/frgnca/AudioDeviceCmdlets, MIT) and must not be misrepresented as being
  the original source code. Altered by Peter Šulek for AudioDeviceLib
  (https://github.com/psulek/AudioDeviceLib).

  Changes from the original:
  - Namespace changed to `AudioDeviceLib.CoreAudioApi.Interfaces` (file-scoped); unused `using`
    directives removed.
  - Reformatted to the project's C# style (full braces, modern C# syntax).
  - `ERole` renamed to `Role`.
*/

using System;
using System.Runtime.InteropServices;

namespace AudioDeviceLib.CoreAudioApi.Interfaces;

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