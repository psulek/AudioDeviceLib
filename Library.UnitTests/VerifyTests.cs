using System;
using System.IO;
using System.Linq;
using AudioDeviceLib.CoreAudioApi;
using AudioDeviceLib.Lib;
using NUnit.Framework;

namespace AudioDeviceLib.UnitTests;

[TestFixture]
public class VerifyTests
{
    [Test]
    public void VerifyInteropCalls()
    {
        using var controller = new AudioController();
        var devices = controller.GetDevices();
        var devicesList = new object[devices.Count];
        for (int i = 0; i < devices.Count; i++)
        {
            devicesList[i] = SerializeAudioDevice(devices[i]);
        }

        string json = System.Text.Json.JsonSerializer.Serialize(devicesList, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        // File.WriteAllText("AudioDevicesOld.json", json);
    }

    private static object SerializeAudioDevice(AudioDevice device)
    {
        object GetMeter()
        {
            var meter = device.Meter;
            if (meter == null) return null;
           
            return new
            {
                meter.HardwareSupport,
                meter.MasterPeakValue,
                meter.MeteringChannelCount,
                ChannelsPeakValues = meter.GetChannelsPeakValues()
            };
        }
        
        PropertyStoreProperty[] GetProperties()
        {
            var store = device.Properties;
            if (store == null) return null;

            var count = store.Count;
            var properties = new PropertyStoreProperty[count];
            for (int i = 0; i < count; i++)
            {
                properties[i] = store[i];
            }

            return [.. properties.OrderBy(x => x.Key.Name)];
        }

        object[] GetSessions()
        {
            var count = device.SessionManager.Sessions.Count;
            object[] sessions = new object[count];
            
            for (int i = 0; i < count; i++)
            {
                var session = device.SessionManager.Sessions[i];
                sessions[i] = new
                {
                    Info = session.ToSessionInfo(),
                    session.SimpleAudioVolume?.MasterVolume,
                    session.SimpleAudioVolume?.Mute
                };
            }
            
            return sessions;
        }
        
        var obj = new
        {
            Device = device.ToDeviceInfo(),
            Meter = GetMeter(),
            device.IsMuted,
            Properties = GetProperties(),
            Sessions = GetSessions()   
        };
        
        return obj;
    }
}