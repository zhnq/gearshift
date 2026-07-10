using Microsoft.Win32;

namespace GearShift.App.Services;

public enum ProgramSource
{
    StartMenu,
    Desktop,
    Installed,
}

public sealed record ProgramCandidate(string Name, string Path, ProgramSource Source)
{
    public string SourceLabel => Source switch
    {
        ProgramSource.StartMenu => "开始菜单",
        ProgramSource.Desktop => "桌面",
        _ => "已安装程序",
    };
}

/// <summary>Finds launchable desktop programs from the places Windows users expect.</summary>
public static class ProgramCatalog
{
    public static IReadOnlyList<ProgramCandidate> GetAll()
    {
        var result = new Dictionary<string, ProgramCandidate>(StringComparer.OrdinalIgnoreCase);
        AddShortcuts(result, Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), ProgramSource.StartMenu);
        AddShortcuts(result, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), ProgramSource.StartMenu);
        AddShortcuts(result, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ProgramSource.Desktop);
        AddShortcuts(result, Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), ProgramSource.Desktop);
        AddInstalled(result, Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall");
        AddInstalled(result, Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Uninstall");
        AddInstalled(result, Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
        return result.Values.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static void AddShortcuts(Dictionary<string, ProgramCandidate> result, string root, ProgramSource source)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return;
        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                         .Where(p => p.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)
                                  || p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
            {
                var target = file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)
                    ? ShortcutResolver.ResolveTarget(file)
                    : file;
                if (string.IsNullOrWhiteSpace(target) || !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    continue;
                var name = Path.GetFileNameWithoutExtension(file);
                result.TryAdd(target, new ProgramCandidate(name, target, source));
            }
        }
        catch { /* one inaccessible shortcut folder must not break the picker */ }
    }

    private static void AddInstalled(Dictionary<string, ProgramCandidate> result, RegistryKey hive, string path)
    {
        try
        {
            using var root = hive.OpenSubKey(path);
            if (root is null) return;
            foreach (var subName in root.GetSubKeyNames())
            {
                using var sub = root.OpenSubKey(subName);
                var name = sub?.GetValue("DisplayName") as string;
                if (string.IsNullOrWhiteSpace(name)) continue;
                var icon = (sub?.GetValue("DisplayIcon") as string)?.Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(icon) && icon.Contains(',')) icon = icon.Split(',')[0].Trim('"');
                var location = sub?.GetValue("InstallLocation") as string;
                var target = ResolveExecutable(icon, location, name);
                if (target is null) continue;
                result.TryAdd(target, new ProgramCandidate(name, target, ProgramSource.Installed));
            }
        }
        catch { /* malformed or protected uninstall entries are ignored */ }
    }

    private static string? ResolveExecutable(string? icon, string? location, string displayName)
    {
        if (!string.IsNullOrWhiteSpace(icon) && File.Exists(icon) && icon.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return icon;
        if (string.IsNullOrWhiteSpace(location) || !Directory.Exists(location)) return null;
        try
        {
            var normalized = new string(displayName.Where(char.IsLetterOrDigit).ToArray());
            return Directory.EnumerateFiles(location, "*.exe", SearchOption.TopDirectoryOnly)
                .OrderByDescending(p => new string(Path.GetFileNameWithoutExtension(p).Where(char.IsLetterOrDigit).ToArray())
                    .Contains(normalized, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
        }
        catch { return null; }
    }
}
