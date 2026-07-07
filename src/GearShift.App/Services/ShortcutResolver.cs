using System.Runtime.InteropServices;

namespace GearShift.App.Services;

/// <summary>
/// Resolves a Windows <c>.lnk</c> shortcut to the executable it points at, via the
/// <c>WScript.Shell</c> COM object. Returns <c>null</c> when the shortcut can't be read or has no
/// file target (e.g. a shortcut to a folder or a store app). Callers fall back to the raw path.
/// </summary>
public static class ShortcutResolver
{
    public static string? ResolveTarget(string lnkPath)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
                return null;

            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                dynamic shortcut = shell.CreateShortcut(lnkPath);
                string target = shortcut.TargetPath;
                Marshal.FinalReleaseComObject(shortcut);
                return string.IsNullOrWhiteSpace(target) ? null : target;
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
        catch
        {
            return null;
        }
    }
}
