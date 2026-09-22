/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  EndpointVolumeEventTests.cs
  Round-trip coverage for AudioEndpointVolumeExtensions - the seven IAudioEndpointVolume entry
  points that take a `ref Guid eventContext`. One AudioController subscribes to the endpoint's
  volume notifications, a second one drives the extension methods, and every call is matched back
  to the notification it produced through the event-context GUID it was given.

  Unlike DeviceTests, these tests CHANGE the machine: master volume, per-channel volume and mute
  all move while they run. SetUp captures the endpoint's state and TearDown puts it back. Each
  test skips via Assert.Ignore when there is no default playback endpoint, so a headless CI run
  reports them as skipped.
*/

using System;
using System.Collections.Generic;
using System.Threading;
using AudioDeviceLib.CoreAudioApi;
using AudioDeviceLib.CoreAudioApi.Extensions;
using AudioDeviceLib.CoreAudioApi.Interfaces;
using NUnit.Framework;

namespace AudioDeviceLib.UnitTests;

[TestFixture]
[NonParallelizable]
public class EndpointVolumeEventTests
{
    // Core Audio dispatches the callback on its own thread, so every wait here is cross-thread and
    // gets a generous ceiling rather than a tight one.
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    // A quiet, unambiguous starting point: far enough from both ends that a step up and a step down
    // both have somewhere to go, and low enough not to startle whoever runs the suite.
    private const float BaselineScalar = 0.20f;

    private AudioController _listener = null!; // Assigned by NUnit setup before a test executes.
    private AudioController _driver = null!; // Assigned by NUnit setup before a test executes.
    private AudioDevice _listenerDevice = null!; // Assigned by NUnit setup before a test executes.
    private AudioDevice _driverDevice = null!; // Assigned by NUnit setup before a test executes.
    private VolumeNotificationRecorder _recorder = null!; // Assigned by NUnit setup before a test executes.
    private IDisposable _volumeRegistration = null!; // Assigned by NUnit setup before a test executes.

    private float _originalMasterScalar;
    private bool _originalMute;
    private float[] _originalChannelScalars = null!; // Assigned by NUnit setup before a test executes.

    // Acquired independently; this fixture owns exactly one native reference.
    private IAudioEndpointVolumeCOM Driver = null!;

    [SetUp]
    public void SetUp()
    {
        _listener = new AudioController();
        _listenerDevice = AudioFixture.RequireDefaultPlayback(_listener);

        // A second controller on the same endpoint: the notifications asserted below therefore
        // travel through Core Audio to a separate client, not through a shortcut in the wrapper.
        _driver = new AudioController();
        _driverDevice = AudioFixture.RequireDeviceById(_driver, _listenerDevice.Id);
        Driver = NativeAudio.Activate<IAudioEndpointVolumeCOM>(_listenerDevice.Id);

        AudioEndpointVolume volume = _driverDevice.Volume;
        _originalMasterScalar = volume.MasterVolumeLevelScalar;
        _originalMute = volume.Mute;
        _originalChannelScalars = new float[volume.Channels.Count];
        for (int i = 0; i < _originalChannelScalars.Length; i++)
        {
            _originalChannelScalars[i] = volume.Channels[i].VolumeLevelScalar;
        }

        _recorder = new VolumeNotificationRecorder();
        _volumeRegistration = _listenerDevice.Volume.RegisterVolumeNotification(_recorder);
    }

    [TearDown]
    public void TearDown()
    {
        _volumeRegistration?.Dispose();
        _volumeRegistration = null!;

        RestoreEndpointState();
        NativeAudio.Release(Driver);
        Driver = null!;

        _driverDevice?.Dispose();
        _driver?.Dispose();
        _listenerDevice?.Dispose();
        _listener?.Dispose();

        _driverDevice = null!;
        _driver = null!;
        _listenerDevice = null!;
        _listener = null!;
        _recorder = null!;
        _originalChannelScalars = null!;
    }

