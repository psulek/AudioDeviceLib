/* Copyright (c) 2026 Peter Šulek. MIT License. */
using System;
using System.Reflection;
using AudioDeviceLib.CoreAudioApi.Interfaces;

namespace AudioDeviceLib.UnitTests;

// Install only before the first operation; inspect only after all racing workers have joined.
// This is the sole private implementation dependency used by the concurrency fixtures.
internal static class ControllerTestAccess
{
    private static readonly FieldInfo EnumeratorField =
        typeof(AudioController).GetField("_deviceEnumerator", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(AudioController).FullName, "_deviceEnumerator");

    internal static AudioController Create(IMMDeviceEnumeratorCOM enumerator)
    {
        var controller = new AudioController();
        try
        {
            EnumeratorField.SetValue(controller, enumerator);
            return controller;
        }
        catch
        {
            controller.Dispose();
            throw;
        }
    }

    internal static bool HasLiveEnumerator(AudioController controller) =>
        EnumeratorField.GetValue(controller) != null;
}
