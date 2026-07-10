using System.Diagnostics;
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
    private readonly IWindowLayoutController _windows;
    private readonly IDisplayManager _display;
    private readonly IAudioDeviceManager _audio;

    public SceneSwitcher(
        DiffEngine engine,
        Func<ISystemProbe> probeFactory,
        IProcessController processes,
        ISystemProxy proxy,
        IPowerPlanManager power,
        IActionRunner actions,
        IWindowLayoutController? windows = null,
        IDisplayManager? display = null,
        IAudioDeviceManager? audio = null)
    {
        _engine = engine;
        _probeFactory = probeFactory;
        _processes = processes;
        _proxy = proxy;
        _power = power;
        _actions = actions;
        _windows = windows ?? new NullWindowLayoutController();
        _display = display ?? new NullDisplayManager();
        _audio = audio ?? new NullAudioDeviceManager();
    }

    public async Task<SwitchResult> SwitchAsync(Scene scene, IProgress<StepOutcome>? progress = null, CancellationToken ct = default)
    {
        var plan = _engine.BuildPlan(scene, _probeFactory());
        var outcomes = new List<StepOutcome>(plan.Count);

        var processSteps = plan.Where(s => s.Kind is StepKind.StartProcess or StepKind.CloseProcess).ToList();
        var otherSteps = plan.Except(processSteps).ToList();
        using var limiter = new SemaphoreSlim(3);
        var processOutcomes = await Task.WhenAll(processSteps.Select(async step =>
        {
            await limiter.WaitAsync(ct);
            try { return await ExecuteAsync(step, ct); }
            finally { limiter.Release(); }
        }));
        outcomes.AddRange(processOutcomes);
        foreach (var outcome in processOutcomes) progress?.Report(outcome);

        foreach (var step in otherSteps)
        {
            ct.ThrowIfCancellationRequested();
            var outcome = await ExecuteAsync(step, ct);
            outcomes.Add(outcome);
            progress?.Report(outcome);
        }

        return new SwitchResult(scene, outcomes);
    }

    private async Task<StepOutcome> ExecuteAsync(SwitchStep step, CancellationToken ct)
    {
        try
        {
            var started = Stopwatch.GetTimestamp();
            StepOutcome Timed(StepOutcome outcome) => outcome with { Duration = Stopwatch.GetElapsedTime(started) };
            switch (step.Kind)
            {
                case StepKind.StartProcess:
                    _processes.Start(step.App!);
                    return Timed(Ok(step, $"已启动 {step.Target}"));

                case StepKind.CloseProcess:
                    return Timed((await Task.Run(() => _processes.Close(step.App!.Match), ct).WaitAsync(TimeSpan.FromSeconds(8), ct)) switch
                    {
                        CloseOutcome.ClosedGracefully => Ok(step, $"已关闭 {step.Target}"),
                        CloseOutcome.ForceKilled => Warn(step, $"{step.Target} 未响应，已强制关闭"),
                        CloseOutcome.NotRunning => new StepOutcome(step, StepStatus.Skipped, $"{step.Target} 已不在运行"),
                        _ => Fail(step, $"无法关闭 {step.Target}"),
                    });

                case StepKind.SetProxy:
                    _proxy.SetEnabled(step.ProxyOn);
                    return Timed(Ok(step, step.ProxyOn ? "系统代理已开启" : "系统代理已关闭"));

                case StepKind.SetPowerPlan:
                    _power.SetActive(step.PowerPlan!);
                    return Timed(Ok(step, $"电源计划已切换为 {step.Target}"));

                case StepKind.SetDisplayMode:
                    _display.SetMode(step.Value!);
                    return Timed(Ok(step, $"显示器已切换为 {step.Target}"));

                case StepKind.SetAudioDevice:
                    _audio.SetDefaultPlayback(step.Value!);
                    return Timed(Ok(step, "默认播放设备已切换"));

                case StepKind.RestoreWindowLayout:
                    _windows.Restore(step.WindowLayouts ?? []);
                    return Timed(Ok(step, $"已恢复 {step.Target}"));

                case StepKind.RunAction:
                    return Timed(await _actions.RunAsync(step, ct));

                default:
                    return Timed(Fail(step, "未知步骤"));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Fail(step, ex is TimeoutException ? $"{step.Target} 执行超时" : ex.Message);
        }
    }

    private static StepOutcome Ok(SwitchStep s, string m) => new(s, StepStatus.Ok, m);
    private static StepOutcome Warn(SwitchStep s, string m) => new(s, StepStatus.Warning, m);
    private static StepOutcome Fail(SwitchStep s, string m) => new(s, StepStatus.Failed, m);
}

internal sealed class NullWindowLayoutController : IWindowLayoutController
{
    public void Restore(IReadOnlyList<WindowLayout> layouts) { }
}
internal sealed class NullDisplayManager : IDisplayManager { public void SetMode(string mode) { } }
internal sealed class NullAudioDeviceManager : IAudioDeviceManager { public void SetDefaultPlayback(string endpointId) { } }
