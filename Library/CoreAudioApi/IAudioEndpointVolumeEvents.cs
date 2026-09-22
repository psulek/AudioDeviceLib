/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  IAudioEndpointVolumeEvents.cs
  Public, attribute-free C# contract for receiving endpoint (device master) volume
  notifications.

  This is the interface library consumers implement. It intentionally carries no COM
  interop attributes. The internal AudioEndpointVolumeCallback receives the raw COM
  callback (Interfaces.IAudioEndpointVolumeCallback) and AudioEndpointVolume forwards it
  to every instance of this interface registered on the endpoint.
*/

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// Receives volume and mute change notifications for a single audio endpoint. Implement this
/// and pass the instance to <see cref="AudioEndpointVolume.RegisterVolumeNotification"/>.
/// </summary>
/// <remarks>
/// THREADING: this callback is raised by Windows Core Audio on an arbitrary, non-UI thread,
/// and calls may arrive concurrently. Keep it fast and non-blocking, do your own
/// synchronization when touching shared state, and marshal to a UI thread yourself if you
/// need to update UI.
/// <para>
/// Each registered consumer is invoked independently: an exception thrown by one consumer does
/// not prevent the others from receiving the notification. Exceptions are collected, converted
/// to an HRESULT and returned to the audio engine rather than escaping into native code, which
/// means they are not otherwise observable - prefer handling errors inside the callback.
/// </para>
/// <para>
/// The notification carries no endpoint identity, so a consumer that listens to several
/// endpoints should register a distinct instance per <see cref="AudioEndpointVolume"/> and let
/// each instance capture which endpoint it belongs to.
/// </para>
/// </remarks>
public interface IAudioEndpointVolumeEvents
{
    /// <summary>Called when the endpoint master volume, per-channel volume or mute state changes.</summary>
    /// <param name="data">A snapshot of the endpoint's volume and mute state at the time of the change.</param>
    /// <remarks>Called on an arbitrary, non-UI thread.</remarks>
    void OnVolumeNotification(AudioVolumeNotificationData data);
}
