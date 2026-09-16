/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  ContractTests.cs
  Tests that hold regardless of whether the machine has any audio hardware. These are the
  COM-interop smoke tests: they exercise enumerator creation, endpoint enumeration, the
  Marshal.ReleaseComObject paths and the disposal guards, all of which run identically on a
  machine with zero endpoints.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using AudioDeviceLib.CoreAudioApi;
using AudioDeviceLib.Lib;
using NUnit.Framework;

namespace AudioDeviceLib.UnitTests;

[TestFixture]
public class ContractTests
{
    [Test]
    public void GetDevices_ReturnsNonNullList()
    {
        using var audio = new AudioController();
        IReadOnlyList<AudioDevice> devices = audio.GetDevices();

        Assert.That(devices, Is.Not.Null);
        AudioFixture.DisposeAll(devices);
    }

    [Test]
    public void GetPlaybackAndRecordingDevices_ReturnNonNullLists()
    {
        using var audio = new AudioController();
        IReadOnlyList<AudioDevice> playback = audio.GetPlaybackDevices();
        IReadOnlyList<AudioDevice> recording = audio.GetRecordingDevices();

        Assert.That(playback, Is.Not.Null);
        Assert.That(recording, Is.Not.Null);

        Assert.That(playback.All(d => d.Kind == AudioDeviceKind.Playback), Is.True,
            "GetPlaybackDevices returned a non-playback endpoint.");
        Assert.That(recording.All(d => d.Kind == AudioDeviceKind.Recording), Is.True,
            "GetRecordingDevices returned a non-recording endpoint.");

        AudioFixture.DisposeAll(playback);
        AudioFixture.DisposeAll(recording);
    }

    // The core Marshal.ReleaseComObject regression. AudioController.Dispose releases the
    // IMMDeviceEnumerator, MMDeviceCollection.Dispose releases the collection, and TryGetDefaultId
    // releases the throwaway endpoint it creates just to read an ID. Each wrapper holds exactly one
    // reference, so an over-release surfaces on a LATER call as InvalidComObjectException rather
    // than at the point of the mistake - which is why this repeats rather than checking once.
    [Test]
    public void RepeatedEnumeration_OnOneController_DoesNotOverRelease()
    {
        using var audio = new AudioController();

        for (int i = 0; i < 20; i++)
        {
            IReadOnlyList<AudioDevice> devices = audio.GetDevices();
            Assert.That(devices, Is.Not.Null, $"enumeration {i} returned null");
            AudioFixture.DisposeAll(devices);
        }
    }

    [Test]
    public void RepeatedEnumeration_AcrossControllers_DoesNotOverRelease()
    {
        for (int i = 0; i < 20; i++)
        {
            using var audio = new AudioController();
            IReadOnlyList<AudioDevice> devices = audio.GetDevices();
            Assert.That(devices, Is.Not.Null, $"controller {i} returned null");
            AudioFixture.DisposeAll(devices);
        }
    }

    [Test]
    public void Controller_DoubleDispose_IsNoOp()
    {
        var audio = new AudioController();
        AudioFixture.DisposeAll(audio.GetDevices());

        audio.Dispose();
        Assert.DoesNotThrow(() => audio.Dispose(), "second Dispose must not over-release the enumerator");
    }

    [Test]
    public void Controller_AfterDispose_EnumerationThrowsObjectDisposed()
    {
        var audio = new AudioController();
        audio.Dispose();

        Assert.Throws<ObjectDisposedException>(() => audio.GetDevices());
        Assert.Throws<ObjectDisposedException>(() => audio.GetPlaybackDevices());
        Assert.Throws<ObjectDisposedException>(() => audio.GetDeviceById(AudioFixture.AbsentDeviceId));
    }

    // Releasing the enumerator in Dispose must not disturb COM state for anything else in the
    // process. If it over-released a shared object, this second controller would fail.
    [Test]
    public void NewController_WorksAfterAnotherWasDisposed()
    {
        var first = new AudioController();
        AudioFixture.DisposeAll(first.GetDevices());
        first.Dispose();

        using var second = new AudioController();
        Assert.DoesNotThrow(() => AudioFixture.DisposeAll(second.GetDevices()));
    }

