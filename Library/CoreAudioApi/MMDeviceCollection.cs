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

using System.Runtime.InteropServices;
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>A read-only collection of <see cref="MMDevice"/> audio endpoints returned by an enumeration.</summary>
public class MMDeviceCollection
{
    private IMMDeviceCollection _MMDeviceCollection;

    /// <summary>Gets the number of endpoints in the collection.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public int Count
    {
        get
        {
            Marshal.ThrowExceptionForHR(_MMDeviceCollection.GetCount(out var result));
            return (int)result;
        }
    }

    /// <summary>Gets the endpoint at the specified zero-based position.</summary>
    /// <param name="index">The zero-based index of the endpoint to retrieve (0 to <see cref="Count"/> - 1).</param>
    /// <returns>The <see cref="MMDevice"/> at the requested position.</returns>
    public MMDevice this[int index]
    {
        get
        {
            _MMDeviceCollection.Item((uint)index, out IMMDevice result);
            return new MMDevice(result);
        }
    }

    internal MMDeviceCollection(IMMDeviceCollection parent)
    {
        _MMDeviceCollection = parent;
    }
}