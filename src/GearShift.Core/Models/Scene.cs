namespace GearShift.Core.Models;

/// <summary>
/// A scene is a declarative description of the desired system state. Applying a scene is a diff:
/// the engine compares the current state to this target and performs only the missing changes.
/// Anything not mentioned here is deliberately left untouched.
/// </summary>
public sealed record Scene
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Emoji or icon key shown on the scene card.</summary>
    public string Icon { get; init; } = "";

    /// <summary>Programs this scene wants running or closed.</summary>
    public IReadOnlyList<AppRef> Apps { get; init; } = [];

    /// <summary>Desired system-proxy state. <see cref="TriState.Unchanged"/> leaves it alone.</summary>
    public TriState Proxy { get; init; } = TriState.Unchanged;

    /// <summary>Desired power plan (GUID or well-known key). <c>null</c> leaves it alone.</summary>
    public string? PowerPlan { get; init; }

    /// <summary>Plugin actions to invoke when entering this scene (from the action library).</summary>
    public IReadOnlyList<ActionInvocation> Actions { get; init; } = [];
}

/// <summary>A reference to a library action plus the parameters it is invoked with.</summary>
public sealed record ActionInvocation
{
    public required string ActionId { get; init; }

    public IReadOnlyDictionary<string, string> Params { get; init; }
        = new Dictionary<string, string>();
}
