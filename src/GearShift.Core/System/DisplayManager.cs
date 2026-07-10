using System.Diagnostics;
using GearShift.Core.Engine;

namespace GearShift.Core.System;

public sealed class DisplayManager : IDisplayManager
{
    public void SetMode(string mode)
    {
        var supported = new HashSet<string>(["internal", "clone", "extend", "external"], StringComparer.OrdinalIgnoreCase);
        if (!supported.Contains(mode)) throw new InvalidOperationException("显示器模式必须是 internal、clone、extend 或 external");
        using var process = Process.Start(new ProcessStartInfo("DisplaySwitch.exe", "/" + mode.ToLowerInvariant()) { UseShellExecute = false, CreateNoWindow = true })
            ?? throw new InvalidOperationException("无法启动 DisplaySwitch.exe");
        if (!process.WaitForExit(10_000)) throw new TimeoutException("显示器切换超时");
        if (process.ExitCode != 0) throw new InvalidOperationException($"显示器切换失败 ({process.ExitCode})");
    }
}
