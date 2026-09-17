/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  IMMNotificationClient.cs
  Interop declaration of the Core Audio IMMNotificationClient sink interface.

  Shape from the Windows SDK (mmdeviceapi.h); IID 7991EEC9-7E89-4D85-8390-6C703CEC60C0.
  The methods appear in vtable order and must not be reordered.
*/

using System.Runtime.InteropServices;

namespace AudioDeviceLib.CoreAudioApi.Interfaces;

[Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMNotificationClientCOM
{
    [PreserveSig]
    int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, DeviceState newState);

    [PreserveSig]
    int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

    [PreserveSig]
    int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string deviceId);

    [PreserveSig]
    int OnDefaultDeviceChanged(DataFlow flow, Role role, [MarshalAs(UnmanagedType.LPWStr)] string defaultDeviceId);

    [PreserveSig]
    int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, PropertyKey key);
}
