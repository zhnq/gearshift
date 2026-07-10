namespace GearShift.App.Services;

/// <summary>Small cross-instance handoff used by desktop scene shortcuts.</summary>
public static class SceneActivationRequest
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GearShift", "activate-scene.txt");

    public static void Write(string id)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, id);
    }

    public static string? Consume()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var id = File.ReadAllText(FilePath).Trim();
            File.Delete(FilePath);
            return string.IsNullOrEmpty(id) ? null : id;
        }
        catch { return null; }
    }
}
