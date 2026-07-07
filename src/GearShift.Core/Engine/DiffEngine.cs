using GearShift.Core.Models;
using GearShift.Core.Safety;

namespace GearShift.Core.Engine;

/// <summary>
/// The heart of GearShift. Given a target <see cref="Scene"/> and a snapshot of the current system,
/// it computes the minimal set of <see cref="SwitchStep"/>s needed to reach the target — and nothing
/// more. Because it is a pure function of (scene, probe), it is fully deterministic and testable:
///   * a program already in the desired run-state produces no step (idempotent),
///   * an <c>EnsureClosed</c> targeting a protected process is dropped (safety),
///   * a global setting equal to its target is left alone,
///   * switching back to another scene is just another diff — no undo bookkeeping required.
/// </summary>
public sealed class DiffEngine
{
    private readonly SafetyList _safety;

    public DiffEngine(SafetyList safety) => _safety = safety;

    public IReadOnlyList<SwitchStep> BuildPlan(Scene target, ISystemProbe probe)
    {
        var running = probe.RunningProcessNames();
        var suspended = probe.SuspendedProcessNames();
        var steps = new List<SwitchStep>();

        foreach (var app in target.Apps)
        {
            var name = app.Match.Trim().ToLowerInvariant();
            var isRunning = running.Contains(name);
            var isSuspended = suspended.Contains(name);

            switch (app.Disposition)
            {
                case AppDisposition.EnsureRunning when !isRunning:
                    steps.Add(new SwitchStep
                    {
                        Kind = StepKind.StartProcess,
                        Target = app.Label,
                        Reason = "未运行，启动",
                        App = app,
                    });
                    break;

                // A frozen process counts as running, so a plain "ensure running" must thaw it to be usable.
                case AppDisposition.EnsureRunning when isSuspended:
                    steps.Add(new SwitchStep
                    {
                        Kind = StepKind.ResumeProcess,
                        Target = app.Label,
                        Reason = "已冻结，解冻",
                        App = app,
                    });
                    break;

                case AppDisposition.EnsureClosed when isRunning && !_safety.IsProtected(name):
                    steps.Add(new SwitchStep
                    {
                        Kind = StepKind.CloseProcess,
                        Target = app.Label,
                        Reason = "运行中，关闭",
                        App = app,
                    });
                    break;

                // Only freeze something that's actually running, not yet frozen, and not protected.
                case AppDisposition.EnsureSuspended when isRunning && !isSuspended && !_safety.IsProtected(name):
                    steps.Add(new SwitchStep
                    {
                        Kind = StepKind.SuspendProcess,
                        Target = app.Label,
                        Reason = "运行中，冻结",
                        App = app,
                    });
                    break;
            }
        }

        if (target.Proxy != TriState.Unchanged)
        {
            var want = target.Proxy == TriState.On;
            if (probe.ProxyEnabled != want)
            {
                steps.Add(new SwitchStep
                {
                    Kind = StepKind.SetProxy,
                    Target = want ? "开启" : "关闭",
                    Reason = "系统代理状态变更",
                    ProxyOn = want,
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(target.PowerPlan)
            && !string.Equals(target.PowerPlan, probe.ActivePowerPlan, StringComparison.OrdinalIgnoreCase))
        {
            steps.Add(new SwitchStep
            {
                Kind = StepKind.SetPowerPlan,
                Target = target.PowerPlan!,
                Reason = "电源计划变更",
                PowerPlan = target.PowerPlan,
            });
        }

        // Plugin actions always fire on entry; the runner decides whether an action with a
        // read step is actually a no-op. Keeping them here preserves scene order.
        foreach (var action in target.Actions)
        {
            steps.Add(new SwitchStep
            {
                Kind = StepKind.RunAction,
                Target = action.ActionId,
                Reason = "执行动作插件",
                Action = action,
            });
        }

        return steps;
    }
}
