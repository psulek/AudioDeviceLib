/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  Enums.cs
  Public enums for AudioDeviceLib.

  Derived in part from AudioDeviceCmdlets by Francois Gendron (MIT),
  https://github.com/frgnca/AudioDeviceCmdlets
*/

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
/// Values mirror the behaviour of AudioDeviceCmdlets' Set-AudioDevice switches.
/// </summary>
public enum DefaultRole
{
    /// <summary>
    /// Set the device as the default for both the multimedia and communications roles.
    /// This matches <c>Set-AudioDevice</c> called with no role switch.
    /// (Note: like the cmdlet, this does NOT set the Console role.)
    /// </summary>
    MultimediaAndCommunications = 0,

    /// <summary>Set the multimedia role only. Matches <c>-DefaultOnly</c>.</summary>
    Multimedia,

    /// <summary>Set the communications role only. Matches <c>-CommunicationOnly</c>.</summary>
    Communications,

    /// <summary>
    /// Set the Console, Multimedia and Communications roles. This is the most
    /// robust "make this THE default output/input" option and is not offered by
    /// the original cmdlet; use it when some applications follow the Console role.
    /// </summary>
    All
}