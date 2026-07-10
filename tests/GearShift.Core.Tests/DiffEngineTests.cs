using GearShift.Core.Engine;
using GearShift.Core.Models;
using GearShift.Core.Safety;
using Xunit;

namespace GearShift.Core.Tests;

public class DiffEngineTests
{
    private sealed class FakeProbe : ISystemProbe
    {
        public HashSet<string> Running { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public bool? ProxyEnabled { get; init; }
        public string? ActivePowerPlan { get; init; }
        public IReadOnlySet<string> RunningProcessNames() => Running;
    }

    private static DiffEngine Engine() => new(new SafetyList());

    private static Scene GameScene(TriState proxy = TriState.Off, string? power = null) => new()
    {
        Id = "game",
        Name = "游戏模式",
        Proxy = proxy,
        PowerPlan = power,
        Apps =
        [
            new AppRef { Match = "steam.exe", Disposition = AppDisposition.EnsureRunning, Path = @"C:\steam.exe" },
            new AppRef { Match = "ts3.exe",   Disposition = AppDisposition.EnsureRunning, Path = @"C:\ts3.exe" },
            new AppRef { Match = "outlook.exe", Disposition = AppDisposition.EnsureClosed },
            new AppRef { Match = "wxwork.exe",  Disposition = AppDisposition.EnsureClosed },
        ],
    };

    [Fact]
    public void Starts_missing_programs_and_closes_present_ones()
    {
        // outlook is running (should be closed); steam is running (already ok); ts3 missing (start).
        var probe = new FakeProbe { Running = { "outlook.exe", "steam.exe" }, ProxyEnabled = false };

        var plan = Engine().BuildPlan(GameScene(), probe);

        Assert.Contains(plan, s => s.Kind == StepKind.StartProcess && s.App!.Match == "ts3.exe");
        Assert.Contains(plan, s => s.Kind == StepKind.CloseProcess && s.App!.Match == "outlook.exe");
        // steam already running -> no start step
        Assert.DoesNotContain(plan, s => s.Kind == StepKind.StartProcess && s.App!.Match == "steam.exe");
        // wxwork not running -> no close step
        Assert.DoesNotContain(plan, s => s.Kind == StepKind.CloseProcess && s.App!.Match == "wxwork.exe");
    }

    [Fact]
    public void Never_closes_a_protected_process()
    {
        var scene = new Scene
        {
            Id = "x", Name = "x",
            Apps = [new AppRef { Match = "explorer.exe", Disposition = AppDisposition.EnsureClosed }],
        };
        var probe = new FakeProbe { Running = { "explorer.exe" } };

        var plan = Engine().BuildPlan(scene, probe);

        Assert.Empty(plan);
    }

    [Fact]
    public void Proxy_step_emitted_only_when_state_differs()
    {
        var proxyAlreadyOff = new FakeProbe { Running = { "steam.exe", "ts3.exe" }, ProxyEnabled = false };
        var proxyCurrentlyOn = new FakeProbe { Running = { "steam.exe", "ts3.exe" }, ProxyEnabled = true };

        var planNoChange = Engine().BuildPlan(GameScene(proxy: TriState.Off), proxyAlreadyOff);
        var planChange = Engine().BuildPlan(GameScene(proxy: TriState.Off), proxyCurrentlyOn);

        Assert.DoesNotContain(planNoChange, s => s.Kind == StepKind.SetProxy);
        Assert.Contains(planChange, s => s.Kind == StepKind.SetProxy && !s.ProxyOn);
    }

    [Fact]
    public void Unchanged_proxy_is_never_touched()
    {
        var probe = new FakeProbe { Running = { "steam.exe", "ts3.exe" }, ProxyEnabled = true };

        var plan = Engine().BuildPlan(GameScene(proxy: TriState.Unchanged), probe);

        Assert.DoesNotContain(plan, s => s.Kind == StepKind.SetProxy);
    }

    [Fact]
    public void Applying_a_scene_whose_target_is_already_met_is_a_no_op()
    {
        // All EnsureRunning are running, no EnsureClosed running, proxy already off.
        var probe = new FakeProbe { Running = { "steam.exe", "ts3.exe" }, ProxyEnabled = false };

        var plan = Engine().BuildPlan(GameScene(), probe);

        Assert.Empty(plan);
    }

    [Fact]
    public void Power_plan_step_emitted_only_when_different()
    {
        var same = new FakeProbe { Running = { "steam.exe", "ts3.exe" }, ProxyEnabled = false, ActivePowerPlan = "HIGH" };
        var diff = new FakeProbe { Running = { "steam.exe", "ts3.exe" }, ProxyEnabled = false, ActivePowerPlan = "BALANCED" };

        Assert.DoesNotContain(Engine().BuildPlan(GameScene(power: "HIGH"), same), s => s.Kind == StepKind.SetPowerPlan);
        Assert.Contains(Engine().BuildPlan(GameScene(power: "HIGH"), diff), s => s.Kind == StepKind.SetPowerPlan);
    }

    [Fact]
    public void Adds_display_audio_and_window_steps_when_scene_configures_them()
    {
        var scene = new Scene
        {
            Id = "x", Name = "x", DisplayMode = "extend", AudioDeviceId = "endpoint",
            WindowLayouts = [new WindowLayout { Match = "app.exe", Width = 800, Height = 600 }],
        };
        var plan = Engine().BuildPlan(scene, new FakeProbe());
        Assert.Contains(plan, x => x.Kind == StepKind.SetDisplayMode);
        Assert.Contains(plan, x => x.Kind == StepKind.SetAudioDevice);
        Assert.Contains(plan, x => x.Kind == StepKind.RestoreWindowLayout);
    }
}
