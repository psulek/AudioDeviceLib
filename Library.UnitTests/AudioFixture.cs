/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  AudioFixture.cs
  Shared helpers for the test suite, including the guard that lets hardware-dependent
  tests skip cleanly on a machine with no audio endpoints.
*/

using System.Collections.Generic;
using NUnit.Framework;

namespace AudioDeviceLib.UnitTests;

internal static class AudioFixture
{
    internal static T RequireValue<T>(T? value) where T : class =>
        value ?? throw new AssertionException("Expected a non-null value.");

    // A name no real endpoint can carry, for exercising the "no match" paths without any risk of
    // actually changing the machine's default device.
    internal const string UnmatchableName = "no-such-device-8f3a1c92-4d77-4e0b-9a55-6b2e1f0c7d31";

    // Core Audio distinguishes the two bad-ID cases, and the library surfaces that distinction:
    //   malformed             -> E_INVALIDARG (0x80070057) -> ArgumentException
    //   well-formed but absent -> E_NOTFOUND  (0x80070490) -> COMException
    internal const string MalformedDeviceId = "not-an-endpoint-id";
    internal const string AbsentDeviceId = "{0.0.0.00000000}.{00000000-0000-0000-0000-000000000000}";

    // Returns the default playback device, or skips the calling test.
    //
    // GitHub-hosted Windows runners have no audio hardware: enumeration succeeds but yields nothing
    // and there is no default endpoint. Tests that genuinely need a device call this so a headless
    // run reports "skipped, no hardware" rather than failing or silently passing.
    internal static AudioDevice RequireDefaultPlayback(AudioController audio)
    {
        AudioDevice? device = audio.GetDefaultPlaybackDevice();
        if (device == null)
        {
            Assert.Ignore("No default playback endpoint on this machine (headless CI or no audio hardware).");
        }

        return device;
    }

    // Resolves an ID that was just obtained from a live device, and fails the calling test if it
    // does not resolve. GetDeviceById answers null for an endpoint that is no longer present, which
    // is a real outcome for a stale ID - but not for one read moments earlier, so a null here is a
    // genuine failure and should say so instead of surfacing as a NullReferenceException.
    internal static AudioDevice RequireDeviceById(AudioController audio, string deviceId)
    {
        AudioDevice? device = audio.GetDeviceById(deviceId);
        Assert.That(device, Is.Not.Null, "an ID read from a live endpoint must still resolve");

        return device ?? throw new AssertionException("Expected a live device for the given ID.");
    }

    // Returns any active endpoint, or skips the calling test.
    internal static AudioDevice RequireAnyDevice(AudioController audio, out IReadOnlyList<AudioDevice> all)    {
        all = audio.GetDevices();
        if (all.Count == 0)
        {
            Assert.Ignore("No active audio endpoints on this machine (headless CI or no audio hardware).");
        }

        return all[0];
    }

    internal static void DisposeAll(IEnumerable<AudioDevice> devices)
    {
        if (devices == null)
        {
            return;
        }

        foreach (AudioDevice device in devices)
        {
            device?.Dispose();
        }
    }
}
