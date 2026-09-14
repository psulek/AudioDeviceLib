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
public sealed class AudioController
{
    private readonly MMDeviceEnumerator _enumerator = new MMDeviceEnumerator();

    // ---- Enumeration --------------------------------------------------------------

    /// <summary>Returns all active endpoints (both playback and recording), in enumeration order.</summary>
    public IReadOnlyList<AudioDevice> GetDevices()
    {
        MMDeviceCollection collection = _enumerator.EnumerateAudioEndPoints(EDataFlow.eAll, EDeviceState.DEVICE_STATE_ACTIVE);

        // Resolve the current defaults once, then tag each endpoint (cheaper than the
        // per-device lookups the original toolkit performed).
        string defaultPlaybackId = TryGetDefaultId(EDataFlow.eRender, ERole.eMultimedia);
        string defaultRecordingId = TryGetDefaultId(EDataFlow.eCapture, ERole.eMultimedia);
        string commPlaybackId = TryGetDefaultId(EDataFlow.eRender, ERole.eCommunications);
        string commRecordingId = TryGetDefaultId(EDataFlow.eCapture, ERole.eCommunications);

        var result = new List<AudioDevice>(collection.Count);
        for (int i = 0; i < collection.Count; i++)
        {
            MMDevice mm = collection[i];
            bool isDefault = mm.ID == defaultPlaybackId || mm.ID == defaultRecordingId;
            bool isDefaultComm = mm.ID == commPlaybackId || mm.ID == commRecordingId;
            result.Add(new AudioDevice(i + 1, mm, isDefault, isDefaultComm));
        }

        return result;
    }

    /// <summary>Returns all active playback (render) endpoints.</summary>
    public IReadOnlyList<AudioDevice> GetPlaybackDevices()
    {
        return GetDevices().Where(d => d.Kind == AudioDeviceKind.Playback).ToList();
    }

    /// <summary>Returns all active recording (capture) endpoints.</summary>
    public IReadOnlyList<AudioDevice> GetRecordingDevices()
    {
        return GetDevices().Where(d => d.Kind == AudioDeviceKind.Recording).ToList();
    }

    // ---- Current defaults ---------------------------------------------------------

    /// <summary>Returns the current default playback device, or null if none is set.</summary>
    public AudioDevice GetDefaultPlaybackDevice(bool communications = false)
    {
        return GetDefault(EDataFlow.eRender, communications);
    }

    /// <summary>Returns the current default recording device, or null if none is set.</summary>
    public AudioDevice GetDefaultRecordingDevice(bool communications = false)
    {
        return GetDefault(EDataFlow.eCapture, communications);
    }

    // ---- Setting the default ------------------------------------------------------

    /// <summary>Sets the given device as the default for the requested role(s).</summary>
    public void SetDefaultDevice(AudioDevice device, DefaultRole role = DefaultRole.MultimediaAndCommunications)
    {
        if (device == null)
        {
            throw new ArgumentNullException(nameof(device));
        }

        SetDefaultDevice(device.Id, role);
    }

    /// <summary>Sets the endpoint with the given ID as the default for the requested role(s).</summary>
    public void SetDefaultDevice(string deviceId, DefaultRole role = DefaultRole.MultimediaAndCommunications)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            throw new ArgumentNullException(nameof(deviceId));
        }

        var client = new PolicyConfigClient();
        switch (role)
        {
            case DefaultRole.Multimedia:
                client.SetDefaultEndpoint(deviceId, ERole.eMultimedia);
                break;
            case DefaultRole.Communications:
                client.SetDefaultEndpoint(deviceId, ERole.eCommunications);
                break;
            case DefaultRole.All:
                client.SetDefaultEndpoint(deviceId, ERole.eConsole);
                client.SetDefaultEndpoint(deviceId, ERole.eMultimedia);
                client.SetDefaultEndpoint(deviceId, ERole.eCommunications);
                break;
            case DefaultRole.MultimediaAndCommunications:
            default:
                // Same pair, in the same order, as Set-AudioDevice with no switch.
                client.SetDefaultEndpoint(deviceId, ERole.eCommunications);
                client.SetDefaultEndpoint(deviceId, ERole.eMultimedia);
                break;
        }
    }

    // ---- Convenience: match by name ----------------------------------------------

    /// <summary>
    /// Finds the first active playback endpoint whose name contains nameSubstring
    /// (case-insensitive), sets it as default, and returns it. Returns null if no match is found.
    /// </summary>
    public AudioDevice SetDefaultPlaybackByName(string nameSubstring, DefaultRole role = DefaultRole.MultimediaAndCommunications)
    {
        AudioDevice match = FindByName(GetPlaybackDevices(), nameSubstring);
        if (match != null)
        {
            SetDefaultDevice(match, role);
        }

        return match;
    }

    /// <summary>
    /// Finds the first active recording endpoint whose name contains nameSubstring
    /// (case-insensitive), sets it as default, and returns it. Returns null if no match is found.
    /// </summary>
    public AudioDevice SetDefaultRecordingByName(string nameSubstring, DefaultRole role = DefaultRole.MultimediaAndCommunications)
    {
        AudioDevice match = FindByName(GetRecordingDevices(), nameSubstring);
        if (match != null)
        {
            SetDefaultDevice(match, role);
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
}