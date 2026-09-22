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
using AudioDeviceLib.CoreAudioApi.Interfaces;
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
        Assert.Throws<ArgumentNullException>(() => AudioController.GetVolume(null!));
    }

    [Test]
    public void GetVolume_MalformedId_ThrowsArgument()
    {
        Assert.Throws<ArgumentException>(() => AudioController.GetVolume(AudioFixture.MalformedDeviceId));
    }

    // The static helpers return a value, not a device, so they cannot answer null the way
    // GetDeviceById does - an absent endpoint has to be an exception. They report it as an
    // ArgumentException naming the ID, which is what WithDevice always intended; before
    // GetDeviceById could return null that branch was unreachable and a raw COMException escaped
    // instead. COMException is now reserved for an endpoint that exists but cannot be read.
    [Test]
    public void GetVolume_AbsentId_ThrowsArgument()
    {
        var ex = Assert.Throws<ArgumentException>(() => AudioController.GetVolume(AudioFixture.AbsentDeviceId));

        ex = AudioFixture.RequireValue(ex);
        Assert.That(ex.Message, Does.Contain(AudioFixture.AbsentDeviceId));
    }

    [Test]
    public void GetDeviceById_NullId_ThrowsArgumentNull()
    {
        using var audio = new AudioController();
        Assert.Throws<ArgumentNullException>(() => audio.GetDeviceById(null!));
    }

    // Core Audio reports a malformed ID and an absent-but-well-formed ID differently, and the
    // library keeps that distinction: malformed is a caller mistake and throws, while absent is an
    // expected outcome of the enumerate-then-resolve race and is reported as null.
    [Test]
    public void GetDeviceById_MalformedId_ThrowsArgument()
    {
        using var audio = new AudioController();
        Assert.Throws<ArgumentException>(() => audio.GetDeviceById(AudioFixture.MalformedDeviceId));
    }

    // A well-formed ID that resolves to nothing is exactly what a caller sees when it holds an ID
    // from an earlier GetDevices() and the endpoint is unplugged or disabled before it resolves it.
    // That is an answer, not a failure - so no exception, and no need for the caller to reach into
    // COMException.ErrorCode to tell the two apart.
    [Test]
    public void GetDeviceById_AbsentId_ReturnsNull()
    {
        using var audio = new AudioController();
        Assert.That(audio.GetDeviceById(AudioFixture.AbsentDeviceId), Is.Null);
    }

    [Test]
    public void GetDeviceInfo_AbsentId_ReturnsNull()
    {
        using var audio = new AudioController();
        Assert.That(audio.GetDeviceInfo(AudioFixture.AbsentDeviceId), Is.Null);
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
        Assert.Throws<ArgumentNullException>(() => AudioController.SetDefaultPlaybackByName(null!));
        Assert.Throws<ArgumentNullException>(() => AudioController.SetDefaultPlaybackByName(string.Empty));
    }

    // MMDeviceCollection releases its IMMDeviceCollection RCW on Dispose and guards its members
    // afterwards. It is reached only through the controller, so this exercises it indirectly:
    // enumeration completing without InvalidComObjectException means the release was well-formed.
    // The policy-config client now refuses to construct unless it actually bound to one of the
    // three IPolicyConfig variants, so activation is no longer allowed to fail open. Constructing
    // it is side-effect free - only SetDefaultEndpoint changes anything - so this asserts the
    // binding works on a supported OS without touching the machine's default device.
    [Test]
    public void PolicyConfigClient_BindsToAnInterfaceVariant()
    {
        Assert.DoesNotThrow(() =>
        {
            using var client = new PolicyConfigClient();
        }, "PolicyConfigClient failed to bind to any IPolicyConfig variant on this system.");
    }

    // Repeated because the constructor was changed from three separate COM activations to one
    // activation plus three QueryInterface casts. An over-release or a leaked wrapper in that
    // path would surface on a later construction rather than the first - and now that Dispose
    // releases the wrapper deterministically, an over-release would surface here too.
    [Test]
    public void PolicyConfigClient_ConstructedRepeatedly_StaysUsable()
    {
        Assert.DoesNotThrow(() =>
        {
            for (int i = 0; i < 5; i++)
            {
                using var client = new PolicyConfigClient();
            }
        });
    }

    // Pins the assumption that makes a single ReleaseComObject in PolicyConfigClient.Dispose
    // correct: casting the activated wrapper to an interface returns THAT wrapper, not a second
    // one. If interop ever handed back distinct wrappers, releasing one would leak the others,
    // and this test is the thing that would say so.
    [Test]
    public void PolicyConfigClient_InterfaceCastsAliasTheSameWrapper()
    {
        object activated = new PolicyConfigClientCOM();
        try
        {
            object? bound = (object?)(activated as IPolicyConfigCOM)
                           ?? (object?)(activated as IPolicyConfigVistaCOM)
                           ?? activated as IPolicyConfig10COM;

            Assert.That(bound, Is.Not.Null, "No IPolicyConfig variant bound on this system.");
            Assert.That(ReferenceEquals(activated, bound), Is.True,
                "Interface cast produced a different wrapper - PolicyConfigClient.Dispose would leak.");
        }
        finally
        {
            Marshal.ReleaseComObject(activated);
        }
    }

    // Verify Count stays stable across first access and every index below Count is valid.
    // This guards against reading different counts for enumeration and cache allocation.
    [Test]
    public void SessionCollection_EveryIndexBelowCount_IsUsable()
    {
        using var audio = new AudioController();
        using AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);

        SessionCollection sessions = device.SessionManager.Sessions;
        int count = sessions.Count;

        Assert.Multiple(() =>
        {
            for (int i = 0; i < count; i++)
            {
                int index = i;
                Assert.DoesNotThrow(() => { var _ = sessions[index]; },
                    $"index {index} was rejected although Count reported {count}");
            }

            Assert.That(sessions.Count, Is.EqualTo(count),
                "Count changed across the first indexed access - it is no longer fixed at construction.");
        });
    }

    // Verify each policy-client activation returns a distinct COM identity.
    // This permits the ComImport coclass pattern without the shared-RCW type collisions
    // that require direct CoCreateInstance activation for MMDeviceEnumerator.
    [Test]
    public void PolicyConfigClient_ActivationsAreDistinctComObjects()
    {
        object first = new PolicyConfigClientCOM();
        object second = new PolicyConfigClientCOM();

        IntPtr firstUnknown = Marshal.GetIUnknownForObject(first);
        IntPtr secondUnknown = Marshal.GetIUnknownForObject(second);

        try
        {
            Assert.That(secondUnknown, Is.Not.EqualTo(firstUnknown),
                "CLSID_PolicyConfigClient has become a process singleton - _policyConfigClient must " +
                "now activate through CoCreateInstance the way AudioController does.");
        }
        finally
        {
            Marshal.Release(firstUnknown);
            Marshal.Release(secondUnknown);
            Marshal.ReleaseComObject(first);
            Marshal.ReleaseComObject(second);
        }
    }

    [Test]
    public void PolicyConfigClient_DisposedTwice_IsNoOp()
    {
        var client = new PolicyConfigClient();
        client.Dispose();

        Assert.DoesNotThrow(() => client.Dispose());
    }

    // The disposed guard matters because Dispose disconnects the wrapper: without it the next call
    // would fail with InvalidComObjectException from deep inside interop rather than saying which
    // object was used after disposal.
    [Test]
    public void PolicyConfigClient_UsedAfterDispose_Throws()
    {
        var client = new PolicyConfigClient();
        client.Dispose();

        Assert.Throws<ObjectDisposedException>(() => client.SetDefaultEndpoint("any-id", Role.Multimedia));
    }

    // The controller caches one policy client and disposes it with everything else. Disposing a
    // controller that never set a default device must not activate one just to release it.
    [Test]
    public void ControllerDispose_WithoutSettingDefault_DoesNotThrow()
    {
        var audio = new AudioController();
        AudioFixture.DisposeAll(audio.GetDevices());

        Assert.DoesNotThrow(() => audio.Dispose());
        Assert.DoesNotThrow(() => audio.Dispose());
    }

    // H7: GetDefault and TryGetDefaultId used to answer "no default device" for every exception,
    // so an over-released COM object or an out-of-memory condition was indistinguishable from a
    // machine with no speakers. Both now filter on this predicate, and these cases pin exactly
    // which failures are allowed to become a null result.
    private static readonly Exception[] TransientEndpointFailures =
    {
        new COMException("endpoint went away", unchecked((int)0x80070490)),
        new COMException("device invalidated", unchecked((int)0x88890004)),
        new InvalidOperationException("the endpoint does not implement IMMEndpoint"),
    };

    [TestCaseSource(nameof(TransientEndpointFailures))]
    public void IsTransientEndpointFailure_EndpointLevelFailures_AreTreatedAsMissingDevice(Exception ex)
    {
        Assert.That(AudioController.IsTransientEndpointFailure(ex), Is.True);
    }

    private static readonly Exception[] FatalFailures =
    {
        new OutOfMemoryException(),
        new InvalidComObjectException("COM object that has been separated from its RCW"),
        new PlatformNotSupportedException(),
        new NotSupportedException("no IPolicyConfig variant"),
        new UnauthorizedAccessException(),
        new ArgumentException("bad argument"),
    };

    [TestCaseSource(nameof(FatalFailures))]
    public void IsTransientEndpointFailure_RealFailures_Propagate(Exception ex)
    {
        Assert.That(AudioController.IsTransientEndpointFailure(ex), Is.False);
    }

    // ObjectDisposedException derives from InvalidOperationException, so it matches the second half
    // of the predicate and only the explicit carve-out keeps it out. Without it a disposed
    // controller would report every device as non-default instead of throwing.
    [Test]
    public void IsTransientEndpointFailure_ObjectDisposed_IsNotTransient()
    {
        var ex = new ObjectDisposedException(nameof(AudioController));

        Assert.That(ex, Is.InstanceOf<InvalidOperationException>(),
            "the carve-out only matters while ObjectDisposedException derives from InvalidOperationException");
        Assert.That(AudioController.IsTransientEndpointFailure(ex), Is.False);
    }

    // The disposed guard runs before any COM work, so this is the one H7 path that can be driven
    // end to end without failing hardware: it must throw rather than answer "no default device".
    [Test]
    public void GetDefaultDevices_OnDisposedController_Throw()
    {
        var audio = new AudioController();
        audio.Dispose();

        Assert.Throws<ObjectDisposedException>(() => audio.GetDefaultPlaybackDevice());
        Assert.Throws<ObjectDisposedException>(() => audio.GetDefaultRecordingDevice());
    }

    // The endpoint lookup is now filtered separately from the work that follows it, and only
    // Core Audio's documented "no such endpoint" HRESULT may become a null answer there. Anything
    // looser would report a stopped audio service as a machine with no default device.
    [Test]
    public void HresultNotFound_IsErrorNotFound()
    {
        Assert.That(AudioController.HresultNotFound, Is.EqualTo(unchecked((int)0x80070490)),
            "HRESULT_FROM_WIN32(ERROR_NOT_FOUND) - what the MMDevice API documents as E_NOTFOUND");
    }

    [Test]
    public void IsEndpointNotFound_ErrorNotFound_IsTrue()
    {
        var ex = new COMException("no endpoint assigned to this flow/role", unchecked((int)0x80070490));

        Assert.That(AudioController.IsEndpointNotFound(ex), Is.True);
    }

    private static readonly Exception[] NotAnAbsentEndpoint =
    {
        new COMException("audio service is not running", unchecked((int)0x80010108)),
        new COMException("device invalidated", unchecked((int)0x88890004)),
        new COMException("out of memory", unchecked((int)0x8007000E)),
        new InvalidOperationException("the endpoint does not implement IMMEndpoint"),
        new ObjectDisposedException(nameof(AudioController)),
        new InvalidComObjectException("COM object that has been separated from its RCW"),
        new OutOfMemoryException(),
    };

    [TestCaseSource(nameof(NotAnAbsentEndpoint))]
    public void IsEndpointNotFound_EverythingElse_IsFalse(Exception ex)
    {
        Assert.That(AudioController.IsEndpointNotFound(ex), Is.False);
    }

    // The two predicates are deliberately different widths: the lookup accepts one HRESULT, the
    // work done on an already-resolved endpoint accepts anything that means "this endpoint is
    // gone". Every absent-endpoint failure must therefore also satisfy the wider one.
    [Test]
    public void IsEndpointNotFound_IsNarrowerThanIsTransientEndpointFailure()
    {
        var notFound = new COMException("no endpoint", AudioController.HresultNotFound);
        var disconnected = new COMException("audio service is not running", unchecked((int)0x80010108));

        Assert.That(AudioController.IsTransientEndpointFailure(notFound), Is.True);
        Assert.That(AudioController.IsEndpointNotFound(disconnected), Is.False);
        Assert.That(AudioController.IsTransientEndpointFailure(disconnected), Is.True,
            "a disconnected endpoint is still tolerated once it has been resolved - just not as 'no default device'");
    }
}