    [Test]
    public void SetMasterVolumeLevelScalar_NotifiesWithEventContext()
    {
        AudioEndpointVolume volume = _driverDevice.Volume;
        volume.MasterVolumeLevelScalar = BaselineScalar;

        float target = BaselineScalar / 2f;
        Guid context = Guid.NewGuid();

        AssertChanged(
            AudioEndpointVolumeExtensions.SetMasterVolumeLevelScalar(Driver, target, context),
            nameof(AudioEndpointVolumeExtensions.SetMasterVolumeLevelScalar));

        AudioVolumeNotificationData data = AssertNotified(
            context, nameof(AudioEndpointVolumeExtensions.SetMasterVolumeLevelScalar));

        Assert.That(data.MasterVolume, Is.EqualTo(target).Within(0.02f));
        Assert.That(volume.MasterVolumeLevelScalar, Is.EqualTo(target).Within(0.02f));
    }

    [Test]
    public void SetMasterVolumeLevel_NotifiesWithEventContext()
    {
        AudioEndpointVolume volume = _driverDevice.Volume;
        volume.MasterVolumeLevelScalar = BaselineScalar;

        float target = RequireQuieterDb(volume.VolumeRange, volume.MasterVolumeLevel);
        Guid context = Guid.NewGuid();

        AssertChanged(
            AudioEndpointVolumeExtensions.SetMasterVolumeLevel(Driver, target, context),
            nameof(AudioEndpointVolumeExtensions.SetMasterVolumeLevel));

        AssertNotified(context, nameof(AudioEndpointVolumeExtensions.SetMasterVolumeLevel));

        Assert.That(volume.MasterVolumeLevel, Is.EqualTo(target).Within(DbTolerance(volume.VolumeRange)));
    }

    [Test]
    public void SetChannelVolumeLevelScalar_NotifiesWithEventContext()
    {
        AudioEndpointVolume volume = _driverDevice.Volume;
        RequireChannels(volume);

        volume.Channels[0].VolumeLevelScalar = BaselineScalar;

        float target = BaselineScalar / 2f;
        Guid context = Guid.NewGuid();

        AssertChanged(
            AudioEndpointVolumeExtensions.SetChannelVolumeLevelScalar(Driver, 0, target, context),
            nameof(AudioEndpointVolumeExtensions.SetChannelVolumeLevelScalar));

        AudioVolumeNotificationData data = AssertNotified(
            context, nameof(AudioEndpointVolumeExtensions.SetChannelVolumeLevelScalar));

        // Only the arity is asserted from the notification. Its per-channel array is whatever the
        // endpoint puts there, and hardware disagrees: an endpoint whose GetChannelCount reports 1
        // can still announce 2 channels here and fill both slots with 0.0. The endpoint itself is
        // the authority on what the call did.
        Assert.That(data.ChannelVolume, Has.Count.EqualTo(data.Channels));
        Assert.That(volume.Channels[0].VolumeLevelScalar, Is.EqualTo(target).Within(0.02f));
    }

    [Test]
    public void SetChannelVolumeLevel_NotifiesWithEventContext()
    {
        AudioEndpointVolume volume = _driverDevice.Volume;
        RequireChannels(volume);

        volume.Channels[0].VolumeLevelScalar = BaselineScalar;

        float target = RequireQuieterDb(volume.VolumeRange, volume.Channels[0].VolumeLevel);
        Guid context = Guid.NewGuid();

        AssertChanged(
            AudioEndpointVolumeExtensions.SetChannelVolumeLevel(Driver, 0, target, context),
            nameof(AudioEndpointVolumeExtensions.SetChannelVolumeLevel));

        AssertNotified(context, nameof(AudioEndpointVolumeExtensions.SetChannelVolumeLevel));

        Assert.That(volume.Channels[0].VolumeLevel, Is.EqualTo(target).Within(DbTolerance(volume.VolumeRange)));
    }

