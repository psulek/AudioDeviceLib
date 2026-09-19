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
  The `_MMDeviceEnumerator` coclass and the endpoint-enumeration helpers in this file are an ALTERED
  version of the original `MMDeviceEnumerator` source by Ray Molenkamp and must not be
  misrepresented as being the original source code. Altered by Peter Šulek for AudioDeviceLib
  (https://github.com/psulek/AudioDeviceLib), starting from the copy bundled in
  AudioDeviceCmdlets (https://github.com/frgnca/AudioDeviceCmdlets, MIT).

  The changes are summarised in MODIFICATIONS.md at the repository root; the Git history of
  this file is the authoritative record.
*/

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
using System.Runtime.InteropServices;
using System.Threading;
using AudioDeviceLib.CoreAudioApi;
using AudioDeviceLib.CoreAudioApi.Interfaces;
using JetBrains.Annotations;

namespace AudioDeviceLib.Lib;

/// <summary>
/// High-level API over the Windows Core Audio endpoints. Create one instance and reuse it.
/// All members are Windows-only and must run on a thread able to use COM.
/// </summary>
[PublicAPI]
public sealed class AudioController : IDisposable
{
    // The default endpoint IDs for one data-flow direction, as they stood at the moment they were
    // read. Null in any slot means "no default endpoint of that kind", which is both what
    // TryGetDefaultId reports when none is set and what GetDeviceDefaults stores for the direction
    // the caller did not ask about.
    private sealed class DeviceDefaultIds
    {
        public string? DefaultPlaybackId;
        public string? DefaultRecordingId;
        public string? CommPlaybackId;
        public string? CommRecordingId;

        // Takes the ID rather than the device: AudioDevice snapshots its ID at construction, so
        // comparing strings here avoids two IMMDevice::GetId round trips per device.
        public bool IsDefault(string deviceId)
        {
            return SameEndpointId(deviceId, DefaultPlaybackId) || SameEndpointId(deviceId, DefaultRecordingId);
        }

        public bool IsDefaultComm(string deviceId)
        {
            return SameEndpointId(deviceId, CommPlaybackId) || SameEndpointId(deviceId, CommRecordingId);
        }
    }

    private static bool SameEndpointId(string? id1, string? id2)
    {
        return id1 != null && id2 != null && string.Equals(id1, id2, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Creates a controller over the machine's Core Audio endpoints.</summary>
    /// <exception cref="PlatformNotSupportedException">Thrown when not running on Windows.</exception>
    public AudioController()
    {
        // Checked here rather than lazily on first use: "this library does not run on this OS" is a
        // property of the process, not of whichever call happens to touch COM first.
        PlatformSupport.ThrowIfUnsupported();
    }

    private IMMDeviceEnumerator? _realEnumerator;

    // Maps each registered consumer to its registration entry (the COM adapter plus the cached
    // token). Holding the adapter keeps its CCW alive while registered (a GC'd sink would stop
    // notifications); caching the token means repeated registration of the same consumer hands back
    // the same IDisposable instead of allocating a new one each time.
    private readonly Dictionary<IAudioDeviceEvents, DeviceRegistration> _deviceRegistrations
        = new Dictionary<IAudioDeviceEvents, DeviceRegistration>(AudioDeviceEventsRefComparer.Instance);

    private readonly object _deviceRegistrationsLock = new object();
    private bool _disposed;

    // Count of calls currently holding the enumerator. Written only under _deviceRegistrationsLock;
    // Dispose waits for it to drain before releasing the RCW.
    private int _activeOperations;

    // How long Dispose will wait for in-flight calls before giving up on releasing the enumerator
    // deterministically. Long enough to cover a slow device transition, short enough that a wedged
    // audio stack cannot hang an application's shutdown.
    private static readonly TimeSpan DisposeDrainTimeout = TimeSpan.FromSeconds(5);

    // Pairs the COM sink adapter with the token handed to the caller, cached together so that
    // removing the entry on disposal invalidates the cache: the next Register then creates a fresh one.
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

    /// <summary>Returns the endpoints matching the given data-flow direction and state, in enumeration order.</summary>
    /// <param name="flow">
    /// Which endpoint directions to include: <see cref="DataFlowFilter.Render"/> (playback),
    /// <see cref="DataFlowFilter.Capture"/> (recording), or <see cref="DataFlowFilter.All"/> for both (the default).
    /// </param>
    /// <param name="state">
    /// A bit mask of endpoint states to include. Defaults to <see cref="DeviceStateFilter.Active"/>; combine
    /// flags or pass <see cref="DeviceStateFilter.All"/> to include disabled, not-present and unplugged endpoints too.
    /// </param>
    /// <returns>
    /// A read-only list of the matching <see cref="AudioDevice"/> endpoints. The list is empty if no endpoints match.
    /// </returns>
    /// <remarks>
    /// The caller owns the returned endpoints and should dispose of them. An endpoint that becomes
    /// unreadable while the list is being built - unplugged, or its driver torn down - is omitted
    /// rather than failing the whole call, so the result can be shorter than the number of endpoints
    /// the system reported.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when this controller has been disposed.</exception>
    public IReadOnlyList<AudioDevice> GetDevices(DataFlowFilter flow = DataFlowFilter.All,
        DeviceStateFilter state = DeviceStateFilter.Active)
    {
        return GetDevicesInternal(flow, state);
    }
    
    /// <summary>
    /// Returns the immutable <see cref="AudioDeviceInfo"/> snapshot of the given endpoint's identifying data.
    /// </summary>
    /// <param name="deviceId"> The ID of the endpoint to retrieve information for. </param>
    /// <returns>
    /// A snapshot carrying this device's information, safe to keep after this <see cref="AudioDevice"/> is disposed of.
    /// </returns>
    /// <exception cref="ArgumentNullException">If <paramref name="deviceId"/> is null or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="deviceId"/> is not a well-formed endpoint ID.</exception>
    /// <exception cref="COMException">Thrown when the ID is well-formed but no such endpoint exists.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this controller has been disposed.</exception>
    public AudioDeviceInfo GetDeviceInfo(string deviceId)
    {
        using var device = GetDeviceById(deviceId);
        return device.ToDeviceInfo();
    }

    /// <summary>
    /// Returns the endpoint with the given ID.
    /// </summary>
    /// <param name="deviceId"> The ID of the endpoint to return. </param>
    /// <returns> The endpoint with the given ID. </returns>
    /// <exception cref="ArgumentNullException"> If <paramref name="deviceId"/> is null or empty. </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="deviceId"/> is not a well-formed endpoint ID.</exception>
    /// <exception cref="COMException">Thrown when the ID is well-formed but no such endpoint exists.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this controller has been disposed.</exception>
    public AudioDevice GetDeviceById(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            throw new ArgumentNullException(nameof(deviceId));
        }

        ThrowIfDisposed();

        var device = new AudioDevice(GetEndpoint(deviceId), false, false);
        try
        {
            // Resolved first so only this endpoint's own direction needs a default lookup: scoping to
            // the device's flow halves the work against asking for both directions up front.
            var defaults = GetDeviceDefaults(
                device.Kind == AudioDeviceKind.Recording ? DataFlowFilter.Capture : DataFlowFilter.Render);

            device.IsDefault = defaults.IsDefault(device.Id);
            device.IsDefaultCommunication = defaults.IsDefaultComm(device.Id);
            return device;
        }
        catch
        {
            // The caller never received the device, so nothing else can dispose of it.
            device.Dispose();
            throw;
        }
    }


    private DeviceDefaultIds GetDeviceDefaults(DataFlowFilter flow)
    {
        var allowRender = flow == DataFlowFilter.Render || flow == DataFlowFilter.All;
        var allowCapture = flow == DataFlowFilter.Capture || flow == DataFlowFilter.All;
        return new DeviceDefaultIds
        {
            DefaultPlaybackId = allowRender ? TryGetDefaultId(DataFlow.Render, Role.Multimedia) : null,
            DefaultRecordingId = allowCapture ? TryGetDefaultId(DataFlow.Capture, Role.Multimedia) : null,
            CommPlaybackId = allowRender ? TryGetDefaultId(DataFlow.Render, Role.Communications) : null,
            CommRecordingId = allowCapture ? TryGetDefaultId(DataFlow.Capture, Role.Communications) : null,
        };
    }

    private IReadOnlyList<AudioDevice> GetDevicesInternal(DataFlowFilter flow, DeviceStateFilter state)
    {
        ThrowIfDisposed();

        var deviceDefaults = GetDeviceDefaults(flow);

        using var devices = EnumerateEndpoints(flow, state);
        var count = devices.Count;
        var result = new List<AudioDevice>(count);
        try
        {
            for (var i = 0; i < count; i++)
            {
                AudioDevice device;
                try
                {
                    device = devices[i];
                }
                catch (Exception ex) when (IsTransientEndpointFailure(ex))
                {
                    // and device can be unplugged or otherwise become unreadable between the time the enumerator reports it and
                    // the time the AudioDevice constructor tries to read its properties. Skip it rather than failing the whole call.
                    continue;
                }

                device.IsDefault = deviceDefaults.IsDefault(device.Id);
                device.IsDefaultCommunication = deviceDefaults.IsDefaultComm(device.Id);
                result.Add(device);
            }
        }
        catch
        {
            // Anything not recognised above is not a per-endpoint problem, so the list never
            // reaches the caller and the wrappers built so far are ours to clean up.
            foreach (var device in result)
            {
                try
                {
                    device.Dispose();
                }
                catch
                {
                    // Swallow any disposal failures: the caller never received the list, so nothing else can clean up these wrappers.
                    // The underlying COM objects are still alive and will be released when the enumerator is released.
                }
            }

            throw;
        }

        return result;

        // What "this one endpoint is no longer readable" looks like coming out of an AudioDevice
        // construction: a COM failure from any of its four interop calls, or the InvalidOperationException
        // ReadDataFlow raises when IMMEndpoint can no longer be queried off the device. Deliberately
        // narrow - anything else (out of memory, a disposed controller, a bug) is not a property of the
        // endpoint and must not be silently dropped. ObjectDisposedException is carved out explicitly
        // because it derives from InvalidOperationException and would otherwise be swallowed here.
        bool IsTransientEndpointFailure(Exception ex)
        {
            return !(ex is ObjectDisposedException) &&
                   (ex is COMException || ex is InvalidOperationException);
        }
    }

    /// <summary>Returns all active playback (render) endpoints.</summary>
    /// <param name="state">
    ///  A bit mask of endpoint states to include. Defaults to <see cref="DeviceStateFilter.Active"/>;
    /// </param>
    /// <returns>
    /// A read-only list of the active endpoints whose <see cref="AudioDevice.Kind"/> is
    /// <see cref="AudioDeviceKind.Playback"/>.
    /// </returns>
    public IReadOnlyList<AudioDevice> GetPlaybackDevices(DeviceStateFilter state = DeviceStateFilter.Active)
    {
        return GetDevices(DataFlowFilter.Render, state);
    }

    /// <summary>Returns all active recording (capture) endpoints.</summary>
    /// <param name="state">
    ///  A bit mask of endpoint states to include. Defaults to <see cref="DeviceStateFilter.Active"/>;
    /// </param>
    /// <returns>
    /// A read-only list of the active endpoints whose <see cref="AudioDevice.Kind"/> is
    /// <see cref="AudioDeviceKind.Recording"/>.
    /// </returns>
    public IReadOnlyList<AudioDevice> GetRecordingDevices(DeviceStateFilter state = DeviceStateFilter.Active)
    {
        return GetDevices(DataFlowFilter.Capture, state);
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

    /// <summary>Sets the given device as the default for the requested role(s).</summary>
    /// <param name="device">The endpoint to make default. Must not be <c>null</c>.</param>
    /// <param name="roles">
    /// The role(s) to assign. Defaults to <see cref="DefaultRole.Default"/> (multimedia + communications).
    /// Each set flag maps to one <c>Role</c> assignment.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="device"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="roles"/> specifies no role.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this controller has been disposed.</exception>
    public void SetDefaultDevice(AudioDevice device, DefaultRole roles = DefaultRole.Default)
    {
        if (device == null)
        {
            throw new ArgumentNullException(nameof(device));
        }

        ThrowIfDisposed();
        SetDefaultDeviceById(device.Id, roles);
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
    private void SetDefaultDeviceById(string deviceId, DefaultRole roles = DefaultRole.Default)
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
        ThrowIfDisposed();

        var role = communications ? Role.Communications : Role.Multimedia;
        AudioDevice device;
        try
        {
            // A COMException here is how Core Audio reports "no default endpoint for this
            // flow/role"; GetDefaultEndpoint never returns null.
            device = new AudioDevice(GetDefaultEndpoint(flow, role), false, false);
        }
        catch (ObjectDisposedException)
        {
            // Disposal is not "no default endpoint": returning null here would hide a disposed
            // controller behind the same answer a machine with no audio hardware gives.
            throw;
        }
        catch
        {
            return null;
        }

        try
        {
            // The endpoint just resolved is this flow's default for `role`, so only the other role
            // still needs a lookup.
            var otherRole = communications ? Role.Multimedia : Role.Communications;
            var otherId = TryGetDefaultId(flow, otherRole);

            // The endpoint was resolved by `role`, so it is that role's default by construction;
            // only the other role needs comparing.
            var isOtherRoleDefault = SameEndpointId(device.Id, otherId);
            device.IsDefault = communications ? isOtherRoleDefault : true;
            device.IsDefaultCommunication = communications ? true : isOtherRoleDefault;
            return device;
        }
        catch
        {
            device.Dispose();
            throw;
        }
    }

    private string TryGetDefaultId(DataFlow flow, Role role)
    {
        IMMDevice endpoint = null;
        try
        {
            endpoint = GetDefaultEndpoint(flow, role);
            Marshal.ThrowExceptionForHR(endpoint.GetId(out var id));
            return id;
        }
        catch (ObjectDisposedException)
        {
            // Disposal is not "no default endpoint": swallowing it here would let a disposed
            // controller report every device as non-default instead of failing.
            throw;
        }
        catch
        {
            // No default endpoint of this kind/role is set.
            return null;
        }
        finally
        {
            // This endpoint exists only to carry an ID out; nothing else ever sees it, so releasing
            // it here is safe and keeps four of these per enumeration off the finalizer queue.
            ReleaseComObject(endpoint);
        }
    }

    // Test seam: Dispose clears the enumerator last, so a non-null field afterwards means something
    // re-created it on a disposed controller. ControllerDisposeRaceTests asserts against this.
    internal bool HasLiveEnumerator => _realEnumerator != null;

    // Test seam: stands in for a COM call that never returns, so the bounded drain in Dispose can be
    // exercised without needing an audio device that actually wedges.
    internal IDisposable AcquireEnumeratorForTest() => AcquireEnumerator();

    // Creates the enumerator on first use. Callers hold _deviceRegistrationsLock: both the disposed
    // check and the assignment have to be atomic with respect to Dispose, or a caller that checked
    // first can create one after Dispose has already released and cleared the field.
    private IMMDeviceEnumerator EnumeratorCore
    {
        get
        {
            if (_realEnumerator == null)
            {
                _realEnumerator = new _MMDeviceEnumerator() as IMMDeviceEnumerator;
            }

            return _realEnumerator;
        }
    }

    // Hands out the enumerator for the duration of one operation and counts the operation as in
    // flight. Dispose waits for the count to reach zero before releasing the RCW, so a call that
    // has already started cannot have the object torn out from under it - Marshal.ReleaseComObject
    // disconnects the wrapper immediately, and every later use of it throws InvalidComObjectException.
    private EnumeratorLease AcquireEnumerator()
    {
        lock (_deviceRegistrationsLock)
        {
            ThrowIfDisposed();
            var enumerator = EnumeratorCore;
            _activeOperations++;
            return new EnumeratorLease(this, enumerator);
        }
    }

    private void ReleaseEnumerator()
    {
        lock (_deviceRegistrationsLock)
        {
            if (--_activeOperations == 0)
            {
                Monitor.PulseAll(_deviceRegistrationsLock);
            }
        }
    }

    // Scopes one enumerator borrow. A struct so the common path costs nothing to allocate.
    private readonly struct EnumeratorLease : IDisposable
    {
        private readonly AudioController _owner;

        internal IMMDeviceEnumerator Enumerator { get; }

        internal EnumeratorLease(AudioController owner, IMMDeviceEnumerator enumerator)
        {
            _owner = owner;
            Enumerator = enumerator;
        }

        public void Dispose()
        {
            _owner?.ReleaseEnumerator();
        }
    }

    private MMDeviceCollection EnumerateEndpoints(DataFlowFilter dataFlow, DeviceStateFilter stateMask)
    {
        using var lease = AcquireEnumerator();
        Marshal.ThrowExceptionForHR(lease.Enumerator.EnumAudioEndpoints(dataFlow, stateMask, out var result));
        return new MMDeviceCollection(result);
    }

    private IMMDevice GetDefaultEndpoint(DataFlow dataFlow, Role role)
    {
        using var lease = AcquireEnumerator();
        Marshal.ThrowExceptionForHR(lease.Enumerator.GetDefaultAudioEndpoint(dataFlow, role, out var endpoint));
        return endpoint;
    }

    private IMMDevice GetEndpoint(string deviceId)
    {
        using var lease = AcquireEnumerator();
        Marshal.ThrowExceptionForHR(lease.Enumerator.GetDevice(deviceId, out var endpoint));
        return endpoint;
    }

    // Deterministic release is only ever applied to COM objects that never leave the library, so a
    // consumer can never be holding one of these. See AudioDevice.Dispose for why the device's own
    // IMMDevice is deliberately left to the GC instead.
    private static void ReleaseComObject(object comObject)
    {
        try
        {
            if (comObject != null && Marshal.IsComObject(comObject))
            {
                Marshal.ReleaseComObject(comObject);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AudioController));
        }
    }

    private static T WithDefaultPlayback<T>(Func<AudioDevice, T> action)
    {
        using (var controller = new AudioController())
        using (var device = controller.GetDefaultPlaybackDevice())
        {
            if (device == null)
            {
                throw new InvalidOperationException("No default playback device is set.");
            }

            return action(device);
        }
    }

    private static T WithDevice<T>(string deviceId, Func<AudioDevice, T> action)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            throw new ArgumentNullException(nameof(deviceId));
        }

        using (var controller = new AudioController())
        using (var device = controller.GetDeviceById(deviceId))
        {
            if (device == null)
            {
                throw new ArgumentException("No device found with ID: " + deviceId, nameof(deviceId));
            }

            return action(device);
        }
    }

    // Resolves the first endpoint of the given kind whose name contains `name`, sets it as the
    // default for `roles`, then re-resolves it so the returned snapshot carries the updated flags.
    private static AudioDeviceInfo SetDefaultByName(string name, AudioDeviceKind kind, DefaultRole roles)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentNullException(nameof(name));
        }

        using var controller = new AudioController();
        var scope = kind == AudioDeviceKind.Recording
            ? controller.GetRecordingDevices()
            : controller.GetPlaybackDevices();

        try
        {
            var match = scope.FirstOrDefault(d =>
                d.Name != null && d.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);

            if (match == null)
            {
                return null;
            }

            controller.SetDefaultDevice(match, roles);

            // Built from the snapshot already in hand, with the roles just applied folded in.
            // Re-resolving the device would mean a second full default lookup and a rebuilt
            // wrapper, for information this method already knows.
            // IsDefault tracks the Multimedia role specifically (see GetDeviceDefaults), so
            // assigning Console alone must not set it.
            match.IsDefault |= (roles & DefaultRole.Multimedia) != 0;
            match.IsDefaultCommunication |= (roles & DefaultRole.Communications) != 0;
            return match.ToDeviceInfo();
        }
        finally
        {
            foreach (var device in scope)
            {
                device.Dispose();
            }
        }
    }

    /// <summary>Returns a snapshot of the current default playback device.</summary>
    /// <param name="communications">
    /// When <c>true</c>, resolves the default for the communications role; when <c>false</c> (the
    /// default), resolves the default for the multimedia role.
    /// </param>
    /// <returns>An immutable snapshot of the default playback endpoint.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no default playback device is set.</exception>
    /// <remarks>
    /// Creates and disposes an <see cref="AudioController"/> per call. For repeated operations,
    /// create one instance and reuse it.
    /// </remarks>
    public static AudioDeviceInfo GetDefaultPlayback(bool communications = false)
    {
        using (var controller = new AudioController())
        using (var device = controller.GetDefaultPlaybackDevice(communications))
        {
            if (device == null)
            {
                throw new InvalidOperationException("No default playback device is set.");
            }

            return device.ToDeviceInfo();
        }
    }

    /// <summary>Returns a snapshot of the current default recording device.</summary>
    /// <param name="communications">
    /// When <c>true</c>, resolves the default for the communications role; when <c>false</c> (the
    /// default), resolves the default for the multimedia role.
    /// </param>
    /// <returns>An immutable snapshot of the default recording endpoint.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no default recording device is set.</exception>
    /// <remarks>
    /// Creates and disposes an <see cref="AudioController"/> per call. For repeated operations,
    /// create one instance and reuse it.
    /// </remarks>
    public static AudioDeviceInfo GetDefaultRecording(bool communications = false)
    {
        using (var controller = new AudioController())
        using (var device = controller.GetDefaultRecordingDevice(communications))
        {
            if (device == null)
            {
                throw new InvalidOperationException("No default recording device is set.");
            }

            return device.ToDeviceInfo();
        }
    }

    /// <summary>Sets the endpoint with the given ID as the default for the requested role(s).</summary>
    /// <param name="deviceId">The ID of the endpoint to make default.</param>
    /// <param name="roles">
    /// The role(s) to assign. Defaults to <see cref="DefaultRole.Default"/> (multimedia + communications).
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="deviceId"/> is null or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="roles"/> specifies no role.</exception>
    /// <remarks>
    /// Creates and disposes an <see cref="AudioController"/> per call. For repeated operations,
    /// create one instance and reuse it.
    /// </remarks>
    public static void SetDefaultDevice(string deviceId, DefaultRole roles = DefaultRole.Default)
    {
        using (var controller = new AudioController())
        {
            controller.SetDefaultDeviceById(deviceId, roles);
        }
    }

    /// <summary>
    /// Sets the first playback endpoint whose name contains <paramref name="name"/> as the default
    /// for the requested role(s). Matching is case-insensitive and takes the first match.
    /// </summary>
    /// <param name="name">A substring of the endpoint's friendly name, e.g. "Speakers".</param>
    /// <param name="roles">
    /// The role(s) to assign. Defaults to <see cref="DefaultRole.Default"/> (multimedia + communications).
    /// </param>
    /// <returns>
    /// A snapshot of the endpoint that was made default, or <c>null</c> if no playback endpoint
    /// matched <paramref name="name"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="roles"/> specifies no role.</exception>
    /// <remarks>
    /// Creates and disposes an <see cref="AudioController"/> per call. For repeated operations,
    /// create one instance and reuse it.
    /// </remarks>
    public static AudioDeviceInfo SetDefaultPlaybackByName(string name, DefaultRole roles = DefaultRole.Default)
    {
        return SetDefaultByName(name, AudioDeviceKind.Playback, roles);
    }

    /// <summary>
    /// Sets the first recording endpoint whose name contains <paramref name="name"/> as the default
    /// for the requested role(s). Matching is case-insensitive and takes the first match.
    /// </summary>
    /// <param name="name">A substring of the endpoint's friendly name, e.g. "Microphone".</param>
    /// <param name="roles">
    /// The role(s) to assign. Defaults to <see cref="DefaultRole.Default"/> (multimedia + communications).
    /// </param>
    /// <returns>
    /// A snapshot of the endpoint that was made default, or <c>null</c> if no recording endpoint
    /// matched <paramref name="name"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="roles"/> specifies no role.</exception>
    /// <remarks>
    /// Creates and disposes an <see cref="AudioController"/> per call. For repeated operations,
    /// create one instance and reuse it.
    /// </remarks>
    public static AudioDeviceInfo SetDefaultRecordingByName(string name, DefaultRole roles = DefaultRole.Default)
    {
        return SetDefaultByName(name, AudioDeviceKind.Recording, roles);
    }

    /// <summary>Returns the master volume of the default playback device, as a percentage in 0..100.</summary>
    /// <returns>The current master volume between 0 and 100.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no default playback device is set.</exception>
    /// <remarks>
    /// Creates and disposes an <see cref="AudioController"/> per call. For repeated operations,
    /// create one instance and reuse it.
    /// </remarks>
    public static float GetVolume()
    {
        return WithDefaultPlayback(d => d.GetVolumePercent());
    }

    /// <summary>Returns the master volume of the given endpoint, as a percentage in 0..100.</summary>
    /// <param name="deviceId">The ID of the endpoint to read.</param>
    /// <returns>The current master volume between 0 and 100.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="deviceId"/> is null or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="deviceId"/> is not a well-formed endpoint ID.</exception>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when no endpoint has that ID.</exception>
    /// <remarks>
    /// Creates and disposes an <see cref="AudioController"/> per call. For repeated operations,
    /// create one instance and reuse it.
    /// </remarks>
    public static float GetVolume(string deviceId)
    {
        return WithDevice(deviceId, d => d.GetVolumePercent());
    }

    /// <summary>Sets the master volume of the default playback device from a percentage in 0..100.</summary>
    /// <param name="percent">The desired volume. Values outside 0..100 are clamped.</param>
    /// <exception cref="InvalidOperationException">Thrown when no default playback device is set.</exception>
    /// <remarks>
    /// Creates and disposes an <see cref="AudioController"/> per call. For repeated operations,
    /// create one instance and reuse it.
    /// </remarks>
    public static void SetVolume(float percent)
    {
        WithDefaultPlayback<object>(d =>
        {
            d.SetVolumePercent(percent);
            return null;
        });
    }

    /// <summary>Sets the master volume of the given endpoint from a percentage in 0..100.</summary>
    /// <param name="deviceId">The ID of the endpoint to change.</param>
    /// <param name="percent">The desired volume. Values outside 0..100 are clamped.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="deviceId"/> is null or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="deviceId"/> is not a well-formed endpoint ID.</exception>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when no endpoint has that ID.</exception>
    /// <remarks>
    /// Creates and disposes an <see cref="AudioController"/> per call. For repeated operations,
    /// create one instance and reuse it.
    /// </remarks>
    public static void SetVolume(string deviceId, float percent)
    {
        WithDevice<object>(deviceId, d =>
        {
            d.SetVolumePercent(percent);
            return null;
        });
    }

    /// <summary>Returns whether the default playback device is muted.</summary>
    /// <returns><c>true</c> when the endpoint is muted.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no default playback device is set.</exception>
    /// <remarks>
    /// Creates and disposes an <see cref="AudioController"/> per call. For repeated operations,
    /// create one instance and reuse it.
    /// </remarks>
    public static bool IsMuted()
    {
        return WithDefaultPlayback(d => d.IsMuted);
    }

    /// <summary>Returns whether the given endpoint is muted.</summary>
    /// <param name="deviceId">The ID of the endpoint to read.</param>
    /// <returns><c>true</c> when the endpoint is muted.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="deviceId"/> is null or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="deviceId"/> is not a well-formed endpoint ID.</exception>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when no endpoint has that ID.</exception>
    /// <remarks>
    /// Creates and disposes an <see cref="AudioController"/> per call. For repeated operations,
    /// create one instance and reuse it.
    /// </remarks>
    public static bool IsMuted(string deviceId)
    {
        return WithDevice(deviceId, d => d.IsMuted);
    }

    /// <summary>Sets the mute state of the default playback device.</summary>
    /// <param name="mute"><c>true</c> to mute, <c>false</c> to unmute.</param>
    /// <exception cref="InvalidOperationException">Thrown when no default playback device is set.</exception>
    /// <remarks>
    /// Creates and disposes an <see cref="AudioController"/> per call. For repeated operations,
    /// create one instance and reuse it.
    /// </remarks>
    public static void SetMute(bool mute)
    {
        WithDefaultPlayback<object>(d =>
        {
            d.IsMuted = mute;
            return null;
        });
    }

    /// <summary>Sets the mute state of the given endpoint.</summary>
    /// <param name="deviceId">The ID of the endpoint to change.</param>
    /// <param name="mute"><c>true</c> to mute, <c>false</c> to unmute.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="deviceId"/> is null or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="deviceId"/> is not a well-formed endpoint ID.</exception>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when no endpoint has that ID.</exception>
    /// <remarks>
    /// Creates and disposes an <see cref="AudioController"/> per call. For repeated operations,
    /// create one instance and reuse it.
    /// </remarks>
    public static void SetMute(string deviceId, bool mute)
    {
        WithDevice<object>(deviceId, d =>
        {
            d.IsMuted = mute;
            return null;
        });
    }

    /// <summary>Inverts the mute state of the default playback device.</summary>
    /// <returns>The resulting mute state: <c>true</c> when the endpoint is now muted.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no default playback device is set.</exception>
    /// <remarks>
    /// Creates and disposes an <see cref="AudioController"/> per call. For repeated operations,
    /// create one instance and reuse it.
    /// </remarks>
    public static bool ToggleMute()
    {
        return WithDefaultPlayback(d => d.ToggleMute());
    }

    /// <summary>Inverts the mute state of the given endpoint.</summary>
    /// <param name="deviceId">The ID of the endpoint to change.</param>
    /// <returns>The resulting mute state: <c>true</c> when the endpoint is now muted.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="deviceId"/> is null or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="deviceId"/> is not a well-formed endpoint ID.</exception>
    /// <exception cref="System.Runtime.InteropServices.COMException">Thrown when no endpoint has that ID.</exception>
    /// <remarks>
    /// Creates and disposes an <see cref="AudioController"/> per call. For repeated operations,
    /// create one instance and reuse it.
    /// </remarks>
    public static bool ToggleMute(string deviceId)
    {
        return WithDevice(deviceId, d => d.ToggleMute());
    }

    /// <summary>Returns snapshots of all active endpoints, in enumeration order.</summary>
    /// <returns>A read-only list of snapshots of the active playback and recording endpoints.</returns>
    /// <remarks>
    /// Returns active endpoints only. Use an <see cref="AudioController"/> instance and
    /// <see cref="GetDevices"/> to include disabled, not-present or unplugged endpoints.
    /// Creates and disposes an <see cref="AudioController"/> per call. For repeated operations,
    /// create one instance and reuse it.
    /// </remarks>
    public static IReadOnlyList<AudioDeviceInfo> ListDevices()
    {
        using (var controller = new AudioController())
        {
            return Snapshot(controller.GetDevices());
        }
    }

    /// <summary>Returns snapshots of the active endpoints of the given kind, in enumeration order.</summary>
    /// <param name="kind">Whether to list playback or recording endpoints.</param>
    /// <returns>A read-only list of snapshots of the matching active endpoints.</returns>
    /// <remarks>
    /// Returns active endpoints only. Use an <see cref="AudioController"/> instance and
    /// <see cref="GetDevices"/> to include disabled, not-present or unplugged endpoints.
    /// Creates and disposes an <see cref="AudioController"/> per call. For repeated operations,
    /// create one instance and reuse it.
    /// </remarks>
    public static IReadOnlyList<AudioDeviceInfo> ListDevices(AudioDeviceKind kind)
    {
        using (var controller = new AudioController())
        {
            return Snapshot(kind == AudioDeviceKind.Recording
                ? controller.GetRecordingDevices()
                : controller.GetPlaybackDevices());
        }
    }

    private static IReadOnlyList<AudioDeviceInfo> Snapshot(IReadOnlyList<AudioDevice> devices)
    {
        var result = new List<AudioDeviceInfo>(devices.Count);
        foreach (var device in devices)
        {
            using (device)
            {
                result.Add(device.ToDeviceInfo());
            }
        }

        return result;
    }

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
            // Serialize native registration with disposal; publish only after success.
            Marshal.ThrowExceptionForHR(EnumeratorCore.RegisterEndpointNotificationCallback(adapter));
            _deviceRegistrations[consumer] = new DeviceRegistration(adapter, token);
            return token;
        }
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

            Marshal.ThrowExceptionForHR(EnumeratorCore.UnregisterEndpointNotificationCallback(adapter));
            _deviceRegistrations.Remove(adapter.Target);
        }
    }

    /// <summary>
    /// Releases resources held by the controller, unregistering any remaining device-notification
    /// callbacks. The controller does not own the <see cref="AudioDevice"/> instances returned by its
    /// methods; dispose of those yourself when you have accessed their volume/session features.
    /// </summary>
    /// <remarks>
    /// Waits up to five seconds for calls already in progress on other threads to finish, because
    /// releasing the underlying COM enumerator while one is running would disconnect it mid-call.
    /// If they have not finished by then the enumerator is left to the garbage collector rather than
    /// blocking any longer. Calls that start after this returns throw
    /// <see cref="ObjectDisposedException"/>.
    /// </remarks>
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
            foreach (var reg in _deviceRegistrations.Values)
            {
                toUnregister.Add(reg.Adapter);
            }

            _deviceRegistrations.Clear();
        }

        foreach (var adapter in toUnregister)
        {
            try
            {
                // Read the field, not EnumeratorCore: if nothing ever enumerated there is no
                // enumerator to create just to unregister nothing.
                _realEnumerator?.UnregisterEndpointNotificationCallback(adapter);
            }
            catch
            {
                // best-effort cleanup
            }
        }

        lock (_deviceRegistrationsLock)
        {
            // Calls that started before _disposed was set are still holding the enumerator. Releasing
            // it now would disconnect the RCW underneath them, so wait for the last one to finish;
            // no new lease can be taken, because AcquireEnumerator checks _disposed under this lock.
            //
            // BOUNDED, because the thing being waited on is a COM call into the audio stack, and a
            // wedged endpoint or a hung audiodg.exe can make one of those never return. Dispose must
            // not be able to hang the caller - it can be reached from a finalizer or a shutdown path,
            // where blocking forever is far worse than leaking one RCW. On timeout the wrapper is left
            // to the GC, which is exactly what AudioDevice already does with its own IMMDevice.
            var drained = true;
            while (_activeOperations > 0 && drained)
            {
                drained = Monitor.Wait(_deviceRegistrationsLock, DisposeDrainTimeout);
            }

            if (_activeOperations == 0)
            {
                // Released under the lock so that nothing can observe, and re-create, a null field.
                ReleaseComObject(_realEnumerator);
            }

            _realEnumerator = null;
        }
    }
}

// The Core Audio MMDeviceEnumerator coclass. Internal because on its own it is no good: it exists
// only to be cast to IMMDeviceEnumerator.
[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class _MMDeviceEnumerator
{
}