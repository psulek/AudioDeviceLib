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

  The changes are summarized in MODIFICATIONS.md at the repository root; the Git history of
  this file is the authoritative record.
*/

using System;
using System.Runtime.InteropServices;
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.CoreAudioApi;

// Separate COM callback implementation keeps OnNotify off AudioEndpointVolume's public API.
internal class AudioEndpointVolumeCallback : IAudioEndpointVolumeCallback
{
    // E_POINTER. Returned when NotifyData is null - see OnNotify.
    private const int EPointer = unchecked((int)0x80004003);

    // Cache the trailing-array offset to avoid reflection on the callback thread.
    // Use nameof so field renames remain compiler-checked.
    private static readonly int ChannelVolumeOffset =
        Marshal.OffsetOf<AUDIO_VOLUME_NOTIFICATION_DATA>(
            nameof(AUDIO_VOLUME_NOTIFICATION_DATA.ChannelVolume)).ToInt32();

    private readonly AudioEndpointVolume _parent;

    internal AudioEndpointVolumeCallback(AudioEndpointVolume parent)
    {
        _parent = parent;
    }

    [PreserveSig]
    public int OnNotify(IntPtr notifyData)
    {
        // Convert marshalling and consumer exceptions to HRESULTs; none may escape into native code.
        try
        {
            // Marshal the fixed header first, then copy the variable-length channel array.
            // Guard against null before unboxing the header.
            if (notifyData == IntPtr.Zero)
            {
                return EPointer;
            }
            var data = Marshal.PtrToStructure<AUDIO_VOLUME_NOTIFICATION_DATA>(notifyData);

            // Address of the trailing float array. IntPtr.Add rather than arithmetic through long:
            // from .NET 7 the (IntPtr)(long) conversion no longer throws on overflow, so on a 32-bit
            // runtime it truncates silently (CA2020). IntPtr.Add is native-width throughout.
            IntPtr firstFloatPtr = IntPtr.Add(notifyData, ChannelVolumeOffset);

            // Cap the driver-supplied count at the cached endpoint channel count before Marshal.Copy.
            // Native overreads can terminate the process; the cached limit requires no callback-time COM call.
            // Truncate rather than reject so master-volume and mute notifications remain available.
            int channelCount = (int)Math.Min(data.nChannels, (uint)_parent.Channels.Count);

            float[] voldata;
            if (channelCount > 0)
            {
                voldata = new float[channelCount];
                Marshal.Copy(firstFloatPtr, voldata, 0, channelCount);
            }
            else
            {
                voldata = Array.Empty<float>();
            }

            //Create combined structure and Fire Event in parent class.
            var notificationData = new AudioVolumeNotificationData(data.guidEventContext, data.bMuted, data.fMasterVolume, voldata);
            _parent.FireNotification(notificationData);
            return InteropUtils.S_OK;
        }
        catch (Exception ex)
        {
            return InteropUtils.ReportFailure(ex);
        }
    }
}