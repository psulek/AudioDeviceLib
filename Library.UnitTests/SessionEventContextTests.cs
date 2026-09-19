/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  SessionEventContextTests.cs
  End-to-end coverage for the nullable session event context.

  The event context is asymmetric across the two directions, and these tests cover one of them.

  OUTBOUND (IAudioSessionControl2) sends `ref Guid`: an omitted context becomes GUID_NULL rather
  than a null pointer. That is safe because a null context was observed arriving at subscribers as
  GUID_NULL anyway - indistinguishable from an explicitly supplied Guid.Empty - so sending
  GUID_NULL ourselves loses nothing, and it keeps pointers out of that interface.

  INBOUND (IAudioSessionEventsCOM) keeps `Guid*`, because the value arriving there was chosen by
  whichever process made the change, not by us, and the docs state it may be null: "If the caller
  supplies a NULL pointer for this parameter, the client's notification method receives a NULL
  context pointer." AudioSessionEventsComAdapter null-checks before dereferencing, and the public
  IAudioSessionEvents surfaces the result as `Guid?`.

  WHAT THESE TESTS DO AND DO NOT ESTABLISH
  They drive real COM rather than calling the adapter directly: the call goes out through
  IAudioSessionControl2, Core Audio dispatches the notification back through the CLR-generated CCW,
  and the assertion is on what the public consumer received. That establishes the vtable is right
  and that a supplied context GUID round-trips exactly.

  They do NOT cover the inbound null branch. Now that the outbound side never sends a null, nothing
  in this process can produce one; reaching that branch would require a second process to call a
  session setter with a NULL context. The guard stays because the documentation says that case
  exists, not because it is exercised here.

  There is deliberately only one test. An "omitted context arrives as Guid.Empty" case was tried
  and removed: once the extension maps an omitted context onto Guid.Empty, that test asserts
  nothing the supplied-context test below does not already assert, and it cost a live audio session
  and a five-second timeout to do it.

  SetIconPath is the vehicle throughout rather than SetDisplayName: on the system-sounds session,
  SetDisplayName returns S_OK but silently does not change the name, so it raises no notification.

  Unlike DeviceTests, these tests CHANGE the machine: the icon path of a live session is modified.
  SetUp captures it and TearDown puts it back. Every test skips via Assert.Ignore when there is no
  default playback endpoint or no active session, so a headless CI run reports them as skipped.
*/

using System;
using System.Collections.Generic;
using System.Threading;
using AudioDeviceLib.CoreAudioApi;
using AudioDeviceLib.CoreAudioApi.Extensions;
using AudioDeviceLib.CoreAudioApi.Interfaces;
using AudioDeviceLib.Lib;
using NUnit.Framework;

namespace AudioDeviceLib.UnitTests;

[TestFixture]
[NonParallelizable]
public class SessionEventContextTests
{
    // Core Audio dispatches the callback on its own thread, so every wait here is cross-thread and
    // gets a generous ceiling rather than a tight one.
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(5);

    private AudioController _audio;
    private AudioDevice _device;
    private AudioSessionControl _session;
    private SessionEventRecorder _recorder;
    private IDisposable _registration;

    private string _originalIconPath;

    // The raw COM interface the extension methods extend. Reached directly because the public
    // AudioSessionControl surface deliberately does not expose an event-context overload.
    private IAudioSessionControl2 Raw => _session._AudioSessionControl;

    [SetUp]
    public void SetUp()
    {
        _audio = new AudioController();
        _device = AudioFixture.RequireDefaultPlayback(_audio);
        _session = RequireSession(_device);

        // Read through the extension rather than the throwing property: a session can be torn down
        // between enumeration and here, and failing to read the old value is not a reason to fail
        // the test before it has started.
        AudioSessionControl2Extensions.GetIconPath(Raw, out _originalIconPath);

        _recorder = new SessionEventRecorder();
        _registration = _session.RegisterAudioSessionNotification(_recorder);
    }

    [TearDown]
    public void TearDown()
    {
        _registration?.Dispose();
        _registration = null;

        RestoreSessionState();

        _session = null;
        _device?.Dispose();
        _audio?.Dispose();

        _device = null;
        _audio = null;
        _recorder = null;
    }

    // The hard guarantee: a supplied context must arrive intact and unmangled. This is what proves
    // the Guid* parameter is being dereferenced correctly through the CLR-generated CCW, as
    // opposed to merely null-checked - a wrong vtable slot or a bad marshalling directive would
    // surface here as a mismatched GUID or a failure to register at all.
    [Test]
    public void SuppliedEventContext_ArrivesIntact()
    {
        Guid context = Guid.NewGuid();
        string target = UniqueIconPath("guid-ctx");

        AssertSucceeded(
            AudioSessionControl2Extensions.SetIconPath(Raw, target, context),
            "SetIconPath(explicit context)");

        SessionEvent received = _recorder.WaitForIconPath(target, NotificationTimeout);

        Assert.That(received, Is.Not.Null,
            $"SetIconPath with event context {context:D} raised no notification within " +
            $"{NotificationTimeout.TotalSeconds:0}s. Seen: {_recorder.DescribeSeen()}");
        Assert.That(received.EventContext, Is.EqualTo(context));
    }

