/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  Program.cs
  Console test harness for AudioDeviceLib. Lists audio endpoints and sets the
  default playback/recording device, plus volume and mute, from the command line.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AudioDeviceLib.CoreAudioApi;
using AudioDeviceLib.Lib;

namespace AudioDeviceLib.Test;

internal static class Program
{
    private const string Exe = "Library.Test";

    private static int Main(string[] args)
    {
        // Help: -h / --help / /h / /? / help, or no args at all.
        if (args.Length == 0 || IsHelp(args[0]) || args.Any(IsHelp))
        {
            PrintHelp();
            return 0;
        }

        // Commands are bare words (e.g. "watch"); also accept a leading --/-// prefix (e.g. "--watch").
        string command = args[0].TrimStart('-', '/').ToLowerInvariant();
        var opts = ParseOptions(args.Skip(1).ToArray());

        try
        {
            var audio = new AudioController();
            switch (command)
            {
                case "list":    return CmdList(audio, opts);
                case "default": return CmdDefault(audio, opts);
                case "set":     return CmdSet(audio, opts);
                case "volume":  return CmdVolume(audio, opts);
                case "mute":    return CmdMute(audio, opts);
                case "watch":   return CmdWatch(audio, opts);
                default:
                    Console.Error.WriteLine("Unknown command: " + command);
                    Console.Error.WriteLine("Run '" + Exe + " --help' for usage.");
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERROR: " + ex.Message);
            return 2;
        }
    }

    // ---- Commands -----------------------------------------------------------------

    private static int CmdList(AudioController audio, Options o)
    {
        IReadOnlyList<AudioDevice> devices;
        if (o.Has("recording"))
        {
            devices = audio.GetRecordingDevices();
        }
        else if (o.Has("playback"))
        {
            devices = audio.GetPlaybackDevices();
        }
        else
        {
            devices = audio.GetDevices();
        }

        RenderDevices(devices);
        return 0;
    }

    private static void RenderDevices(IReadOnlyList<AudioDevice> devices)
    {
        if (devices.Count == 0)
        {
            Console.WriteLine("(no active devices)");
            return;
        }

        Console.WriteLine("Idx  Kind       Def  Comm  Name, ID");
        Console.WriteLine("---  ---------  ---  ----  --------------------------------------");
        foreach (var d in devices)
        {
            Console.WriteLine("{0,3}  {1,-9}  {2,-3}  {3,-4}  {4}, {5}",
                d.Index,
                d.Kind,
                d.IsDefault ? "*" : "",
                d.IsDefaultCommunication ? "*" : "",
                d.Name,
                d.Id);
        }
    }

    private static int CmdDefault(AudioController audio, Options o)
    {
        bool comm = o.Has("comm");
        bool recording = o.Has("recording");

        AudioDevice dev = recording
            ? audio.GetDefaultRecordingDevice(comm)
            : audio.GetDefaultPlaybackDevice(comm);

        string role = comm ? "communications" : "multimedia";
        string kind = recording ? "recording" : "playback";

        if (dev == null)
        {
            Console.WriteLine($"No default {kind} {role} device is set.");
            return 0;
        }

        Console.WriteLine($"Default {kind} {role} device:");
        PrintDevice(dev);
        return 0;
    }

    private static int CmdSet(AudioController audio, Options o)
    {
        if (!TryGetRole(o, out DefaultRole role))
        {
            return 1;
        }

        AudioDevice target = ResolveSelector(audio, o);
        if (target == null)
        {
            return 1;
        }

        audio.SetDefaultDevice(target, role);
        Console.WriteLine($"Set default ({role}):");
        // Re-read so the printed default flags reflect the change.
        AudioDevice updated = audio.GetDevices().FirstOrDefault(d => d.Id == target.Id) ?? target;
        PrintDevice(updated);
        return 0;
    }

    private static int CmdVolume(AudioController audio, Options o)
    {
        AudioDevice target = ResolveSelector(audio, o);
        if (target == null)
        {
            return 1;
        }

        string setVal = o.Get("set");
        if (setVal == null)
        {
            Console.WriteLine($"{target.Name}: volume {target.GetVolumePercent():0}%");
            return 0;
        }

        if (!float.TryParse(setVal, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var pct))
        {
            Console.Error.WriteLine("Invalid volume value: " + setVal + " (expected 0..100)");
            return 1;
        }

        target.SetVolumePercent(pct);
        Console.WriteLine($"{target.Name}: volume set to {target.GetVolumePercent():0}%");
        return 0;
    }

    private static int CmdMute(AudioController audio, Options o)
    {
        AudioDevice target = ResolveSelector(audio, o);
        if (target == null)
        {
            return 1;
        }

        string setVal = o.Get("set");
        if (setVal == null)
        {
            Console.WriteLine($"{target.Name}: muted={target.IsMuted}");
            return 0;
        }

        switch (setVal.ToLowerInvariant())
        {
            case "on":
            case "true":
            case "1":
                target.IsMuted = true;
                break;
            case "off":
            case "false":
            case "0":
                target.IsMuted = false;
                break;
            case "toggle":
                target.ToggleMute();
                break;
            default:
                Console.Error.WriteLine("Invalid mute value: " + setVal + " (expected on|off|toggle)");
                return 1;
        }

        Console.WriteLine($"{target.Name}: muted={target.IsMuted}");
        return 0;
    }

    // Registers a logging IAudioSessionEvents on every session of the chosen device and
    // waits. Change an app's volume/mute (e.g. in the Windows volume mixer) to see callbacks.
    private static int CmdWatch(AudioController audio, Options o)
    {
        // Route Debug.WriteLine output to the console so the events are visible when run normally.
        if (!Debugger.IsAttached)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
        }

        var devices = audio.GetDevices();
        RenderDevices(devices);

        // Device: use the selector if one was given, otherwise the default playback device.
        AudioDevice device;
        bool hasSelector = o.Get("name") != null || o.Get("id") != null || o.Get("index") != null;
        if (hasSelector)
        {
            device = ResolveSelector(audio, o);
            if (device == null)
            {
                return 1;
            }
        }
        else
        {
            device = audio.GetDefaultPlaybackDevice();
            if (device == null)
            {
                Console.Error.WriteLine("No default playback device to watch.");
                return 1;
            }
        }

        Console.WriteLine($"Watching audio sessions on: {device.Index}: {device.Name}, ID={device.Id}");

        // 1) Per-session events (the app / "System sounds" sliders in the mixer).
        SessionCollection sessions = device.Device.AudioSessionManager.Sessions;
        var registrations = new List<IDisposable>();

        for (int i = 0; i < sessions.Count; i++)
        {
            AudioSessionControl session = sessions[i];

            // The logger identifies each session from the AudioSessionInfo it receives per callback.
            var logger = new SessionEventsLogger();
            // Register returns an IDisposable token; dispose it to unregister.
            registrations.Add(session.RegisterAudioSessionNotification(logger));
        }

        // 2) Endpoint (device master) volume events (the "System -> Volume" slider).
        //    This is a separate notification path (IAudioEndpointVolume), so subscribe to it too.
        AudioEndpointVolume endpointVolume = device.Device.AudioEndpointVolume;
        AudioEndpointVolumeNotificationDelegate endpointHandler = data =>
            Console.WriteLine(
                $"[endpoint: {device.Name}] master={data.MasterVolume:P0} muted={data.Muted} channels={data.Channels}");
        endpointVolume.OnVolumeNotification += endpointHandler;

        // 3) Device (endpoint) change events, incl. default / default-communications device changes.
        //    This is registered on the controller (IMMNotificationClient), independent of any device.
        IDisposable deviceRegistration = audio.RegisterDeviceNotification(new AudioDeviceLogger());

        Console.WriteLine(
            $"Registered on {registrations.Count} session(s) + endpoint master volume + device events. " +
            "Change app/system volume/mute in the mixer, or switch the default device in Sound settings, " +
            "to see events. Press Enter to stop.");
        Console.ReadLine();

        deviceRegistration.Dispose();
        endpointVolume.OnVolumeNotification -= endpointHandler;
        foreach (IDisposable registration in registrations)
        {
            try { registration.Dispose(); }
            catch { /* best-effort cleanup */ }
        }

        Console.WriteLine("Unregistered. Done.");
        return 0;
    }

