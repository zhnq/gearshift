using System.Text.Json;
using System.Text.Json.Serialization;
using GearShift.Core.Models;

namespace GearShift.Core.Storage;

/// <summary>The persisted document: all scenes plus which one is currently active.</summary>
public sealed record SceneDocument
{
    public IReadOnlyList<Scene> Scenes { get; init; } = [];
    public string? ActiveSceneId { get; init; }
    public IReadOnlyList<string> ExtraProtectedProcesses { get; init; } = [];
}

/// <summary>
/// Loads and saves <see cref="SceneDocument"/> as JSON under <c>%AppData%\GearShift\scenes.json</c>.
/// A missing file yields an empty document rather than throwing, so first run just works.
/// </summary>
public sealed class SceneStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string FilePath { get; }

    public SceneStore(string? filePath = null)
        => FilePath = filePath ?? DefaultPath();

    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GearShift",
        "scenes.json");

    public SceneDocument Load()
    {
        if (!File.Exists(FilePath))
            return new SceneDocument();

        var json = File.ReadAllText(FilePath);
        return JsonSerializer.Deserialize<SceneDocument>(json, Options) ?? new SceneDocument();
    }

    public void Save(SceneDocument document)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(document, Options);
        File.WriteAllText(FilePath, json);
    }
}
