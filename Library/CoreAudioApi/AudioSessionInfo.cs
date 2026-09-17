/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  AudioSessionInfo.cs
  Immutable, point-in-time snapshot of an audio session's information.
*/

using JetBrains.Annotations;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>
/// An immutable, point-in-time snapshot of the information about an <see cref="AudioSessionControl"/>.
/// It is handed to every <see cref="IAudioSessionEvents"/> callback so a consumer can identify and
/// inspect the source session without touching the live COM object from a notification thread.
/// </summary>
/// <remarks>
/// The values are captured when the snapshot is created (once per callback) and never change
/// afterward, so an instance is safe to read from any thread, keep, or store. Volatile values such
/// as <see cref="DisplayName"/>, <see cref="IconPath"/> and <see cref="State"/> reflect the moment
/// the snapshot was taken. Any value that could not be read is left at its default
/// (an empty string, <c>0</c> or <c>false</c>).
/// </remarks>
[PublicAPI]
public sealed record AudioSessionInfo : AudioSessionBaseInfo
{
    /// <summary>Gets the display name reported by the session at snapshot time, or an empty string if unavailable.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the icon path reported by the session at snapshot time, or an empty string if unavailable.</summary>
    public string IconPath { get; }

    /// <summary>Gets the activity state of the session at snapshot time.</summary>
    public AudioSessionState State { get; }

    internal AudioSessionInfo(string displayName, string iconPath, AudioSessionState state, uint processId,
        string sessionIdentifier, string sessionInstanceIdentifier, bool isSystemSoundsSession)
    : base(processId, sessionIdentifier, sessionInstanceIdentifier, isSystemSoundsSession)
    {
        // ReSharper disable NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        DisplayName = displayName ?? string.Empty;
        IconPath = iconPath ?? string.Empty;
        // ReSharper restore NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        State = state;
    }
}
