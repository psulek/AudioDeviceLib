/* Copyright (c) 2026 Peter Šulek. MIT License. */
using System;
using NUnit.Framework;

namespace AudioDeviceLib.UnitTests;

// The native driver owns its session; observations travel through the public library callback.
// This verifies a supplied GUID, not the inbound null-pointer branch of the COM adapter.
[TestFixture]
[NonParallelizable]
public class SessionEventContextTests
{
    [Test]
    public void SuppliedEventContext_ArrivesIntact()
    {
        using var session = OwnedAudioSession.Create();
        Guid context = Guid.NewGuid();
        string target = $@"C:\AudioDeviceLib-test\context-{Guid.NewGuid():N}.ico";
        OwnedAudioSession.IconNotification received = session.ChangeIcon(target, context);
        Assert.That(received.Context, Is.EqualTo(context));
        Assert.That(received.Session.IconPath, Is.EqualTo(target));
    }
}