    // ---- Selector / role helpers --------------------------------------------------

    // Resolves a device from --name / --id / --index (+ --recording for name/index scope).
    private static AudioDevice ResolveSelector(AudioController audio, Options o)
    {
        bool recording = o.Has("recording");
        IReadOnlyList<AudioDevice> scope = recording ? audio.GetRecordingDevices() : audio.GetPlaybackDevices();

        string id = o.Get("id");
        string name = o.Get("name");
        string indexStr = o.Get("index");

        int provided = (id != null ? 1 : 0) + (name != null ? 1 : 0) + (indexStr != null ? 1 : 0);
        if (provided == 0)
        {
            Console.Error.WriteLine("Missing selector. Use one of: --name <substr> | --id <id> | --index <n>");
            return null;
        }
        if (provided > 1)
        {
            Console.Error.WriteLine("Use only one selector (--name, --id or --index).");
            return null;
        }

        AudioDevice match = null;
        if (id != null)
        {
            // --id searches across all devices (ID is globally unique).
            match = audio.GetDevices().FirstOrDefault(d =>
                string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                Console.Error.WriteLine("No device found with ID: " + id);
            }
        }
        else if (indexStr != null)
        {
            if (!int.TryParse(indexStr, out var idx))
            {
                Console.Error.WriteLine("Invalid index: " + indexStr);
                return null;
            }
            // Index is the 1-based position from the full 'list'.
            match = audio.GetDevices().FirstOrDefault(d => d.Index == idx);
            if (match == null)
            {
                Console.Error.WriteLine("No device found with index: " + idx);
            }
        }
        else
        {
            match = scope.FirstOrDefault(d =>
                d.Name != null && d.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
            if (match == null)
            {
                Console.Error.WriteLine(string.Format("No {0} device found matching name: {1}",
                    recording ? "recording" : "playback", name));
            }
        }

        return match;
    }

    private static bool TryGetRole(Options o, out DefaultRole role)
    {
        role = DefaultRole.Default;

        bool defaultOnly = o.Has("default-only");
        bool commOnly = o.Has("comm-only");
        string roleStr = o.Get("role");

        if (defaultOnly && commOnly)
        {
            Console.Error.WriteLine("Cannot combine --default-only and --comm-only.");
            return false;
        }
        if (defaultOnly) { role = DefaultRole.Multimedia; return true; }
        if (commOnly) { role = DefaultRole.Communications; return true; }

        if (roleStr != null)
        {
            // --role accepts one or more flags combined with ',', '+', ';' or space,
            // e.g. --role all, --role console+multimedia, --role "media,comm".
            DefaultRole combined = 0;
            foreach (string token in roleStr.Split(new[] { ',', '+', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                switch (token.ToLowerInvariant())
                {
                    case "default":
                    case "both":
                        combined |= DefaultRole.Default; break;
                    case "console":
                        combined |= DefaultRole.Console; break;
                    case "multimedia":
                    case "media":
                        combined |= DefaultRole.Multimedia; break;
                    case "communications":
                    case "comm":
                        combined |= DefaultRole.Communications; break;
                    case "all":
                        combined |= DefaultRole.All; break;
                    default:
                        Console.Error.WriteLine("Invalid --role token: " + token +
                                                " (expected console|multimedia|communications|default|all)");
                        return false;
                }
            }

            if (combined == 0)
            {
                Console.Error.WriteLine("Invalid --role: no role specified.");
                return false;
            }

            role = combined;
        }

        return true;
    }

    private static void PrintDevice(AudioDevice d)
    {
        Console.WriteLine($"  Index : {d.Index}");
        Console.WriteLine($"  Name  : {d.Name}");
        Console.WriteLine($"  Kind  : {d.Kind}");
        Console.WriteLine($"  ID    : {d.Id}");
        Console.WriteLine($"  Default: {d.IsDefault}   DefaultComm: {d.IsDefaultCommunication}");
        Console.WriteLine($"  Volume: {d.GetVolumePercent():0}%   Muted: {d.IsMuted}");
    }

    // ---- Tiny option parser -------------------------------------------------------

    private static bool IsHelp(string a)
    {
        switch (a.ToLowerInvariant())
        {
            case "-h":
            case "--help":
            case "/h":
            case "/?":
            case "help":
                return true;
            default:
                return false;
        }
    }

    // Parses "--key value", "--key=value", "/key value", and bare flags "--flag".
    private static Options ParseOptions(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a.StartsWith("--"))
            {
                a = a.Substring(2);
            }
            else if (a.StartsWith("/") || a.StartsWith("-"))
            {
                a = a.Substring(1);
            }
            else
            {
                continue; // positional args are ignored; selectors are explicit
            }

            string key, val;
            int eq = a.IndexOf('=');
            if (eq >= 0)
            {
                key = a.Substring(0, eq);
                val = a.Substring(eq + 1);
            }
            else
            {
                key = a;
                // Consume the next token as the value unless it is itself an option.
                if (i + 1 < args.Length && !IsOptionToken(args[i + 1]))
                {
                    val = args[i + 1];
                    i++;
                }
                else
                {
                    val = ""; // bare flag
                }
            }
            map[key] = val;
        }
        return new Options(map);
    }

    private static bool IsOptionToken(string s)
    {
        return s.StartsWith("--") || s.StartsWith("/") || (s.StartsWith("-") && s.Length > 1 && !char.IsDigit(s[1]));
    }

    private sealed class Options
    {
        private readonly Dictionary<string, string> _map;
        public Options(Dictionary<string, string> map) { _map = map; }

        // Present as a flag (bare) or with any value.
        public bool Has(string key) { return _map.ContainsKey(key); }

        // Returns the value, or null if the key was not supplied.
        public string Get(string key)
        {
            return _map.TryGetValue(key, out var v) ? v : null;
        }
    }

    // ---- Help ---------------------------------------------------------------------

    private static void PrintHelp()
    {
        Console.WriteLine(@"
AudioDeviceLib test harness - list and control Windows audio devices.

USAGE:
  " + Exe + @" <command> [options]
  " + Exe + @" -h | --help | /h | /?

COMMANDS:
  list       List audio devices
  default    Show the current default device
  set        Set the default device
  volume     Get or set a device's volume
  mute       Get, set or toggle a device's mute state
  watch      Log session, endpoint-volume and device (default-change) events for a device
  help       Show this help

SELECTORS (for set / volume / mute - pick exactly one):
  --name <substr>   Match by name, case-insensitive substring (e.g. Speakers, Realtek)
  --id <id>         Match by exact endpoint ID
  --index <n>       Match by 1-based index shown in 'list'
  --recording       Scope --name/--index to recording devices (default: playback)

ROLE (for set - which Windows default role(s) to assign; combinable flags):
  --role <r>        one or more of: console | multimedia | communications | default | all
                    combine with ',', '+' or space, e.g. --role console+multimedia
                    (default: 'default' = multimedia + communications; 'all' adds console)
  --default-only    Alias for --role multimedia      (matches AudioDeviceCmdlets -DefaultOnly)
  --comm-only       Alias for --role communications   (matches -CommunicationOnly)

OPTIONS:
  list      [--playback | --recording]        (default: all)
  default   [--playback | --recording] [--comm]
  set       <selector> [role]
  volume    <selector> [--set <0..100>]       (omit --set to just read)
  mute      <selector> [--set <on|off|toggle>] (omit --set to just read)
  watch     [<selector>]                       (default: default playback device)

EXAMPLES:
  " + Exe + @" list
  " + Exe + @" list --playback
  " + Exe + @" default
  " + Exe + @" default --comm
  " + Exe + @" set --name Speakers
  " + Exe + @" set --name Realtek --default-only
  " + Exe + @" set --index 3 --role all
  " + Exe + @" set --name Speakers --role console+multimedia
  " + Exe + @" set --recording --name Microphone
  " + Exe + @" volume --name Speakers
  " + Exe + @" volume --name Speakers --set 50
  " + Exe + @" mute --name Speakers --set toggle
  " + Exe + @" watch
  " + Exe + @" watch --name Speakers

EXIT CODES:
  0  success
  1  not found or usage error
  2  runtime error
");
    }
}