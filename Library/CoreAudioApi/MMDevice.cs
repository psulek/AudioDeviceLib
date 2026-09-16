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

/// <summary>
/// Managed wrapper over a Core Audio <c>IMMDevice</c>. Exposes device metadata (name, ID, state,
/// data flow) and lazily-activated sub-interfaces for volume, metering and session management.
/// </summary>
public class MMDevice : IDisposable
{
    #region Variables

    private readonly IMMDevice _realDevice;
    private PropertyStore _propertyStore;
    private AudioMeterInformation _audioMeterInformation;
    private AudioEndpointVolume _audioEndpointVolume;
    private AudioSessionManager _audioSessionManager;

    #endregion

    #region Guids

    private static Guid IID_IAudioMeterInformation = typeof(IAudioMeterInformation).GUID;
    private static Guid IID_IAudioEndpointVolume = typeof(IAudioEndpointVolume).GUID;
    private static Guid IID_IAudioSessionManager = typeof(IAudioSessionManager2).GUID;

    #endregion

    #region Init

    private void GetPropertyInformation()
    {
        Marshal.ThrowExceptionForHR(_realDevice.OpenPropertyStore(StgmAccess.Read, out var propstore));
        _propertyStore = new PropertyStore(propstore);
    }

    private void GetAudioSessionManager()
    {
        Marshal.ThrowExceptionForHR(_realDevice.Activate(ref IID_IAudioSessionManager, CLSCTX.ALL, IntPtr.Zero,
            out var result));
        _audioSessionManager = new AudioSessionManager(result as IAudioSessionManager2);
    }

    private void GetAudioMeterInformation()
    {
        Marshal.ThrowExceptionForHR(_realDevice.Activate(ref IID_IAudioMeterInformation, CLSCTX.ALL, IntPtr.Zero,
            out var result));
        _audioMeterInformation = new AudioMeterInformation(result as IAudioMeterInformation);
    }

    private void GetAudioEndpointVolume()
    {
        Marshal.ThrowExceptionForHR(_realDevice.Activate(ref IID_IAudioEndpointVolume, CLSCTX.ALL, IntPtr.Zero,
            out var result));
        _audioEndpointVolume = new AudioEndpointVolume(result as IAudioEndpointVolume);
    }

    #endregion

    #region Properties

    /// <summary>Gets the audio session manager for this endpoint (activated on first access).</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the interface cannot be activated.</exception>
    public AudioSessionManager AudioSessionManager
    {
        get
        {
            if (_audioSessionManager == null)
            {
                GetAudioSessionManager();
            }

            return _audioSessionManager;
        }
    }

    /// <summary>Gets the peak-meter information for this endpoint (activated on first access).</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the interface cannot be activated.</exception>
    public AudioMeterInformation AudioMeterInformation
    {
        get
        {
            if (_audioMeterInformation == null)
            {
                GetAudioMeterInformation();
            }

            return _audioMeterInformation;
        }
    }

    /// <summary>Gets the volume/mute control for this endpoint (activated on first access).</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the interface cannot be activated.</exception>
    public AudioEndpointVolume AudioEndpointVolume
    {
        get
        {
            if (_audioEndpointVolume == null)
            {
                GetAudioEndpointVolume();
            }

            return _audioEndpointVolume;
        }
    }

    /// <summary>Gets the property store for this endpoint (opened on first access).</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the property store cannot be opened.</exception>
    public PropertyStore Properties
    {
        get
        {
            if (_propertyStore == null)
            {
                GetPropertyInformation();
            }

            return _propertyStore;
        }
    }

    /// <summary>Gets the friendly display name of the endpoint, or "Unknown" when unavailable.</summary>
    public string FriendlyName
    {
        get
        {
            if (_propertyStore == null)
            {
                GetPropertyInformation();
            }

            if (_propertyStore.Contains(PKEY.PKEY_DeviceInterface_FriendlyName))
            {
                return (string)_propertyStore[PKEY.PKEY_DeviceInterface_FriendlyName].Value;
            }

            return "Unknown";
        }
    }


    /// <summary>Gets the Core Audio endpoint ID string that uniquely identifies this device.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public string ID
    {
        get
        {
            Marshal.ThrowExceptionForHR(_realDevice.GetId(out var result));
            return result;
        }
    }

    /// <summary>Gets the data-flow direction of the endpoint (render or capture).</summary>
    public DataFlow DataFlow
    {
        get
        {
            var ep = _realDevice as IMMEndpoint;
            ep.GetDataFlow(out var result);
            return result;
        }
    }

    /// <summary>Gets the current state of the endpoint (active, disabled, not present or unplugged).</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public DeviceState State
    {
        get
        {
            Marshal.ThrowExceptionForHR(_realDevice.GetState(out var result));
            return result;
        }
    }

    #endregion

    #region Constructor

    internal MMDevice(IMMDevice realDevice)
    {
        _realDevice = realDevice;
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the lazily-activated sub-interfaces that hold registered COM callbacks or cached
    /// sessions (the endpoint volume and the session manager), if they were created.
    /// </summary>
    public void Dispose()
    {
        _audioEndpointVolume?.Dispose();
        _audioEndpointVolume = null;

        _audioSessionManager?.Dispose();
        _audioSessionManager = null;
    }

    #endregion
}