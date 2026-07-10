using System.Runtime.InteropServices;
using GearShift.Core.Engine;

namespace GearShift.Core.System;

public sealed record AudioEndpoint(string Id, string Name);

/// <summary>Core Audio default-playback selector. Uses Windows' policy configuration COM interface.</summary>
public sealed class AudioDeviceManager : IAudioDeviceManager
{
    public IReadOnlyList<AudioEndpoint> PlaybackDevices()
    {
        var result = new List<AudioEndpoint>();
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        try
        {
            Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(EDataFlow.eRender, 1, out var devices));
            devices.GetCount(out var count);
            for (uint i = 0; i < count; i++)
            {
                devices.Item(i, out var device);
                device.GetId(out var id);
                result.Add(new AudioEndpoint(id, id));
            }
        }
        finally { Marshal.FinalReleaseComObject(enumerator); }
        return result;
    }

    public void SetDefaultPlayback(string endpointId)
    {
        if (string.IsNullOrWhiteSpace(endpointId)) throw new ArgumentException("音频设备 ID 不能为空", nameof(endpointId));
        var policy = (IPolicyConfig)new PolicyConfigClient();
        try
        {
            foreach (ERole role in Enum.GetValues<ERole>()) Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(endpointId, role));
        }
        finally { Marshal.FinalReleaseComObject(policy); }
    }

    private enum EDataFlow { eRender, eCapture, eAll }
    private enum ERole { eConsole, eMultimedia, eCommunications }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator { int EnumAudioEndpoints(EDataFlow flow, int state, out IMMDeviceCollection devices); }
    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection { int GetCount(out uint count); int Item(uint index, out IMMDevice device); }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice { int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, out IntPtr instance); int OpenPropertyStore(int access, out IntPtr properties); int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id); int GetState(out int state); }
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] private class MMDeviceEnumeratorComObject { }
    [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")] private class PolicyConfigClient { }
    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        int GetMixFormat(string device, IntPtr format); int GetDeviceFormat(string device, bool defaultFormat, IntPtr format); int ResetDeviceFormat(string device); int SetDeviceFormat(string device, IntPtr endpoint, IntPtr mix);
        int GetProcessingPeriod(string device, bool defaultPeriod, IntPtr period, IntPtr minimum); int SetProcessingPeriod(string device, IntPtr period);
        int GetShareMode(string device, IntPtr mode); int SetShareMode(string device, IntPtr mode); int GetPropertyValue(string device, IntPtr key, IntPtr value); int SetPropertyValue(string device, IntPtr key, IntPtr value);
        int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string device, ERole role); int SetEndpointVisibility(string device, bool visible);
    }
}
