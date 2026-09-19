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

using System;
using System.Runtime.InteropServices;
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.CoreAudioApi;

// This class implements the IAudioEndpointVolumeCallback interface,
// it is implemented in this class because implementing it on AudioEndpointVolume 
// (where the functionality is really wanted, would cause the OnNotify function 
// to show up in the public API. 
internal class AudioEndpointVolumeCallback : IAudioEndpointVolumeCallback
{
    private AudioEndpointVolume _Parent;

    internal AudioEndpointVolumeCallback(AudioEndpointVolume parent)
    {
        _Parent = parent;
    }

    [PreserveSig]
    public int OnNotify(IntPtr NotifyData)
    {
        //Since AUDIO_VOLUME_NOTIFICATION_DATA is dynamic in length based on the
        //number of audio channels available we cannot just call PtrToStructure 
        //to get all data, thats why it is split up into two steps, first the static
        //data is marshalled into the data structure, then with some IntPtr math the
        //remaining floats are read from memory.
        //
        AUDIO_VOLUME_NOTIFICATION_DATA data =
            (AUDIO_VOLUME_NOTIFICATION_DATA)Marshal.PtrToStructure(NotifyData, typeof(AUDIO_VOLUME_NOTIFICATION_DATA));

        //Determine offset in structure of the first float
        IntPtr Offset = Marshal.OffsetOf(typeof(AUDIO_VOLUME_NOTIFICATION_DATA), "ChannelVolume");
        //Determine offset in memory of the first float
        IntPtr FirstFloatPtr = (IntPtr)((long)NotifyData + (long)Offset);

        float[] voldata = new float[data.nChannels];

        Marshal.Copy(FirstFloatPtr, voldata, 0, voldata.Length);

        //Create combined structure and Fire Event in parent class.
        AudioVolumeNotificationData NotificationData =
            new AudioVolumeNotificationData(data.guidEventContext, data.bMuted, data.fMasterVolume, voldata);
        _Parent.FireNotification(NotificationData);
        return 0; //S_OK
    }
}