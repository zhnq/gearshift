using GearShift.Core.Models;

namespace GearShift.Core.Engine;

public enum StepStatus
{
    Ok,
    Warning,
    Failed,
    Skipped,
}

/// <summary>Outcome of executing one <see cref="SwitchStep"/> — feeds the switch-result UI.</summary>
public sealed record StepOutcome(SwitchStep Step, StepStatus Status, string Message);

/// <summary>Aggregate result of applying a scene.</summary>
public sealed record SwitchResult(Scene Scene, IReadOnlyList<StepOutcome> Outcomes)
{
    public int OkCount => Outcomes.Count(o => o.Status == StepStatus.Ok);
    public int WarningCount => Outcomes.Count(o => o.Status is StepStatus.Warning);
    public int FailedCount => Outcomes.Count(o => o.Status == StepStatus.Failed);
    public bool HadTrouble => Outcomes.Any(o => o.Status is StepStatus.Warning or StepStatus.Failed);

    /// <summary>A scene whose target was already met produces no steps.</summary>
    public bool WasNoOp => Outcomes.Count == 0;
}