    // Distinct per call so a notification can be matched to the call that produced it without
    // relying on the context GUID - which is the thing under test and cannot also be the key.
    private static string UniqueIconPath(string tag) =>
        $@"C:\AudioDeviceLib-test\{tag}-{Guid.NewGuid():N}.ico";

    private static void AssertSucceeded(int hr, string method)
    {
        Assert.That(hr, Is.GreaterThanOrEqualTo(0), $"{method} failed with HRESULT 0x{hr:X8}");
    }

    // Returns a session to drive, preferring an ordinary application session over the reserved
    // system-sounds one, or skips the calling test. A machine with audio hardware but nothing
    // playing can still have zero sessions, so this is a separate guard from
    // AudioFixture.RequireDefaultPlayback.
    private static AudioSessionControl RequireSession(AudioDevice device)
    {
        SessionCollection sessions = device.SessionManager.Sessions;
        if (sessions.Count == 0)
        {
            Assert.Ignore("No active audio sessions on this machine (headless CI, or nothing is playing audio).");
        }

        for (int i = 0; i < sessions.Count; i++)
        {
            if (!sessions[i].IsSystemSoundsSession)
            {
                return sessions[i];
            }
        }

        // Falling back to the system-sounds session is fine for the icon-path path used here, but
        // it is the session on which SetDisplayName silently no-ops, so anything added later that
        // needs a real application session should guard for this case separately.
        return sessions[0];
    }

    // Restoration is best-effort and reported rather than thrown: a failure here must not mask the
    // assertion failure that a test is already reporting.
    private void RestoreSessionState()
    {
        if (_session == null || _originalIconPath == null)
        {
            return;
        }

        try
        {
            AudioSessionControl2Extensions.SetIconPath(Raw, _originalIconPath);
        }
        catch (Exception ex)
        {
            TestContext.Progress.WriteLine($"Could not restore session icon path: {ex.Message}");
        }
    }

    // One received callback, flattened to just what these tests assert on.
    private sealed class SessionEvent
    {
        internal SessionEvent(string kind, string value, Guid? eventContext)
        {
            Kind = kind;
            Value = value;
            EventContext = eventContext;
        }

        internal string Kind { get; }
        internal string Value { get; }
        internal Guid? EventContext { get; }

        public override string ToString() =>
            $"{Kind}('{Value}', ctx {EventContext?.ToString() ?? "<null>"})";
    }

    // Collects callbacks off the Core Audio thread and lets the test thread block until a matching
    // one arrives. The notification for a given call is identified by the value it carries, never
    // by its event context - that is the value under test.
    private sealed class SessionEventRecorder : IAudioSessionEvents
    {
        private readonly object _gate = new object();
        private readonly List<SessionEvent> _received = new List<SessionEvent>();

        internal SessionEvent WaitForIconPath(string value, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;

            lock (_gate)
            {
                while (true)
                {
                    foreach (SessionEvent received in _received)
                    {
                        if (received.Kind == "IconPath" && received.Value == value)
                        {
                            return received;
                        }
                    }

                    TimeSpan remaining = deadline - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero || !Monitor.Wait(_gate, remaining))
                    {
                        return null;
                    }
                }
            }
        }

        internal string DescribeSeen()
        {
            lock (_gate)
            {
                return _received.Count == 0 ? "(none)" : string.Join("; ", _received);
            }
        }

        private void Record(SessionEvent received)
        {
            lock (_gate)
            {
                _received.Add(received);
                Monitor.PulseAll(_gate);
            }
        }

        public void OnDisplayNameChanged(AudioSessionInfo session, string newDisplayName, Guid? eventContext) =>
            Record(new SessionEvent("DisplayName", newDisplayName, eventContext));

        public void OnIconPathChanged(AudioSessionInfo session, string newIconPath, Guid? eventContext) =>
            Record(new SessionEvent("IconPath", newIconPath, eventContext));

        public void OnSimpleVolumeChanged(AudioSessionInfo session, float newVolume, bool newMute, Guid? eventContext) =>
            Record(new SessionEvent("SimpleVolume", newVolume.ToString("0.###"), eventContext));

        public void OnChannelVolumeChanged(AudioSessionInfo session, float[] newChannelVolumes, uint changedChannel,
            Guid? eventContext) =>
            Record(new SessionEvent("ChannelVolume", changedChannel.ToString(), eventContext));

        public void OnGroupingParamChanged(AudioSessionInfo session, Guid newGroupingParam, Guid? eventContext) =>
            Record(new SessionEvent("GroupingParam", newGroupingParam.ToString("D"), eventContext));

        public void OnStateChanged(AudioSessionInfo session, AudioSessionState newState) =>
            Record(new SessionEvent("State", newState.ToString(), null));

        public void OnSessionDisconnected(AudioSessionInfo session, AudioSessionDisconnectReason disconnectReason) =>
            Record(new SessionEvent("Disconnected", disconnectReason.ToString(), null));
    }
}
