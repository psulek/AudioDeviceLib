/* Copyright (c) 2026 Peter Šulek. MIT License. */
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using AudioDeviceLib.CoreAudioApi;
using AudioDeviceLib.CoreAudioApi.Extensions;
using AudioDeviceLib.CoreAudioApi.Interfaces;
using NUnit.Framework;

namespace AudioDeviceLib.UnitTests;

// An initialized, never-started WASAPI stream owns a unique silent session. Tests may change its
// metadata freely without touching another application's session. Library observations use callbacks.
internal sealed class OwnedAudioSession : IDisposable
{
    private AudioController? _controller;
    private AudioDevice? _device;
    private ITestAudioClient? _client;
    private object? _service;
    private readonly List<IDisposable> _registrations = new List<IDisposable>();
    private readonly IconRecorder _recorder = new IconRecorder();

    internal IAudioSessionControl2COM Raw { get; private set; } = null!;

    internal static OwnedAudioSession Create()
    {
        var fixture = new OwnedAudioSession();
        try
        {
            fixture.Initialize();
            return fixture;
        }
        catch
        {
            fixture.Dispose();
            throw;
        }
    }

    private void Initialize()
    {
        _controller = new AudioController();
        _device = AudioFixture.RequireDefaultPlayback(_controller);
        _client = NativeAudio.Activate<ITestAudioClient>(_device.Id);
        NativeAudio.Check(_client.GetMixFormat(out IntPtr format));
        try
        {
            Guid sessionId = Guid.NewGuid();
            NativeAudio.Check(_client.Initialize(0, 0, 0, 0, format, ref sessionId));
        }
        finally
        {
            Marshal.FreeCoTaskMem(format);
        }

        // GetService accepts IAudioSessionControl; QI then obtains the extended interface.
        Guid controlId = new Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD");
        NativeAudio.Check(_client.GetService(ref controlId, out object service));
        _service = service;
        Raw = (IAudioSessionControl2COM)service;

        // Observe through the same enumeration and subscription APIs a library consumer uses.
        // A unique icon value correlates the callback with our session without reading wrapper state.
        foreach (AudioSessionControl session in _device.SessionManager.Sessions)
        {
            _registrations.Add(session.RegisterAudioSessionNotification(_recorder));
        }
    }

    internal IconNotification ChangeIcon(string path, Guid? context = null)
    {
        NativeAudio.Check(Raw.SetIconPath(path, context));
        return _recorder.WaitFor(path);
    }

    public void Dispose()
    {
        try
        {
            foreach (IDisposable registration in _registrations)
            {
                registration.Dispose();
            }
        }
        finally
        {
            _registrations.Clear();
            try
            {
                _device?.Dispose();
            }
            finally
            {
                _device = null;
                _controller?.Dispose();
                _controller = null;
                NativeAudio.Release(_service);
                _service = null;
                Raw = null!;
                NativeAudio.Release(_client);
                _client = null;
            }
        }
    }

    internal sealed class IconNotification
    {
        internal IconNotification(AudioSessionInfo session, Guid? context)
        {
            Session = session;
            Context = context;
        }

        internal AudioSessionInfo Session { get; }
        internal Guid? Context { get; }
    }

    private sealed class IconRecorder : IAudioSessionEvents
    {
        private readonly Dictionary<string, IconNotification> _icons = new Dictionary<string, IconNotification>();

        internal IconNotification WaitFor(string path)
        {
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            lock (_icons)
            {
                while (!_icons.ContainsKey(path))
                {
                    TimeSpan remaining = TimeSpan.FromSeconds(5) - deadline.Elapsed;
                    if (remaining <= TimeSpan.Zero || !Monitor.Wait(_icons, remaining))
                    {
                        Assert.Fail($"No public icon notification for test-owned session: {path}");
                    }
                }
                return _icons[path];
            }
        }

        public void OnIconPathChanged(AudioSessionInfo session, string newIconPath, Guid? eventContext)
        {
            lock (_icons)
            {
                _icons[newIconPath] = new IconNotification(session, eventContext);
                Monitor.PulseAll(_icons);
            }
        }

        public void OnDisplayNameChanged(AudioSessionInfo session, string newDisplayName, Guid? eventContext) { }
        public void OnSimpleVolumeChanged(AudioSessionInfo session, float newVolume, bool newMute, Guid? eventContext) { }
        public void OnChannelVolumeChanged(AudioSessionInfo session, float[] volumes, uint channel, Guid? eventContext) { }
        public void OnGroupingParamChanged(AudioSessionInfo session, Guid grouping, Guid? eventContext) { }
        public void OnStateChanged(AudioSessionInfo session, AudioSessionState state) { }
        public void OnSessionDisconnected(AudioSessionBaseInfo session, AudioSessionDisconnectReason reason) { }
    }
}
