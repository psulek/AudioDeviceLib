/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  SessionMetadataTests.cs
  Round-trip coverage for the IAudioSessionControl2 string accessors.

  These pin the vtable layout, not just the values. Declaration order in the interop interface IS
  the vtable order, so transposing a getter and a setter still compiles, still type-checks, and
  quietly calls the wrong native method - GetIconPath landing on SetDisplayName. A round trip
  through the real COM object is the only thing that catches it.
*/

using System;
using AudioDeviceLib.CoreAudioApi;
using AudioDeviceLib.CoreAudioApi.Extensions;
using AudioDeviceLib.CoreAudioApi.Interfaces;
using AudioDeviceLib.Lib;
using NUnit.Framework;

namespace AudioDeviceLib.UnitTests;

[TestFixture]
[NonParallelizable]
public class SessionMetadataTests
{
    private AudioController _audio;
    private AudioDevice _device;
    private AudioSessionControl _session;
    private string _originalIconPath;

    // AudioSessionControl exposes the getters but not the setters, so writes go through the raw
    // interop interface, as SessionEventContextTests does.
    private IAudioSessionControl2 Raw => _session._AudioSessionControl;

    [SetUp]
    public void SetUp()
    {
        _audio = new AudioController();
        _device = AudioFixture.RequireDefaultPlayback(_audio);
        _session = RequireSession(_device);
        _originalIconPath = _session.IconPath;
    }

    [TearDown]
    public void TearDown()
    {
        if (_session != null && _originalIconPath != null)
        {
            try
            {
                AudioSessionControl2Extensions.SetIconPath(Raw, _originalIconPath);
            }
            catch (Exception ex)
            {
                // Reported, not thrown: this must not mask an assertion failure already in flight.
                TestContext.Progress.WriteLine($"Could not restore session icon path: {ex.Message}");
            }
        }

        _device?.Dispose();
        _audio?.Dispose();
    }

    // The getter must land on IAudioSessionControl::GetIconPath. If it lands on the adjacent
    // SetDisplayName slot instead, the value written here never comes back - the read returns null
    // and the session's display name is overwritten as a side effect.
    [Test]
    public void IconPath_RoundTripsThroughTheSession()
    {
        string expected = $@"C:\AudioDeviceLib-test\icon-{Guid.NewGuid():N}.ico";

        AssertSucceeded(AudioSessionControl2Extensions.SetIconPath(Raw, expected), "SetIconPath");

        Assert.That(_session.IconPath, Is.EqualTo(expected),
            "IconPath did not read back what SetIconPath wrote; the getter is on the wrong vtable slot");
    }

    // ToSessionInfo reads the same accessors with the HRESULTs ignored, so a misrouted getter shows
    // up here as a null field rather than an exception.
    [Test]
    public void ToSessionInfo_ReportsTheSameIconPathAsTheGetter()
    {
        string expected = $@"C:\AudioDeviceLib-test\snapshot-{Guid.NewGuid():N}.ico";

        AssertSucceeded(AudioSessionControl2Extensions.SetIconPath(Raw, expected), "SetIconPath");
        AudioSessionInfo info = _session.ToSessionInfo();

        Assert.That(info.IconPath, Is.EqualTo(expected));
        Assert.That(info.IconPath, Is.EqualTo(_session.IconPath));
    }

    // Reading a snapshot must not write anything. A getter sitting on a setter's slot turns every
    // ToSessionInfo call into a SetDisplayName with an empty string.
    [Test]
    public void ToSessionInfo_DoesNotModifyTheSession()
    {
        string before = _session.DisplayName;

        for (var i = 0; i < 5; i++)
        {
            _session.ToSessionInfo();
        }

        Assert.That(_session.DisplayName, Is.EqualTo(before),
            "reading a session snapshot changed the session's display name");
    }

    // Identifiers come from IAudioSessionControl2's own slots, after the transposed pair. They are
    // read here so that a shift affecting the whole tail of the vtable would also show up.
    [Test]
    public void Identifiers_AreReadableAndStable()
    {
        string identifier = _session.SessionIdentifier;
        string instance = _session.SessionInstanceIdentifier;

        Assert.That(identifier, Is.Not.Null.And.Not.Empty);
        Assert.That(instance, Is.Not.Null.And.Not.Empty);
        Assert.That(_session.SessionIdentifier, Is.EqualTo(identifier));
        Assert.That(_session.ToSessionInfo().SessionInstanceIdentifier, Is.EqualTo(instance));
    }

    private static void AssertSucceeded(int hr, string method)
    {
        Assert.That(hr, Is.GreaterThanOrEqualTo(0), $"{method} failed with HRESULT 0x{hr:X8}");
    }

    // Prefers an ordinary application session over the reserved system-sounds one, which silently
    // no-ops SetDisplayName, or skips the calling test. A machine with audio hardware but nothing
    // playing can still have zero sessions.
    private static AudioSessionControl RequireSession(AudioDevice device)
    {
        SessionCollection sessions = device.SessionManager.Sessions;
        if (sessions.Count == 0)
        {
            Assert.Ignore("No active audio sessions on this machine (headless CI, or nothing is playing audio).");
        }

        for (var i = 0; i < sessions.Count; i++)
        {
            if (!sessions[i].IsSystemSoundsSession)
            {
                return sessions[i];
            }
        }

        return sessions[0];
    }
}
