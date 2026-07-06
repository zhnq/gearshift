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

    public static void RestartElevated()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                Verb = "runas",
            });
            Application.Current.Exit();
        }
        catch
        {
            // User dismissed the UAC prompt — stay running unelevated.
        }
    }
}
