using GearShift.Core.Engine;

namespace GearShift.Core.Actions;

/// <summary>
/// Placeholder action runner used until the plugin/action-library subsystem is wired in. It reports
/// plugin-action steps as skipped rather than failing a switch.
/// </summary>
public sealed class NullActionRunner : IActionRunner
{
    public Task<StepOutcome> RunAsync(SwitchStep step, CancellationToken ct = default)
        => Task.FromResult(new StepOutcome(step, StepStatus.Skipped, "动作插件将在动作库接入后执行"));
}
