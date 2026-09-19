using System;
using System.Runtime.InteropServices;
using AudioDeviceLib.CoreAudioApi.Interfaces;
using JetBrains.Annotations;
using static AudioDeviceLib.CoreAudioApi.InteropUtils;

namespace AudioDeviceLib.CoreAudioApi.Extensions;

// Wrappers over the raw IAudioSessionControl2 entry points: they keep the raw HRESULTs out of callers,
// convert the interop-shaped parameters to natural .NET types, and own the lifetime of the
// COM-allocated strings the getters hand back.
//
// An omitted event context passes a null pointer rather than Guid.Empty. Core Audio delivers that
// null through to session subscribers as a null LPCGUID, so the two are distinguishable here and
// collapsing them would lose information - unlike IAudioEndpointVolume, where they are equivalent.
[PublicAPI]
internal static class AudioSessionControl2Extensions
{
    public static int GetDisplayName(this IAudioSessionControl2 audioSessionControl, out string? value)
    {
        return TryGetString(audioSessionControl.GetDisplayName, out value);
    }

    public static int GetIconPath(this IAudioSessionControl2 audioSessionControl, out string? value)
    {
        return TryGetString(audioSessionControl.GetIconPath, out value);
    }

    public static int GetSessionIdentifier(this IAudioSessionControl2 audioSessionControl, out string? value)
    {
        return TryGetString(audioSessionControl.GetSessionIdentifier, out value);
    }

    public static int GetSessionInstanceIdentifier(this IAudioSessionControl2 audioSessionControl, out string? value)
    {
        return TryGetString(audioSessionControl.GetSessionInstanceIdentifier, out value);
    }

    public static int SetDisplayName(this IAudioSessionControl2 audioSessionControl, string value,
        Guid? eventContext = null)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        Guid ctx = eventContext ?? Guid.Empty;
        return audioSessionControl.SetDisplayName(value, ref ctx);
    }

    public static int SetIconPath(this IAudioSessionControl2 audioSessionControl, string value,
        Guid? eventContext = null)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        Guid ctx = eventContext ?? Guid.Empty;
        return audioSessionControl.SetIconPath(value, ref ctx);
    }

    public static int SetGroupingParam(this IAudioSessionControl2 audioSessionControl, Guid @override,
        Guid? eventContext = null)
    {
        Guid ctx = eventContext ?? Guid.Empty;
        return audioSessionControl.SetGroupingParam(ref @override, ref ctx);
    }

    // CALL THIS BY CLASS NAME, not with extension syntax: the interface declares a method of the
    // same name taking no parameters, and an instance method always wins overload resolution, so
    // `control.IsSystemSoundsSession()` silently binds to the raw HRESULT version instead.
    // public static bool IsSystemSoundsSession(this IAudioSessionControl2 audioSessionControl)
    // {
    //     // only hr == S_OK is a positive match; hr == S_FALSE is a negative match; any other HRESULT is an error
    //     return audioSessionControl.IsSystemSoundsSession() == 0;
    // }

    public static int SetDuckingPreference(this IAudioSessionControl2 audioSessionControl, bool value)
    {
        return audioSessionControl.SetDuckingPreference(value ? 1 : 0);
    }

    // Signature shared by the getters that return a COM-allocated string pointer.
    private delegate int GetStringPtr(out IntPtr ptr);

    // Core Audio allocates the buffer with CoTaskMemAlloc and hands ownership to us, so it is freed
    // on every path. The string is only produced when the call succeeded; the pointer is left
    // untouched otherwise.
    private static int TryGetString(GetStringPtr getter, out string? value)
    {
        int hr = getter(out IntPtr ptr);

        try
        {
            value = HrSuccess(hr) ? Marshal.PtrToStringUni(ptr) : null;
            return hr;
        }
        finally
        {
            if (ptr != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(ptr);
            }
        }
    }
}