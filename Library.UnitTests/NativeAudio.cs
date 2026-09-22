/* Copyright (c) 2026 Peter Šulek. MIT License. */
using System;
using System.Runtime.InteropServices;
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.UnitTests;

// Independently acquire native drivers for integration tests. No library wrapper is unwrapped.
internal static class NativeAudio
{
    internal static T Activate<T>(string endpointId) where T : class
    {
        Guid clsid = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
        Guid iid = typeof(IMMDeviceEnumeratorCOM).GUID;
        Check(CoCreateInstance(ref clsid, IntPtr.Zero, 23, ref iid, out object instance));
        try
        {
            var enumerator = (IMMDeviceEnumeratorCOM)instance;
            Check(enumerator.GetDevice(endpointId, out IMMDeviceCOM device));
            try
            {
                Guid service = typeof(T).GUID;
                Check(device.Activate(ref service, CLSCTX.ALL, IntPtr.Zero, out object result));
                if (result is T typed)
                {
                    return typed;
                }
                Release(result);
                throw new InvalidCastException($"Endpoint did not provide {typeof(T).Name}.");
            }
            finally
            {
                Release(device);
            }
        }
        finally
        {
            Release(instance);
        }
    }

    internal static void Check(int hr) => Marshal.ThrowExceptionForHR(hr, new IntPtr(-1));

    // Release only the reference acquired by this helper, never FinalRelease a potentially shared RCW.
    internal static void Release(object? value)
    {
        if (value != null && Marshal.IsComObject(value))
        {
            Marshal.ReleaseComObject(value);
        }
    }

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, int context, ref Guid iid,
        [MarshalAs(UnmanagedType.IUnknown)] out object instance);
}

// Test-only WASAPI stream ownership; declaration order follows audioclient.h.
// https://learn.microsoft.com/windows/win32/api/audioclient/nn-audioclient-iaudioclient
[ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITestAudioClient
{
    [PreserveSig] int Initialize(int shareMode, uint flags, long bufferDuration, long periodicity,
        IntPtr format, ref Guid sessionGuid);
    [PreserveSig] int GetBufferSize(out uint frames);
    [PreserveSig] int GetStreamLatency(out long latency);
    [PreserveSig] int GetCurrentPadding(out uint padding);
    [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closestMatch);
    [PreserveSig] int GetMixFormat(out IntPtr format);
    [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
    [PreserveSig] int Start();
    [PreserveSig] int Stop();
    [PreserveSig] int Reset();
    [PreserveSig] int SetEventHandle(IntPtr handle);
    [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
}
