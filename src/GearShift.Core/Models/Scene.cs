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

    /// <summary>Optional Windows display topology: internal, clone, extend, or external.</summary>
    public string? DisplayMode { get; init; }

    /// <summary>Optional Core Audio endpoint id to make the default playback device.</summary>
    public string? AudioDeviceId { get; init; }

    /// <summary>Plugin actions to invoke when entering this scene (from the action library).</summary>
    public IReadOnlyList<ActionInvocation> Actions { get; init; } = [];

    /// <summary>Optional rules that can activate this scene without opening the app.</summary>
    public IReadOnlyList<SceneTrigger> Triggers { get; init; } = [];

    /// <summary>Window rectangles to restore after the scene's programs are ready.</summary>
    public IReadOnlyList<WindowLayout> WindowLayouts { get; init; } = [];

    /// <summary>When set, return to the previous scene after all listed primary programs exit.</summary>
    public IReadOnlyList<string> RestoreWhenStopped { get; init; } = [];
}

public sealed record SceneTrigger
{
    public SceneTriggerKind Kind { get; init; }
    public string? Value { get; init; }
    public string? EndValue { get; init; }
    public bool Enabled { get; init; } = true;
    public int CooldownSeconds { get; init; } = 30;
}

public sealed record WindowLayout
{
    public required string Match { get; init; }
    public int Left { get; init; }
    public int Top { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public bool Maximized { get; init; }
}

/// <summary>A reference to a library action plus the parameters it is invoked with.</summary>
public sealed record ActionInvocation
{
    public required string ActionId { get; init; }

    public IReadOnlyDictionary<string, string> Params { get; init; }
        = new Dictionary<string, string>();
}