    [Test]
    public void ListDevices_PlaybackPlusRecording_EqualsAll()
    {
        IReadOnlyList<AudioDeviceInfo> all = AudioController.ListDevices();
        IReadOnlyList<AudioDeviceInfo> playback = AudioController.ListDevices(AudioDeviceKind.Playback);
        IReadOnlyList<AudioDeviceInfo> recording = AudioController.ListDevices(AudioDeviceKind.Recording);

        Assert.That(playback.Count + recording.Count, Is.EqualTo(all.Count));
    }

    [Test]
    public void ListDevices_ByKind_ReturnsOnlyThatKind()
    {
        Assert.That(AudioController.ListDevices(AudioDeviceKind.Playback)
            .All(d => d.Kind == AudioDeviceKind.Playback), Is.True);
        Assert.That(AudioController.ListDevices(AudioDeviceKind.Recording)
            .All(d => d.Kind == AudioDeviceKind.Recording), Is.True);
    }

    [Test]
    public void DeviceIds_AreUnique()
    {
        using var audio = new AudioController();
        IReadOnlyList<AudioDevice> devices = audio.GetDevices();

        string[] ids = devices.Select(d => d.Id).ToArray();
        Assert.That(ids, Is.Unique);

        AudioFixture.DisposeAll(devices);
    }

    [Test]
    public void GetVolume_NullId_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => AudioController.GetVolume(null));
    }

    [Test]
    public void GetVolume_MalformedId_ThrowsArgument()
    {
        Assert.Throws<ArgumentException>(() => AudioController.GetVolume(AudioFixture.MalformedDeviceId));
    }

    [Test]
    public void GetVolume_AbsentId_ThrowsCom()
    {
        Assert.Throws<COMException>(() => AudioController.GetVolume(AudioFixture.AbsentDeviceId));
    }

    [Test]
    public void GetDeviceById_NullId_ThrowsArgumentNull()
    {
        using var audio = new AudioController();
        Assert.Throws<ArgumentNullException>(() => audio.GetDeviceById(null));
    }

    // Core Audio reports a malformed ID and an absent-but-well-formed ID differently, and the
    // library passes both through. Asserting each separately pins the contract that GetDeviceById's
    // XML docs now describe.
    [Test]
    public void GetDeviceById_MalformedId_ThrowsArgument()
    {
        using var audio = new AudioController();
        Assert.Throws<ArgumentException>(() => audio.GetDeviceById(AudioFixture.MalformedDeviceId));
    }

    [Test]
    public void GetDeviceById_AbsentId_ThrowsCom()
    {
        using var audio = new AudioController();
        Assert.Throws<COMException>(() => audio.GetDeviceById(AudioFixture.AbsentDeviceId));
    }

    [Test]
    public void GetDeviceInfo_AbsentId_ThrowsCom()
    {
        using var audio = new AudioController();
        Assert.Throws<COMException>(() => audio.GetDeviceInfo(AudioFixture.AbsentDeviceId));
    }

    // Read-only in effect: the name cannot match any endpoint, so nothing is ever set.
    [Test]
    public void SetDefaultPlaybackByName_NoMatch_ReturnsNull()
    {
        Assert.That(AudioController.SetDefaultPlaybackByName(AudioFixture.UnmatchableName), Is.Null);
    }

    [Test]
    public void SetDefaultRecordingByName_NoMatch_ReturnsNull()
    {
        Assert.That(AudioController.SetDefaultRecordingByName(AudioFixture.UnmatchableName), Is.Null);
    }

    [Test]
    public void SetDefaultByName_NullOrEmpty_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => AudioController.SetDefaultPlaybackByName(null));
        Assert.Throws<ArgumentNullException>(() => AudioController.SetDefaultPlaybackByName(string.Empty));
    }

    // MMDeviceCollection releases its IMMDeviceCollection RCW on Dispose and guards its members
    // afterwards. It is reached only through the controller, so this exercises it indirectly:
    // enumeration completing without InvalidComObjectException means the release was well-formed.
    [Test]
    public void Enumeration_LeavesNoInvalidComObject()
    {
        using var audio = new AudioController();

        Assert.DoesNotThrow(() =>
        {
            for (int i = 0; i < 5; i++)
            {
                AudioFixture.DisposeAll(audio.GetDevices(DataFlowFilter.All, DeviceStateFilter.All));
            }
        });
    }
}
