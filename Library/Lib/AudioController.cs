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
  The endpoint-enumeration helpers in this file are an ALTERED version of the original
  `MMDeviceEnumerator` source by Ray Molenkamp and must not be misrepresented as being the
  original source code. Altered by Peter Šulek for AudioDeviceLib
  (https://github.com/psulek/AudioDeviceLib), starting from the copy bundled in
  AudioDeviceCmdlets (https://github.com/frgnca/AudioDeviceCmdlets, MIT).

  The changes are summarized in MODIFICATIONS.md at the repository root; the Git history of
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
using static AudioDeviceLib.CoreAudioApi.InteropUtils;

namespace AudioDeviceLib;

/// <summary>
/// High-level API over the Windows Core Audio endpoints. Create one instance and reuse it.
/// All members are Windows-only and must run on a thread able to use COM.
/// </summary>
[PublicAPI]
public sealed class AudioController : IDisposable
{
    private const string EnumeratorUnsupportedMessage =
        "Core Audio is not available on this system: the MMDeviceEnumerator COM class (CLSID " +
        "bcde0395-e52f-467c-8e3d-c4579291692e) does not expose IMMDeviceEnumerator. This interface " +
        "has been part of Windows since Vista, so this usually indicates a damaged or heavily " +
        "customised audio stack rather than an unsupported Windows version.";

    private const string EnumeratorNotRegisteredMessage =
        "Core Audio is not available on this system: the MMDeviceEnumerator COM class (CLSID " +
        "bcde0395-e52f-467c-8e3d-c4579291692e) is not registered. This class has been part of " +
        "Windows since Vista, so this usually indicates a damaged audio stack or a Windows " +
        "installation with the audio components stripped out.";

    private const string EnumeratorNotInitializedMessage =
        "COM has not been initialised on this thread, so the MMDeviceEnumerator COM class (CLSID " +
        "bcde0395-e52f-467c-8e3d-c4579291692e) could not be activated. Call CoInitializeEx, or run " +
        "on a thread marked [STAThread] or [MTAThread], before using AudioController.";

    private const string EnumeratorActivationFailedFormat =
        "Core Audio is not available on this system: activating the MMDeviceEnumerator COM class " +
        "(CLSID bcde0395-e52f-467c-8e3d-c4579291692e) failed with HRESULT {0}. The inner exception " +
        "carries the underlying COM failure.";

    private IMMDeviceEnumeratorCOM? _deviceEnumerator;

    // Lazily created to avoid failing when policy-config COM is unavailable, then cached for subsequent SetDefaultDevice calls.
    private PolicyConfigClient? _policyClient;
    
    // Maps each consumer to its registration, keeping the COM adapter alive and reusing the cached IDisposable token across repeated registrations.
    private readonly Dictionary<IAudioDeviceEvents, DeviceRegistration> _deviceRegistrations
        = new Dictionary<IAudioDeviceEvents, DeviceRegistration>(AudioDeviceEventsRefComparer.Instance);

    private readonly object _notificationLock = new object();
    private readonly object _deviceRegistrationsLock = new object();

    // Volatile ensures disposal is visible to lock-free reads in ThrowIfDisposed while writes remain protected by _deviceRegistrationsLock.
    private volatile bool _disposed;

    // Tracks active enumerator operations so Dispose can wait for them to complete before releasing the RCW.
    private int _activeOperations;
    
    // Maximum time Dispose waits for active operations to complete before giving up on deterministic enumerator release.
    private static readonly TimeSpan DisposeDrainTimeout = TimeSpan.FromSeconds(5);

    // CLSID_MMDeviceEnumerator and IID_IMMDeviceEnumerator.
    private static readonly Guid MMDeviceEnumeratorClsid = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid MMDeviceEnumeratorIid = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");

    /// <summary>Creates a controller over the machine's Core Audio endpoints.</summary>
    /// <exception cref="PlatformNotSupportedException">Thrown when not running on Windows.</exception>
    public AudioController()
    {
        // Checked here rather than lazily on first use: "this library does not run on this OS" is a
        // property of the process, not of whichever call happens to touch COM first.
        PlatformSupport.ThrowIfUnsupported();
    }

    private static bool SameEndpointId(string? id1, string? id2)
    {
        return id1 != null && id2 != null && string.Equals(id1, id2, StringComparison.OrdinalIgnoreCase);
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
    /// Returns the immutable <see cref="AudioDeviceInfo"/> snapshot of the given endpoint's identifying data,
    /// or <c>null</c> if no endpoint with that ID is present on the system.
    /// </summary>
    /// <param name="deviceId"> The ID of the endpoint to retrieve information for. </param>
    /// <returns>
    /// A snapshot carrying this device's information, safe to keep after this <see cref="AudioDevice"/> is disposed of,
    /// or <c>null</c> if the ID matches no endpoint currently on the system. See
    /// <see cref="GetDeviceById"/> for why absence is an answer rather than an error.
    /// </returns>
    /// <exception cref="ArgumentNullException">If <paramref name="deviceId"/> is null or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="deviceId"/> is not a well-formed endpoint ID.</exception>
    /// <exception cref="COMException">Thrown when the endpoint exists but could not be read.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this controller has been disposed.</exception>
    public AudioDeviceInfo? GetDeviceInfo(string deviceId)
    {
        using var device = GetDeviceById(deviceId);
        return device?.ToDeviceInfo();
    }

    /// <summary>
    /// Returns the endpoint with the given ID, or <c>null</c> if no such endpoint is present on the system.
    /// </summary>
    /// <param name="deviceId"> The ID of the endpoint to return. </param>
    /// <returns>
    /// The endpoint with the given ID, or <c>null</c> if the ID matches no endpoint currently on the
    /// system. An ID obtained from <see cref="GetDevices"/> can stop resolving at any time - the
    /// endpoint may be unplugged or disabled between the two calls - so a null result is an expected
    /// outcome of that race, not a failure. Anything that is genuinely wrong still throws.
    /// </returns>
    /// <exception cref="ArgumentNullException"> If <paramref name="deviceId"/> is null or empty. </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="deviceId"/> is not a well-formed endpoint ID.</exception>
    /// <exception cref="COMException">Thrown when the endpoint exists but could not be read - for
    /// example when the audio service is stopping. Only Core Audio's
    /// <c>HRESULT_FROM_WIN32(ERROR_NOT_FOUND)</c> (0x80070490) becomes a null result; every other
    /// HRESULT is reported.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this controller has been disposed.</exception>
    public AudioDevice? GetDeviceById(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            throw new ArgumentNullException(nameof(deviceId));
        }

        ThrowIfDisposed();

        IMMDeviceCOM endpoint;
        try
        {
            endpoint = GetEndpoint(deviceId);
        }
        catch (Exception ex) when (IsEndpointNotFound(ex))
        {
            // The endpoint may have been unplugged or disabled since enumeration, so treat a missing device as unavailable.
            return null;
        }

        var device = new AudioDevice(endpoint, false, false);
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
        catch (Exception cleanupException)
        {
            ReportFailure(cleanupException);
            // The endpoint exists but initialization failed, so dispose it before propagating the failure.
            device.Dispose();
            throw;
        }
    }

    private DeviceDefaultIds GetDeviceDefaults(DataFlowFilter flow)
    {
        using var lease = AcquireEnumerator();
        var allowRender = flow == DataFlowFilter.Render || flow == DataFlowFilter.All;
        var allowCapture = flow == DataFlowFilter.Capture || flow == DataFlowFilter.All;
        return new DeviceDefaultIds
        {
            DefaultPlaybackId = allowRender ? TryGetDefaultId(lease.Enumerator, DataFlow.Render, Role.Multimedia) : null,
            DefaultRecordingId = allowCapture ? TryGetDefaultId(lease.Enumerator, DataFlow.Capture, Role.Multimedia) : null,
            CommPlaybackId = allowRender ? TryGetDefaultId(lease.Enumerator, DataFlow.Render, Role.Communications) : null,
            CommRecordingId = allowCapture ? TryGetDefaultId(lease.Enumerator, DataFlow.Capture, Role.Communications) : null,
        };
    }

    private List<AudioDevice> GetDevicesInternal(DataFlowFilter flow, DeviceStateFilter state)
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
        catch (Exception cleanupException)
        {
            ReportFailure(cleanupException);
            
            // Anything not recognized above is not a per-endpoint problem, so the list never
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
    public AudioDevice? GetDefaultPlaybackDevice(bool communications = false)
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
    public AudioDevice? GetDefaultRecordingDevice(bool communications = false)
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
    /// <exception cref="NotSupportedException">Thrown when this system does not expose the undocumented
    /// policy-config API that changing the default endpoint requires.</exception>
    /// <remarks>Roles are applied sequentially (console, multimedia, communications). If a later
    /// assignment fails, earlier assignments remain in effect; no rollback is attempted. The cached
    /// endpoint ID can be used after its wrapper is disposed and across controllers. Windows validates
    /// whether that ID still exists when applying each role.</remarks>
    public void SetDefaultDevice(AudioDevice device, DefaultRole roles = DefaultRole.Default)
    {
        RequireNotNull(device, nameof(device));

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
    /// <exception cref="NotSupportedException">Thrown when this system does not expose the undocumented
    /// policy-config API that changing the default endpoint requires. Thrown before any role is
    /// applied. Later failures can leave earlier role assignments in effect.</exception>
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

        var client = AcquirePolicyClient();
        try
        {
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
        finally
        {
            ReleaseOperation();
        }
    }

    // Returns the cached policy client and marks the operation active to prevent disposal during use.
    // Client activation occurs under the lock before incrementing the operation count, so failed
    // activation requires no operation cleanup.
    private PolicyConfigClient AcquirePolicyClient()
    {
        lock (_deviceRegistrationsLock)
        {
            ThrowIfDisposed();
            _policyClient ??= new PolicyConfigClient();
            _activeOperations++;
            return _policyClient;
        }
    }

    private AudioDevice? GetDefault(DataFlow flow, bool communications)
    {
        ThrowIfDisposed();

        var role = communications ? Role.Communications : Role.Multimedia;

        // Keep lookup separate because only endpoint-not-found should mean "no default device".
        IMMDeviceCOM endpoint;
        try
        {
            endpoint = GetDefaultEndpoint(flow, role);
        }
        catch (Exception ex) when (IsEndpointNotFound(ex))
        {
            // No endpoint is assigned to this flow/role.
            return null;
        }

        AudioDevice device;
        try
        {
            device = new AudioDevice(endpoint, false, false);
        }
        catch (Exception ex) when (IsTransientEndpointFailure(ex))
        {
            // The default endpoint became unavailable before it could be initialized.
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
            device.IsDefault = !communications || isOtherRoleDefault;
            device.IsDefaultCommunication = communications || isOtherRoleDefault;
            return device;
        }
        catch (Exception cleanupException)
        {
            ReportFailure(cleanupException);
            device.Dispose();
            throw;
        }
    }

    private string? TryGetDefaultId(DataFlow flow, Role role)
    {
        using var lease = AcquireEnumerator();
        return TryGetDefaultId(lease.Enumerator, flow, role);
    }

    private static string? TryGetDefaultId(IMMDeviceEnumeratorCOM enumerator, DataFlow flow, Role role)
    {
        // Keep lookup separate because only endpoint-not-found should mean "no default device".
        IMMDeviceCOM endpoint;
        try
        {
            ThrowIfFailed(enumerator.GetDefaultAudioEndpoint(flow, role, out endpoint));
        }
        catch (Exception ex) when (IsEndpointNotFound(ex))
        {
            // No endpoint exists for this flow/role.
            return null;
        }

        try
        {
            ThrowIfFailed(endpoint.GetId(out var id));
            return id;
        }
        catch (Exception ex) when (IsTransientEndpointFailure(ex))
        {
            // The endpoint became unavailable before its ID could be read.
            return null;
        }
        finally
        {
            // Release the temporary endpoint used only to read its ID.
            ReleaseComObject(endpoint);
        }
    }

    // E_NOTFOUND: Core Audio uses this when no endpoint exists for a flow/role or device ID.
    internal const int HresultNotFound = unchecked((int)0x80070490);

    // Internal to allow testing the error classification without requiring specific failing hardware.
    internal static bool IsEndpointNotFound(Exception ex)
    {
        return ex is COMException { ErrorCode: HresultNotFound };
    }

    // Identifies endpoint-specific failures that may occur when a device becomes unavailable mid-operation.
    // Excludes ObjectDisposedException and other failures that indicate controller state or programming errors.
    // Internal to allow direct testing without requiring failing hardware.
    internal static bool IsTransientEndpointFailure(Exception ex)
    {
        return !(ex is ObjectDisposedException) &&
               ex is COMException or InvalidOperationException;
    }

    // Lazily creates the enumerator under the lock to prevent recreation after disposal.
    private IMMDeviceEnumeratorCOM EnumeratorCore => _deviceEnumerator ??= CreateEnumerator();
    
    // Activates MMDeviceEnumerator directly by IID, avoiding coclass casts that can fail when another library created the singleton RCW first.
    // Direct activation also preserves the HRESULT for error reporting.
    private static IMMDeviceEnumeratorCOM CreateEnumerator()
    {
        var clsid = MMDeviceEnumeratorClsid;
        var iid = MMDeviceEnumeratorIid;

        var hr = CoCreateInstance(ref clsid, IntPtr.Zero, CLSCTX_ALL, ref iid, out var instance);

        if (HrSuccess(hr) && instance is IMMDeviceEnumeratorCOM enumerator)
        {
            return enumerator;
        }

        // Release an unexpected successful activation result because it cannot be returned safely.
        ReleaseComObject(instance);

        // Report activation failures as unsupported while preserving the original COM error.
        throw new NotSupportedException(EnumeratorFailureMessage(hr), Marshal.GetExceptionForHR(hr, new IntPtr(-1)));
    }

    private static string EnumeratorFailureMessage(int hr) => hr switch
    {
        E_NOINTERFACE => EnumeratorUnsupportedMessage,
        REGDB_E_CLASSNOTREG => EnumeratorNotRegisteredMessage,
        CO_E_NOTINITIALIZED => EnumeratorNotInitializedMessage,
        _ => $"Core Audio activation failed with HRESULT 0x{hr:X8}. See the inner exception."
    };

    // Marks an enumerator operation as active so Dispose cannot release the RCW while it is in use.
    // Releasing the RCW mid-call would disconnect the wrapper and cause subsequent COM calls to fail.
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

    private void ReleaseOperation()
    {
        lock (_deviceRegistrationsLock)
        {
            if (--_activeOperations == 0)
            {
                Monitor.PulseAll(_deviceRegistrationsLock);
            }
        }
    }
    
    private MMDeviceCollection EnumerateEndpoints(DataFlowFilter dataFlow, DeviceStateFilter stateMask)
    {
        using var lease = AcquireEnumerator();
        ThrowIfFailed(lease.Enumerator.EnumAudioEndpoints(dataFlow, stateMask, out var result));
        return new MMDeviceCollection(result);
    }

    private IMMDeviceCOM GetDefaultEndpoint(DataFlow dataFlow, Role role)
    {
        using var lease = AcquireEnumerator();
        ThrowIfFailed(lease.Enumerator.GetDefaultAudioEndpoint(dataFlow, role, out var endpoint));
        return endpoint;
    }

    private IMMDeviceCOM GetEndpoint(string deviceId)
    {
        using var lease = AcquireEnumerator();
        ThrowIfFailed(lease.Enumerator.GetDevice(deviceId, out var endpoint));
        return endpoint;
    }

    // Deterministically releases only COM objects that never leave this library.
    // Uses ReleaseComObject to release only this library's reference, avoiding invalidation
    // of shared RCWs that may also be used by other audio libraries.
    private static void ReleaseComObject(object? comObject)
    {
        try
        {
            if (comObject != null && Marshal.IsComObject(comObject))
            {
                Marshal.ReleaseComObject(comObject);
            }
        }
        catch (Exception cleanupException)
        {
            ReportFailure(cleanupException);
            // best-effort cleanup
        }
    }

    private void ThrowIfDisposed()
    {
        RequireNotDisposed(_disposed, this);
    }

    private static void WithDefaultPlayback(Action<AudioDevice> action)
    {
        WithDefaultPlayback(device =>
        {
            action(device);
            return true;
        });
    }
    
    private static T WithDefaultPlayback<T>(Func<AudioDevice, T> action)
    {
        using var controller = new AudioController();
        using var device = controller.GetDefaultPlaybackDevice();
        return device == null ? throw new InvalidOperationException("No default playback device is set.") : action(device);
    }

    private static void WithDevice(string deviceId, Action<AudioDevice> action)
    {
        WithDevice(deviceId, device =>
        {
            action(device);
            return true;
        });
    }
    
    private static T WithDevice<T>(string deviceId, Func<AudioDevice, T> action)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            throw new ArgumentNullException(nameof(deviceId));
        }

        using var controller = new AudioController();
        using var device = controller.GetDeviceById(deviceId);
        if (device == null)
        {
            throw new ArgumentException("No device found with ID: " + deviceId, nameof(deviceId));
        }

        return action(device);
    }

    // Resolves the first endpoint of the given kind whose name contains `name`, sets it as the
    // default for `roles`, then re-resolves it so the returned snapshot carries the updated flags.
    private static AudioDeviceInfo? SetDefaultByName(string name, AudioDeviceKind kind, DefaultRole roles)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentNullException(nameof(name));
        }

        using var controller = new AudioController();
        var devices = kind == AudioDeviceKind.Recording
            ? controller.GetRecordingDevices()
            : controller.GetPlaybackDevices();

        try
        {
            #if NET8_0_OR_GREATER
            var match = devices.FirstOrDefault(x => x.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
#else
            var match = devices.FirstOrDefault(x => x.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
#endif
            if (match == null)
            {
                return null;
            }

            controller.SetDefaultDevice(match, roles);

            match.IsDefault |= (roles & DefaultRole.Multimedia) != 0;
            match.IsDefaultCommunication |= (roles & DefaultRole.Communications) != 0;
            return match.ToDeviceInfo();
        }
        finally
        {
            // Dispose all enumerated devices, even if no match was found, to avoid leaking COM wrappers.
            foreach (var device in devices)
            {
                device.Dispose();
            }
        }
    }

    /// <summary>Returns a snapshot of the current default playback device or null when no default playback device is set.</summary>
    /// <param name="communications">
    /// When <c>true</c>, resolves the default for the communications role; when <c>false</c> (the
    /// default), resolves the default for the multimedia role.
    /// </param>
    /// <returns>An immutable snapshot of the default playback endpoint.</returns>
    /// <remarks>
    /// Creates and disposes an <see cref="AudioController"/> per call. For repeated operations,
    /// create one instance and reuse it.
    /// </remarks>
    public static AudioDeviceInfo? GetDefaultPlayback(bool communications = false)
    {
        using var controller = new AudioController();
        using var device = controller.GetDefaultPlaybackDevice(communications);
        return device?.ToDeviceInfo();
    }

    /// <summary>Returns a snapshot of the current default recording device or null when no default recording device is set.</summary>
    /// <param name="communications">
    /// When <c>true</c>, resolves the default for the communications role; when <c>false</c> (the
    /// default), resolves the default for the multimedia role.
    /// </param>
    /// <returns>An immutable snapshot of the default recording endpoint.</returns>
    /// <remarks>
    /// Creates and disposes an <see cref="AudioController"/> per call. For repeated operations,
    /// create one instance and reuse it.
    /// </remarks>
    public static AudioDeviceInfo? GetDefaultRecording(bool communications = false)
    {
        using var controller = new AudioController();
        using var device = controller.GetDefaultRecordingDevice(communications);
        return device?.ToDeviceInfo();
    }

    /// <summary>Sets the endpoint with the given ID as the default for the requested role(s).</summary>
    /// <param name="deviceId">The ID of the endpoint to make default.</param>
    /// <param name="roles">
    /// The role(s) to assign. Defaults to <see cref="DefaultRole.Default"/> (multimedia + communications).
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="deviceId"/> is null or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="roles"/> specifies no role.</exception>
    /// <exception cref="NotSupportedException">Thrown when this system does not expose the undocumented
    /// policy-config API that changing the default endpoint requires.</exception>
    /// <remarks>
    /// Creates and disposes an <see cref="AudioController"/> per call. For repeated operations,
    /// create one instance and reuse it.
    /// </remarks>
    public static void SetDefaultDevice(string deviceId, DefaultRole roles = DefaultRole.Default)
    {
        using var controller = new AudioController();
        controller.SetDefaultDeviceById(deviceId, roles);
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
    /// <exception cref="NotSupportedException">Thrown when this system does not expose the undocumented
    /// policy-config API that changing the default endpoint requires.</exception>
    /// <remarks>
    /// Creates and disposes an <see cref="AudioController"/> per call. For repeated operations,
    /// create one instance and reuse it.
    /// </remarks>
    public static AudioDeviceInfo? SetDefaultPlaybackByName(string name, DefaultRole roles = DefaultRole.Default)
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
    /// <exception cref="NotSupportedException">Thrown when this system does not expose the undocumented
    /// policy-config API that changing the default endpoint requires.</exception>
    /// <remarks>
    /// Creates and disposes an <see cref="AudioController"/> per call. For repeated operations,
    /// create one instance and reuse it.
    /// </remarks>
    public static AudioDeviceInfo? SetDefaultRecordingByName(string name, DefaultRole roles = DefaultRole.Default)
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
        WithDefaultPlayback(d => d.SetVolumePercent(percent));
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
        WithDevice(deviceId, d => d.SetVolumePercent(percent));
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
        WithDefaultPlayback(d => d.IsMuted = mute);
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
        WithDevice(deviceId, d => d.IsMuted = mute);
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
        using var controller = new AudioController();
        return Snapshot(controller.GetDevices());
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
        using var controller = new AudioController();
        return Snapshot(kind == AudioDeviceKind.Recording
            ? controller.GetRecordingDevices()
            : controller.GetPlaybackDevices());
    }

    private static AudioDeviceInfo[] Snapshot(IReadOnlyList<AudioDevice> devices)
    {
        AudioDeviceInfo[] result = new AudioDeviceInfo[devices.Count];
        for (var i = 0; i < devices.Count; i++)
        {
            using var device = devices[i];
            result[i] = device.ToDeviceInfo();
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
        RequireNotNull(consumer, nameof(consumer));
        lock (_notificationLock)
        {
            using var lease = AcquireEnumerator();
            lock (_deviceRegistrationsLock)
            {
                if (_deviceRegistrations.TryGetValue(consumer, out var existing))
                {
                    return existing.Token;
                }
            }
            var adapter = new MMNotificationClientComAdapter(consumer);
            var token = new DeviceEventsRegistration(this, adapter);
            ThrowIfFailed(lease.Enumerator.RegisterEndpointNotificationCallback(adapter));
            lock (_deviceRegistrationsLock)
            {
                if (!_disposed)
                {
                    _deviceRegistrations[consumer] = new DeviceRegistration(adapter, token);
                    return token;
                }
            }
            // Dispose won the race. The lease keeps the enumerator alive through rollback.
            int hr = lease.Enumerator.UnregisterEndpointNotificationCallback(adapter);
            if (hr != HresultNotFound)
            {
                ThrowIfFailed(hr);
            }
            throw new ObjectDisposedException(nameof(AudioController));
        }
    }

    internal void RemoveDeviceRegistration(MMNotificationClientComAdapter adapter)
    {
        lock (_notificationLock)
        {
            EnumeratorLease lease;
            lock (_deviceRegistrationsLock)
            {
                if (_disposed || !_deviceRegistrations.TryGetValue(adapter.Target, out var existing) ||
                    !ReferenceEquals(existing.Adapter, adapter))
                {
                    return;
                }
                lease = AcquireEnumerator();
            }
            using (lease)
            {
                int hr = lease.Enumerator.UnregisterEndpointNotificationCallback(adapter);
                if (hr != HresultNotFound)
                {
                    ThrowIfFailed(hr);
                }
                lock (_deviceRegistrationsLock)
                {
                    _deviceRegistrations.Remove(adapter.Target);
                }
            }
        }
    }

    /// <summary>
    /// Releases resources held by the controller, unregistering any remaining device-notification
    /// callbacks. The controller does not own the <see cref="AudioDevice"/> instances returned by its
    /// methods; dispose of those yourself when you have accessed their volume/session features.
    /// </summary>
    /// <remarks>
    /// Waits up to five seconds for calls already in progress on other threads to finish, because
    /// releasing the underlying COM enumerator (or the cached policy-config client) while one is
    /// running would disconnect it mid-call.
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
                if (_deviceEnumerator != null)
                {
                    int hr = _deviceEnumerator.UnregisterEndpointNotificationCallback(adapter);
                    if (hr != HresultNotFound)
                    {
                        ThrowIfFailed(hr);
                    }
                }
            }
            catch (Exception cleanupException)
            {
                ReportFailure(cleanupException);
                // best-effort cleanup
            }
        }

        lock (_deviceRegistrationsLock)
        {
            // Wait for existing leases before releasing COM wrappers; disposal prevents new leases.
            // Bound the wait because native calls can hang indefinitely.
            // On timeout, leave COM cleanup to the GC rather than disconnecting an in-flight call.
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            var drained = true;
            while (_activeOperations > 0 && drained)
            {
                var remaining = DisposeDrainTimeout - deadline.Elapsed;
                drained = remaining > TimeSpan.Zero && Monitor.Wait(_deviceRegistrationsLock, remaining);
            }

            if (_activeOperations == 0)
            {
                // Released under the lock so that nothing can observe, and re-create, a null field.
                ReleaseComObject(_deviceEnumerator);

                // Same condition, same reason: a SetDefaultDevice call that is still running holds
                // this client, and releasing its wrapper now would disconnect it mid-call. On a
                // drain timeout both wrappers are left to the GC together.
                _policyClient?.Dispose();
                _policyClient = null;
            }

            _deviceEnumerator = null;
        }
    }
    
    // Snapshot of default endpoint IDs for a data-flow direction.
    // Null indicates no default endpoint or a direction that was not requested.
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
    
    // Scopes one enumerator borrow. A struct so the common path costs nothing to allocate.
    private readonly struct EnumeratorLease : IDisposable
    {
        private readonly AudioController _owner;

        internal IMMDeviceEnumeratorCOM Enumerator { get; }

        internal EnumeratorLease(AudioController owner, IMMDeviceEnumeratorCOM enumerator)
        {
            _owner = owner;
            Enumerator = enumerator;
        }

        public void Dispose()
        {
            _owner?.ReleaseOperation();
        }
    }
}