/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  EndpointVolumeRegistrationTests.cs
  Coverage for the AudioEndpointVolume.RegisterVolumeNotification registration model: fan-out to
  several consumers, per-consumer isolation, token lifetime and disposal.

  These tests drive AudioEndpointVolume.FireNotification directly (visible through
  InternalsVisibleTo) instead of moving the machine's volume. Dispatch is what is under test here,
  not the COM plumbing - EndpointVolumeEventTests already covers real notifications arriving from
  Core Audio - so the tests are deterministic and leave the endpoint untouched.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using AudioDeviceLib.CoreAudioApi;
using NUnit.Framework;

namespace AudioDeviceLib.UnitTests;

[TestFixture]
[NonParallelizable]
public class EndpointVolumeRegistrationTests
{
    private AudioController _audio = null!; // Assigned by NUnit setup before a test executes.
    private AudioDevice _device = null!; // Assigned by NUnit setup before a test executes.

    private static AudioVolumeNotificationData Notification(float masterVolume = 0.5f) =>
        new AudioVolumeNotificationData(Guid.NewGuid(), false, masterVolume, new float[] { masterVolume });

    [SetUp]
    public void SetUp()
    {
        _audio = new AudioController();
        _device = AudioFixture.RequireDefaultPlayback(_audio);
    }

    [TearDown]
    public void TearDown()
    {
        _device?.Dispose();
        _audio?.Dispose();
        _device = null!;
        _audio = null!;
    }

