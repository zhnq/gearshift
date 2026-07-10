using GearShift.Core.Models;

namespace GearShift.Core.Engine;

public enum StepKind
{
    StartProcess,
    CloseProcess,
    SetProxy,
    SetPowerPlan,
    SetDisplayMode,
    SetAudioDevice,
    RestoreWindowLayout,
    RunAction,
}

/// <summary>
/// One concrete change the engine decided is needed to reach the target scene. Steps are pure data
/// produced by <see cref="DiffEngine"/>; a separate executor performs the side effects. This split
/// is what makes the whole switching logic testable without touching the real system.
/// </summary>
public sealed record SwitchStep
{
    public required StepKind Kind { get; init; }

    /// <summary>Human-readable subject of the step (program label, "开启", power-plan name…).</summary>
    public required string Target { get; init; }

    /// <summary>Why this step was scheduled — surfaced in the switch-result UI.</summary>
    public required string Reason { get; init; }

    /// <summary>Set for <see cref="StepKind.StartProcess"/> / <see cref="StepKind.CloseProcess"/>.</summary>
    public AppRef? App { get; init; }

    /// <summary>Set for <see cref="StepKind.RunAction"/>.</summary>
    public ActionInvocation? Action { get; init; }

    /// <summary>Desired proxy state for <see cref="StepKind.SetProxy"/>.</summary>
    public bool ProxyOn { get; init; }

    /// <summary>Desired power-plan key for <see cref="StepKind.SetPowerPlan"/>.</summary>
    public string? PowerPlan { get; init; }

    public string? Value { get; init; }

    public IReadOnlyList<WindowLayout>? WindowLayouts { get; init; }
}
