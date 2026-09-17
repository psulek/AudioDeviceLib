/* Copyright (c) 2026 Peter Šulek. MIT License. */
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AudioDeviceLib.CoreAudioApi;
using NUnit.Framework;

namespace AudioDeviceLib.UnitTests;

[TestFixture]
public class VerifyTests
{
    [Test]
    public void SessionSnapshot_UnavailableStringsAreEmpty()
    {
        var snapshot = new AudioSessionInfo(null!, null!, AudioSessionState.Inactive, 0, null!, null!, false);
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.DisplayName, Is.Empty);
            Assert.That(snapshot.IconPath, Is.Empty);
            Assert.That(snapshot.SessionIdentifier, Is.Empty);
            Assert.That(snapshot.SessionInstanceIdentifier, Is.Empty);
        });
    }

    [Test]
    public void NotificationSnapshot_DefendsAgainstBothMutationPaths()
    {
        var source = new[] { 0.2f };
        var snapshot = new AudioVolumeNotificationData(Guid.Empty, false, 0.2f, source);
        source[0] = 0.9f;
        Assert.That(snapshot.ChannelVolume[0], Is.EqualTo(0.2f));
        Assert.Throws<NotSupportedException>(() => ((IList<float>)snapshot.ChannelVolume)[0] = 0.8f);
        Assert.Throws<ArgumentNullException>(() => new AudioVolumeNotificationData(Guid.Empty, false, 0, null!));
    }

    [Test]
    public void PropertyKey_NativeLayoutAndEqualityArePreserved()
    {
        var key = PKEY.DeviceFriendlyName;
        IntPtr memory = Marshal.AllocHGlobal(20);
        try
        {
            Assert.That(Marshal.SizeOf<PropertyKey>(), Is.EqualTo(20));
            Marshal.StructureToPtr(key, memory, false);
            Assert.That(Marshal.PtrToStructure<Guid>(memory), Is.EqualTo(key.FormatId));
            Assert.That(Marshal.ReadInt32(memory, 16), Is.EqualTo(key.PropertyId));
            Assert.That(Marshal.PtrToStructure<PropertyKey>(memory), Is.EqualTo(key));
            Assert.That(key == PKEY.DeviceFriendlyName, Is.True);
            Assert.That(key != PKEY.DeviceDeviceDesc, Is.True);
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    [Test]
    public void CachedDeviceChildren_RejectUseAfterOwnerDisposal()
    {
        using var controller = new AudioController();
        using var device = AudioFixture.RequireDefaultPlayback(controller);
        var meter = device.Meter;
        var properties = device.Properties;
        var channels = device.Volume.Channels;
        var step = device.Volume.StepInformation;
        device.Dispose();
        Assert.Throws<ObjectDisposedException>(() => meter.GetChannelsPeakValues());
        Assert.Throws<ObjectDisposedException>(() => properties.TryGetValue(PKEY.DeviceFriendlyName, out _));
        Assert.Throws<ObjectDisposedException>(() => { _ = step.Step; });
        if (channels.Count > 0)
        {
            Assert.Throws<ObjectDisposedException>(() => { _ = channels[0].VolumeLevel; });
        }
    }

    [Test]
    public void ChannelCollection_ValidatesBothBounds()
    {
        using var controller = new AudioController();
        using var device = AudioFixture.RequireDefaultPlayback(controller);
        var channels = device.Volume.Channels;
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = channels[-1]; });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = channels[channels.Count]; });
        Assert.That(new List<AudioEndpointVolumeChannel>(channels).Count, Is.EqualTo(channels.Count));
    }
}
