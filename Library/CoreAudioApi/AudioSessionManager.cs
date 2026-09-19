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

  The changes are summarised in MODIFICATIONS.md at the repository root; the Git history of
  this file is the authoritative record.
*/

using System;
using System.Runtime.InteropServices;
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// Managed wrapper over the Core Audio <c>IAudioSessionManager2</c> interface. Provides access to
/// the collection of audio sessions on an endpoint.
/// </summary>
public class AudioSessionManager : IDisposable
{
    private IAudioSessionManager2 _AudioSessionManager;
    private SessionCollection _Sessions;
    private bool _disposed;

    internal AudioSessionManager(IAudioSessionManager2 realAudioSessionManager)
    {
        _AudioSessionManager = realAudioSessionManager;
        Marshal.ThrowExceptionForHR(_AudioSessionManager.GetSessionEnumerator(out IAudioSessionEnumerator _SessionEnum));
        _Sessions = new SessionCollection(_SessionEnum);
    }

    /// <summary>Gets the collection of audio sessions currently associated with the endpoint.</summary>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    public SessionCollection Sessions
    {
        get
        {
            ThrowIfDisposed();
            return _Sessions;
        }
    }

    /// <summary>Disposes the owned <see cref="SessionCollection"/> (and its cached sessions).</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _Sessions?.Dispose();
        _Sessions = null;
        _AudioSessionManager = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AudioSessionManager));
        }
    }
}