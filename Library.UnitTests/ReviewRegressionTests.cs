/* Copyright (c) 2026 Peter Šulek. MIT License. */
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AudioDeviceLib.CoreAudioApi;
using AudioDeviceLib.CoreAudioApi.Interfaces;
using NUnit.Framework;

namespace AudioDeviceLib.UnitTests;

[TestFixture]
public sealed class ReviewRegressionTests
{
    [Test]
    public void SlowRegistration_DoesNotBlockDefaultLookup()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var controller = ControllerTestAccess.Create(new BlockingEnumerator(entered, release));
        var registration = Task.Run(() => controller.RegisterDeviceNotification(new Consumer()));
        Task<AudioDevice?>? lookup = null;
        bool completed = false;
        try
        {
            Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
            lookup = Task.Run(() => controller.GetDefaultPlaybackDevice());
            completed = lookup.Wait(TimeSpan.FromSeconds(3));
        }
        finally
        {
            release.Set();
            Assert.That(registration.Wait(TimeSpan.FromSeconds(5)), Is.True);
            registration.Result.Dispose();
            if (lookup != null)
            {
                Assert.That(lookup.Wait(TimeSpan.FromSeconds(5)), Is.True);
                lookup.Result?.Dispose();
            }
        }
        Assert.That(completed, Is.True, "registration held the controller's enumeration lock");
    }

    [Test]
    public void FailureMapping_IgnoresStaleThreadErrorInfo()
    {
        Marshal.GetHRForException(new InvalidOperationException("stale callback error"));
        var error = Assert.Throws<COMException>(() => InteropUtils.ThrowIfFailed(unchecked((int)0x80004005)));
        error = AudioFixture.RequireValue(error);
        Assert.That(error.ErrorCode, Is.EqualTo(unchecked((int)0x80004005)));
        Assert.That(error.Message, Does.Not.Contain("stale callback error"));
    }

    [Test]
    public void SessionRefresh_InvalidatesPreviousCollectionAndControls()
    {
        using var controller = new AudioController();
        using var device = AudioFixture.RequireDefaultPlayback(controller);
        var manager = device.SessionManager;
        var previous = manager.Sessions;
        var session = previous.Count > 0 ? previous[0] : null;
        var meter = session?.AudioMeterInformation;
        var volume = session?.SimpleAudioVolume;
        manager.Refresh();
        Assert.That(manager.Sessions, Is.Not.SameAs(previous));
        Assert.Throws<ObjectDisposedException>(() => { _ = previous.Count; });
        if (session != null)
        {
            Assert.Throws<ObjectDisposedException>(() => { _ = session.State; });
            Assert.Throws<ObjectDisposedException>(() => { _ = session.AudioMeterInformation; });
            Assert.Throws<ObjectDisposedException>(() => { _ = session.SimpleAudioVolume; });
        }
        if (meter != null)
        {
            Assert.Throws<ObjectDisposedException>(() => { _ = meter.MasterPeakValue; });
        }
        if (volume != null)
        {
            Assert.Throws<ObjectDisposedException>(() => { _ = volume.MasterVolume; });
        }
    }

    [Test]
    public void DeviceCollection_InvalidIndicesNeverReachNativeItem()
    {
        var native = new CountingCollection();
        using var collection = new MMDeviceCollection(native);
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = collection[-1]; });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = collection[collection.Count]; });
        Assert.That(native.ItemCalls, Is.Zero);
    }

    [Test]
    [NonParallelizable]
    public void ControllerDispose_LogsFailedUnregistrationWithoutThrowing()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim(true);
        var native = new BlockingEnumerator(entered, release)
        {
            UnregisterResult = unchecked((int)0x80004005)
        };
        using var controller = ControllerTestAccess.Create(native);
        using var registration = controller.RegisterDeviceNotification(new Consumer());
        using var output = new StringWriter();
        using var listener = new TextWriterTraceListener(output);
        Trace.Listeners.Add(listener);
        try
        {
            Assert.DoesNotThrow(controller.Dispose);
            listener.Flush();
            Assert.That(output.ToString(), Does.Contain("AudioDeviceLib:"));
            Assert.That(output.ToString(), Does.Contain("80004005"));
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    private sealed class CountingCollection : IMMDeviceCollectionCOM
    {
        internal int ItemCalls { get; private set; }
        public int GetCount(out uint count)
        {
            count = 1;
            return 0;
        }
        public int Item(uint index, out IMMDeviceCOM device)
        {
            ItemCalls++;
            throw new InvalidOperationException("Invalid index reached native Item");
        }
    }

    private sealed class BlockingEnumerator : IMMDeviceEnumeratorCOM
    {
        private readonly ManualResetEventSlim _entered;
        private readonly ManualResetEventSlim _release;
        internal BlockingEnumerator(ManualResetEventSlim entered, ManualResetEventSlim release)
        {
            _entered = entered;
            _release = release;
        }
        public int EnumAudioEndpoints(DataFlowFilter flow, DeviceStateFilter state, out IMMDeviceCollectionCOM devices)
        {
            throw new NotSupportedException();
        }
        public int GetDefaultAudioEndpoint(DataFlow flow, Role role, out IMMDeviceCOM endpoint)
        {
            endpoint = null!;
            return unchecked((int)0x80070490);
        }
        public int GetDevice(string id, out IMMDeviceCOM device) => throw new NotSupportedException();
        public int RegisterEndpointNotificationCallback(IMMNotificationClientCOM client)
        {
            _entered.Set();
            if (!_release.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("registration was not released by test cleanup");
            }
            return 0;
        }
        internal int UnregisterResult { get; set; }
        public int UnregisterEndpointNotificationCallback(IMMNotificationClientCOM client) => UnregisterResult;
    }

    private sealed class Consumer : IAudioDeviceEvents
    {
        public void OnDeviceStateChanged(string id, DeviceState state) { }
        public void OnDeviceAdded(string id) { }
        public void OnDeviceRemoved(string id) { }
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string id) { }
        public void OnPropertyValueChanged(string id, PropertyKey key) { }
    }
}
