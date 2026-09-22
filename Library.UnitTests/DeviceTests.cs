/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  DeviceTests.cs
  Tests that need a real audio endpoint. Each one skips via Assert.Ignore when the machine
  has none, so a headless CI run reports them as skipped rather than failing.

  Getters only: nothing here changes volume, mute or the default device.
*/

using System;
using System.Collections.Generic;
using AudioDeviceLib.CoreAudioApi;
using NUnit.Framework;

namespace AudioDeviceLib.UnitTests;

[TestFixture]
public class DeviceTests
{
    [Test]
    public void DefaultPlayback_HasIdAndName()
    {
        using var audio = new AudioController();
        using AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);

        Assert.That(device.Id, Is.Not.Null.And.Not.Empty);
        Assert.That(device.Name, Is.Not.Null.And.Not.Empty);
        Assert.That(device.Kind, Is.EqualTo(AudioDeviceKind.Playback));
        Assert.That(device.IsDefault, Is.True, "the endpoint resolved by the multimedia role must report IsDefault");
    }

    [Test]
    public void GetDeviceById_RoundTripsById()
    {
        using var audio = new AudioController();
        using AudioDevice original = AudioFixture.RequireDefaultPlayback(audio);

        using AudioDevice resolved = AudioFixture.RequireDeviceById(audio, original.Id);

        Assert.That(resolved.Id, Is.EqualTo(original.Id));
        Assert.That(resolved.Name, Is.EqualTo(original.Name));
        Assert.That(resolved.Kind, Is.EqualTo(original.Kind));
    }

    // Core Audio hands out a distinct COM object per acquisition, so two handles on one endpoint are
    // never reference-equal. Equality has to come from the ID.
    [Test]
    public void Equals_IsByIdNotByReference()
    {
        using var audio = new AudioController();
        using AudioDevice a = AudioFixture.RequireDefaultPlayback(audio);
        using AudioDevice b = AudioFixture.RequireDeviceById(audio, a.Id);

        Assert.That(ReferenceEquals(a, b), Is.False, "expected distinct wrappers");
        Assert.That(a, Is.EqualTo(b));
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void Snapshot_StaysReadableAfterDispose()
    {
        using var audio = new AudioController();
        AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);

        string id = device.Id;
        string name = device.Name;
        AudioDeviceKind kind = device.Kind;
        DeviceState state = device.State;

        device.Dispose();

        Assert.That(device.Id, Is.EqualTo(id));
        Assert.That(device.Name, Is.EqualTo(name));
        Assert.That(device.Kind, Is.EqualTo(kind));
        Assert.That(device.State, Is.EqualTo(state));
        Assert.DoesNotThrow(() => device.ToDeviceInfo());
        Assert.DoesNotThrow(() => device.ToString());
    }

    [Test]
    public void Device_AfterDispose_ComMembersThrowObjectDisposed()
    {
        using var audio = new AudioController();
        AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);
        device.Dispose();

        Assert.Throws<ObjectDisposedException>(() => { var _ = device.Volume; });
        Assert.Throws<ObjectDisposedException>(() => { var _ = device.Properties; });
        Assert.Throws<ObjectDisposedException>(() => { var _ = device.SessionManager; });
        Assert.Throws<ObjectDisposedException>(() => { var _ = device.Meter; });
        Assert.Throws<ObjectDisposedException>(() => device.Refresh());
    }

    [Test]
    public void Device_DoubleDispose_IsNoOp()
    {
        using var audio = new AudioController();
        AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);

        device.Dispose();
        Assert.DoesNotThrow(() => device.Dispose());
    }

    [Test]
    public void GetVolumePercent_IsBetween0And100()
    {
        using var audio = new AudioController();
        using AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);

        float volume = device.GetVolumePercent();
        Assert.That(volume, Is.InRange(0f, 100f));
    }

    [Test]
    public void IsMuted_ReadsWithoutThrowing()
    {
        using var audio = new AudioController();
        using AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);

        Assert.DoesNotThrow(() => { bool _ = device.IsMuted; });
    }

    [Test]
    public void GetPeakValue_IsBetween0And1()
    {
        using var audio = new AudioController();
        using AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);

        float peak = device.GetPeakValue();
        Assert.That(peak, Is.InRange(0f, 1f));
    }

    [Test]
    public void ToDeviceInfo_MatchesDevice()
    {
        using var audio = new AudioController();
        using AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);

        AudioDeviceInfo info = device.ToDeviceInfo();

        info = AudioFixture.RequireValue(info);
        Assert.That(info.Id, Is.EqualTo(device.Id));
        Assert.That(info.Name, Is.EqualTo(device.Name));
        Assert.That(info.Kind, Is.EqualTo(device.Kind));
        Assert.That(info.State, Is.EqualTo(device.State));
        Assert.That(info.IsDefault, Is.EqualTo(device.IsDefault));
        Assert.That(info.IsDefaultCommunication, Is.EqualTo(device.IsDefaultCommunication));
    }

    [Test]
    public void GetDeviceInfo_MatchesGetDeviceById()
    {
        using var audio = new AudioController();
        using AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);

        AudioDeviceInfo? info = audio.GetDeviceInfo(device.Id);

        Assert.That(info, Is.Not.Null);
        info = AudioFixture.RequireValue(info);
        Assert.That(info.Id, Is.EqualTo(device.Id));
        Assert.That(info.Name, Is.EqualTo(device.Name));
    }

    // IPropertyStore::GetValue returns S_OK with a VT_EMPTY variant for a key that is not in the
    // store - success, not failure. TryGetValue has to treat emptiness as "not found", otherwise the
    // caller gets an empty snapshot reported as a hit.
    [Test]
    public void Properties_TryGetValue_AbsentKey_ReturnsFalseAndEmptyVariant()
    {
        using var audio = new AudioController();
        using AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);

        var absent = new PropertyKey
        {
            FormatId = new Guid("DEADBEEF-1111-2222-3333-444455556666"),
            PropertyId = 42,
        };

        bool found = device.Properties.TryGetValue(absent, out var value);

        Assert.That(found, Is.False, "an absent key must not report as found");
        Assert.That(value.IsEmpty, Is.True);
        Assert.That(value.Value, Is.Null, "PropertyValue.Value must be null for VT_EMPTY, not a placeholder string");
    }

    [Test]
    public void Properties_TryGetValue_PresentKey_ReturnsTrue()
    {
        using var audio = new AudioController();
        using AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);

        bool found = device.Properties.TryGetValue(PKEY.DeviceFriendlyName, out var value);

        Assert.That(found, Is.True);
        Assert.That(value.IsEmpty, Is.False);
        Assert.That(value.Value, Is.EqualTo(device.Name));
    }

    // If the absent-key handling above ever regressed, the unhandled-variant fallback string would
    // surface as a device name. This is the cheap end-to-end guard for that.
    [Test]
    public void DeviceNames_AreNotPlaceholders()
    {
        using var audio = new AudioController();
        AudioFixture.RequireAnyDevice(audio, out IReadOnlyList<AudioDevice> all);

        try
        {
            foreach (AudioDevice device in all)
            {
                Assert.That(device.Name, Does.Not.Contain("FIXME"),
                    "a PropVariant fallback string leaked into a device name");
            }
        }
        finally
        {
            AudioFixture.DisposeAll(all);
        }
    }

    [Test]
    public void EnumeratedDevice_IsActive_WhenFilteredToActive()
    {
        using var audio = new AudioController();
        AudioFixture.RequireAnyDevice(audio, out IReadOnlyList<AudioDevice> all);

        try
        {
            foreach (AudioDevice device in all)
            {
                Assert.That(device.State, Is.EqualTo(DeviceState.Active));
                Assert.That(device.IsActive, Is.True);
            }
        }
        finally
        {
            AudioFixture.DisposeAll(all);
        }
    }

    [Test]
    public void Refresh_KeepsIdentityStable()
    {
        using var audio = new AudioController();
        using AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);

        string id = device.Id;
        string name = device.Name;

        device.Refresh();

        Assert.That(device.Id, Is.EqualTo(id));
        Assert.That(device.Name, Is.EqualTo(name));
    }

    [Test]
    public void StaticGetters_AgreeWithInstanceApi()
    {
        using var audio = new AudioController();
        using AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);

        AudioDeviceInfo? viaStatic = AudioController.GetDefaultPlayback();

        viaStatic = AudioFixture.RequireValue(viaStatic);
        Assert.That(viaStatic.Id, Is.EqualTo(device.Id));
        Assert.That(AudioController.GetVolume(), Is.EqualTo(device.GetVolumePercent()).Within(0.5f));
        Assert.That(AudioController.IsMuted(), Is.EqualTo(device.IsMuted));
        Assert.That(AudioController.GetVolume(device.Id), Is.EqualTo(device.GetVolumePercent()).Within(0.5f));
        Assert.That(AudioController.IsMuted(device.Id), Is.EqualTo(device.IsMuted));
    }
    
    // PropertyStore converts and clears the native variant before returning the snapshot.
    [Test]
    public void PropertyStoreProperty_Value_SurvivesTheVariantBeingReleased()
    {
        using var audio = new AudioController();
        using AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);

        PropertyStoreProperty? property = device.Properties[PKEY.DeviceFriendlyName];

        Assert.That(property, Is.Not.Null);
        property = AudioFixture.RequireValue(property);
        Assert.That(property.Value.Value, Is.EqualTo(device.Name));
        property = AudioFixture.RequireValue(property);
        Assert.That(property.Value.Value, Is.EqualTo(device.Name), "second read differs - the value is not owned");
    }

    // Every PKEY_AudioEndpoint_* key lives in one property set and is selected by its PID. A key
    // that carried only the set GUID would make all of these resolve to the same property, so this
    // asserts against the real store that they address different things.
    [Test]
    public void Properties_AudioEndpointKeys_AddressDistinctProperties()
    {
        using var audio = new AudioController();
        using AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);

        PropertyKey[] keys =
        {
            PKEY.AudioEndpointFormFactor,
            PKEY.AudioEndpointGuid,
            PKEY.AudioEndpointJackSubType,
        };

        var resolved = new List<PropertyKey>();
        foreach (PropertyKey key in keys)
        {
            PropertyStoreProperty? property = device.Properties[key];
            if (property == null)
            {
                continue;
            }

            Assert.That(property.Key.PropertyId, Is.EqualTo(key.PropertyId),
                $"lookup for {key.Name} returned pid {property.Key.PropertyId}; the key is not selecting on PID");
            resolved.Add(property.Key);
        }

        Assert.That(resolved, Is.Not.Empty, "no AudioEndpoint properties present on this endpoint");
        Assert.That(resolved.ConvertAll(k => k.PropertyId), Is.Unique);
    }

    // The two friendly names are different properties in different sets: one names the endpoint,
    // the other the adapter behind it. AudioDevice.Name is the endpoint one.
    [Test]
    public void Properties_DeviceAndInterfaceFriendlyName_AreReadSeparately()
    {
        using var audio = new AudioController();
        using AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);

        bool hasDeviceName = device.Properties.TryGetValue(PKEY.DeviceFriendlyName, out var deviceName);

        Assert.That(hasDeviceName, Is.True);
        Assert.That(deviceName.Value, Is.EqualTo(device.Name));

        if (device.Properties.TryGetValue(PKEY.DeviceInterfaceFriendlyName, out var interfaceName))
        {
            Assert.That(interfaceName.Value, Is.Not.Null);
            Assert.That(AudioFixture.RequireValue(device.Properties[PKEY.DeviceInterfaceFriendlyName]).Key.FormatId,
                Is.Not.EqualTo(AudioFixture.RequireValue(device.Properties[PKEY.DeviceFriendlyName]).Key.FormatId),
                "both friendly-name lookups landed in the same property set");
        }
    }
}
