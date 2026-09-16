# Third-Party Notices

AudioDeviceLib incorporates source code from the following third-party projects.
Their license notices are reproduced below as required.

---

## 1. AudioDeviceCmdlets — Francois Gendron

- Project: https://github.com/frgnca/AudioDeviceCmdlets
- Copyright (c) 2016-2022 Francois Gendron `<fg@frgn.ca>`
- License: MIT

AudioDeviceLib's managed facade (`AudioController`, `AudioDevice`, the enumeration,
default-detection and default-setting logic) is **derived from** the AudioDevice class
and the cmdlet logic of AudioDeviceCmdlets. The PowerShell cmdlet layer itself
(`AudioDeviceCmdlets.cs`) was **not** copied; it was replaced by a plain managed API.

```
MIT License

Copyright (c) 2016-2022 Francois Gendron <fg@frgn.ca>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## 2. CoreAudioApi (WASAPI interop) — Ray Molenkamp

- Origin: the CoreAudioApi interop layer originally authored by Ray Molenkamp
  (as used by NAudio and later bundled into AudioDeviceCmdlets).
- Copyright (c) 2007-2010 Ray Molenkamp
- License: zlib/libpng-style permissive license.

All files under `CoreAudioApi/` and `CoreAudioApi/Interfaces/` that originate from this
work carry the notice below.

Per condition 2 of that license: **every one of these files has been altered** and none is
an unmodified copy. Each altered file carries a `MODIFICATIONS` block directly beneath the
license notice, listing the changes made to it. The alterations range from namespace and
formatting changes through renamed types and enum members to reworked disposal, caching and
notification-registration behaviour. Nothing here may be taken as the original source code.

The four `PolicyConfig` interop files (`PolicyConfigClient.cs`, `Interfaces/IPolicyConfig.cs`,
`Interfaces/IPolicyConfig10.cs`, `Interfaces/IPolicyConfigVista.cs`) carry no license header
upstream either; they are covered by the AudioDeviceCmdlets MIT license above and are likewise
marked as altered.

```
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
```
