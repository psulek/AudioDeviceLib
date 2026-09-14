/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  Enums.cs
  Public enums for AudioDeviceLib.

  Derived in part from AudioDeviceCmdlets by Francois Gendron (MIT),
  https://github.com/frgnca/AudioDeviceCmdlets
*/

using System;

namespace AudioDeviceLib.Lib;

/// <summary>Direction of an audio endpoint.</summary>
public enum AudioDeviceKind
{
    /// <summary>A render (output / playback) endpoint, e.g. speakers or headphones.</summary>
    Playback,

    /// <summary>A capture (input / recording) endpoint, e.g. a microphone.</summary>
    Recording
}

/// <summary>
/// Which Windows default-device role(s) to assign when setting a default device.
/// This is a bit flag: the three real Windows roles (<see cref="Console"/>,
/// <see cref="Multimedia"/>, <see cref="Communications"/>) can be combined, and each
/// set flag maps to one native <c>ERole</c> call. <see cref="Default"/> and
/// <see cref="All"/> are convenience combinations.
/// </summary>
[Flags]
public enum DefaultRole
{
    /// <summary>Console role (system sounds, games, voice commands) — native <c>eConsole</c>.</summary>
    Console = 1,

    /// <summary>Multimedia role (music, movies) — native <c>eMultimedia</c>. Matches <c>-DefaultOnly</c>.</summary>
    Multimedia = 2,

    /// <summary>Communications role (voice chat) — native <c>eCommunications</c>. Matches <c>-CommunicationOnly</c>.</summary>
    Communications = 4,

    /// <summary>
    /// Multimedia + Communications. This matches <c>Set-AudioDevice</c> called with no
    /// role switch (and, like the cmdlet, does NOT set the Console role).
    /// </summary>
    Default = Multimedia | Communications,

    /// <summary>
    /// Console + Multimedia + Communications. The most robust "make this THE default
    /// device for everything" option; use it when some applications follow the Console role.
    /// </summary>
    All = Console | Multimedia | Communications
}