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
using System.Runtime.InteropServices;

namespace AudioDeviceLib.CoreAudioApi.Interfaces;

/// <summary>
/// Callback interface for receiving audio session change notifications. Implement this and pass it
/// to <see cref="AudioSessionControl.RegisterAudioSessionNotification"/>. Every method returns an
/// HRESULT (0 / <c>S_OK</c> on success).
/// </summary>
[Guid("24918ACC-64B3-37C1-8CA9-74A66E9957A8"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAudioSessionEvents
{
    /// <summary>Called when the session display name changes.</summary>
    /// <param name="NewDisplayName">The new display name.</param>
    /// <param name="EventContext">The context GUID of the caller that made the change.</param>
    /// <returns>An HRESULT; return 0 (<c>S_OK</c>) on success.</returns>
    [PreserveSig] 
    int OnDisplayNameChanged( [MarshalAs(UnmanagedType.LPWStr)] string NewDisplayName, Guid EventContext );

    /// <summary>Called when the session icon path changes.</summary>
    /// <param name="NewIconPath">The new icon path.</param>
    /// <param name="EventContext">The context GUID of the caller that made the change.</param>
    /// <returns>An HRESULT; return 0 (<c>S_OK</c>) on success.</returns>
    [PreserveSig] 
    int OnIconPathChanged(  [MarshalAs(UnmanagedType.LPWStr)] string NewIconPath, Guid EventContext );

    /// <summary>Called when the session master volume or mute state changes.</summary>
    /// <param name="NewVolume">The new master volume scalar in the range 0.0 to 1.0.</param>
    /// <param name="newMute">The new mute state.</param>
    /// <param name="EventContext">The context GUID of the caller that made the change.</param>
    /// <returns>An HRESULT; return 0 (<c>S_OK</c>) on success.</returns>
    [PreserveSig] 
    int OnSimpleVolumeChanged( float NewVolume,bool newMute, Guid EventContext );

    /// <summary>Called when one or more per-channel volumes change.</summary>
    /// <param name="ChannelCount">The number of channels in <paramref name="NewChannelVolumeArray"/>.</param>
    /// <param name="NewChannelVolumeArray">A pointer to an array of channel volume scalars.</param>
    /// <param name="ChangedChannel">The index of the channel that changed, or 0xFFFFFFFF if multiple changed.</param>
    /// <param name="EventContext">The context GUID of the caller that made the change.</param>
    /// <returns>An HRESULT; return 0 (<c>S_OK</c>) on success.</returns>
    [PreserveSig] 
    int OnChannelVolumeChanged( UInt32 ChannelCount,  IntPtr NewChannelVolumeArray, UInt32 ChangedChannel, Guid EventContext );

    /// <summary>Called when the session grouping parameter changes.</summary>
    /// <param name="NewGroupingParam">The new grouping parameter GUID.</param>
    /// <param name="EventContext">The context GUID of the caller that made the change.</param>
    /// <returns>An HRESULT; return 0 (<c>S_OK</c>) on success.</returns>
    [PreserveSig] 
    int OnGroupingParamChanged( Guid NewGroupingParam, Guid EventContext );

    /// <summary>Called when the session activity state changes.</summary>
    /// <param name="NewState">The new session state.</param>
    /// <returns>An HRESULT; return 0 (<c>S_OK</c>) on success.</returns>
    [PreserveSig] 
    int OnStateChanged( AudioSessionState NewState);

    /// <summary>Called when the session is disconnected from its audio device.</summary>
    /// <param name="DisconnectReason">The reason the session was disconnected.</param>
    /// <returns>An HRESULT; return 0 (<c>S_OK</c>) on success.</returns>
    [PreserveSig] 
    int OnSessionDisconnected( AudioSessionDisconnectReason DisconnectReason);
}