    [Test]
    public void SetMute_NotifiesWithEventContext()
    {
        AudioEndpointVolume volume = _driverDevice.Volume;
        volume.Mute = false;

        Guid context = Guid.NewGuid();

        AssertChanged(
            AudioEndpointVolumeExtensions.SetMute(Driver, true, context),
            nameof(AudioEndpointVolumeExtensions.SetMute));

        AudioVolumeNotificationData data = AssertNotified(context, nameof(AudioEndpointVolumeExtensions.SetMute));

        Assert.That(data.Muted, Is.True);
        Assert.That(volume.Mute, Is.True);
    }

    [Test]
    public void VolumeStepUp_NotifiesWithEventContext()
    {
        AudioEndpointVolume volume = _driverDevice.Volume;
        RequireVolumeSteps(volume);
        volume.MasterVolumeLevelScalar = BaselineScalar;

        float before = volume.MasterVolumeLevelScalar;
        Guid context = Guid.NewGuid();

        AssertChanged(
            AudioEndpointVolumeExtensions.VolumeStepUp(Driver, context),
            nameof(AudioEndpointVolumeExtensions.VolumeStepUp));

        AssertNotified(context, nameof(AudioEndpointVolumeExtensions.VolumeStepUp));

        Assert.That(volume.MasterVolumeLevelScalar, Is.GreaterThan(before));
    }

    [Test]
    public void VolumeStepDown_NotifiesWithEventContext()
    {
        AudioEndpointVolume volume = _driverDevice.Volume;
        RequireVolumeSteps(volume);
        volume.MasterVolumeLevelScalar = BaselineScalar;

        float before = volume.MasterVolumeLevelScalar;
        Guid context = Guid.NewGuid();

        AssertChanged(
            AudioEndpointVolumeExtensions.VolumeStepDown(Driver, context),
            nameof(AudioEndpointVolumeExtensions.VolumeStepDown));

        AssertNotified(context, nameof(AudioEndpointVolumeExtensions.VolumeStepDown));

        Assert.That(volume.MasterVolumeLevelScalar, Is.LessThan(before));
    }

    // The omnibus case the per-method tests decompose: drive all seven event-context extensions in
    // one run, each with its own GUID, then check that every GUID came back on a notification.
    [Test]
    public void EveryEventContextExtension_NotifiesWithItsOwnEventContext()
    {
        AudioEndpointVolume volume = _driverDevice.Volume;
        RequireChannels(volume);
        RequireVolumeSteps(volume);

        var calls = new List<KeyValuePair<string, Guid>>
        {
            Invoke(nameof(AudioEndpointVolumeExtensions.SetMasterVolumeLevelScalar), context =>
            {
                volume.MasterVolumeLevelScalar = BaselineScalar;
                return AudioEndpointVolumeExtensions.SetMasterVolumeLevelScalar(
                    Driver, BaselineScalar / 2f, context);
            }),

            Invoke(nameof(AudioEndpointVolumeExtensions.SetMasterVolumeLevel), context =>
            {
                volume.MasterVolumeLevelScalar = BaselineScalar;
                float target = RequireQuieterDb(volume.VolumeRange, volume.MasterVolumeLevel);
                return AudioEndpointVolumeExtensions.SetMasterVolumeLevel(Driver, target, context);
            }),

            Invoke(nameof(AudioEndpointVolumeExtensions.SetChannelVolumeLevelScalar), context =>
            {
                volume.Channels[0].VolumeLevelScalar = BaselineScalar;
                return AudioEndpointVolumeExtensions.SetChannelVolumeLevelScalar(
                    Driver, 0, BaselineScalar / 2f, context);
            }),

            Invoke(nameof(AudioEndpointVolumeExtensions.SetChannelVolumeLevel), context =>
            {
                volume.Channels[0].VolumeLevelScalar = BaselineScalar;
                float target = RequireQuieterDb(volume.VolumeRange, volume.Channels[0].VolumeLevel);
                return AudioEndpointVolumeExtensions.SetChannelVolumeLevel(Driver, 0, target, context);
            }),

            Invoke(nameof(AudioEndpointVolumeExtensions.SetMute), context =>
            {
                volume.Mute = false;
                return AudioEndpointVolumeExtensions.SetMute(Driver, true, context);
            }),

            Invoke(nameof(AudioEndpointVolumeExtensions.VolumeStepUp), context =>
            {
                volume.Mute = false;
                volume.MasterVolumeLevelScalar = BaselineScalar;
                return AudioEndpointVolumeExtensions.VolumeStepUp(Driver, context);
            }),

            Invoke(nameof(AudioEndpointVolumeExtensions.VolumeStepDown), context =>
            {
                volume.MasterVolumeLevelScalar = BaselineScalar;
                return AudioEndpointVolumeExtensions.VolumeStepDown(Driver, context);
            }),
        };

        var missing = new List<string>();
        var contexts = new List<Guid>();
        foreach (KeyValuePair<string, Guid> call in calls)
        {
            contexts.Add(call.Value);
            if (_recorder.WaitFor(call.Value, NotificationTimeout) == null)
            {
                missing.Add($"{call.Key} ({call.Value:D})");
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(calls, Has.Count.EqualTo(7), "every extension method carrying an event context must be driven here");
            Assert.That(contexts, Is.Unique, "each call must be identifiable by its own event context");
            Assert.That(missing, Is.Empty,
                $"no notification carried these event contexts: {string.Join(", ", missing)}. " +
                $"Contexts seen: {_recorder.DescribeSeen()}");
        });
    }

