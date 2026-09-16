/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  IAudioDeviceEvents.cs
  Public, attribute-free C# contract for receiving audio endpoint (device) notifications.

  This is the interface library consumers implement. It carries no COM interop attributes.
  The internal MMNotificationClientComAdapter receives the raw COM callbacks
  (Interfaces.IMMNotificationClient) and forwards them to an instance of this interface.
*/

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// Receives change notifications for the set of audio endpoints. Implement this and register it with
/// <see cref="AudioDeviceLib.Lib.AudioController.RegisterDeviceNotification"/>.
/// </summary>
/// <remarks>
/// THREADING: these callbacks are raised by Windows Core Audio on arbitrary, non-UI threads, and may
/// arrive concurrently. Keep every method fast and non-blocking, do your own synchronization when
/// touching shared state, and marshal to a UI thread yourself if you need to update UI. Exceptions
/// thrown from a callback are converted to an HRESULT and returned to the audio engine; prefer
/// handling errors inside the callback.
/// <para>
/// Do NOT register/unregister notifications or dispose the <see cref="AudioDeviceLib.Lib.AudioController"/>
/// from inside a callback — that reenters Core Audio while it is dispatching. The payload is plain
/// data (endpoint ID strings and enums), so there is no live COM object to misuse.
/// </para>
/// </remarks>
public interface IAudioDeviceEvents
{
    /// <summary>Called when an endpoint's state changes (active, disabled, not present or unplugged).</summary>
    /// <param name="deviceId">The endpoint ID of the device whose state changed.</param>
    /// <param name="newState">The new device state reported by the system.</param>
    /// <remarks>Called on an arbitrary, non-UI thread.</remarks>
    void OnDeviceStateChanged(string deviceId, DeviceState newState);

    /// <summary>Called when a new endpoint is added.</summary>
    /// <param name="deviceId">The endpoint ID of the added device.</param>
    /// <remarks>Called on an arbitrary, non-UI thread.</remarks>
    void OnDeviceAdded(string deviceId);

    /// <summary>Called when an endpoint is removed.</summary>
    /// <param name="deviceId">The endpoint ID of the removed device.</param>
    /// <remarks>Called on an arbitrary, non-UI thread.</remarks>
    void OnDeviceRemoved(string deviceId);

    /// <summary>Called when the default endpoint changes for a given data flow and role.</summary>
    /// <param name="flow">The data-flow direction whose default changed (<see cref="DataFlow.Render"/> or <see cref="DataFlow.Capture"/>).</param>
    /// <param name="role">
    /// The role whose default changed — for example <see cref="Role.Communications"/> distinguishes a
    /// change to the default communications device from the console/multimedia default.
    /// </param>
    /// <param name="defaultDeviceId">
    /// The endpoint ID of the new default device, or <c>null</c> when there is no longer a default for
    /// this flow/role (e.g. the last matching device was removed).
    /// </param>
    /// <remarks>Called on an arbitrary, non-UI thread.</remarks>
    void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId);

    /// <summary>Called when a property value of an endpoint changes.</summary>
    /// <param name="deviceId">The endpoint ID of the device whose property changed.</param>
    /// <param name="key">
    /// The property that changed. Use <see cref="PropertyKey.Name"/> for a friendly name of the property.
    /// </param>
    /// <remarks>Called on an arbitrary, non-UI thread.</remarks>
    // TODO: expose the new value too — read it from the device's property store (open the MMDevice for
    //       deviceId, then Properties[key]); deferred for now to avoid a COM read on the callback thread.
    void OnPropertyValueChanged(string deviceId, PropertyKey key);
}
