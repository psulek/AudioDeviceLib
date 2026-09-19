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
using AudioDeviceLib.Lib;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>A read-only collection of <see cref="AudioDevice"/> audio endpoints returned by an enumeration.</summary>
internal class MMDeviceCollection : IDisposable
{
    private IMMDeviceCollection _MMDeviceCollection;
    private int _count = -1;
    private bool _disposed;

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
                Marshal.ThrowExceptionForHR(_MMDeviceCollection.GetCount(out var result));
                _count = (int)result;
            }

            return _count;
        }
    }

    /// <summary>Gets the endpoint at the specified zero-based position.</summary>
    /// <param name="index">The zero-based index of the endpoint to retrieve (0 to <see cref="Count"/> - 1).</param>
    /// <returns>A new <see cref="AudioDevice"/> for the endpoint at the requested position.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    /// <exception cref="COMException">Thrown when the index is out of range or the call fails.</exception>
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
            Marshal.ThrowExceptionForHR(_MMDeviceCollection.Item((uint)index, out IMMDevice result));
            return new AudioDevice(result, false, false);
        }
    }

    internal MMDeviceCollection(IMMDeviceCollection parent)
    {
        _MMDeviceCollection = parent;
    }

    /// <summary>Releases the underlying Core Audio collection.</summary>
    /// <remarks>Devices already obtained from the indexer are unaffected and remain usable.</remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Safe to release deterministically: this RCW never leaves the library, so nothing else can
        // be holding it.
        try
        {
            if (_MMDeviceCollection != null && Marshal.IsComObject(_MMDeviceCollection))
            {
                Marshal.ReleaseComObject(_MMDeviceCollection);
            }
        }
        catch
        {
            // best-effort cleanup
        }

        _MMDeviceCollection = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MMDeviceCollection));
        }
    }
}