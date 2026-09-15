/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  AudioSessionInfo.cs
  Immutable, point-in-time snapshot of an audio session's information.
*/

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// An immutable, point-in-time snapshot of the information about an <see cref="AudioSessionControl"/>.
/// It is handed to every <see cref="IAudioSessionEvents"/> callback so a consumer can identify and
/// inspect the source session without touching the live COM object from a notification thread.
/// </summary>
/// <remarks>
/// The values are captured when the snapshot is created (once per callback) and never change
/// afterwards, so an instance is safe to read from any thread, keep, or store. Volatile values such
/// as <see cref="DisplayName"/>, <see cref="IconPath"/> and <see cref="State"/> reflect the moment
/// the snapshot was taken. Any value that could not be read is left at its default
/// (<c>null</c>, <c>0</c> or <c>false</c>).
/// </remarks>
public sealed class AudioSessionInfo
{
    /// <summary>Gets the display name reported by the session at snapshot time, or <c>null</c> if unavailable.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the icon path reported by the session at snapshot time, or <c>null</c> if unavailable.</summary>
    public string IconPath { get; }

    /// <summary>Gets the activity state of the session at snapshot time.</summary>
    public AudioSessionState State { get; }

    /// <summary>Gets the process identifier (PID) that owns the session, or 0 if it was unavailable.</summary>
    public uint ProcessID { get; }

    /// <summary>Gets the session identifier string, shared by all instances of the same session, or <c>null</c> if unavailable.</summary>
    public string SessionIdentifier { get; }

    /// <summary>Gets the identifier that uniquely distinguishes this session instance, or <c>null</c> if unavailable.</summary>
    public string SessionInstanceIdentifier { get; }

    /// <summary>Gets a value indicating whether this is the reserved system-sounds session.</summary>
    public bool IsSystemSoundsSession { get; }

    internal AudioSessionInfo(string displayName, string iconPath, AudioSessionState state, uint processId,
        string sessionIdentifier, string sessionInstanceIdentifier, bool isSystemSoundsSession)
    {
        DisplayName = displayName;
        IconPath = iconPath;
        State = state;
        ProcessID = processId;
        SessionIdentifier = sessionIdentifier;
        SessionInstanceIdentifier = sessionInstanceIdentifier;
        IsSystemSoundsSession = isSystemSoundsSession;
    }
}