    // The same extensions with no context supplied substitute Guid.Empty, which is what Core Audio
    // would have reported for a null eventContext anyway.
    [Test]
    public void OmittedEventContext_NotifiesWithEmptyGuid()
    {
        AudioEndpointVolume volume = _driverDevice.Volume;
        volume.MasterVolumeLevelScalar = BaselineScalar;

        float target = BaselineScalar / 2f;

        AssertChanged(
            AudioEndpointVolumeExtensions.SetMasterVolumeLevelScalar(Driver, target),
            nameof(AudioEndpointVolumeExtensions.SetMasterVolumeLevelScalar));

        // Matched on the value as well as the context: the baseline set above also arrives with an
        // empty context, so the GUID alone would not identify this call.
        AudioVolumeNotificationData? data = _recorder.WaitFor(
            d => d.EventContext == Guid.Empty && Math.Abs(d.MasterVolume - target) < 0.02f,
            NotificationTimeout);

        Assert.That(data, Is.Not.Null,
            $"expected a notification with an empty event context at {target:0.###}. " +
            $"Contexts seen: {_recorder.DescribeSeen()}");
    }

    private KeyValuePair<string, Guid> Invoke(string name, Func<Guid, int> call)
    {
        Guid context = Guid.NewGuid();
        AssertChanged(call(context), name);
        return new KeyValuePair<string, Guid>(name, context);
    }

    private AudioVolumeNotificationData AssertNotified(Guid eventContext, string method)
    {
        AudioVolumeNotificationData? data = _recorder.WaitFor(eventContext, NotificationTimeout);

        Assert.That(data, Is.Not.Null,
            $"{method} raised no notification carrying event context {eventContext:D} within " +
            $"{NotificationTimeout.TotalSeconds:0}s. Contexts seen: {_recorder.DescribeSeen()}");
        data = AudioFixture.RequireValue(data);
        Assert.That(data.EventContext, Is.EqualTo(eventContext));

        return data;
    }

    private static void AssertChanged(int hr, string method)
    {
        Assert.That(hr, Is.GreaterThanOrEqualTo(0), $"{method} failed with HRESULT 0x{hr:X8}");

        // S_FALSE (1) means Core Audio found nothing to change, and an unchanged endpoint raises no
        // notification - so a test that saw it would time out for the wrong reason.
        //
        // Core Audio documents S_FALSE-on-no-change only for SetMute. The four volume-level setters
        // document no S_FALSE at all, so treating every setter alike here rests on observed
        // behaviour rather than on the contract; it fails loudly if that ever stops holding.
        Assert.That(hr, Is.Zero,
            $"{method} returned S_FALSE: the endpoint was already in the requested state, so no " +
            "notification is raised. The test's starting point needs to differ from its target.");
    }

