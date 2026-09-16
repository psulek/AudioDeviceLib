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
  Derived from `SOURCE/EDataFlow.cs` upstream.

  Changes from the original:
  - Namespace changed to `AudioDeviceLib.CoreAudioApi` (file-scoped); unused `using`
    directives removed.
  - Reformatted to the project's C# style (full braces, modern C# syntax) and annotated with XML
    documentation comments.
  - Split out of the original `EDataFlow`: this is the query-side half, used only to select which
    endpoints an enumeration returns. It keeps the `eAll` sentinel (renamed `All`); the state-side
    half is `DataFlow`, which drops it because `IMMEndpoint::GetDataFlow` can never return `eAll`.
  - Members `eRender`/`eCapture`/`eAll` renamed to `Render`/`Capture`/`All`; the
    `EDataFlow_enum_count` sentinel was dropped.
*/

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// Selects which endpoint directions an enumeration returns, matching the native <c>EDataFlow</c>.
/// The direction of a single endpoint is <see cref="DataFlow"/>.
/// </summary>
/// <remarks>This is not a bit field; the members are discrete values and cannot be combined.</remarks>
public enum DataFlowFilter
{
    /// <summary>Render (output / playback) endpoints only.</summary>
    Render = 0,

    /// <summary>Capture (input / recording) endpoints only.</summary>
    Capture = 1,

    /// <summary>Endpoints in either direction.</summary>
    All = 2
}
