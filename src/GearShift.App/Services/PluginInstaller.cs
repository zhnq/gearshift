using System.IO.Compression;
using System.Text.Json;
using GearShift.Core.Actions;

namespace GearShift.App.Services;

/// <summary>
/// Installs an action plugin from a <c>.zip</c> package (containing <c>action.json</c> plus its
/// scripts). Inspection is separated from installation so the UI can show a trust prompt with the
/// script contents before anything is written to disk.
/// </summary>
public static class PluginInstaller
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public sealed class Candidate
    {
        public required string ZipPath { get; init; }
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string Description { get; init; }
        public required string ScriptText { get; init; }
        public required string BasePrefix { get; init; }
    }

    /// <summary>Reads a package's manifest and apply-script text without extracting anything.</summary>
    public static Candidate? Inspect(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);

        var manifestEntry = zip.Entries.FirstOrDefault(
            e => e.Name.Equals("action.json", StringComparison.OrdinalIgnoreCase));
        if (manifestEntry is null)
            return null;

        var basePrefix = manifestEntry.FullName[..^manifestEntry.Name.Length];

        ActionManifest? manifest;
        using (var reader = new StreamReader(manifestEntry.Open()))
            manifest = JsonSerializer.Deserialize<ActionManifest>(reader.ReadToEnd(), Options);

        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id))
            return null;

        var scriptText = string.Empty;
        if (manifest.Apply?.Script is { } scriptName)
        {
            var scriptEntry = zip.GetEntry(basePrefix + scriptName);
            if (scriptEntry is not null)
            {
                using var sr = new StreamReader(scriptEntry.Open());
                scriptText = sr.ReadToEnd();
            }
        }

        return new Candidate
        {
            ZipPath = zipPath,
            Id = manifest.Id,
            Name = manifest.Name,
            Description = manifest.Description,
            ScriptText = scriptText,
            BasePrefix = basePrefix,
        };
    }

    /// <summary>Extracts the package into <c>&lt;actionsRoot&gt;/&lt;id&gt;/</c>, overwriting an existing copy.</summary>
    public static void Install(Candidate candidate, string actionsRoot)
    {
        var target = Path.Combine(actionsRoot, candidate.Id);
        Directory.CreateDirectory(target);

        using var zip = ZipFile.OpenRead(candidate.ZipPath);
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
                continue; // directory marker
            if (!entry.FullName.StartsWith(candidate.BasePrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var relative = entry.FullName[candidate.BasePrefix.Length..];
            if (string.IsNullOrEmpty(relative))
                continue;

            var destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
        }
    }
}
