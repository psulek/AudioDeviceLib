/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  EndpointVolumeCallbackTests.cs
  Coverage for the COM boundary itself: AudioEndpointVolumeCallback.OnNotify.

  These tests hand OnNotify a synthesized unmanaged AUDIO_VOLUME_NOTIFICATION_DATA buffer instead
  of waiting for Core Audio to raise one, which is the only way to exercise the paths a real driver
  is not expected to produce - a consumer that throws, and a bogus trailing channel count.

  The channel-count test is a genuine regression guard: before the count was clamped, it drove
  Marshal.Copy several megabytes past the end of the allocation and would take the test process
  down with an access violation rather than fail.
*/

using System;
using System.Runtime.InteropServices;
using System.Threading;
using AudioDeviceLib.CoreAudioApi;
using AudioDeviceLib.CoreAudioApi.Interfaces;
using NUnit.Framework;

namespace AudioDeviceLib.UnitTests;

[TestFixture]
[NonParallelizable]
public class EndpointVolumeCallbackTests
{
    private const int S_OK = 0;

    private AudioController _audio = null!; // Assigned by NUnit setup before a test executes.
    private AudioDevice _device = null!; // Assigned by NUnit setup before a test executes.
    private AudioEndpointVolume _volume = null!; // Assigned by NUnit setup before a test executes.
    private AudioEndpointVolumeCallback _callback = null!; // Assigned by NUnit setup before a test executes.
    private int _endpointChannels;

    [SetUp]
    public void SetUp()
    {
        _audio = new AudioController();
        _device = AudioFixture.RequireDefaultPlayback(_audio);
        _volume = _device.Volume;
        _endpointChannels = _volume.Channels.Count;

        // A second callback object alongside the one the endpoint registered in its constructor.
        // Constructing it registers nothing with COM, so this cannot disturb the real notification
        // path; it just gives the test a direct entry point into OnNotify.
        _callback = new AudioEndpointVolumeCallback(_volume);
    }

    [TearDown]
    public void TearDown()
    {
        _callback = null!;
        _volume = null!;
        _device?.Dispose();
        _audio?.Dispose();
        _device = null!;
        _audio = null!;
    }

    [Test]
    public void OnNotify_WellFormedBuffer_DeliversNotificationAndReturnsSOk()
    {
        float[] channels = BuildChannelValues(_endpointChannels);
        Guid context = Guid.NewGuid();
        var recorder = new Recorder();

        using (_volume.RegisterVolumeNotification(recorder))
        {
            int hr = Notify(context, true, 0.375f, (uint)channels.Length, channels);

            Assert.That(hr, Is.EqualTo(S_OK));
        }

        Assert.That(recorder.Last, Is.Not.Null);
        Assert.That(AudioFixture.RequireValue(recorder.Last).EventContext, Is.EqualTo(context));
        Assert.That(AudioFixture.RequireValue(recorder.Last).Muted, Is.True);
        Assert.That(AudioFixture.RequireValue(recorder.Last).MasterVolume, Is.EqualTo(0.375f));
        Assert.That(AudioFixture.RequireValue(recorder.Last).ChannelVolume, Is.EqualTo(channels));
    }

    // The H1 boundary: a consumer exception must become an HRESULT here, never unwind into the
    // native caller.
    [Test]
    public void OnNotify_ThrowingConsumer_ReturnsFailureHResultInsteadOfThrowing()
    {
        float[] channels = BuildChannelValues(_endpointChannels);

        using (_volume.RegisterVolumeNotification(new ThrowingConsumer()))
        {
            int hr = 0;
            Assert.DoesNotThrow(
                () => hr = Notify(Guid.NewGuid(), false, 0.5f, (uint)channels.Length, channels),
                "an exception must not cross the COM boundary");

            Assert.That(hr, Is.LessThan(0), "a failed callback must report a failure HRESULT");
        }
    }

