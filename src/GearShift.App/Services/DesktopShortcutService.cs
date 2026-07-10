using System.Runtime.InteropServices;

namespace GearShift.App.Services;

public static class DesktopShortcutService
{
    public static string CreateSceneShortcut(string sceneId, string sceneName)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var path = Path.Combine(desktop, $"GearShift · {Sanitize(sceneName)}.lnk");
        var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows Script Host 不可用");
        dynamic shell = Activator.CreateInstance(type)!;
        try
        {
            dynamic shortcut = shell.CreateShortcut(path);
            shortcut.TargetPath = Environment.ProcessPath ?? throw new InvalidOperationException("无法定位 GearShift.exe");
            shortcut.Arguments = "--scene " + sceneId;
            shortcut.WorkingDirectory = AppContext.BaseDirectory;
            shortcut.IconLocation = shortcut.TargetPath;
            shortcut.Save();
            Marshal.FinalReleaseComObject(shortcut);
            return path;
        }
        finally { Marshal.FinalReleaseComObject(shell); }
    }

    private static string Sanitize(string value) => string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
