/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  PropertyKeyTests.cs
  Pins the well-known property keys to the values DEFINE_PROPERTYKEY gives them in the Windows SDK
  headers (mmdeviceapi.h and functiondiscoverykeys_devpkey.h).

  The AudioEndpoint keys all share one property-set GUID and are told apart only by their PID, so a
  constant that carried the set GUID alone would silently alias every other key in the set.
*/

using System;
using System.Collections.Generic;
using AudioDeviceLib.CoreAudioApi;
using NUnit.Framework;

namespace AudioDeviceLib.UnitTests;

[TestFixture]
public class PropertyKeyTests
{
    private static readonly Guid AudioEndpointSet =
        new Guid("1da5d803-d492-4edd-8c23-e0c0ffee7f0e");

    // name, expected fmtid, expected pid - transcribed from the SDK headers.
    private static readonly (string Name, PropertyKey Key, string Fmtid, int Pid)[] Expected =
    {
        ("AudioEndpoint_FormFactor", PKEY.PKEY_AudioEndpoint_FormFactor, "1da5d803-d492-4edd-8c23-e0c0ffee7f0e", 0),
        ("AudioEndpoint_ControlPanelPageProvider", PKEY.PKEY_AudioEndpoint_ControlPanelPageProvider, "1da5d803-d492-4edd-8c23-e0c0ffee7f0e", 1),
        ("AudioEndpoint_Association", PKEY.PKEY_AudioEndpoint_Association, "1da5d803-d492-4edd-8c23-e0c0ffee7f0e", 2),
        ("AudioEndpoint_PhysicalSpeakers", PKEY.PKEY_AudioEndpoint_PhysicalSpeakers, "1da5d803-d492-4edd-8c23-e0c0ffee7f0e", 3),
        ("AudioEndpoint_GUID", PKEY.PKEY_AudioEndpoint_GUID, "1da5d803-d492-4edd-8c23-e0c0ffee7f0e", 4),
        ("AudioEndpoint_Disable_SysFx", PKEY.PKEY_AudioEndpoint_Disable_SysFx, "1da5d803-d492-4edd-8c23-e0c0ffee7f0e", 5),
        ("AudioEndpoint_FullRangeSpeakers", PKEY.PKEY_AudioEndpoint_FullRangeSpeakers, "1da5d803-d492-4edd-8c23-e0c0ffee7f0e", 6),
        ("AudioEndpoint_Supports_EventDriven_Mode", PKEY.PKEY_AudioEndpoint_Supports_EventDriven_Mode, "1da5d803-d492-4edd-8c23-e0c0ffee7f0e", 7),
        ("AudioEndpoint_JackSubType", PKEY.PKEY_AudioEndpoint_JackSubType, "1da5d803-d492-4edd-8c23-e0c0ffee7f0e", 8),
        ("AudioEngine_DeviceFormat", PKEY.PKEY_AudioEngine_DeviceFormat, "f19f064d-082c-4e27-bc73-6882a1bb8e4c", 0),
        ("AudioEngine_OEMFormat", PKEY.PKEY_AudioEngine_OEMFormat, "e4870e26-3cc5-4cd2-ba46-ca0a9a70ed04", 3),
        ("Device_FriendlyName", PKEY.PKEY_Device_FriendlyName, "a45c254e-df1c-4efd-8020-67d146a850e0", 14),
        ("Device_DeviceDesc", PKEY.PKEY_Device_DeviceDesc, "a45c254e-df1c-4efd-8020-67d146a850e0", 2),
        ("DeviceInterface_FriendlyName", PKEY.PKEY_DeviceInterface_FriendlyName, "026e516e-b814-414b-83cd-856d6fef4822", 2),
    };

    [Test]
    public void WellKnownKeys_MatchTheSdkDefinitions()
    {
        Assert.Multiple(() =>
        {
            foreach ((string name, PropertyKey key, string fmtid, int pid) in Expected)
            {
                Assert.That(key.fmtid, Is.EqualTo(new Guid(fmtid)), $"{name} fmtid");
                Assert.That(key.pid, Is.EqualTo(pid), $"{name} pid");
            }
        });
    }

    // The defect this guards: seven of these were once bare set GUIDs with no PID, so every lookup
    // in the AudioEndpoint set resolved to whichever property enumerated first.
    [Test]
    public void WellKnownKeys_AreAllDistinct()
    {
        var seen = new Dictionary<string, string>();

        foreach ((string name, PropertyKey key, _, _) in Expected)
        {
            string identity = $"{key.fmtid:D}/{key.pid}";
            Assert.That(seen.ContainsKey(identity), Is.False,
                $"{name} has the same fmtid and pid as {(seen.TryGetValue(identity, out var other) ? other : null)}");
            seen[identity] = name;
        }
    }

    // Every AudioEndpoint key shares one set GUID, so the PID is the only thing separating them.
    [Test]
    public void AudioEndpointKeys_ShareOneSetAndDifferByPid()
    {
        var pids = new HashSet<int>();

        foreach ((string name, PropertyKey key, _, _) in Expected)
        {
            if (!name.StartsWith("AudioEndpoint_", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.That(key.fmtid, Is.EqualTo(AudioEndpointSet), $"{name} is not in the AudioEndpoint set");
            Assert.That(pids.Add(key.pid), Is.True, $"{name} reuses pid {key.pid}");
        }

        Assert.That(pids, Has.Count.EqualTo(9));
    }

    // PropertyKey.Name resolves through PropertyKeyNames, so a key whose fmtid or pid is wrong
    // falls through to the raw "{guid}/pid" form instead of a friendly name.
    [Test]
    public void WellKnownKeys_ResolveToFriendlyNames()
    {
        Assert.Multiple(() =>
        {
            foreach ((string name, PropertyKey key, _, _) in Expected)
            {
                Assert.That(key.Name, Does.Not.StartWith("{"),
                    $"{name} has no entry in PropertyKeyNames; its fmtid or pid does not match the table");
            }
        });
    }

    [Test]
    public void DeviceAndDeviceInterfaceFriendlyName_AreDifferentProperties()
    {
        Assert.That(PKEY.PKEY_Device_FriendlyName.fmtid,
            Is.Not.EqualTo(PKEY.PKEY_DeviceInterface_FriendlyName.fmtid));
        Assert.That(PKEY.PKEY_Device_FriendlyName.Name, Is.EqualTo("Device.FriendlyName"));
        Assert.That(PKEY.PKEY_DeviceInterface_FriendlyName.Name, Is.EqualTo("DeviceInterface.FriendlyName"));
    }
}
