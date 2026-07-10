using System.Diagnostics;
using System.Runtime.InteropServices;
using GearShift.Core.Engine;
using GearShift.Core.Models;

namespace GearShift.Core.System;

/// <summary>Captures and restores normal top-level window rectangles for scene programs.</summary>
public sealed class WindowLayoutManager : IWindowLayoutController
{
    public IReadOnlyList<WindowLayout> CaptureVisible()
    {
        var layouts = new List<WindowLayout>();
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window) || GetWindowTextLength(window) == 0) return true;
            GetWindowThreadProcessId(window, out var pid);
            if (pid == 0 || !GetWindowRect(window, out var rect)) return true;
            try
            {
                using var process = Process.GetProcessById((int)pid);
                var match = process.ProcessName.ToLowerInvariant() + ".exe";
                var placement = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
                var maximized = GetWindowPlacement(window, ref placement) && placement.showCmd == SW_MAXIMIZE;
                layouts.Add(new WindowLayout
                {
                    Match = match,
                    Left = rect.Left,
                    Top = rect.Top,
                    Width = Math.Max(1, rect.Right - rect.Left),
                    Height = Math.Max(1, rect.Bottom - rect.Top),
                    Maximized = maximized,
                });
            }
            catch { }
            return true;
        }, IntPtr.Zero);
        return layouts
            .GroupBy(x => x.Match, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    public void Restore(IReadOnlyList<WindowLayout> layouts)
    {
        if (layouts.Count == 0) return;
        var targets = layouts.ToDictionary(x => x.Match, StringComparer.OrdinalIgnoreCase);
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out var pid);
            if (pid == 0 || !targets.Any()) return true;
            try
            {
                using var process = Process.GetProcessById((int)pid);
                var name = process.ProcessName + ".exe";
                if (!targets.TryGetValue(name, out var target)) return true;
                ShowWindow(window, SW_RESTORE);
                SetWindowPos(window, IntPtr.Zero, target.Left, target.Top, target.Width, target.Height,
                    SWP_NOZORDER | SWP_NOACTIVATE);
                if (target.Maximized) ShowWindow(window, SW_MAXIMIZE);
                targets.Remove(name);
            }
            catch { }
            return true;
        }, IntPtr.Zero);
    }

    private const int SW_RESTORE = 9;
    private const int SW_MAXIMIZE = 3;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct WINDOWPLACEMENT
    {
        public int length, flags, showCmd;
        public IntPtr ptMinPosition, ptMaxPosition;
        public RECT rcNormalPosition;
    }
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT placement);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int command);
}
