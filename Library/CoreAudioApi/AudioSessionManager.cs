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
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// Managed wrapper over the Core Audio <c>IAudioSessionManager2</c> interface. Provides access to
/// the collection of audio sessions on an endpoint.
/// </summary>
public sealed class AudioSessionManager : IDisposable
{
    private readonly IAudioSessionManager2COM _audioSessionManager;
    private readonly object _lock = new object();
    private SessionCollection? _sessions;
    // Deliberately not volatile: the write and every guard check happen under _lock, which already
    // supplies the ordering. Contrast AudioSessionControl, whose guard runs outside its lock.
    private bool _disposed;

    internal AudioSessionManager(IAudioSessionManager2COM audioSessionManager)
    {
        _audioSessionManager = audioSessionManager ?? throw new ArgumentNullException(nameof(audioSessionManager));
    }

    /// <summary>Gets the session snapshot, created on first access.</summary>
    /// <remarks>The snapshot stays fixed until Refresh is called. The manager owns it.</remarks>
    public SessionCollection Sessions
    {
        get
        {
            lock (_lock)
            {
                ThrowIfDisposed();
                return _sessions ??= CreateSnapshot();
            }
        }
    }

    /// <summary>Replaces the snapshot with the endpoint's current sessions.</summary>
    /// <remarks>Disposes the previous collection and its controls, including their notification tokens.</remarks>
    public void Refresh()
    {
        SessionCollection? previous;
        lock (_lock)
        {
            ThrowIfDisposed();
            var replacement = CreateSnapshot();
            previous = _sessions;
            _sessions = replacement;
        }
        previous?.Dispose();
    }

    private SessionCollection CreateSnapshot()
    {
        InteropUtils.ThrowIfFailed(_audioSessionManager.GetSessionEnumerator(out var enumerator));
        return new SessionCollection(enumerator);
    }

    /// <summary>Disposes the owned session collection and its controls.</summary>
    public void Dispose()
    {
        SessionCollection? previous;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            previous = _sessions;
            _sessions = null;
        }
        previous?.Dispose();
    }

    private void ThrowIfDisposed()
    {
        InteropUtils.RequireNotDisposed(_disposed, this);
    }
}