    [Test]
    public void RegisterVolumeNotification_NullConsumer_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _device.Volume.RegisterVolumeNotification(null!));
    }

    [Test]
    public void RegisterVolumeNotification_DeliversToEveryConsumer()
    {
        var first = new Recorder();
        var second = new Recorder();

        using (_device.Volume.RegisterVolumeNotification(first))
        using (_device.Volume.RegisterVolumeNotification(second))
        {
            _device.Volume.FireNotification(Notification(0.25f));
        }

        Assert.That(first.Received.Count, Is.EqualTo(1), "first consumer");
        Assert.That(second.Received.Count, Is.EqualTo(1), "second consumer");
        Assert.That(first.Received[0].MasterVolume, Is.EqualTo(0.25f));
    }

    [Test]
    public void RegisterVolumeNotification_SameConsumerTwice_RegistersOnceAndReturnsSameToken()
    {
        var consumer = new Recorder();

        IDisposable first = _device.Volume.RegisterVolumeNotification(consumer);
        IDisposable second = _device.Volume.RegisterVolumeNotification(consumer);

        try
        {
            Assert.That(second, Is.SameAs(first), "re-registering must hand back the original token");

            _device.Volume.FireNotification(Notification());
            Assert.That(consumer.Received.Count, Is.EqualTo(1), "a consumer must not be notified twice");
        }
        finally
        {
            first.Dispose();
        }
    }

    // The reason the registration model replaced the multicast event: with an event, the first
    // handler to throw suppressed every handler after it.
    [Test]
    public void FireNotification_ThrowingConsumer_DoesNotStarveTheOthers()
    {
        var before = new Recorder();
        var thrower = new ThrowingRecorder();
        var after = new Recorder();

        using (_device.Volume.RegisterVolumeNotification(before))
        using (_device.Volume.RegisterVolumeNotification(thrower))
        using (_device.Volume.RegisterVolumeNotification(after))
        {
            Assert.Throws<InvalidOperationException>(() => _device.Volume.FireNotification(Notification()));
        }

        // Order of dispatch is deliberately not asserted; what matters is that neither consumer is
        // skipped because another one threw.
        Assert.That(before.Received.Count, Is.EqualTo(1), "consumer registered before the throwing one");
        Assert.That(thrower.Calls, Is.EqualTo(1), "throwing consumer");
        Assert.That(after.Received.Count, Is.EqualTo(1), "consumer registered after the throwing one");
    }

    [Test]
    public void FireNotification_SeveralThrowingConsumers_ReportsThemAllAsAggregate()
    {
        var first = new ThrowingRecorder();
        var second = new ThrowingRecorder();

        using (_device.Volume.RegisterVolumeNotification(first))
        using (_device.Volume.RegisterVolumeNotification(second))
        {
            AggregateException? ex = Assert.Throws<AggregateException>(
                () => _device.Volume.FireNotification(Notification()));

            ex = AudioFixture.RequireValue(ex);
            Assert.That(ex.InnerExceptions.Count, Is.EqualTo(2));
        }

        Assert.That(first.Calls, Is.EqualTo(1));
        Assert.That(second.Calls, Is.EqualTo(1));
    }

    [Test]
    public void DisposingToken_StopsDelivery_AndIsIdempotent()
    {
        var consumer = new Recorder();
        IDisposable token = _device.Volume.RegisterVolumeNotification(consumer);

        _device.Volume.FireNotification(Notification());
        Assert.That(consumer.Received.Count, Is.EqualTo(1), "registered");

        token.Dispose();
        token.Dispose();

        _device.Volume.FireNotification(Notification());
        Assert.That(consumer.Received.Count, Is.EqualTo(1), "no delivery after the token is disposed");
    }

    [Test]
    public void DisposingToken_LeavesOtherConsumersRegistered()
    {
        var removed = new Recorder();
        var kept = new Recorder();

        IDisposable removedToken = _device.Volume.RegisterVolumeNotification(removed);
        using (_device.Volume.RegisterVolumeNotification(kept))
        {
            removedToken.Dispose();
            _device.Volume.FireNotification(Notification());
        }

        Assert.That(removed.Received.Count, Is.EqualTo(0));
        Assert.That(kept.Received.Count, Is.EqualTo(1));
    }

    [Test]
    public void StaleToken_DoesNotRevokeALaterRegistrationOfTheSameConsumer()
    {
        var consumer = new Recorder();

        IDisposable stale = _device.Volume.RegisterVolumeNotification(consumer);
        stale.Dispose();

        using (_device.Volume.RegisterVolumeNotification(consumer))
        {
            stale.Dispose();

            _device.Volume.FireNotification(Notification());
            Assert.That(consumer.Received.Count, Is.EqualTo(1), "the re-registration must survive the stale token");
        }
    }

    [Test]
    public void Dispose_UnregistersEveryConsumer_AndBlocksFurtherRegistration()
    {
        var consumer = new Recorder();

        // A dedicated endpoint object: disposing the one owned by the shared _device would leave
        // the device holding a disposed child.
        AudioController audio = new AudioController();
        try
        {
            AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);
            try
            {
                AudioEndpointVolume volume = device.Volume;
                volume.RegisterVolumeNotification(consumer);

                volume.Dispose();

                volume.FireNotification(Notification());
                Assert.That(consumer.Received.Count, Is.EqualTo(0), "disposal must drop every registration");

                Assert.Throws<ObjectDisposedException>(() => volume.RegisterVolumeNotification(consumer));
            }
            finally
            {
                device.Dispose();
            }
        }
        finally
        {
            audio.Dispose();
        }
    }

    [Test]
    public void Dispose_CalledTwice_IsNoOp()
    {
        AudioController audio = new AudioController();
        try
        {
            AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);
            try
            {
                AudioEndpointVolume volume = device.Volume;
                volume.RegisterVolumeNotification(new Recorder());

                volume.Dispose();
                Assert.DoesNotThrow(() => volume.Dispose(), "a second Dispose must be a no-op");

                Assert.Throws<ObjectDisposedException>(() => volume.RegisterVolumeNotification(new Recorder()));
            }
            finally
            {
                device.Dispose();
            }
        }
        finally
        {
            audio.Dispose();
        }
    }

    // The native callback must be unregistered exactly once, so Dispose claims it under the same
    // lock that guards registration. Without that, racing disposers can each see a non-null
    // callback and unregister it twice.
    [Test]
    public void Dispose_FromSeveralThreadsAtOnce_DoesNotThrow()
    {
        AudioController audio = new AudioController();
        try
        {
            AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);
            try
            {
                AudioEndpointVolume volume = device.Volume;
                volume.RegisterVolumeNotification(new Recorder());

                const int racers = 8;
                var start = new ManualResetEventSlim(false);
                var failures = new ConcurrentQueue<Exception>();
                var threads = new Thread[racers];

                for (int i = 0; i < racers; i++)
                {
                    threads[i] = new Thread(() =>
                    {
                        start.Wait();
                        try
                        {
                            volume.Dispose();
                        }
                        catch (Exception ex)
                        {
                            failures.Enqueue(ex);
                        }
                    });
                    threads[i].Start();
                }

                start.Set();
                foreach (Thread thread in threads)
                {
                    Assert.That(thread.Join(TimeSpan.FromSeconds(10)), Is.True, "a disposing thread hung");
                }

                Assert.That(failures, Is.Empty, "no Dispose call may throw");
                Assert.Throws<ObjectDisposedException>(() => volume.RegisterVolumeNotification(new Recorder()));
            }
            finally
            {
                device.Dispose();
            }
        }
        finally
        {
            audio.Dispose();
        }
    }

    private sealed class Recorder : IAudioEndpointVolumeEvents
    {
        internal readonly List<AudioVolumeNotificationData> Received = new List<AudioVolumeNotificationData>();

        public void OnVolumeNotification(AudioVolumeNotificationData data)
        {
            Received.Add(data);
        }
    }

    private sealed class ThrowingRecorder : IAudioEndpointVolumeEvents
    {
        internal int Calls;

        public void OnVolumeNotification(AudioVolumeNotificationData data)
        {
            Calls++;
            throw new InvalidOperationException("consumer failure");
        }
    }
}
