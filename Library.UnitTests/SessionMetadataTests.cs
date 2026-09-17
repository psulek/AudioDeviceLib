/* Copyright (c) 2026 Peter Šulek. MIT License. */
using System;
using System.Diagnostics;
using AudioDeviceLib.CoreAudioApi;
using AudioDeviceLib.CoreAudioApi.Extensions;
using NUnit.Framework;

namespace AudioDeviceLib.UnitTests;

[TestFixture]
[NonParallelizable]
public class SessionMetadataTests
{
    private OwnedAudioSession _session = null!;

    [SetUp]
    public void SetUp() => _session = OwnedAudioSession.Create();

    [TearDown]
    public void TearDown()
    {
        _session?.Dispose();
        _session = null!;
    }

    [Test]
    public void IconPath_RoundTripsThroughTheNativeSession()
    {
        string expected = UniqueIconPath();
        NativeAudio.Check(_session.Raw.SetIconPath(expected));
        NativeAudio.Check(_session.Raw.GetIconPath(out string actual));
        Assert.That(actual, Is.EqualTo(expected), "native icon accessor is on the wrong vtable slot");
    }

    [Test]
    public void IconNotification_ReportsExpectedSnapshotIconPath()
    {
        string expected = UniqueIconPath();
        AudioSessionInfo snapshot = _session.ChangeIcon(expected).Session;
        Assert.That(snapshot.IconPath, Is.EqualTo(expected));
        NativeAudio.Check(_session.Raw.GetIconPath(out string actual));
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void NotificationSnapshots_DoNotModifyDisplayName()
    {
        string expected = $"AudioDeviceLib test {Guid.NewGuid():N}";
        NativeAudio.Check(_session.Raw.SetDisplayName(expected));
        for (int i = 0; i < 5; i++)
        {
            AudioSessionInfo snapshot = _session.ChangeIcon(UniqueIconPath()).Session;
            Assert.That(snapshot.DisplayName, Is.EqualTo(expected));
        }
        NativeAudio.Check(_session.Raw.GetDisplayName(out string actual));
        Assert.That(actual, Is.EqualTo(expected), "snapshot construction changed the native display name");
    }

    [Test]
    public void NotificationIdentity_IsStableAndBelongsToTestProcess()
    {
        NativeAudio.Check(_session.Raw.GetSessionIdentifier(out string identifier));
        NativeAudio.Check(_session.Raw.GetSessionInstanceIdentifier(out string instance));
        Assert.That(identifier, Is.Not.Null.And.Not.Empty);
        Assert.That(instance, Is.Not.Null.And.Not.Empty);
        using var process = Process.GetCurrentProcess();
        for (int i = 0; i < 3; i++)
        {
            AudioSessionInfo snapshot = _session.ChangeIcon(UniqueIconPath()).Session;
            Assert.Multiple(() =>
            {
                Assert.That(snapshot.SessionIdentifier, Is.EqualTo(identifier));
                Assert.That(snapshot.SessionInstanceIdentifier, Is.EqualTo(instance));
                Assert.That(snapshot.ProcessId, Is.EqualTo((uint)process.Id));
                Assert.That(snapshot.IsSystemSoundsSession, Is.False);
            });
        }
    }

    private static string UniqueIconPath() => $@"C:\AudioDeviceLib-test\icon-{Guid.NewGuid():N}.ico";
}
