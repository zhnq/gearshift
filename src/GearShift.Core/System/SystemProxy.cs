using System.Runtime.InteropServices;
using Microsoft.Win32;
using GearShift.Core.Engine;

namespace GearShift.Core.System;

/// <summary>
/// Reads and writes the per-user Windows system proxy via the Internet Settings registry key, then
/// broadcasts the change through WinINet so running apps pick it up immediately (the step everyone
/// forgets, which is why toggling the registry alone appears to "do nothing").
/// </summary>
public sealed class SystemProxy : ISystemProxy
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    public bool? IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        if (key?.GetValue("ProxyEnable") is int v)
            return v != 0;
        return null;
    }

    public void SetEnabled(bool on)
    {
        using (var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true)
                         ?? Registry.CurrentUser.CreateSubKey(KeyPath))
        {
            key.SetValue("ProxyEnable", on ? 1 : 0, RegistryValueKind.DWord);
        }

        // Tell WinINet the settings changed and to refresh them.
        InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
        InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
    }

    private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
    private const int INTERNET_OPTION_REFRESH = 37;

    [DllImport("wininet.dll", SetLastError = true)]
    private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);
}