    [Test]
    public void OnNotify_ChannelCountLargerThanEndpoint_IsClampedAndDoesNotOverread()
    {
        float[] channels = BuildChannelValues(_endpointChannels);
        var recorder = new Recorder();

        using (_volume.RegisterVolumeNotification(recorder))
        {
            // The buffer only holds the endpoint's real channels; the header lies about how many
            // floats follow it.
            int hr = Notify(Guid.NewGuid(), false, 0.5f, claimedChannels: 1_000_000, channelValues: channels);

            Assert.That(hr, Is.EqualTo(S_OK));
        }

        Assert.That(recorder.Last, Is.Not.Null);
        Assert.That(AudioFixture.RequireValue(recorder.Last).Channels, Is.EqualTo(_endpointChannels), "clamped to the endpoint's channel count");
        Assert.That(AudioFixture.RequireValue(recorder.Last).ChannelVolume, Is.EqualTo(channels));
    }

    [Test]
    public void OnNotify_ZeroChannels_DeliversEmptyChannelArray()
    {
        var recorder = new Recorder();

        using (_volume.RegisterVolumeNotification(recorder))
        {
            int hr = Notify(Guid.NewGuid(), false, 0.5f, 0, Array.Empty<float>());

            Assert.That(hr, Is.EqualTo(S_OK));
        }

        Assert.That(recorder.Last, Is.Not.Null);
        Assert.That(AudioFixture.RequireValue(recorder.Last).Channels, Is.EqualTo(0));
        Assert.That(AudioFixture.RequireValue(recorder.Last).ChannelVolume, Is.Empty);
    }

    [Test]
    public void OnNotify_NoConsumers_ReturnsSOk()
    {
        float[] channels = BuildChannelValues(_endpointChannels);

        Assert.That(Notify(Guid.NewGuid(), false, 0.5f, (uint)channels.Length, channels), Is.EqualTo(S_OK));
    }

    private static float[] BuildChannelValues(int count)
    {
        var values = new float[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = 0.1f * (i + 1);
        }

        return values;
    }

    // Lays out an AUDIO_VOLUME_NOTIFICATION_DATA followed by `channelValues` in unmanaged memory,
    // exactly as Core Audio does, and invokes the callback with it. `claimedChannels` is written to
    // the header independently of how many floats are actually stored, so a test can describe a
    // buffer that lies about its own length.
    private int Notify(Guid eventContext, bool muted, float masterVolume, uint claimedChannels, float[] channelValues)
    {
        int offset = Marshal.OffsetOf(typeof(AUDIO_VOLUME_NOTIFICATION_DATA), "ChannelVolume").ToInt32();
        int size = Math.Max(offset + (channelValues.Length * sizeof(float)),
                            Marshal.SizeOf(typeof(AUDIO_VOLUME_NOTIFICATION_DATA)));

        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            for (int i = 0; i < size; i++)
            {
                Marshal.WriteByte(buffer, i, 0);
            }

            var header = new AUDIO_VOLUME_NOTIFICATION_DATA
            {
                guidEventContext = eventContext,
                bMuted = muted,
                fMasterVolume = masterVolume,
                nChannels = claimedChannels
            };
            Marshal.StructureToPtr(header, buffer, false);

            if (channelValues.Length > 0)
            {
                Marshal.Copy(channelValues, 0, new IntPtr(buffer.ToInt64() + offset), channelValues.Length);
            }

            return Dispatch(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // Run callbacks on a dedicated thread to isolate Marshal.GetHRForException's IErrorInfo.
    // Otherwise, later NUnit tests on the same thread may receive a stale exception.
    private int Dispatch(IntPtr buffer)
    {
        int hr = 0;
        Exception? escaped = null;

        var thread = new Thread(() =>
        {
            try
            {
                hr = _callback.OnNotify(buffer);
            }
            catch (Exception ex)
            {
                escaped = ex;
            }
        });

        thread.IsBackground = true;
        thread.Start();
        thread.Join();

        if (escaped != null)
        {
            // OnNotify let something out; hand it to the caller so Assert.DoesNotThrow still fails.
            throw escaped;
        }

        return hr;
    }

    private sealed class Recorder : IAudioEndpointVolumeEvents
    {
        internal AudioVolumeNotificationData? Last;

        public void OnVolumeNotification(AudioVolumeNotificationData data)
        {
            Last = data;
        }
    }

    private sealed class ThrowingConsumer : IAudioEndpointVolumeEvents
    {
        public void OnVolumeNotification(AudioVolumeNotificationData data)
        {
            throw new InvalidOperationException("consumer failure");
        }
    }
}
