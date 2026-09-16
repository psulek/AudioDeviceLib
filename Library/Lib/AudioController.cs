/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  AudioController.cs
  Public entry point for AudioDeviceLib: enumerate endpoints, read/set the default
  device, and control volume/mute.

  The enumeration, default-detection and default-setting logic is derived from
  AudioDeviceCmdlets by Francois Gendron (MIT),
  https://github.com/frgnca/AudioDeviceCmdlets
*/

using System;
using System.Collections.Generic;
using System.Linq;
using AudioDeviceLib.CoreAudioApi;

namespace AudioDeviceLib.Lib;

/// <summary>
/// High-level API over the Windows Core Audio endpoints. Create one instance and reuse it.
/// All members are Windows-only and must run on a thread able to use COM.
/// </summary>
public sealed class AudioController : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new MMDeviceEnumerator();

    // Maps each registered consumer to its registration entry (the COM adapter plus the cached
    // token). Holding the adapter keeps its CCW alive while registered (a GC'd sink would stop
    // notifications); caching the token means repeated registration of the same consumer hands back
    // the same IDisposable instead of allocating a new one each time.
    private readonly Dictionary<IAudioDeviceEvents, DeviceRegistration> _deviceRegistrations
        = new Dictionary<IAudioDeviceEvents, DeviceRegistration>(AudioDeviceEventsRefComparer.Instance);

    private readonly object _deviceRegistrationsLock = new object();
    private bool _disposed;

    // Pairs the COM sink adapter with the token handed to the caller, cached together so that
    // removing the entry on dispose invalidates the cache: the next Register then creates a fresh one.
    private sealed class DeviceRegistration
    {
        internal readonly MMNotificationClientComAdapter Adapter;
        internal readonly DeviceEventsRegistration Token;

        internal DeviceRegistration(MMNotificationClientComAdapter adapter, DeviceEventsRegistration token)
        {
            Adapter = adapter;
            Token = token;
        }
    }

    // ---- Enumeration --------------------------------------------------------------

    /// <summary>Returns the endpoints matching the given data-flow direction and state, in enumeration order.</summary>
    /// <param name="flow">
    /// Which endpoint directions to include: <see cref="DataFlow.Render"/> (playback),
    /// <see cref="DataFlow.Capture"/> (recording), or <see cref="DataFlow.All"/> for both (the default).
    /// </param>
    /// <param name="state">
    /// A bit mask of endpoint states to include. Defaults to <see cref="DeviceState.Active"/>; combine
    /// flags or pass <see cref="DeviceState.All"/> to include disabled, not-present and unplugged endpoints too.
    /// </param>
    /// <returns>
    /// A read-only list of the matching <see cref="AudioDevice"/> endpoints. The list is empty if no endpoints match.
    /// </returns>
    public IReadOnlyList<AudioDevice> GetDevices(DataFlow flow = DataFlow.All, DeviceState state = DeviceState.Active)
    {
        return GetDevicesInternal(flow, state);
    }

    /// <summary>
    /// Returns the endpoint with the given ID, or null if no such endpoint exists.
    /// </summary>
    /// <param name="deviceId"> The ID of the endpoint to return. </param>
    /// <returns> The endpoint with the given ID, or null if no such endpoint exists. </returns>
    /// <exception cref="ArgumentNullException"> If <paramref name="deviceId"/> is null or empty. </exception>
    public AudioDevice GetDeviceById(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            throw new ArgumentNullException(nameof(deviceId));
        }

        var devices = GetDevicesInternal(deviceId: deviceId);
        return devices.FirstOrDefault();
    }
    
    private class DeviceDefaultIds
    {
        public string DefaultPlaybackId;
        public string DefaultRecordingId;
        public string CommPlaybackId;
        public string CommRecordingId;
        
        public bool IsDefault(MMDevice device)
        {
            return device.ID == DefaultPlaybackId || device.ID == DefaultRecordingId;
        }
        
        public bool IsDefaultComm(MMDevice device)
        {
            return device.ID == CommPlaybackId || device.ID == CommRecordingId;
        }
    }

    private DeviceDefaultIds GetDeviceDefaults(DataFlow flow)
    {
        var allowRender = flow == DataFlow.Render || flow == DataFlow.All;
        var allowCapture = flow == DataFlow.Capture || flow == DataFlow.All;
        return new DeviceDefaultIds
        {
            DefaultPlaybackId = allowRender ? TryGetDefaultId(DataFlow.Render, Role.Multimedia) : null,
            DefaultRecordingId = allowCapture ? TryGetDefaultId(DataFlow.Capture, Role.Multimedia) : null,
            CommPlaybackId = allowRender ? TryGetDefaultId(DataFlow.Render, Role.Communications) : null,
            CommRecordingId = allowCapture ? TryGetDefaultId(DataFlow.Capture, Role.Communications) : null,
        };
    }

    private IReadOnlyList<AudioDevice> GetDevicesInternal(DataFlow flow = DataFlow.All,
        DeviceState state = DeviceState.Active,
        string deviceId = null)
    {
        // var allowRender = flow == DataFlow.Render || flow == DataFlow.All;
        // var allowCapture = flow == DataFlow.Capture || flow == DataFlow.All;

        // var defaultPlaybackId = allowRender ? TryGetDefaultId(DataFlow.Render, Role.Multimedia) : null;
        // var defaultRecordingId = allowCapture ? TryGetDefaultId(DataFlow.Capture, Role.Multimedia) : null;
        // var commPlaybackId = allowRender ? TryGetDefaultId(DataFlow.Render, Role.Communications) : null;
        // var commRecordingId = allowCapture ? TryGetDefaultId(DataFlow.Capture, Role.Communications) : null;
        var deviceDefaults = GetDeviceDefaults(flow);

        if (!string.IsNullOrEmpty(deviceId))
        {
            var device = _enumerator.GetDevice(deviceId);
            if (device != null)
            {
                return new List<AudioDevice>(1)
                {
                    new AudioDevice(device, deviceDefaults.IsDefault(device), deviceDefaults.IsDefaultComm(device))
                };
            }
        }

        var devices = _enumerator.EnumerateAudioEndPoints(flow, state);
        var result = new List<AudioDevice>(devices.Count);
        for (var i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            result.Add(new AudioDevice(device, deviceDefaults.IsDefault(device), deviceDefaults.IsDefaultComm(device)));
        }

        return result;
    }

    /// <summary>Returns all active playback (render) endpoints.</summary>
    /// <param name="state">
    ///  A bit mask of endpoint states to include. Defaults to <see cref="DeviceState.Active"/>;
    /// </param>
    /// <returns>
    /// A read-only list of the active endpoints whose <see cref="AudioDevice.Kind"/> is
    /// <see cref="AudioDeviceKind.Playback"/>.
    /// </returns>
    public IReadOnlyList<AudioDevice> GetPlaybackDevices(DeviceState state = DeviceState.Active)
    {
        return GetDevices(DataFlow.Render, state);
    }

    /// <summary>Returns all active recording (capture) endpoints.</summary>
    /// <param name="state">
    ///  A bit mask of endpoint states to include. Defaults to <see cref="DeviceState.Active"/>;
    /// </param>
    /// <returns>
    /// A read-only list of the active endpoints whose <see cref="AudioDevice.Kind"/> is
    /// <see cref="AudioDeviceKind.Recording"/>.
    /// </returns>
    public IReadOnlyList<AudioDevice> GetRecordingDevices(DeviceState state = DeviceState.Active)
    {
        return GetDevices(DataFlow.Capture, state);
    }

    /// <summary>Returns the current default playback device, or null if none is set.</summary>
    /// <param name="communications">
    /// When <c>true</c>, resolves the default for the communications role (voice chat); when
    /// <c>false</c> (the default), resolves the default for the multimedia role (music, movies).
    /// </param>
    /// <returns>
    /// The default playback <see cref="AudioDevice"/> for the requested role, or <c>null</c> if no
    /// default playback device is currently set.
    /// </returns>
    public AudioDevice GetDefaultPlaybackDevice(bool communications = false)
    {
        return GetDefault(DataFlow.Render, communications);
    }

    /// <summary>Returns the current default recording device, or null if none is set.</summary>
    /// <param name="communications">
    /// When <c>true</c>, resolves the default for the communications role (voice chat); when
    /// <c>false</c> (the default), resolves the default for the multimedia role.
    /// </param>
    /// <returns>
    /// The default recording <see cref="AudioDevice"/> for the requested role, or <c>null</c> if no
    /// default recording device is currently set.
    /// </returns>
    public AudioDevice GetDefaultRecordingDevice(bool communications = false)
    {
        return GetDefault(DataFlow.Capture, communications);
    }

    // ---- Setting the default ------------------------------------------------------

    /// <summary>Sets the given device as the default for the requested role(s).</summary>
    /// <param name="device">The endpoint to make default. Must not be <c>null</c>.</param>
    /// <param name="roles">
    /// The role(s) to assign. Defaults to <see cref="DefaultRole.Default"/> (multimedia + communications).
    /// Each set flag maps to one <c>Role</c> assignment.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="device"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="roles"/> specifies no role.</exception>
    public void SetDefaultDevice(AudioDevice device, DefaultRole roles = DefaultRole.Default)
    {
        if (device == null)
        {
            throw new ArgumentNullException(nameof(device));
        }

        SetDefaultDevice(device.Id, roles);
    }

    /// <summary>
    /// Sets the endpoint with the given ID as the default for the requested role(s).
    /// <paramref name="roles"/> is a bit flag; each set flag maps to one <c>Role</c> assignment.
    /// </summary>
    /// <param name="deviceId">The endpoint ID to make default. Must not be <c>null</c> or empty.</param>
    /// <param name="roles">
    /// The role(s) to assign. Defaults to <see cref="DefaultRole.Default"/> (multimedia + communications).
    /// At least one of <see cref="DefaultRole.Console"/>, <see cref="DefaultRole.Multimedia"/> or
    /// <see cref="DefaultRole.Communications"/> must be set.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="deviceId"/> is <c>null</c> or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="roles"/> specifies no role.</exception>
    private void SetDefaultDevice(string deviceId, DefaultRole roles = DefaultRole.Default)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            throw new ArgumentNullException(nameof(deviceId));
        }

        if ((roles & DefaultRole.All) == 0)
        {
            throw new ArgumentException("At least one role (Console, Multimedia or Communications) must be specified.",
                nameof(roles));
        }

        var client = new PolicyConfigClient();
        if ((roles & DefaultRole.Console) != 0)
        {
            client.SetDefaultEndpoint(deviceId, Role.Console);
        }

        if ((roles & DefaultRole.Multimedia) != 0)
        {
            client.SetDefaultEndpoint(deviceId, Role.Multimedia);
        }

        if ((roles & DefaultRole.Communications) != 0)
        {
            client.SetDefaultEndpoint(deviceId, Role.Communications);
        }
    }

    private AudioDevice GetDefault(DataFlow flow, bool communications)
    {
        var role = communications ? Role.Communications : Role.Multimedia;
        MMDevice device;
        try
        {
            device = _enumerator.GetDefaultAudioEndpoint(flow, role);
        }
        catch
        {
            return null;
        }
        
        if (device == null)
        {
            return null;
        }

        var deviceDefaults = GetDeviceDefaults(flow);
        return new AudioDevice(device, deviceDefaults.IsDefault(device), deviceDefaults.IsDefaultComm(device));
    }

    private string TryGetDefaultId(DataFlow flow, Role role)
    {
        try
        {
            return _enumerator.GetDefaultAudioEndpointDeviceId(flow, role);
        }
        catch
        {
            // No default endpoint of this kind/role is set.
            return null;
        }
    }

    // ---- Device (endpoint) notifications ------------------------------------------

    /// <summary>Registers a callback to receive audio endpoint change notifications.</summary>
    /// <param name="consumer">The consumer that will receive <see cref="IAudioDeviceEvents"/> callbacks.</param>
    /// <returns>
    /// A token that unregisters the callback when disposed. Disposing the token (or the controller)
    /// is the way to stop notifications.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="consumer"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this controller has been disposed.</exception>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when the underlying Core Audio call fails.</exception>
    /// <remarks>
    /// THREADING: the consumer's callbacks are raised by Windows Core Audio on arbitrary, non-UI
    /// threads and may arrive concurrently, so the implementation must be fast and thread-safe.
    /// Registering the same consumer again returns the same token for the existing registration
    /// without registering twice.
    /// </remarks>
    public IDisposable RegisterDeviceNotification(IAudioDeviceEvents consumer)
    {
        if (consumer == null)
        {
            throw new ArgumentNullException(nameof(consumer));
        }

        MMNotificationClientComAdapter adapter;
        DeviceEventsRegistration token;
        lock (_deviceRegistrationsLock)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AudioController));
            }

            if (_deviceRegistrations.TryGetValue(consumer, out var existing))
            {
                // Already registered: hand back the same token for the existing registration.
                return existing.Token;
            }

            adapter = new MMNotificationClientComAdapter(consumer);
            token = new DeviceEventsRegistration(this, adapter);
            _deviceRegistrations[consumer] = new DeviceRegistration(adapter, token);
        }

        _enumerator.RegisterEndpointNotificationCallback(adapter);
        return token;
    }

    // Called by a registration token to undo exactly one registration. Idempotent: a no-op if the
    // adapter was already removed (e.g. by Dispose or a second token).
    internal void RemoveDeviceRegistration(MMNotificationClientComAdapter adapter)
    {
        if (adapter == null)
        {
            return;
        }

        lock (_deviceRegistrationsLock)
        {
            if (!_deviceRegistrations.TryGetValue(adapter.Target, out var existing) ||
                !ReferenceEquals(existing.Adapter, adapter))
            {
                return;
            }

            _deviceRegistrations.Remove(adapter.Target);
        }

        _enumerator.UnregisterEndpointNotificationCallback(adapter);
    }

    /// <summary>
    /// Releases resources held by the controller, unregistering any remaining device-notification
    /// callbacks. The controller does not own the <see cref="AudioDevice"/> instances returned by its
    /// methods; dispose of those yourself when you have accessed their volume/session features.
    /// </summary>
    public void Dispose()
    {
        List<MMNotificationClientComAdapter> toUnregister;
        lock (_deviceRegistrationsLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            toUnregister = new List<MMNotificationClientComAdapter>(_deviceRegistrations.Count);
            foreach (DeviceRegistration reg in _deviceRegistrations.Values)
            {
                toUnregister.Add(reg.Adapter);
            }

            _deviceRegistrations.Clear();
        }

        foreach (MMNotificationClientComAdapter adapter in toUnregister)
        {
            try
            {
                _enumerator.UnregisterEndpointNotificationCallback(adapter);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}