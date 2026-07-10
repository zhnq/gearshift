using System.Text.Json;
using GearShift.Core.Engine;

namespace GearShift.App.Services;

public sealed record SceneRunRecord(
    DateTimeOffset StartedAt,
    string SceneId,
    string SceneName,
    IReadOnlyList<StepOutcome> Outcomes);

/// <summary>Small, bounded local history used for retry diagnostics and performance tuning.</summary>
public static class SceneRunHistory
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GearShift", "run-history.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static IReadOnlyList<SceneRunRecord> Load()
    {
        try { return JsonSerializer.Deserialize<List<SceneRunRecord>>(File.ReadAllText(FilePath), Options) ?? []; }
        catch { return []; }
    }

    public static void Append(SceneRunRecord record)
    {
        var items = Load().Prepend(record).Take(100).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(items, Options));
    }
}
