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

/// <summary>A collection of the <see cref="AudioSessionControl"/> sessions on an audio endpoint.</summary>
public class SessionCollection
{
    IAudioSessionEnumerator _AudioSessionEnumerator;
    internal SessionCollection(IAudioSessionEnumerator realEnumerator)
    {
        _AudioSessionEnumerator = realEnumerator;
    }

    /// <summary>Gets the session at the specified zero-based index.</summary>
    /// <param name="index">The zero-based index of the session (0 to <see cref="Count"/> - 1).</param>
    /// <returns>The <see cref="AudioSessionControl"/> at the requested position.</returns>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public AudioSessionControl this[int index]
    {
        get
        {
            IAudioSessionControl2 _Result;
            Marshal.ThrowExceptionForHR(_AudioSessionEnumerator.GetSession(index, out _Result));
            return new AudioSessionControl(_Result);
        }
    }

    /// <summary>Gets the number of sessions in the collection.</summary>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    public int Count
    {
        get
        {
            int result;
            Marshal.ThrowExceptionForHR(_AudioSessionEnumerator.GetCount(out result));
            return (int)result;
        }
    }
}