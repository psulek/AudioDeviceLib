/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  PlatformSupport.cs
  The one runtime check that this library can actually run here.
*/

using System;
using System.Runtime.InteropServices;

namespace AudioDeviceLib.Lib;

internal static class PlatformSupport
{
    private const string NotWindows =
        "AudioDeviceLib requires Windows: it is a wrapper over the Windows Core Audio (MMDevice) COM API, " +
        "which has no equivalent on this platform.";

    /// <summary>
    /// Throws when the current OS cannot host Core Audio.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>net8.0-windows</c> asset carries <c>[SupportedOSPlatform("Windows7.0")]</c>, which the SDK
    /// derives from the target framework, so consumers of that asset get CA1416 at compile time. The
    /// <c>netstandard2.0</c> asset carries no platform annotation and can be resolved by a non-Windows
    /// runtime, which is the case this guard exists for. The <c>net48</c> asset is Windows-only by
    /// construction.
    /// </para>
    /// <para>
    /// This deliberately does not check the Windows version. Core Audio needs Vista, but the oldest
    /// runtime that can load any of these assets - .NET Framework 4.8 - already requires Windows 7, so
    /// a version test can never fail on a process that got far enough to call it. It would also be
    /// unreliable: on .NET Framework, <see cref="Environment.OSVersion"/> is subject to Windows version
    /// shimming and reports 6.2 on Windows 10 unless the host application ships a compatibility manifest.
    /// </para>
    /// </remarks>
    /// <exception cref="PlatformNotSupportedException">Thrown when not running on Windows.</exception>
    internal static void ThrowIfUnsupported()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException(NotWindows);
        }
    }
}
