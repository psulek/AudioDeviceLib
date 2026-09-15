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

/// <summary>Immutable snapshot of an endpoint's volume state delivered with a volume-change notification.</summary>
public class AudioVolumeNotificationData
{
    private Guid _EventContext;
    private bool _Muted;
    private float _MasterVolume;
    private int _Channels;
    private float[] _ChannelVolume;

    /// <summary>Gets the context GUID identifying the caller that triggered the change, if any.</summary>
    public Guid EventContext => _EventContext;

    /// <summary>Gets a value indicating whether the endpoint is muted.</summary>
    public bool Muted => _Muted;

    /// <summary>Gets the master volume as a normalized scalar in the range 0.0 to 1.0.</summary>
    public float MasterVolume => _MasterVolume;

    /// <summary>Gets the number of channels reported in <see cref="ChannelVolume"/>.</summary>
    public int Channels => _Channels;

    /// <summary>Gets the per-channel volume scalars, each in the range 0.0 to 1.0.</summary>
    public float[] ChannelVolume => _ChannelVolume;

    /// <summary>Creates a new volume notification data snapshot.</summary>
    /// <param name="eventContext">The context GUID of the caller that triggered the change.</param>
    /// <param name="muted">Whether the endpoint is muted.</param>
    /// <param name="masterVolume">The master volume scalar in the range 0.0 to 1.0.</param>
    /// <param name="channelVolume">The per-channel volume scalars; its length determines <see cref="Channels"/>.</param>
    public AudioVolumeNotificationData(Guid eventContext, bool muted, float masterVolume, float[] channelVolume)
    {
        _EventContext = eventContext;
        _Muted = muted;
        _MasterVolume = masterVolume;
        _Channels = channelVolume.Length;
        _ChannelVolume = channelVolume;
    }
}