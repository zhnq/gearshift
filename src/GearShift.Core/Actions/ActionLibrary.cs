using System.Text.Json;

namespace GearShift.Core.Actions;

/// <summary>A manifest paired with the directory it was loaded from (so scripts resolve relatively).</summary>
public sealed record ResolvedAction(ActionManifest Manifest, string Directory);

/// <summary>
/// Discovers plugin actions by scanning <c>&lt;root&gt;/*/action.json</c>. A bad manifest is skipped
/// rather than failing the whole library, so one broken plugin can't take the others down.
/// </summary>
public sealed class ActionLibrary
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private readonly Dictionary<string, ResolvedAction> _byId = new(StringComparer.OrdinalIgnoreCase);

    public string Root { get; }

    public ActionLibrary(string root)
    {
        Root = root;
        Scan();
    }

    public void Reload() => Scan();

    public ResolvedAction? Resolve(string id)
        => _byId.TryGetValue(id, out var resolved) ? resolved : null;

    public IReadOnlyList<ActionManifest> All => _byId.Values.Select(v => v.Manifest).ToList();

    private void Scan()
    {
        _byId.Clear();
        if (!Directory.Exists(Root))
            return;

        foreach (var dir in Directory.GetDirectories(Root))
        {
            var file = Path.Combine(dir, "action.json");
            if (!File.Exists(file))
                continue;

            try
            {
                var manifest = JsonSerializer.Deserialize<ActionManifest>(File.ReadAllText(file), Options);
                if (manifest is not null && !string.IsNullOrWhiteSpace(manifest.Id))
                    _byId[manifest.Id] = new ResolvedAction(manifest, dir);
            }
            catch
            {
                // Skip a malformed manifest; the rest of the library still loads.
            }
        }
    }
}
