using System.Diagnostics;
using System.Security.Principal;
using Microsoft.UI.Xaml;

namespace GearShift.App.Services;

/// <summary>
/// Detects and (re)acquires administrator rights. Closing programs owned by other sessions or editing
/// services requires elevation; the app runs asInvoker and offers to relaunch elevated on demand.
/// </summary>
public static class ElevationHelper
{
    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void RestartElevated(string? sceneId = null)
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
            return;

        try
        {
            if (!string.IsNullOrWhiteSpace(sceneId))
            {
                SettingsService.Current.PendingElevatedSceneId = sceneId;
                SettingsService.Save();
            }

            // Starting the elevated copy immediately races the single-instance lock. Let a tiny
            // detached helper wait for this process to exit, then request elevation.
            var script = Path.Combine(Path.GetTempPath(), $"gearshift-elevate-{Guid.NewGuid():N}.ps1");
            File.WriteAllText(script, """
param([int]$ProcessId, [string]$Exe, [string]$Script)
try { Wait-Process -Id $ProcessId -Timeout 20 -ErrorAction SilentlyContinue } catch {}
Start-Process -FilePath $Exe -Verb RunAs
Remove-Item -LiteralPath $Script -Force -ErrorAction SilentlyContinue
""");
            var helper = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script,
                         "-ProcessId", Environment.ProcessId.ToString(), "-Exe", exe, "-Script", script })
                helper.ArgumentList.Add(arg);
            _ = Process.Start(helper);
            Application.Current.Exit();
        }
        catch
        {
            // User dismissed the UAC prompt — stay running unelevated.
            SettingsService.Current.PendingElevatedSceneId = null;
            SettingsService.Save();
        }
    }
}
