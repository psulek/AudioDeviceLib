/* Copyright (c) 2026 Peter Šulek. MIT License. */
using System;
using System.Threading;
using NUnit.Framework;

namespace AudioDeviceLib.UnitTests;

[SetUpFixture]
public sealed class HardwareIsolation
{
    private Semaphore _gate = null!; // Assigned by NUnit setup before a test executes.
    private bool _acquired;

    [OneTimeSetUp]
    public void AcquireHardware()
    {
        // Semaphore has no thread affinity: NUnit can run teardown on a different thread.
        _gate = new Semaphore(1, 1, @"Local\AudioDeviceLib.HardwareTests");
        _acquired = _gate.WaitOne(TimeSpan.FromMinutes(3));
        Assert.That(_acquired, Is.True, "another test process held the audio hardware for over three minutes");
    }

    [OneTimeTearDown]
    public void ReleaseHardware()
    {
        if (_acquired)
        {
            _gate.Release();
        }
        _gate?.Dispose();
    }
}
