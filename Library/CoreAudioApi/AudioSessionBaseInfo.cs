/* Copyright (c) 2026 Peter Šulek. MIT License. */

using JetBrains.Annotations;

namespace AudioDeviceLib.CoreAudioApi;

/// <summary>Immutable identity of an audio session, retained after disconnection.</summary>
[PublicAPI]
public record AudioSessionBaseInfo
{
    /// <summary>Gets the process identifier (PID) that owns the session, or 0 if it was unavailable.</summary>
    public uint ProcessId { get; }

    /// <summary>Gets the session identifier string, shared by all instances of the same session, or an empty string if unavailable.</summary>
    public string SessionIdentifier { get; }

    /// <summary>Gets the identifier that uniquely distinguishes this session instance, or an empty string if unavailable.</summary>
    public string SessionInstanceIdentifier { get; }

    /// <summary>Gets a value indicating whether this is the reserved system-sounds session.</summary>
    public bool IsSystemSoundsSession { get; }

    /// <summary>Creates a session identity snapshot.</summary>
    /// <param name="processId">The owning process ID, or zero if unavailable.</param>
    /// <param name="sessionIdentifier">The session identifier, or an empty string.</param>
    /// <param name="sessionInstanceIdentifier">The instance identifier, or an empty string.</param>
    /// <param name="isSystemSoundsSession">Whether this is the system-sounds session.</param>
    internal AudioSessionBaseInfo(uint processId, string sessionIdentifier, string sessionInstanceIdentifier, bool isSystemSoundsSession)
    {
        ProcessId = processId;
        // ReSharper disable NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        SessionIdentifier = sessionIdentifier ?? string.Empty;
        SessionInstanceIdentifier = sessionInstanceIdentifier ?? string.Empty;
        // ReSharper restore NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        IsSystemSoundsSession = isSystemSoundsSession;
    }
}