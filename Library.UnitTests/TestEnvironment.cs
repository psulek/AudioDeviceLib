/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  TestEnvironment.cs
  Reports what the machine actually offers before any test runs, so a failure on a headless
  CI runner is immediately legible instead of an unexplained COM exception.
*/

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace AudioDeviceLib.UnitTests;

[SetUpFixture]
public class TestEnvironment
{
    [OneTimeSetUp]
    public void ReportEnvironment()
    {
        TestContext.Progress.WriteLine($"Runtime : {RuntimeInformation.FrameworkDescription}");
        TestContext.Progress.WriteLine($"OS      : {RuntimeInformation.OSDescription}");

        try
        {
            using var audio = new AudioController();
            IReadOnlyList<AudioDevice> devices = audio.GetDevices();
            int count = devices.Count;
            AudioFixture.DisposeAll(devices);

            TestContext.Progress.WriteLine($"Endpoints: {count}");
            if (count == 0)
            {
                TestContext.Progress.WriteLine(
                    "No audio endpoints. Hardware-dependent tests will be skipped; the COM and " +
                    "ReleaseComObject coverage still runs.");
            }
        }
        catch (Exception ex)
        {
            // Not swallowed - the tests that follow will fail and should. This only makes the cause
            // obvious at the top of the log rather than buried in the first failing assertion.
            TestContext.Progress.WriteLine(
                $"Core Audio unavailable on this machine: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
