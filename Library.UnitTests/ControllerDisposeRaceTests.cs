/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  ControllerDisposeRaceTests.cs
  Concurrency coverage for AudioController.Dispose. These do not need audio hardware:
  enumeration on a machine with no endpoints still round-trips through the COM enumerator,
  which is the object under test.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AudioDeviceLib.CoreAudioApi.Interfaces;
using AudioDeviceLib.CoreAudioApi;
using NUnit.Framework;

namespace AudioDeviceLib.UnitTests;

[TestFixture]
[NonParallelizable]
[Explicit("Slow tests, only run when debugging race conditions")]
public class ControllerDisposeRaceTests
{
    // Enough attempts to hit the window reliably; each one is a handful of COM calls.
    private const int Attempts = 100;

    // Enough concurrent readers that at least one is always between the disposed check and its
    // next enumerator call, which is the window Dispose has to close.
    private const int Readers = 8;

    // A reader that passed the disposed check before Dispose ran must not re-create the enumerator
    // Dispose just released: that CoCreates a fresh IMMDeviceEnumerator onto an already-disposed
    // controller, which nothing will ever release.
    [Test]
    public void Dispose_ConcurrentWithGetDevices_DoesNotResurrectEnumerator()
    {
        var resurrected = 0;

        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            using (AudioController audio = RaceReadersAgainstDispose(out _))
            {
                if (ControllerTestAccess.HasLiveEnumerator(audio))
                {
                    resurrected++;
                }
            }
        }

