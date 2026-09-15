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

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>The device role used to resolve or assign a default endpoint, matching the native <c>ERole</c>.</summary>
public enum ERole
{
    /// <summary>Games, system notification sounds and voice commands.</summary>
    eConsole =0,

    /// <summary>Music, movies, narration and live-music recording.</summary>
    eMultimedia =1,

    /// <summary>Voice communications (e.g. talking to another person).</summary>
    eCommunications=2,

    /// <summary>The number of defined role values (not a real role).</summary>
    ERole_enum_count=3
}