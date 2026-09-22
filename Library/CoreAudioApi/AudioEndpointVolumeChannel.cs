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

/*
  MODIFICATIONS
  -------------
  This file is an ALTERED version of the original source by Ray Molenkamp and must not be
  misrepresented as being the original source code. Altered by Peter Šulek for AudioDeviceLib
  (https://github.com/psulek/AudioDeviceLib), starting from the copy bundled in
  AudioDeviceCmdlets (https://github.com/frgnca/AudioDeviceCmdlets, MIT).

  The changes are summarized in MODIFICATIONS.md at the repository root; the Git history of
  this file is the authoritative record.
*/

using System;
using AudioDeviceLib.CoreAudioApi.Extensions;
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>Volume control for a single channel of an audio endpoint.</summary>
public sealed class AudioEndpointVolumeChannel : IDisposable
{
    private readonly uint _channel;
    private readonly IAudioEndpointVolumeCOM _parent;
    private volatile bool _disposed;


    internal AudioEndpointVolumeChannel(IAudioEndpointVolumeCOM parent, int channel)
    {
        _channel = (uint)channel;
        _parent = parent;
    }

    /// <summary>Gets or sets this channel's volume level in decibels, within the endpoint's volume range.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public float VolumeLevel
    {
        get
        {
            ThrowIfDisposed();
            InteropUtils.ThrowIfFailed(_parent.GetChannelVolumeLevel(_channel, out var result));
            return result;
        }
        set
        {
            ThrowIfDisposed();
            InteropUtils.ThrowIfFailed(_parent.SetChannelVolumeLevel(_channel, value));
        }
    }

    /// <summary>Gets or sets this channel's volume as a normalized scalar in the range 0.0 to 1.0.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public float VolumeLevelScalar
    {
        get
        {
            ThrowIfDisposed();
            InteropUtils.ThrowIfFailed(_parent.GetChannelVolumeLevelScalar(_channel, out var result));
            return result;
        }
        set
        {
            ThrowIfDisposed();
            InteropUtils.ThrowIfFailed(
            _parent.SetChannelVolumeLevelScalar(_channel, value));
        }
    }

    /// <summary>Invalidates this wrapper without releasing externally held COM references.</summary>
    public void Dispose() => _disposed = true;

    private void ThrowIfDisposed()
    {
        InteropUtils.RequireNotDisposed(_disposed, this);
    }
}