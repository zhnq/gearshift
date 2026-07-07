using GearShift.Core.Engine;
using GearShift.Core.Models;
using GearShift.Core.Safety;
using Xunit;

namespace GearShift.Core.Tests;

public class SceneSwitcherTests
{
    private sealed class FakeProbe : ISystemProbe
    {
        public HashSet<string> Running { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Suspended { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public bool? ProxyEnabled { get; init; }
        public string? ActivePowerPlan { get; init; }
        public IReadOnlySet<string> RunningProcessNames() => Running;
        public IReadOnlySet<string> SuspendedProcessNames() => Suspended;
    }

    private sealed class FakeProcesses : IProcessController
    {
        public List<string> Started { get; } = [];
        public List<string> Closed { get; } = [];
        public List<string> Suspended { get; } = [];
        public List<string> Resumed { get; } = [];
        public CloseOutcome CloseResult { get; init; } = CloseOutcome.ClosedGracefully;
        public bool ProcessPresent { get; init; } = true;
        public void Start(AppRef app) => Started.Add(app.Match);
        public CloseOutcome Close(string match) { Closed.Add(match); return CloseResult; }
        public bool Suspend(string match) { Suspended.Add(match); return ProcessPresent; }
        public bool Resume(string match) { Resumed.Add(match); return ProcessPresent; }
    }

    private sealed class FakeProxy : ISystemProxy
    {
        public bool? Value;
        public bool? IsEnabled() => Value;
        public void SetEnabled(bool on) => Value = on;
    }

    private sealed class FakePower : IPowerPlanManager
    {
        public string? Current;
        public string? Active() => Current;
        public void SetActive(string key) => Current = key;
    }

    [Fact]
    public async Task Switch_starts_closes_and_sets_proxy_reporting_each_outcome()
    {
        var probe = new FakeProbe { Running = { "outlook.exe" }, ProxyEnabled = true };
        var procs = new FakeProcesses();
        var proxy = new FakeProxy { Value = true };

        var switcher = new SceneSwitcher(
            new DiffEngine(new SafetyList()),
            () => probe, procs, proxy, new FakePower(), new NoopActions());

        var scene = new Scene
        {
            Id = "game", Name = "游戏模式", Proxy = TriState.Off,
            Apps =
            [
                new AppRef { Match = "steam.exe", Disposition = AppDisposition.EnsureRunning, Path = @"C:\steam.exe" },
                new AppRef { Match = "outlook.exe", Disposition = AppDisposition.EnsureClosed },
            ],
        };

        var result = await switcher.SwitchAsync(scene);

        Assert.Contains("steam.exe", procs.Started);
        Assert.Contains("outlook.exe", procs.Closed);
        Assert.False(proxy.Value);
        Assert.False(result.HadTrouble);
        Assert.Equal(3, result.Outcomes.Count);
    }

    [Fact]
    public async Task Force_killed_program_is_reported_as_warning()
    {
        var probe = new FakeProbe { Running = { "wxwork.exe" } };
        var procs = new FakeProcesses { CloseResult = CloseOutcome.ForceKilled };

        var switcher = new SceneSwitcher(
            new DiffEngine(new SafetyList()),
            () => probe, procs, new FakeProxy(), new FakePower(), new NoopActions());

        var scene = new Scene
        {
            Id = "x", Name = "x",
            Apps = [new AppRef { Match = "wxwork.exe", Disposition = AppDisposition.EnsureClosed }],
        };

        var result = await switcher.SwitchAsync(scene);

        Assert.True(result.HadTrouble);
        Assert.Equal(1, result.WarningCount);
    }

    [Fact]
    public async Task Switch_freezes_a_running_program()
    {
        var probe = new FakeProbe { Running = { "chrome.exe" } };
        var procs = new FakeProcesses();

        var switcher = new SceneSwitcher(
            new DiffEngine(new SafetyList()),
            () => probe, procs, new FakeProxy(), new FakePower(), new NoopActions());

        var scene = new Scene
        {
            Id = "x", Name = "x",
            Apps = [new AppRef { Match = "chrome.exe", Disposition = AppDisposition.EnsureSuspended }],
        };

        var result = await switcher.SwitchAsync(scene);

        Assert.Contains("chrome.exe", procs.Suspended);
        Assert.False(result.HadTrouble);
    }

    [Fact]
    public async Task Switch_thaws_a_frozen_program_for_ensure_running()
    {
        var probe = new FakeProbe { Running = { "chrome.exe" }, Suspended = { "chrome.exe" } };
        var procs = new FakeProcesses();

        var switcher = new SceneSwitcher(
            new DiffEngine(new SafetyList()),
            () => probe, procs, new FakeProxy(), new FakePower(), new NoopActions());

        var scene = new Scene
        {
            Id = "x", Name = "x",
            Apps = [new AppRef { Match = "chrome.exe", Disposition = AppDisposition.EnsureRunning, Path = @"C:\chrome.exe" }],
        };

        var result = await switcher.SwitchAsync(scene);

        Assert.Contains("chrome.exe", procs.Resumed);
        Assert.Empty(procs.Started);
        Assert.False(result.HadTrouble);
    }

    private sealed class NoopActions : IActionRunner
    {
        public Task<StepOutcome> RunAsync(SwitchStep step, CancellationToken ct = default)
            => Task.FromResult(new StepOutcome(step, StepStatus.Ok, "ok"));
    }
}
