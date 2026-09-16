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
  Derived from `SOURCE/EDeviceState.cs` upstream.

  Changes from the original:
  - Namespace changed to `AudioDeviceLib.CoreAudioApi` (file-scoped); unused `using`
    directives removed.
  - Reformatted to the project's C# style (full braces, modern C# syntax) and annotated with XML
    documentation comments.
  - Enum renamed from `EDeviceState` to `DeviceState`; members renamed from `DEVICE_STATE_*` /
    `DEVICE_STATEMASK_ALL` to `Active`/`NotPresent`/`Unplugged`/`All`.
  - Values corrected against the Windows SDK (`mmdeviceapi.h`): `Unplugged` is `0x8` (was `0x2`),
    the missing `Disabled` (`0x2`) was added, and `All` is `0xF` (was `0x7`).
*/

using System;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>Bit flags describing the state of an audio endpoint, matching the native <c>DEVICE_STATE_*</c> constants.</summary>
[Flags]
public enum DeviceState : uint
{
    /// <summary>The endpoint is active and available for use.</summary>
    Active = 0x00000001,

    /// <summary>The endpoint is disabled (turned off in the Windows sound control panel).</summary>
    Disabled = 0x00000002,

    /// <summary>The endpoint device is not present (e.g. removed).</summary>
    NotPresent = 0x00000004,

    /// <summary>The endpoint is present but its audio jack is unplugged.</summary>
    Unplugged = 0x00000008,

    /// <summary>Mask matching endpoints in any state.</summary>
    All = Active | Disabled | NotPresent | Unplugged
}
