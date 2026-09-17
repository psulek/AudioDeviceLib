/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  DeviceActivationRaceTests.cs
  Concurrency coverage for AudioDevice's four lazily-activated members (Volume, SessionManager,
  Meter, Properties) and for Dispose racing them.

  These need a real endpoint: the thing under test is the activation of a Core Audio interface,
  which a machine with no hardware never reaches.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using NUnit.Framework;

namespace AudioDeviceLib.UnitTests;

[TestFixture]
[NonParallelizable]
public class DeviceActivationRaceTests
{
    // Enough concurrent readers to make the check-then-act window matter. All of them are released
    // from a single barrier, so they arrive at the unactivated field together rather than in turn.
    private const int Readers = 8;

    // Repeated because a lost race is probabilistic: one attempt that happens to serialise proves
    // nothing. Each attempt is one activation plus a handful of reads.
    private const int Attempts = 25;

    // Concurrent getters must return the same instance.
    // Duplicate activation can orphan a registered native callback.
    [Test]
    public void Volume_FirstAccessFromManyThreads_ActivatesExactlyOnce()
    {
        AssertSingleInstanceUnderRace(device => device.Volume);
    }

    // Same race, and the review called this one out specifically: losing here orphans an
    // IAudioSessionManager2 plus the whole SessionCollection hanging off it.
    [Test]
    public void SessionManager_FirstAccessFromManyThreads_ActivatesExactlyOnce()
    {
        AssertSingleInstanceUnderRace(device => device.SessionManager);
    }

    [Test]
    public void Meter_FirstAccessFromManyThreads_ActivatesExactlyOnce()
    {
        AssertSingleInstanceUnderRace(device => device.Meter);
    }

    // Properties reaches the store through PropertyStoreCore, which the constructor and Refresh
    // also use, so it is the one of the four with more than one entry point.
    [Test]
    public void Properties_FirstAccessFromManyThreads_ActivatesExactlyOnce()
    {
        AssertSingleInstanceUnderRace(device => device.Properties);
    }

    // All four at once, because they share a single lock: if that lock were ever split or taken in
    // an inconsistent order, mixing the four accessors is what would expose it.
    [Test]
    public void AllFourMembers_ReadConcurrently_StayConsistent()
    {
        using var audio = new AudioController();
        using AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);

        var failures = new ConcurrentBag<Exception>();
        var barrier = new Barrier(Readers);

        var threads = new List<Thread>(Readers);
        for (var i = 0; i < Readers; i++)
        {
            int index = i;
            var thread = new Thread(() =>
            {
                try
                {
                    barrier.SignalAndWait();
                    for (var n = 0; n < 20; n++)
                    {
                        switch ((index + n) % 4)
                        {
                            case 0:
                                Assert.That(device.Volume, Is.Not.Null);
                                break;
                            case 1:
                                Assert.That(device.SessionManager, Is.Not.Null);
                                break;
                            case 2:
                                Assert.That(device.Meter, Is.Not.Null);
                                break;
                            default:
                                Assert.That(device.Properties, Is.Not.Null);
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                }
            });

            threads.Add(thread);
            thread.Start();
        }

        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        Assert.That(failures, Is.Empty, Describe(failures));
    }

    // Dispose used to be unsynchronised against the getters, so a thread inside Volume could
    // activate an interface on a device another thread was tearing down, and Dispose's
    // "_volume = null" could be overwritten by the racing assignment - leaving a disposed device
    // holding a live volume registration.
    //
    // A reader here may legitimately succeed (it got in first) or throw ObjectDisposedException
    // (it did not). Anything else - a separated RCW, a NullReferenceException - is the bug.
    [Test]
    public void Dispose_ConcurrentWithFirstAccess_ThrowsNothingUnexpected()
    {
        var unexpected = new ConcurrentBag<Exception>();

        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            using var audio = new AudioController();
            AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);

            var barrier = new Barrier(Readers + 1);
            var threads = new List<Thread>(Readers);

            for (var i = 0; i < Readers; i++)
            {
                int index = i;
                var thread = new Thread(() =>
                {
                    try
                    {
                        barrier.SignalAndWait();
                        switch (index % 4)
                        {
                            case 0:
                                _ = device.Volume;
                                break;
                            case 1:
                                _ = device.SessionManager;
                                break;
                            case 2:
                                _ = device.Meter;
                                break;
                            default:
                                _ = device.Properties;
                                break;
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Expected: this reader lost the race with Dispose.
                    }
                    catch (Exception ex)
                    {
                        unexpected.Add(ex);
                    }
                });

                threads.Add(thread);
                thread.Start();
            }

            barrier.SignalAndWait();
            device.Dispose();

            foreach (Thread thread in threads)
            {
                thread.Join();
            }
        }

        Assert.That(unexpected, Is.Empty, Describe(unexpected));
    }

    [Test]
    public void Dispose_CalledFromSeveralThreadsAtOnce_DisposesOnce()
    {
        using var audio = new AudioController();
        AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);

        // Activated up front so that disposal has real work to do - an already-empty device would
        // not exercise the teardown path at all.
        Assert.That(device.Volume, Is.Not.Null);
        Assert.That(device.SessionManager, Is.Not.Null);

        var failures = new ConcurrentBag<Exception>();
        var barrier = new Barrier(Readers);
        var threads = new List<Thread>(Readers);

        for (var i = 0; i < Readers; i++)
        {
            var thread = new Thread(() =>
            {
                try
                {
                    barrier.SignalAndWait();
                    device.Dispose();
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                }
            });

            threads.Add(thread);
            thread.Start();
        }

        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        Assert.That(failures, Is.Empty, Describe(failures));
        Assert.Throws<ObjectDisposedException>(() => _ = device.Volume);
    }

    // Releases `Readers` threads simultaneously onto a device that has never had `read` called on
    // it, then asserts they all came back with the same object.
    private static void AssertSingleInstanceUnderRace(Func<AudioDevice, object> read)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            using var audio = new AudioController();
            using AudioDevice device = AudioFixture.RequireDefaultPlayback(audio);

            var seen = new ConcurrentBag<object>();
            var failures = new ConcurrentBag<Exception>();
            var barrier = new Barrier(Readers);
            var threads = new List<Thread>(Readers);

            for (var i = 0; i < Readers; i++)
            {
                var thread = new Thread(() =>
                {
                    try
                    {
                        barrier.SignalAndWait();
                        seen.Add(read(device));
                    }
                    catch (Exception ex)
                    {
                        failures.Add(ex);
                    }
                });

                threads.Add(thread);
                thread.Start();
            }

            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            Assert.That(failures, Is.Empty, Describe(failures));
            Assert.That(seen.Count, Is.EqualTo(Readers), "a reader returned without recording a result");

            // Reference equality, not Equals: two distinct wrappers over the same endpoint would
            // compare equal on identity-based members while still being two live activations.
            int distinct = seen.Distinct(ReferenceComparer.Instance).Count();
            Assert.That(distinct, Is.EqualTo(1),
                $"attempt {attempt}: {distinct} distinct instances were activated concurrently; " +
                "all but one are orphaned");
        }
    }

    private static string Describe(ConcurrentBag<Exception> errors)
    {
        return errors.IsEmpty
            ? string.Empty
            : string.Join(Environment.NewLine, errors.Select(e => $"{e.GetType().Name}: {e.Message}"));
    }

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        internal static readonly ReferenceComparer Instance = new ReferenceComparer();

        public new bool Equals(object? x, object? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
