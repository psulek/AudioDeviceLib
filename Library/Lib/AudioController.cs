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

    // ---- Enumeration --------------------------------------------------------------

    /// <summary>Returns all active endpoints (both playback and recording), in enumeration order.</summary>
    /// <returns>
    /// A read-only list of all active <see cref="AudioDevice"/> endpoints. Each device is tagged with
    /// its 1-based <see cref="AudioDevice.Index"/> and whether it is the current default / default
    /// communications device. The list is empty if no active endpoints exist.
    /// </returns>
    public IReadOnlyList<AudioDevice> GetDevices()
    {
        MMDeviceCollection devices =
            _enumerator.EnumerateAudioEndPoints(EDataFlow.eAll, EDeviceState.DEVICE_STATE_ACTIVE);

        // Resolve the current defaults once, then tag each endpoint (cheaper than the
        // per-device lookups the original toolkit performed).
        string defaultPlaybackId = TryGetDefaultId(EDataFlow.eRender, ERole.eMultimedia);
        string defaultRecordingId = TryGetDefaultId(EDataFlow.eCapture, ERole.eMultimedia);
        string commPlaybackId = TryGetDefaultId(EDataFlow.eRender, ERole.eCommunications);
        string commRecordingId = TryGetDefaultId(EDataFlow.eCapture, ERole.eCommunications);

        var result = new List<AudioDevice>(devices.Count);
        for (int i = 0; i < devices.Count; i++)
        {
            MMDevice device = devices[i];
            bool isDefault = device.ID == defaultPlaybackId || device.ID == defaultRecordingId;
            bool isDefaultComm = device.ID == commPlaybackId || device.ID == commRecordingId;
            result.Add(new AudioDevice(i + 1, device, isDefault, isDefaultComm));
        }

        return result;
    }

    /// <summary>Returns all active playback (render) endpoints.</summary>
    /// <returns>
    /// A read-only list of the active endpoints whose <see cref="AudioDevice.Kind"/> is
    /// <see cref="AudioDeviceKind.Playback"/>. The list is empty if no playback endpoints are active.
    /// </returns>
    public IReadOnlyList<AudioDevice> GetPlaybackDevices()
    {
        return GetDevices().Where(d => d.Kind == AudioDeviceKind.Playback).ToList();
    }

    /// <summary>Returns all active recording (capture) endpoints.</summary>
    /// <returns>
    /// A read-only list of the active endpoints whose <see cref="AudioDevice.Kind"/> is
    /// <see cref="AudioDeviceKind.Recording"/>. The list is empty if no recording endpoints are active.
    /// </returns>
    public IReadOnlyList<AudioDevice> GetRecordingDevices()
    {
        return GetDevices().Where(d => d.Kind == AudioDeviceKind.Recording).ToList();
    }

    // ---- Current defaults ---------------------------------------------------------

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
        return GetDefault(EDataFlow.eRender, communications);
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
        return GetDefault(EDataFlow.eCapture, communications);
    }

    // ---- Setting the default ------------------------------------------------------

    /// <summary>Sets the given device as the default for the requested role(s).</summary>
    /// <param name="device">The endpoint to make default. Must not be <c>null</c>.</param>
    /// <param name="roles">
    /// The role(s) to assign. Defaults to <see cref="DefaultRole.Default"/> (multimedia + communications).
    /// Each set flag maps to one native <c>ERole</c> call.
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
    /// <paramref name="roles"/> is a bit flag; each set flag maps to one native ERole call.
    /// </summary>
    /// <param name="deviceId">The endpoint ID to make default. Must not be <c>null</c> or empty.</param>
    /// <param name="roles">
    /// The role(s) to assign. Defaults to <see cref="DefaultRole.Default"/> (multimedia + communications).
    /// At least one of <see cref="DefaultRole.Console"/>, <see cref="DefaultRole.Multimedia"/> or
    /// <see cref="DefaultRole.Communications"/> must be set.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="deviceId"/> is <c>null</c> or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="roles"/> specifies no role.</exception>
    public void SetDefaultDevice(string deviceId, DefaultRole roles = DefaultRole.Default)
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
            client.SetDefaultEndpoint(deviceId, ERole.eConsole);
        }

        if ((roles & DefaultRole.Multimedia) != 0)
        {
            client.SetDefaultEndpoint(deviceId, ERole.eMultimedia);
        }

        if ((roles & DefaultRole.Communications) != 0)
        {
            client.SetDefaultEndpoint(deviceId, ERole.eCommunications);
        }
    }

    // ---- Convenience: match by name ----------------------------------------------

    /// <summary>
    /// Finds the first active playback endpoint whose name contains nameSubstring
    /// (case-insensitive), sets it as default, and returns it. Returns null if no match is found.
    /// </summary>
    /// <param name="nameSubstring">
    /// The substring to match against each endpoint's <see cref="AudioDevice.Name"/> (case-insensitive).
    /// Must not be <c>null</c> or empty.
    /// </param>
    /// <param name="roles">
    /// The role(s) to assign to the matched device. Defaults to <see cref="DefaultRole.Default"/>.
    /// </param>
    /// <returns>The matched <see cref="AudioDevice"/> that was set as default, or <c>null</c> if no endpoint matched.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="nameSubstring"/> is <c>null</c> or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="roles"/> specifies no role.</exception>
    public AudioDevice SetDefaultPlaybackByName(string nameSubstring, DefaultRole roles = DefaultRole.Default)
    {
        AudioDevice match = FindByName(GetPlaybackDevices(), nameSubstring);
        if (match != null)
        {
            SetDefaultDevice(match, roles);
        }

        return match;
    }

    /// <summary>
    /// Finds the first active recording endpoint whose name contains nameSubstring
    /// (case-insensitive), sets it as default, and returns it. Returns null if no match is found.
    /// </summary>
    /// <param name="nameSubstring">
    /// The substring to match against each endpoint's <see cref="AudioDevice.Name"/> (case-insensitive).
    /// Must not be <c>null</c> or empty.
    /// </param>
    /// <param name="roles">
    /// The role(s) to assign to the matched device. Defaults to <see cref="DefaultRole.Default"/>.
    /// </param>
    /// <returns>The matched <see cref="AudioDevice"/> that was set as default, or <c>null</c> if no endpoint matched.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="nameSubstring"/> is <c>null</c> or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="roles"/> specifies no role.</exception>
    public AudioDevice SetDefaultRecordingByName(string nameSubstring, DefaultRole roles = DefaultRole.Default)
    {
        AudioDevice match = FindByName(GetRecordingDevices(), nameSubstring);
        if (match != null)
        {
            SetDefaultDevice(match, roles);
        }

        return match;
    }

    // ---- Internals ----------------------------------------------------------------

    private AudioDevice GetDefault(EDataFlow flow, bool communications)
    {
        ERole role = communications ? ERole.eCommunications : ERole.eMultimedia;
        MMDevice mm;
        try
        {
            mm = _enumerator.GetDefaultAudioEndpoint(flow, role);
        }
        catch
        {
            return null;
        }

        if (mm == null)
        {
            return null;
        }

        // Locate it in the full enumeration so Index and the default flags are accurate.
        return GetDevices().FirstOrDefault(d => d.Id == mm.ID);
    }

    private string TryGetDefaultId(EDataFlow flow, ERole role)
    {
        try
        {
            MMDevice mm = _enumerator.GetDefaultAudioEndpoint(flow, role);
            return mm != null ? mm.ID : null;
        }
        catch
        {
            // No default endpoint of this kind/role is set.
            return null;
        }
    }

    private static AudioDevice FindByName(IEnumerable<AudioDevice> devices, string nameSubstring)
    {
        if (string.IsNullOrEmpty(nameSubstring))
        {
            throw new ArgumentNullException(nameof(nameSubstring));
        }

        return devices.FirstOrDefault(d =>
            d.Name != null &&
            d.Name.IndexOf(nameSubstring, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    /// <summary>
    /// Releases resources held by the controller. The controller does not own the
    /// <see cref="AudioDevice"/> instances returned by its methods; dispose those yourself when
    /// you have accessed their volume/session features.
    /// </summary>
    public void Dispose()
    {
        // The enumerator registers no callbacks; nothing to release deterministically today.
        // Present so callers can `using` the controller and to give future cleanup a home.
    }
}