/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  IAudioSessionEvents.cs
  Public, attribute-free C# contract for receiving audio session notifications.

  This is the interface library consumers implement. It intentionally carries no COM
  interop attributes and uses natural .NET types (e.g. float[] instead of a raw pointer).
  The internal AudioSessionEventsComAdapter receives the raw COM callbacks
  (Interfaces.IAudioSessionEventsCOM) and forwards them to an instance of this interface.
*/

using System;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// Receives change notifications for a single audio session. Implement this and pass the
/// instance to <see cref="AudioSessionControl.RegisterAudioSessionNotification"/>.
/// </summary>
/// <remarks>
/// THREADING: these callbacks are raised by Windows Core Audio on arbitrary, non-UI threads,
/// and may arrive concurrently. Keep every method fast and non-blocking, do your own
/// synchronization when touching shared state, and marshal to a UI thread yourself if you
/// need to update UI. Exceptions thrown from a callback are converted to an HRESULT and
/// returned to the audio engine; prefer handling errors inside the callback.
/// <para>
/// The <c>session</c> parameter passed to every method is an immutable <see cref="AudioSessionInfo"/>
/// snapshot of the session the notification came from, so a single consumer registered on several
/// sessions can tell them apart. It is a point-in-time copy (identifiers, display name, icon path,
/// state), never the live COM object, so nothing you do with it can reenter Core Audio while it is
/// dispatching.
/// </para>
/// </remarks>
public interface IAudioSessionEvents
{
    /// <summary>Called when the session display name changes.</summary>
    /// <param name="session">Immutable snapshot identifying the session that raised the notification.</param>
    /// <param name="newDisplayName">The new display name.</param>
    /// <param name="eventContext">The context GUID supplied by the caller that made the change,
    /// or <see langword="null"/> if that caller supplied none.</param>
    /// <remarks>Called on an arbitrary, non-UI thread.</remarks>
    void OnDisplayNameChanged(AudioSessionInfo session, string newDisplayName, Guid? eventContext);

    /// <summary>Called when the session icon path changes.</summary>
    /// <param name="session">Immutable snapshot identifying the session that raised the notification.</param>
    /// <param name="newIconPath">The new icon path.</param>
    /// <param name="eventContext">The context GUID supplied by the caller that made the change,
    /// or <see langword="null"/> if that caller supplied none.</param>
    /// <remarks>Called on an arbitrary, non-UI thread.</remarks>
    void OnIconPathChanged(AudioSessionInfo session, string newIconPath, Guid? eventContext);

    /// <summary>Called when the session master volume or mute state changes.</summary>
    /// <param name="session">Immutable snapshot identifying the session that raised the notification.</param>
    /// <param name="newVolume">The new master volume scalar in the range 0.0 to 1.0.</param>
    /// <param name="newMute">The new mute state.</param>
    /// <param name="eventContext">The context GUID supplied by the caller that made the change,
    /// or <see langword="null"/> if that caller supplied none.</param>
    /// <remarks>Called on an arbitrary, non-UI thread.</remarks>
    void OnSimpleVolumeChanged(AudioSessionInfo session, float newVolume, bool newMute, Guid? eventContext);

    /// <summary>Called when one or more per-channel volumes change.</summary>
    /// <param name="session">Immutable snapshot identifying the session that raised the notification.</param>
    /// <param name="newChannelVolumes">The new per-channel volume scalars (one entry per channel).</param>
    /// <param name="changedChannel">The index of the channel that changed, or 0xFFFFFFFF if multiple changed.</param>
    /// <param name="eventContext">The context GUID supplied by the caller that made the change,
    /// or <see langword="null"/> if that caller supplied none.</param>
    /// <remarks>Called on an arbitrary, non-UI thread.</remarks>
    void OnChannelVolumeChanged(AudioSessionInfo session, float[] newChannelVolumes, uint changedChannel, Guid? eventContext);

    /// <summary>Called when the session grouping parameter changes.</summary>
    /// <param name="session">Immutable snapshot identifying the session that raised the notification.</param>
    /// <param name="newGroupingParam">The new grouping parameter GUID.</param>
    /// <param name="eventContext">The context GUID supplied by the caller that made the change,
    /// or <see langword="null"/> if that caller supplied none.</param>
    /// <remarks>Called on an arbitrary, non-UI thread.</remarks>
    void OnGroupingParamChanged(AudioSessionInfo session, Guid newGroupingParam, Guid? eventContext);

    /// <summary>Called when the session activity state changes.</summary>
    /// <param name="session">Immutable snapshot identifying the session that raised the notification.</param>
    /// <param name="newState">The new session state.</param>
    /// <remarks>Called on an arbitrary, non-UI thread.</remarks>
    void OnStateChanged(AudioSessionInfo session, AudioSessionState newState);

    /// <summary>Called when the session is disconnected from its audio device.</summary>
    /// <param name="session">Immutable snapshot identifying the session that raised the notification.</param>
    /// <param name="disconnectReason">The reason the session was disconnected.</param>
    /// <remarks>Called on an arbitrary, non-UI thread.</remarks>
    void OnSessionDisconnected(AudioSessionInfo session, AudioSessionDisconnectReason disconnectReason);
}