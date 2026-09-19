/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  PropertyKeyNames.cs
  Maps well-known Core Audio property keys (fmtid + pid) to a friendly, human-readable name.
  Used by PropertyKey.Name so consumers can identify a changed property without a raw GUID.
*/

using System;
using System.Collections.Generic;

namespace AudioDeviceLib.CoreAudioApi;

internal static class PropertyKeyNames
{
    // Well-known property-set GUIDs (from the Windows SDK: mmdeviceapi.h / functiondiscoverykeys_devpkey.h).
    private static readonly Guid AudioEndpoint =
        new Guid(0x1da5d803, 0xd492, 0x4edd, 0x8c, 0x23, 0xe0, 0xc0, 0xff, 0xee, 0x7f, 0x0e);

    private static readonly Guid AudioEngineDeviceFormat =
        new Guid(0xf19f064d, 0x082c, 0x4e27, 0xbc, 0x73, 0x68, 0x82, 0xa1, 0xbb, 0x8e, 0x4c);

    // PKEY_AudioEngine_OEMFormat is in a set of its own, not pid 3 of the DeviceFormat set.
    private static readonly Guid AudioEngineOemFormat =
        new Guid(0xe4870e26, 0x3cc5, 0x4cd2, 0xba, 0x46, 0xca, 0x0a, 0x9a, 0x70, 0xed, 0x04);

    private static readonly Guid Device =
        new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0);

    private static readonly Guid DeviceInterface =
        new Guid(0x026e516e, 0xb814, 0x414b, 0x83, 0xcd, 0x85, 0x6d, 0x6f, 0xef, 0x48, 0x22);

    // Undocumented set behind the Sound control panel "Listen" tab (audio loopback).
    private static readonly Guid Listen =
        new Guid(0x24dbb0fc, 0x9311, 0x4b3d, 0x9c, 0xf0, 0x18, 0xff, 0x15, 0x56, 0x39, 0xd4);

    private static readonly Dictionary<Guid, Dictionary<int, string>> Names =
        new Dictionary<Guid, Dictionary<int, string>>
        {
            [AudioEndpoint] = new Dictionary<int, string>
            {
                [0] = "AudioEndpoint.FormFactor",
                [1] = "AudioEndpoint.ControlPanelPageProvider",
                [2] = "AudioEndpoint.Association",
                [3] = "AudioEndpoint.PhysicalSpeakers",
                [4] = "AudioEndpoint.GUID",
                [5] = "AudioEndpoint.Disable_SysFx",
                [6] = "AudioEndpoint.FullRangeSpeakers",
                [7] = "AudioEndpoint.Supports_EventDriven_Mode",
                [8] = "AudioEndpoint.JackSubType",
            },
            [AudioEngineDeviceFormat] = new Dictionary<int, string>
            {
                [0] = "AudioEngine.DeviceFormat",
            },
            [AudioEngineOemFormat] = new Dictionary<int, string>
            {
                [3] = "AudioEngine.OEMFormat",
            },
            [Device] = new Dictionary<int, string>
            {
                [2] = "Device.DeviceDesc",
                [14] = "Device.FriendlyName",
            },
            [DeviceInterface] = new Dictionary<int, string>
            {
                [2] = "DeviceInterface.FriendlyName",
                [3] = "DeviceInterface.Enabled",
            },
            [Listen] = new Dictionary<int, string>
            {
                [0] = "Listen.PlaybackThroughThisDevice",
                [1] = "Listen.ListenToThisDevice",
                [2] = "Listen.PlaybackDeviceId",
            },
        };

    /// <summary>Returns a friendly name for the key, or a "{fmtid}/{pid}" fallback when unknown.</summary>
    internal static string GetName(PropertyKey key)
    {
        if (Names.TryGetValue(key.fmtid, out var byPid) && byPid.TryGetValue(key.pid, out var name))
        {
            return name;
        }

        return key.fmtid.ToString("B") + "/" + key.pid;
    }
}
