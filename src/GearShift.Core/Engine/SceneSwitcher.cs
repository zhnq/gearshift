using GearShift.Core.Models;

namespace GearShift.Core.Engine;

/// <summary>
/// Orchestrates a scene switch: take a fresh system snapshot, ask the <see cref="DiffEngine"/> for the
/// plan, then run each step through the injected executors, collecting a per-step outcome. Every step
/// is wrapped so a single failure (e.g. a program that won't die) degrades to a warning instead of
/// aborting the whole switch.
/// </summary>
public sealed class SceneSwitcher
{
    private readonly DiffEngine _engine;
    private readonly Func<ISystemProbe> _probeFactory;
    private readonly IProcessController _processes;
    private readonly ISystemProxy _proxy;
    private readonly IPowerPlanManager _power;
    private readonly IActionRunner _actions;

    public SceneSwitcher(
        DiffEngine engine,
        Func<ISystemProbe> probeFactory,
        IProcessController processes,
        ISystemProxy proxy,
        IPowerPlanManager power,
        IActionRunner actions)
    {
        _engine = engine;
        _probeFactory = probeFactory;
        _processes = processes;
        _proxy = proxy;
        _power = power;
        _actions = actions;
    }

    public async Task<SwitchResult> SwitchAsync(Scene scene, CancellationToken ct = default)
    {
        var plan = _engine.BuildPlan(scene, _probeFactory());
        var outcomes = new List<StepOutcome>(plan.Count);

        foreach (var step in plan)
        {
            ct.ThrowIfCancellationRequested();
            outcomes.Add(await ExecuteAsync(step, ct));
        }

        return new SwitchResult(scene, outcomes);
    }

    private async Task<StepOutcome> ExecuteAsync(SwitchStep step, CancellationToken ct)
    {
        try
        {
            switch (step.Kind)
            {
                case StepKind.StartProcess:
                    _processes.Start(step.App!);
                    return Ok(step, $"已启动 {step.Target}");

                case StepKind.CloseProcess:
                    return _processes.Close(step.App!.Match) switch
                    {
                        CloseOutcome.ClosedGracefully => Ok(step, $"已关闭 {step.Target}"),
                        CloseOutcome.ForceKilled => Warn(step, $"{step.Target} 未响应，已强制关闭"),
                        CloseOutcome.NotRunning => new StepOutcome(step, StepStatus.Skipped, $"{step.Target} 已不在运行"),
                        _ => Fail(step, $"无法关闭 {step.Target}"),
                    };

                case StepKind.SuspendProcess:
                    return _processes.Suspend(step.App!.Match)
                        ? Ok(step, $"已冻结 {step.Target}")
                        : new StepOutcome(step, StepStatus.Skipped, $"{step.Target} 已不在运行");

                case StepKind.ResumeProcess:
                    return _processes.Resume(step.App!.Match)
                        ? Ok(step, $"已解冻 {step.Target}")
                        : new StepOutcome(step, StepStatus.Skipped, $"{step.Target} 已不在运行");

                case StepKind.SetProxy:
                    _proxy.SetEnabled(step.ProxyOn);
                    return Ok(step, step.ProxyOn ? "系统代理已开启" : "系统代理已关闭");

                case StepKind.SetPowerPlan:
                    _power.SetActive(step.PowerPlan!);
                    return Ok(step, $"电源计划已切换为 {step.Target}");

                case StepKind.RunAction:
                    return await _actions.RunAsync(step, ct);

                default:
                    return Fail(step, "未知步骤");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Fail(step, ex.Message);
        }
    }

    private static StepOutcome Ok(SwitchStep s, string m) => new(s, StepStatus.Ok, m);
    private static StepOutcome Warn(SwitchStep s, string m) => new(s, StepStatus.Warning, m);
    private static StepOutcome Fail(SwitchStep s, string m) => new(s, StepStatus.Failed, m);
}
