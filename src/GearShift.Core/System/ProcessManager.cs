using System.Diagnostics;
using System.Runtime.InteropServices;
using GearShift.Core.Engine;
using GearShift.Core.Models;

namespace GearShift.Core.System;

/// <summary>
/// Enumerates, launches and terminates processes. Closing tries a graceful window close first and
/// only force-kills as a fallback, so unsaved work gets a chance to prompt.
/// </summary>
public sealed partial class ProcessManager : IProcessController
{
    /// <summary>
    /// Executable names of processes that own a visible, titled top-level window — i.e. the apps a
    /// user actually interacts with, filtering out the hundreds of background/system processes.
    /// </summary>
    public IReadOnlySet<string> VisibleWindowProcessNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in VisibleWindowApps())
            names.Add(app.Name);
        return names;
    }

    /// <summary>
    /// Visible-window apps together with their executable path (used to show real program icons).
    /// The path may be <c>null</c> when the OS denies access to the process module.
    /// </summary>
    public IReadOnlyList<RunningApp> VisibleWindowApps()
    {
        var pids = new HashSet<uint>();
        EnumWindows((hWnd, _) =>
        {
            if (IsWindowVisible(hWnd) && GetWindowTextLength(hWnd) > 0)
            {
                GetWindowThreadProcessId(hWnd, out var pid);
                if (pid != 0) pids.Add(pid);
            }
            return true;
        }, IntPtr.Zero);

        var byName = new Dictionary<string, RunningApp>(StringComparer.OrdinalIgnoreCase);
        foreach (var pid in pids)
        {
            try
            {
                using var p = Process.GetProcessById((int)pid);
                var name = p.ProcessName.ToLowerInvariant() + ".exe";
                if (byName.ContainsKey(name))
                    continue;

                string? path = null;
                try { path = p.MainModule?.FileName; }
                catch { /* access denied for elevated / protected processes */ }

                byName[name] = new RunningApp(name, path);
            }
            catch
            {
                // Process exited between enumeration and lookup; skip it.
            }
        }
        return byName.Values.ToList();
    }

    /// <summary>Lower-cased executable names of every running process, e.g. <c>steam.exe</c>.</summary>
    public IReadOnlySet<string> RunningProcessNames()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                set.Add(p.ProcessName.ToLowerInvariant() + ".exe");
            }
            catch
            {
                // Some system processes deny access to their metadata; ignore them.
            }
            finally
            {
                p.Dispose();
            }
        }
        return set;
    }

    public void Start(AppRef app)
    {
        if (string.IsNullOrWhiteSpace(app.Path))
            throw new InvalidOperationException($"{app.Label} 缺少可执行文件路径");

        var psi = new ProcessStartInfo
        {
            FileName = app.Path,
            Arguments = app.Args ?? string.Empty,
            WorkingDirectory = string.IsNullOrWhiteSpace(app.WorkingDirectory)
                ? Path.GetDirectoryName(app.Path) ?? string.Empty
                : app.WorkingDirectory,
            UseShellExecute = true,
        };
        Process.Start(psi);
    }

    public CloseOutcome Close(string match)
    {
        var name = NormalizeName(match);
        var processes = Process.GetProcessesByName(name);
        if (processes.Length == 0)
            return CloseOutcome.NotRunning;

        var forced = false;
        var anyClosed = false;

        foreach (var p in processes)
        {
            try
            {
                if (p.CloseMainWindow() && p.WaitForExit(2500))
                {
                    anyClosed = true;
                    continue;
                }

                p.Kill(entireProcessTree: true);
                p.WaitForExit(2500);
                forced = true;
                anyClosed = true;
            }
            catch
            {
                // Access denied or already gone — record nothing and continue.
            }
            finally
            {
                p.Dispose();
            }
        }

        return anyClosed
            ? (forced ? CloseOutcome.ForceKilled : CloseOutcome.ClosedGracefully)
            : CloseOutcome.Failed;
    }

    private static string NormalizeName(string match)
    {
        var n = match.Trim();
        return n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? n[..^4] : n;
    }

    // ---- Win32 window enumeration (for VisibleWindowProcessNames) ----

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}
