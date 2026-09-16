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
  - Split out of the original `EDeviceState`: this is the query-side half, used only as the
    `StateMask` that selects which endpoints an enumeration returns, where combining bits is
    correct. The state-side half is `DeviceState`, which is not `[Flags]` and drops `All`, because
    `IMMDevice::GetState` always reports exactly one state.
  - Members renamed from `DEVICE_STATE_*` / `DEVICE_STATEMASK_ALL` to `Active`/`Disabled`/
    `NotPresent`/`Unplugged`/`All`.
  - Values corrected against the Windows SDK (`mmdeviceapi.h`): `Unplugged` is `0x8` (was `0x2`),
    the missing `Disabled` (`0x2`) was added, and `All` is `0xF` (was `0x7`).
*/

using System;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// A bit mask of endpoint states selecting which endpoints an enumeration returns, matching the
/// native <c>DEVICE_STATE_*</c> constants. The state of a single endpoint is
/// <see cref="DeviceState"/>.
/// </summary>
[Flags]
public enum DeviceStateFilter : uint
{
    /// <summary>Match endpoints that are active and available for use.</summary>
    Active = 0x00000001,

    /// <summary>Match endpoints that are disabled (turned off in the Windows sound control panel).</summary>
    Disabled = 0x00000002,

    /// <summary>Match endpoints that are not present (e.g. removed).</summary>
    NotPresent = 0x00000004,

    /// <summary>Match endpoints that are present but whose audio jack is unplugged.</summary>
    Unplugged = 0x00000008,

    /// <summary>Match endpoints in any state.</summary>
    All = Active | Disabled | NotPresent | Unplugged
}
