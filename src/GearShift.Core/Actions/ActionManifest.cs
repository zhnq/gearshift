namespace GearShift.Core.Actions;

/// <summary>How to run one phase (apply/read) of an action — a command plus optional script and args.</summary>
public sealed record ActionCommand
{
    /// <summary>Executable to run. <c>powershell</c> is special-cased to run <see cref="Script"/> as a file.</summary>
    public string Run { get; init; } = "powershell";

    /// <summary>Script file name, relative to the action's own directory.</summary>
    public string? Script { get; init; }

    /// <summary>Argument template; <c>{key}</c> placeholders are filled from the invocation params.</summary>
    public string? Args { get; init; }
}

/// <summary>A parameter an action accepts.</summary>
public sealed record ActionParam
{
    public required string Key { get; init; }
    public string Type { get; init; } = "string";           // string | enum
    public IReadOnlyList<string> Values { get; init; } = []; // for enum
    public string? Default { get; init; }
}

/// <summary>
/// The <c>action.json</c> that describes a plugin action. Actions with a <see cref="Read"/> phase can
/// participate in state diffing; apply-only actions simply fire on entry.
/// </summary>
public sealed record ActionManifest
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public bool Experimental { get; init; }
    public IReadOnlyList<ActionParam> Params { get; init; } = [];
    public ActionCommand? Apply { get; init; }
    public ActionCommand? Read { get; init; }
}
