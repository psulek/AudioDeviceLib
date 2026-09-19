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

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>Reasons an audio session can be disconnected, matching the native <c>AudioSessionDisconnectReason</c>.</summary>
public enum AudioSessionDisconnectReason
{
    /// <summary>The audio endpoint device was removed.</summary>
    DeviceRemoval = 0,

    /// <summary>The Windows audio service was shut down.</summary>
    ServerShutdown = (DeviceRemoval + 1),

    /// <summary>The stream format changed for the device the session is connected to.</summary>
    FormatChanged = (ServerShutdown + 1),

    /// <summary>The user logged off the Windows session the audio session was running under.</summary>
    SessionLogoff = (FormatChanged + 1),

    /// <summary>The Windows session was disconnected.</summary>
    SessionDisconnected = (SessionLogoff + 1),

    /// <summary>The (shared-mode) session was pre-empted by an exclusive-mode connection.</summary>
    ExclusiveModeOverride = (SessionDisconnected + 1)
}