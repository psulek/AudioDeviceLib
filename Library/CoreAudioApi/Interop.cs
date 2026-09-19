namespace AudioDeviceLib.CoreAudioApi;

public static class InteropUtils
{
    public static bool HrFailed(int hr) => hr < 0;
    
    public static bool HrSuccess(int hr) => hr >= 0;
}