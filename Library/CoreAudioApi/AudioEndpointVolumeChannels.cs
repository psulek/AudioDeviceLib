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
using System.Collections;
using System.Collections.Generic;
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>The collection of per-channel volume controls for an audio endpoint.</summary>
/// <remarks>
/// This collection is a snapshot taken when the owning <see cref="AudioEndpointVolume"/> was created:
/// the channel count is read once and the per-channel controls are built once. The count comes from
/// the endpoint's stream format, which a user can in principle alter (by reconfiguring the speaker
/// layout, for example), and Windows raises no notification for such a change. Should it happen, this
/// collection keeps describing the layout as it was; re-acquire the device - and with it
/// <see cref="AudioEndpointVolume"/> - to pick up the new one. In practice endpoint channel counts are
/// fixed: onboard, USB, Bluetooth and HDMI endpoints alike reject any channel count but their native one.
/// </remarks>
public sealed class AudioEndpointVolumeChannels : IReadOnlyList<AudioEndpointVolumeChannel>
{
    private readonly IAudioEndpointVolumeCOM _parent;
    private readonly AudioEndpointVolumeChannel[] _channels;

    /// <summary>
    /// Gets the number of channels in this snapshot, which is always the number of controls the
    /// indexer can return.
    /// </summary>
    /// <remarks>
    /// Read from the endpoint once, when this collection was built. It therefore costs no COM call,
    /// cannot fail, and cannot disagree with the indexer. See the remarks on
    /// <see cref="AudioEndpointVolumeChannels"/> for when the snapshot can go stale.
    /// </remarks>
    public int Count => _channels.Length;

    /// <summary>Gets the volume control for the channel at the specified zero-based index.</summary>
    /// <param name="index">The zero-based channel index (0 to <see cref="Count"/> - 1).</param>
    /// <returns>The <see cref="AudioEndpointVolumeChannel"/> at the requested position.</returns>
    public AudioEndpointVolumeChannel this[int index] =>
        index >= 0 && index < Count ? _channels[index] : throw new ArgumentOutOfRangeException(nameof(index));

    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    internal AudioEndpointVolumeChannels(IAudioEndpointVolumeCOM parent)
    {
        _parent = parent;

        InteropUtils.ThrowIfFailed(_parent.GetChannelCount(out var channelCount));
        _channels = new AudioEndpointVolumeChannel[channelCount];
        for (int i = 0; i < _channels.Length; i++)
        {
            _channels[i] = new AudioEndpointVolumeChannel(_parent, i);
        }
    }
    /// <summary>Enumerates the controls in this snapshot.</summary>
    /// <returns>An enumerator over the snapshot.</returns>
    public IEnumerator<AudioEndpointVolumeChannel> GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}