        Assert.That(resurrected, Is.Zero,
            $"a racing reader re-created the enumerator on a disposed controller in {resurrected} of " +
            $"{Attempts} attempts; that instance is never released");
    }

    // Dispose releases the enumerator RCW that in-flight calls are still using. A reader that has
    // already passed the disposed check must either complete or report ObjectDisposedException -
    // never InvalidComObjectException ("COM object that has been separated from its underlying
    // RCW"), which is what releasing underneath a live call produces.
    [Test]
    public void Dispose_ConcurrentWithGetDevices_NeverSeparatesLiveRcw()
    {
        var unexpected = new ConcurrentBag<Exception>();

        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            RaceReadersAgainstDispose(out ConcurrentBag<Exception> thrown).Dispose();
            foreach (Exception ex in thrown)
            {
                unexpected.Add(ex);
            }
        }

        Assert.That(unexpected, Is.Empty,
            "concurrent Dispose must not tear the enumerator out from under an in-flight call; got: " +
            Describe(unexpected));
    }

    // Once Dispose has returned, no later call may quietly succeed.
    [Test]
    public void GetDevices_AfterDispose_AlwaysThrowsObjectDisposed()
    {
        var audio = new AudioController();
        AudioFixture.DisposeAll(audio.GetDevices());
        audio.Dispose();

        Assert.Throws<ObjectDisposedException>(() => audio.GetDevices());
        Assert.Throws<ObjectDisposedException>(() => audio.GetDefaultPlaybackDevice());
        Assert.Throws<ObjectDisposedException>(() => audio.GetDefaultRecordingDevice());
    }

    // Dispose waits for in-flight calls so it does not release the enumerator underneath one, but
    // that wait is bounded: it can be reached from a finalizer or a shutdown path, and the thing it
    // waits on is a COM call into the audio stack that a wedged device can stall indefinitely.
    [Test]
    public void Dispose_WithAStuckOperation_GivesUpRatherThanHanging()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var audio = ControllerTestAccess.Create(new StuckEnumerator(entered, release));
        var operation = Task.Run(() => AudioFixture.DisposeAll(audio.GetDevices()));
        Task? disposal = null;
        try
        {
            Assert.That(entered.Wait(TimeSpan.FromSeconds(10)), Is.True, "native enumeration was never entered");
            disposal = Task.Run(audio.Dispose);
            Assert.That(disposal.Wait(TimeSpan.FromSeconds(30)), Is.True,
                "Dispose blocked on an operation that never completed");
            Assert.That(operation.IsCompleted, Is.False,
                "the native call must still be blocked when Dispose returns");
        }
        finally
        {
            release.Set();
            Assert.That(operation.Wait(TimeSpan.FromSeconds(10)), Is.True);
            if (disposal != null)
            {
                Assert.That(disposal.Wait(TimeSpan.FromSeconds(10)), Is.True);
            }
        }
    }

    private sealed class StuckEnumerator : IMMDeviceEnumeratorCOM
    {
        private readonly ManualResetEventSlim _entered;
        private readonly ManualResetEventSlim _release;

        internal StuckEnumerator(ManualResetEventSlim entered, ManualResetEventSlim release)
        {
            _entered = entered;
            _release = release;
        }

        public int EnumAudioEndpoints(DataFlowFilter flow, DeviceStateFilter state, out IMMDeviceCollectionCOM devices)
        {
            _entered.Set();
            if (!_release.Wait(TimeSpan.FromSeconds(45)))
            {
                throw new TimeoutException("test cleanup did not release enumeration");
            }
            devices = new EmptyCollection();
            return 0;
        }

        public int GetDefaultAudioEndpoint(DataFlow flow, Role role, out IMMDeviceCOM endpoint)
        {
            endpoint = null!;
            return unchecked((int)0x80070490);
        }

        public int GetDevice(string id, out IMMDeviceCOM device) => throw new NotSupportedException();
        public int RegisterEndpointNotificationCallback(IMMNotificationClientCOM client) => throw new NotSupportedException();
        public int UnregisterEndpointNotificationCallback(IMMNotificationClientCOM client) => throw new NotSupportedException();
    }

    private sealed class EmptyCollection : IMMDeviceCollectionCOM
    {
        public int GetCount(out uint count)
        {
            count = 0;
            return 0;
        }

        public int Item(uint index, out IMMDeviceCOM device) => throw new ArgumentOutOfRangeException(nameof(index));
    }

    // Registration and disposal share a lock, but the unregister sweep runs outside it. A token
    // disposed while the controller is tearing down must not throw out of either path.
    [Test]
    public void Dispose_ConcurrentWithTokenDispose_DoesNotThrow()
    {
        var unexpected = new ConcurrentBag<Exception>();

        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var audio = new AudioController();
            var consumer = new NullDeviceEvents();
            IDisposable token = audio.RegisterDeviceNotification(consumer);

            using var ready = new ManualResetEventSlim(false);
            var unregister = new Thread(() =>
            {
                ready.Set();
                try
                {
                    token.Dispose();
                }
                catch (Exception ex)
                {
                    unexpected.Add(ex);
                }
            });

            unregister.Start();
            ready.Wait();
            audio.Dispose();
            unregister.Join();
        }

        Assert.That(unexpected, Is.Empty,
            "disposing a registration token while the controller is disposing must be a no-op; got: " +
            Describe(unexpected));
    }

    // Drives Readers threads through GetDevices in a tight loop, disposes the controller once they
    // are all in steady state, and returns the (disposed) controller plus anything they threw that
    // was not the documented ObjectDisposedException.
    private static AudioController RaceReadersAgainstDispose(out ConcurrentBag<Exception> unexpected)
    {
        var thrown = new ConcurrentBag<Exception>();
        unexpected = thrown;

        var audio = new AudioController();

        // Force the enumerator to exist before the race, so the window under test is the release of
        // a live RCW rather than its creation.
        AudioFixture.DisposeAll(audio.GetDevices());

        // Published by each reader every lap. The disposer waits for the count to advance so that
        // Dispose lands while calls are genuinely in flight, rather than before the reader threads
        // have even been scheduled.
        var laps = 0;
        var threads = new Thread[Readers];
        for (var i = 0; i < Readers; i++)
        {
            threads[i] = new Thread(() =>
            {
                for (var lap = 0; lap < 200; lap++)
                {
                    Interlocked.Increment(ref laps);
                    try
                    {
                        AudioFixture.DisposeAll(audio.GetDevices());
                    }
                    catch (ObjectDisposedException)
                    {
                        // The contract: a disposed controller reports exactly this.
                        return;
                    }
                    catch (Exception ex)
                    {
                        thrown.Add(ex);
                        return;
                    }
                }
            });
            threads[i].Start();
        }

        SpinWait.SpinUntil(() => Volatile.Read(ref laps) >= Readers * 2, TimeSpan.FromSeconds(10));
        audio.Dispose();

        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        return audio;
    }

    private static string Describe(IEnumerable<Exception> exceptions)
    {
        var seen = new HashSet<string>();
        foreach (Exception ex in exceptions)
        {
            seen.Add($"{ex.GetType().Name}: {ex.Message}");
        }

        return seen.Count == 0 ? "(none)" : string.Join(" | ", seen);
    }

    private sealed class NullDeviceEvents : IAudioDeviceEvents
    {
        public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }

        public void OnDeviceAdded(string deviceId) { }

        public void OnDeviceRemoved(string deviceId) { }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) { }

        public void OnPropertyValueChanged(string deviceId, PropertyKey key) { }
    }
}
