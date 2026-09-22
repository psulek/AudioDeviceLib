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
using System.Runtime.InteropServices;
using System.Threading;
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>A read-only collection of <see cref="AudioDevice"/> audio endpoints returned by an enumeration.</summary>
internal sealed class MMDeviceCollection : IDisposable
{
    private IMMDeviceCollectionCOM? _mmDeviceCollection;
    private int _count = -1;
    // 0 = live, 1 = disposed. CompareExchange ensures only one caller releases the COM wrapper;
    // Volatile.Read makes disposal visible to concurrent guards.
    private int _disposed;

    /// <summary>Gets the number of endpoints in the collection.</summary>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    /// <exception cref="COMException">Thrown when the underlying Core Audio call fails.</exception>
    public int Count
    {
        get
        {
            ThrowIfDisposed();

            // Cached: an IMMDeviceCollection is a snapshot of one enumeration, so the count is
            // fixed for the lifetime of this object. Re-reading it made every `i < Count` loop
            // condition a COM round trip.
            if (_count < 0)
            {
                // Non-null until Dispose clears it, which ThrowIfDisposed above has ruled out.
                InteropUtils.ThrowIfFailed(_mmDeviceCollection!.GetCount(out var result));
                _count = (int)result;
            }

            return _count;
        }
    }

    /// <summary>Gets the endpoint at the specified zero-based position.</summary>
    /// <param name="index">The zero-based index of the endpoint to retrieve (0 to <see cref="Count"/> - 1).</param>
    /// <returns>A new <see cref="AudioDevice"/> for the endpoint at the requested position.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside this snapshot.</exception>
    /// <exception cref="COMException">Thrown when the underlying Core Audio call fails.</exception>
    /// <remarks>
    /// Each access returns a new instance wrapping its own COM endpoint object; the caller owns it
    /// and is responsible for disposing it. The collection intentionally keeps no reference, so
    /// disposing the collection never invalidates a device it handed out.
    /// </remarks>
    public AudioDevice this[int index]
    {
        get
        {
            ThrowIfDisposed();
            // Non-null until Dispose clears it, which ThrowIfDisposed above has ruled out.
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            InteropUtils.ThrowIfFailed(_mmDeviceCollection!.Item((uint)index, out IMMDeviceCOM device));
            return new AudioDevice(device, false, false);
        }
    }

    internal MMDeviceCollection(IMMDeviceCollectionCOM parent)
    {
        _mmDeviceCollection = parent;
    }

    private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>Releases the underlying Core Audio collection.</summary>
    /// <remarks>Devices already obtained from the indexer are unaffected and remain usable.</remarks>
    public void Dispose()
    {
        // Atomic transition: the thread that flips 0 -> 1 owns the one-time release; any concurrent
        // or repeat caller sees a non-zero prior value and returns without touching the wrapper.
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        // Safe to release deterministically: this RCW never leaves the library, so nothing else can
        // be holding it.
        try
        {
            if (_mmDeviceCollection != null && Marshal.IsComObject(_mmDeviceCollection))
            {
                Marshal.ReleaseComObject(_mmDeviceCollection);
            }
        }
        catch (Exception cleanupException)
        {
            InteropUtils.ReportFailure(cleanupException);
            // best-effort cleanup
        }

        _mmDeviceCollection = null;
    }

    private void ThrowIfDisposed()
    {
        InteropUtils.RequireNotDisposed(IsDisposed, this);
    }
}