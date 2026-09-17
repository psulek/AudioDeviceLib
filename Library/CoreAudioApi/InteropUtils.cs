/* Copyright (c) 2026 Peter Šulek. MIT License. */

using System;
using System.Runtime.InteropServices;

namespace AudioDeviceLib.CoreAudioApi;

internal static class InteropUtils
{
    [DllImport("ole32.dll")]
    internal static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, int context, ref Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out object? instance);

    internal static void RequireNotNull(object? value, string parameterName)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value, parameterName);
#else
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }
#endif
    }

    internal static void RequireNotDisposed(bool disposed, object owner)
    {
#if NET8_0_OR_GREATER
        ObjectDisposedException.ThrowIf(disposed, owner);
#else
        if (disposed)
        {
            throw new ObjectDisposedException(owner.GetType().Name);
        }
#endif
    }

    internal static int ReportFailure(Exception exception)
    {
        // Trace listeners are consumer code too. Preserve the original HRESULT even if one throws.
        try
        {
            System.Diagnostics.Trace.TraceError("AudioDeviceLib: {0}", exception);
        }
        catch (Exception)
        {
            return exception.HResult;
        }
        return exception.HResult;
    }

    // Ignore stale thread-local IErrorInfo left by a previous managed callback.
    internal static void ThrowIfFailed(int hr) => Marshal.ThrowExceptionForHR(hr, new IntPtr(-1));

    // ReSharper disable InconsistentNaming
    public const int S_OK = 0;

    public const int E_NOINTERFACE = unchecked((int)0x80004002);

    public const int REGDB_E_CLASSNOTREG = unchecked((int)0x80040154);

    public const int CO_E_NOTINITIALIZED = unchecked((int)0x800401F0);

    // CLSCTX_INPROC_SERVER | CLSCTX_INPROC_HANDLER | CLSCTX_LOCAL_SERVER | CLSCTX_REMOTE_SERVER.
    public const int CLSCTX_ALL = 0x17;
    // ReSharper restore InconsistentNaming
    
    public static bool HrFailed(int hr) => hr < S_OK;
    
    public static bool HrSuccess(int hr) => hr >= S_OK;
}