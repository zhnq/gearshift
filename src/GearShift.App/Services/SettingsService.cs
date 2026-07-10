using System.Text.Json;

namespace GearShift.App.Services;

/// <summary>User preferences that aren't scene data. Persisted next to scenes.json.</summary>
public sealed class AppSettings
{
    public string Theme { get; set; } = "Default";      // Default | Light | Dark
    public bool StartMinimized { get; set; } = false;
    public bool NotifyOnSwitch { get; set; } = true;
    public bool ConfirmPluginScripts { get; set; } = true;
    public bool AutoCheckUpdates { get; set; } = true;
    public bool EnableAutomation { get; set; } = true;
    public bool EnableHotkeys { get; set; } = true;
    public string? DefaultSceneId { get; set; }
    public string? PendingElevatedSceneId { get; set; }
    public string? PendingRestoreSceneId { get; set; }
    public List<string> PendingRestoreApps { get; set; } = [];
    public bool PendingRestoreObservedRunning { get; set; }

    /// <summary>Action ids the user has disabled in the library. Disabled actions are never run.</summary>
    public List<string> DisabledActions { get; set; } = [];
}

/// <summary>Enabled/disabled state for library actions, backed by <see cref="AppSettings"/>.</summary>
public static class ActionState
{
    public static bool IsEnabled(string id) => !SettingsService.Current.DisabledActions.Contains(id);

    public static void SetEnabled(string id, bool enabled)
    {
        var disabled = SettingsService.Current.DisabledActions;
        var changed = enabled ? disabled.Remove(id) : (!disabled.Contains(id) && AddDisabled(disabled, id));
        if (changed) SettingsService.Save();
    }

    private static bool AddDisabled(List<string> list, string id)
    {
        list.Add(id);
        return true;
    }
}

/// <summary>Loads/saves <see cref="AppSettings"/> as JSON under %AppData%\GearShift\settings.json.</summary>
public static class SettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GearShift", "settings.json");

    public static AppSettings Current { get; private set; } = Load();

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(Path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path), Options) ?? new AppSettings();
        }
        catch
        {
            // Corrupt settings shouldn't stop the app — fall back to defaults.
        }
        return new AppSettings();
    }

    public static void Save()
    {
        var dir = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(Path, JsonSerializer.Serialize(Current, Options));
    }
}
