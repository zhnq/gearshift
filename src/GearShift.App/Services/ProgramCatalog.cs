using Microsoft.Win32;
using System.Diagnostics;

namespace GearShift.App.Services;

public enum ProgramSource
{
    StartMenu,
    Desktop,
    Installed,
    StoreApp,
    GamePlatform,
}

public sealed record ProgramCandidate(string Name, string Path, ProgramSource Source)
{
    public string SourceLabel => Source switch
    {
        ProgramSource.StartMenu => "开始菜单",
        ProgramSource.Desktop => "桌面",
        ProgramSource.Installed => "已安装程序",
        ProgramSource.StoreApp => "Microsoft Store",
        _ => "游戏平台",
    };
}

/// <summary>Finds launchable desktop programs from the places Windows users expect.</summary>
public static class ProgramCatalog
{
    private static readonly object Gate = new();
    private static IReadOnlyList<ProgramCandidate>? _cache;
    private static Task? _warming;

    public static IReadOnlyList<ProgramCandidate> GetAll()
    {
        lock (Gate)
            if (_cache is not null) return _cache;
        var built = Build();
        lock (Gate) return _cache ??= built;
    }

    public static Task WarmAsync()
    {
        lock (Gate) return _warming ??= Task.Run(GetAll);
    }

    public static void Invalidate()
    {
        lock (Gate) { _cache = null; _warming = null; }
    }

    private static IReadOnlyList<ProgramCandidate> Build()
    {
        var result = new Dictionary<string, ProgramCandidate>(StringComparer.OrdinalIgnoreCase);
        AddShortcuts(result, Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), ProgramSource.StartMenu);
        AddShortcuts(result, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), ProgramSource.StartMenu);
        AddShortcuts(result, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ProgramSource.Desktop);
        AddShortcuts(result, Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), ProgramSource.Desktop);
        AddInstalled(result, Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall");
        AddInstalled(result, Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Uninstall");
        AddInstalled(result, Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
        AddStoreApps(result);
        AddSteamGames(result);
        return result.Values.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static void AddStoreApps(Dictionary<string, ProgramCandidate> result)
    {
        try
        {
            var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -Command \"Get-StartApps | ForEach-Object { $_.Name + '|' + $_.AppID }\"")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var process = Process.Start(psi)!;
            var lines = process.StandardOutput.ReadToEnd().Split('\n');
            process.WaitForExit(5_000);
            foreach (var line in lines)
            {
                var bits = line.Trim().Split('|', 2);
                if (bits.Length != 2 || !bits[1].Contains('!')) continue;
                var target = "shell:AppsFolder\\" + bits[1];
                result.TryAdd(target, new ProgramCandidate(bits[0], target, ProgramSource.StoreApp));
            }
        }
        catch { }
    }

    private static void AddSteamGames(Dictionary<string, ProgramCandidate> result)
    {
        var steam = Environment.GetEnvironmentVariable("ProgramFiles(x86)") is { Length: > 0 } pf ? Path.Combine(pf, "Steam") : null;
        if (steam is null || !Directory.Exists(steam)) return;
        try
        {
            var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.Combine(steam, "steamapps") };
            var folders = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(folders))
                foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(folders), "\\\"path\\\"\\s+\\\"([^\\\"]+)\\\""))
                    libraries.Add(Path.Combine(match.Groups[1].Value.Replace("\\\\", "\\"), "steamapps"));
            foreach (var library in libraries.Where(Directory.Exists))
            foreach (var manifest in Directory.EnumerateFiles(library, "appmanifest_*.acf", SearchOption.TopDirectoryOnly))
            {
                var text = File.ReadAllText(manifest);
                var id = System.Text.RegularExpressions.Regex.Match(text, "\\\"appid\\\"\\s+\\\"(\\d+)\\\"").Groups[1].Value;
                var name = System.Text.RegularExpressions.Regex.Match(text, "\\\"name\\\"\\s+\\\"([^\\\"]+)\\\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) continue;
                var target = "steam://rungameid/" + id;
                result.TryAdd(target, new ProgramCandidate(name, target, ProgramSource.GamePlatform));
            }
        }
        catch { }
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