    private static void RequireChannels(AudioEndpointVolume volume)
    {
        if (volume.Channels.Count == 0)
        {
            Assert.Ignore("Endpoint exposes no per-channel volume controls.");
        }
    }

    private static void RequireVolumeSteps(AudioEndpointVolume volume)
    {
        if (volume.StepInformation.StepCount < 2)
        {
            Assert.Ignore("Endpoint exposes fewer than two volume steps, so stepping cannot change it.");
        }
    }

    // A decibel target six increments below the current level, or above it when there is no room
    // below. Quieter by preference so a test run never makes the machine suddenly louder.
    private static float RequireQuieterDb(AudioEndPointVolumeVolumeRange range, float currentDb)
    {
        float delta = Math.Max(range.IncrementdB, 0.5f) * 6f;
        float target = currentDb - delta;
        if (target < range.MindB)
        {
            target = Math.Min(currentDb + delta, range.MaxdB);
        }

        if (Math.Abs(target - currentDb) < 0.01f)
        {
            Assert.Ignore($"Endpoint exposes no usable decibel range ({range.MindB} to {range.MaxdB} dB).");
        }

        return target;
    }

    private static float DbTolerance(AudioEndPointVolumeVolumeRange range)
    {
        return Math.Max(range.IncrementdB, 0.5f);
    }

    private void RestoreEndpointState()
    {
        if (_driverDevice == null || _originalChannelScalars == null)
        {
            return;
        }

        try
        {
            AudioEndpointVolume volume = _driverDevice.Volume;
            int channels = Math.Min(_originalChannelScalars.Length, volume.Channels.Count);
            for (int i = 0; i < channels; i++)
            {
                volume.Channels[i].VolumeLevelScalar = _originalChannelScalars[i];
            }

            volume.MasterVolumeLevelScalar = _originalMasterScalar;
            volume.Mute = _originalMute;
        }
        catch (Exception ex)
        {
            // Reported, not thrown: a throwing teardown would replace the real assertion failure
            // with a teardown error and hide what the test actually found.
            TestContext.Progress.WriteLine(
                $"Could not restore endpoint volume state: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // Collects the notifications the listening controller receives. Core Audio raises them on its
    // own thread, so the list is guarded and waiters are woken from inside the callback.
    private sealed class VolumeNotificationRecorder : IAudioEndpointVolumeEvents
    {
        private readonly object _gate = new object();
        private readonly List<AudioVolumeNotificationData> _received = new List<AudioVolumeNotificationData>();

        public void OnVolumeNotification(AudioVolumeNotificationData data)
        {
            lock (_gate)
            {
                _received.Add(data);
                Monitor.PulseAll(_gate);
            }
        }

        internal AudioVolumeNotificationData? WaitFor(Guid eventContext, TimeSpan timeout)
        {
            return WaitFor(data => data.EventContext == eventContext, timeout);
        }

        internal AudioVolumeNotificationData? WaitFor(Func<AudioVolumeNotificationData, bool> match, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;

            lock (_gate)
            {
                int scanned = 0;
                while (true)
                {
                    for (; scanned < _received.Count; scanned++)
                    {
                        if (match(_received[scanned]))
                        {
                            return _received[scanned];
                        }
                    }

                    TimeSpan remaining = deadline - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                    {
                        return null;
                    }

                    Monitor.Wait(_gate, remaining);
                }
            }
        }

        internal string DescribeSeen()
        {
            lock (_gate)
            {
                if (_received.Count == 0)
                {
                    return "(none)";
                }

                var parts = new List<string>(_received.Count);
                foreach (AudioVolumeNotificationData data in _received)
                {
                    parts.Add($"{data.EventContext:D}@{data.MasterVolume:0.###}{(data.Muted ? " muted" : string.Empty)}");
                }

                return string.Join(", ", parts);
            }
        }
    }
}
