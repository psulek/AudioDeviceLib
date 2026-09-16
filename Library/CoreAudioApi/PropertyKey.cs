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

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>Identifies a single property in a Core Audio property store (the native <c>PROPERTYKEY</c>).</summary>
public struct PropertyKey
{
    /// <summary>The format identifier (GUID) of the property set the property belongs to.</summary>
    public Guid fmtid;

    /// <summary>The property identifier within the property set identified by <see cref="fmtid"/>.</summary>
    public int pid;

    /// <summary>
    /// A friendly, human-readable name for well-known audio property keys (e.g.
    /// <c>Device.FriendlyName</c>), or a <c>"{fmtid}/{pid}"</c> fallback when the key is not recognized.
    /// </summary>
    public readonly string Name => PropertyKeyNames.GetName(this);
};