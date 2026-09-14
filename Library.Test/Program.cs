/*
  Copyright (c) 2026 Peter Šulek
  MIT License

  Program.cs
  Console test harness for AudioDeviceLib. Lists audio endpoints and sets the
  default playback/recording device, plus volume and mute, from the command line.
*/

using System;
using System.Collections.Generic;
using System.Linq;
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

        string command = args[0].ToLowerInvariant();
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

        if (devices.Count == 0)
        {
            Console.WriteLine("(no active devices)");
            return 0;
        }

        Console.WriteLine("Idx  Kind       Def  Comm  Name");
        Console.WriteLine("---  ---------  ---  ----  --------------------------------------");
        foreach (var d in devices)
        {
            Console.WriteLine("{0,3}  {1,-9}  {2,-3}  {3,-4}  {4}",
                d.Index,
                d.Kind,
                d.IsDefault ? "*" : "",
                d.IsDefaultCommunication ? "*" : "",
                d.Name);
        }
        return 0;
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
            Console.WriteLine(string.Format("No default {0} {1} device is set.", kind, role));
            return 0;
        }

        Console.WriteLine(string.Format("Default {0} {1} device:", kind, role));
        PrintDevice(dev);
        return 0;
    }

    private static int CmdSet(AudioController audio, Options o)
    {
        DefaultRole role;
        if (!TryGetRole(o, out role))
        {
            return 1;
        }

        AudioDevice target = ResolveSelector(audio, o);
        if (target == null)
        {
            return 1;
        }

        audio.SetDefaultDevice(target, role);
        Console.WriteLine(string.Format("Set default ({0}):", role));
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
            Console.WriteLine(string.Format("{0}: volume {1:0}%", target.Name, target.GetVolumePercent()));
            return 0;
        }

        float pct;
        if (!float.TryParse(setVal, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out pct))
        {
            Console.Error.WriteLine("Invalid volume value: " + setVal + " (expected 0..100)");
            return 1;
        }

        target.SetVolumePercent(pct);
        Console.WriteLine(string.Format("{0}: volume set to {1:0}%", target.Name, target.GetVolumePercent()));
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
            Console.WriteLine(string.Format("{0}: muted={1}", target.Name, target.IsMuted));
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

        Console.WriteLine(string.Format("{0}: muted={1}", target.Name, target.IsMuted));
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
            int idx;
            if (!int.TryParse(indexStr, out idx))
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
        role = DefaultRole.MultimediaAndCommunications;

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
            switch (roleStr.ToLowerInvariant())
            {
                case "default":
                case "both":
                    role = DefaultRole.MultimediaAndCommunications; break;
                case "multimedia":
                case "media":
                    role = DefaultRole.Multimedia; break;
                case "communications":
                case "comm":
                    role = DefaultRole.Communications; break;
                case "all":
                    role = DefaultRole.All; break;
                default:
                    Console.Error.WriteLine("Invalid --role: " + roleStr +
                                            " (expected default|multimedia|communications|all)");
                    return false;
            }
        }

        return true;
    }

    private static void PrintDevice(AudioDevice d)
    {
        Console.WriteLine(string.Format("  Index : {0}", d.Index));
        Console.WriteLine(string.Format("  Name  : {0}", d.Name));
        Console.WriteLine(string.Format("  Kind  : {0}", d.Kind));
        Console.WriteLine(string.Format("  ID    : {0}", d.Id));
        Console.WriteLine(string.Format("  Default: {0}   DefaultComm: {1}", d.IsDefault, d.IsDefaultCommunication));
        Console.WriteLine(string.Format("  Volume: {0:0}%   Muted: {1}", d.GetVolumePercent(), d.IsMuted));
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
            string v;
            return _map.TryGetValue(key, out v) ? v : null;
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
  help       Show this help

SELECTORS (for set / volume / mute - pick exactly one):
  --name <substr>   Match by name, case-insensitive substring (e.g. Speakers, Realtek)
  --id <id>         Match by exact endpoint ID
  --index <n>       Match by 1-based index shown in 'list'
  --recording       Scope --name/--index to recording devices (default: playback)

ROLE (for set - which Windows default role to assign):
  --role <r>        default | multimedia | communications | all   (default: default)
  --default-only    Alias for --role multimedia      (matches AudioDeviceCmdlets -DefaultOnly)
  --comm-only       Alias for --role communications   (matches -CommunicationOnly)
                    'default' sets Multimedia + Communications; 'all' also sets Console.

OPTIONS:
  list      [--playback | --recording]        (default: all)
  default   [--playback | --recording] [--comm]
  set       <selector> [role]
  volume    <selector> [--set <0..100>]       (omit --set to just read)
  mute      <selector> [--set <on|off|toggle>] (omit --set to just read)

EXAMPLES:
  " + Exe + @" list
  " + Exe + @" list --playback
  " + Exe + @" default
  " + Exe + @" default --comm
  " + Exe + @" set --name Speakers
  " + Exe + @" set --name Realtek --default-only
  " + Exe + @" set --index 3 --role all
  " + Exe + @" set --recording --name Microphone
  " + Exe + @" volume --name Speakers
  " + Exe + @" volume --name Speakers --set 50
  " + Exe + @" mute --name Speakers --set toggle

EXIT CODES:
  0  success
  1  not found or usage error
  2  runtime error
");
    }
}