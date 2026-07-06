using System.Text.Json.Serialization;

namespace GearShift.Core.Models;

/// <summary>
/// A reference to a program inside a scene, together with the state the scene wants it in.
/// Matching is done by executable name (<see cref="Match"/>); launching uses <see cref="Path"/>.
/// </summary>
public sealed record AppRef
{
    /// <summary>Executable name used to detect/close the program, e.g. <c>steam.exe</c>. Case-insensitive.</summary>
    public required string Match { get; init; }

    /// <summary>Whether the scene wants this program running or closed.</summary>
    public AppDisposition Disposition { get; init; }

    /// <summary>Full path to the executable, required for <see cref="AppDisposition.EnsureRunning"/>.</summary>
    public string? Path { get; init; }

    /// <summary>Optional launch arguments.</summary>
    public string? Args { get; init; }

    /// <summary>Optional working directory for launch.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Friendly name shown in the UI. Falls back to <see cref="Match"/> when null.</summary>
    public string? DisplayName { get; init; }

    [JsonIgnore]
    public string Label => DisplayName ?? Match;
}
