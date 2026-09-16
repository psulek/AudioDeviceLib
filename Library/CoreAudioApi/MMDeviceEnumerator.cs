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

using System;
using System.Runtime.InteropServices;
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.CoreAudioApi;

//Marked as internal, since on its own its no good
[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class _MMDeviceEnumerator
{
}

//Small wrapper class
/// <summary>
/// Managed wrapper over the Core Audio <c>IMMDeviceEnumerator</c>. Enumerates audio endpoints and
/// resolves default devices. Windows Vista or newer is required.
/// </summary>
internal class MMDeviceEnumerator
{
    private IMMDeviceEnumerator _realEnumerator = new _MMDeviceEnumerator() as IMMDeviceEnumerator;

    /// <summary>Enumerates the audio endpoints that match the given data flow and state mask.</summary>
    /// <param name="dataFlow">The data-flow direction to enumerate (render, capture or all).</param>
    /// <param name="dwStateMask">A bit mask of device states to include (e.g. active, disabled, unplugged).</param>
    /// <returns>An <see cref="MMDeviceCollection"/> of the matching endpoints.</returns>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    internal MMDeviceCollection EnumerateAudioEndPoints(DataFlow dataFlow, DeviceState dwStateMask)
    {
        Marshal.ThrowExceptionForHR(
            _realEnumerator.EnumAudioEndpoints(dataFlow, dwStateMask, out var result));
        return new MMDeviceCollection(result);
    }

    /// <summary>Gets the current default endpoint for the given data flow and role.</summary>
    /// <param name="dataFlow">The data-flow direction (render or capture).</param>
    /// <param name="role">The device role (console, multimedia or communications).</param>
    /// <returns>The default <see cref="MMDevice"/> for the requested flow and role.</returns>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when no default endpoint exists or the underlying Core Audio call fails.</exception>
    internal MMDevice GetDefaultAudioEndpoint(DataFlow dataFlow, Role role)
    {
        Marshal.ThrowExceptionForHR(_realEnumerator.GetDefaultAudioEndpoint(dataFlow, role, out var device));
        return new MMDevice(device);
    }

    /// <summary>
    ///  Query existence of the current default endpoint for the given data flow and role.
    /// </summary>
    /// <param name="dataFlow">The data-flow direction (render or capture).</param>
    /// <param name="role">The device role (console, multimedia or communications).</param>
    /// <returns> <c>true</c> if a default endpoint exists; otherwise, <c>false</c>.</returns>
    // internal bool QueryDefaultAudioEndpoint(DataFlow dataFlow, Role role)
    // {
    //     Marshal.ThrowExceptionForHR(_realEnumerator.GetDefaultAudioEndpoint(dataFlow, role, out var device));
    //     return device != null;
    // }

    internal string GetDefaultAudioEndpointDeviceId(DataFlow dataFlow, Role role)
    {
        var result = _realEnumerator.GetDefaultAudioEndpoint(dataFlow, role, out var realDevice);
        if (result != 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }
        
        Marshal.ThrowExceptionForHR(realDevice.GetId(out var id));
        return id;
    }

    /// <summary>Gets a specific endpoint by its Core Audio device ID.</summary>
    /// <param name="ID">The endpoint ID string returned by <see cref="MMDevice.ID"/>.</param>
    /// <returns>The <see cref="MMDevice"/> identified by <paramref name="ID"/>.</returns>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the ID is unknown or the underlying Core Audio call fails.</exception>
    internal MMDevice GetDevice(string ID)
    {
        Marshal.ThrowExceptionForHR(_realEnumerator.GetDevice(ID, out var device));
        return new MMDevice(device);
    }

    /// <summary>Registers a sink to receive endpoint (device) change notifications.</summary>
    /// <param name="client">The COM notification sink to register.</param>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    internal void RegisterEndpointNotificationCallback(IMMNotificationClient client)
    {
        Marshal.ThrowExceptionForHR(_realEnumerator.RegisterEndpointNotificationCallback(client));
    }

    /// <summary>Unregisters a previously registered endpoint notification sink.</summary>
    /// <param name="client">The same COM notification sink instance that was registered.</param>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    internal void UnregisterEndpointNotificationCallback(IMMNotificationClient client)
    {
        Marshal.ThrowExceptionForHR(_realEnumerator.UnregisterEndpointNotificationCallback(client));
    }

    /// <summary>Creates a new enumerator instance.</summary>
    /// <exception cref="NotSupportedException">Thrown on Windows versions older than Vista.</exception>
    internal MMDeviceEnumerator()
    {
        if (System.Environment.OSVersion.Version.Major < 6)
        {
            throw new NotSupportedException("This functionality is only supported on Windows Vista or newer.");
        }
    }
}