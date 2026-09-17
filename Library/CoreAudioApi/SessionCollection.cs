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
using System.Collections;
using System.Collections.Generic;
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>A collection of the <see cref="AudioSessionControl"/> sessions on an audio endpoint.</summary>
/// <remarks>
/// The underlying <c>IAudioSessionEnumerator</c> is a point-in-time snapshot with a fixed count,
/// so its count is read once when this collection is created and never changes. The sessions
/// themselves are built on first access and memoized one per index: repeated access to the same
/// index returns the same instance (important for registering and later unregistering session
/// notifications on the same object).
/// </remarks>
public sealed class SessionCollection : IDisposable, IReadOnlyList<AudioSessionControl>
{
    private readonly IAudioSessionEnumeratorCOM _enumerator;
    private readonly object _lock = new object();
    private readonly int _count;
    private AudioSessionControl?[]? _cache;
    // Deliberately not volatile: the write and every guard check happen under _lock, which already
    // supplies the ordering. Contrast AudioSessionControl, whose guard runs outside its lock.
    private bool _disposed;

    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    internal SessionCollection(IAudioSessionEnumeratorCOM audioSessionEnumerator)
    {
        this._enumerator = audioSessionEnumerator;

        // Read the count once so enumeration bounds and cache size cannot disagree if a session ends.
        InteropUtils.ThrowIfFailed(audioSessionEnumerator.GetCount(out var count));
        _count = count;
    }

    /// <summary>Gets the session at the specified zero-based index (cached per index).</summary>
    /// <param name="index">The zero-based index of the session (0 to <see cref="Count"/> - 1).</param>
    /// <returns>The <see cref="AudioSessionControl"/> at the requested position.</returns>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public AudioSessionControl this[int index]
    {
        get
        {
            lock (_lock)
            {
                ThrowIfDisposed();

                _cache ??= new AudioSessionControl[_count];

                if (index < 0 || index >= _cache.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                var result = _cache[index];
                if (result is null)
                {
                    InteropUtils.ThrowIfFailed(_enumerator.GetSession(index, out var session));
                    result = new AudioSessionControl(session);
                    _cache[index] = result;
                }

                return result;
            }
        }
    }

    /// <summary>Gets the number of sessions in the collection, fixed when it was created.</summary>
    /// <exception cref="System.ObjectDisposedException">Thrown when this collection has been disposed.</exception>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                ThrowIfDisposed();

                return _count;
            }
        }
    }

    /// <summary>Disposes every <see cref="AudioSessionControl"/> this collection created.</summary>
    public void Dispose()
    {
        AudioSessionControl?[]? toDispose;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            toDispose = _cache;
            _cache = null;
        }

        if (toDispose != null)
        {
            foreach (var session in toDispose)
            {
                session?.Dispose();
            }
        }
    }

    // Callers hold _lock; the flag is only ever written under it.
    private void ThrowIfDisposed()
    {
        InteropUtils.RequireNotDisposed(_disposed, this);
    }
    /// <summary>Enumerates the controls in this snapshot.</summary>
    /// <returns>An enumerator over the snapshot.</returns>
    public IEnumerator<AudioSessionControl> GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}