using System.Diagnostics;
using System.Runtime.InteropServices;
using GearShift.Core.Models;

namespace GearShift.App.Services;

/// <summary>Low-frequency local monitor for scene triggers and temporary-scene restoration.</summary>
public sealed class SceneAutomationService : IDisposable
{
    private readonly Func<Scene, Task> _activate;
    private readonly CancellationTokenSource _stop = new();
    private readonly Dictionary<string, DateTimeOffset> _lastFired = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlySet<string> _previous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private string? _wifi;
    private DateTimeOffset _lastWifiCheck = DateTimeOffset.MinValue;
    private Task? _loop;
    private int _switching;

    public SceneAutomationService(Func<Scene, Task> activate) => _activate = activate;
    public void Start() => _loop ??= Task.Run(LoopAsync);

    private async Task LoopAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        while (await timer.WaitForNextTickAsync(_stop.Token))
        {
            try { await EvaluateAsync(_stop.Token); }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            catch { /* monitoring must never take down the tray app */ }
        }
    }

    private async Task EvaluateAsync(CancellationToken ct)
    {
        var requested = SceneActivationRequest.Consume();
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var requestedScene = AppServices.Scenes.FirstOrDefault(x => x.Id == requested);
            if (requestedScene is not null) await ActivateAsync(requestedScene);
        }
        var running = AppServices.Processes.RunningProcessNames();
        if (DateTimeOffset.Now - _lastWifiCheck > TimeSpan.FromSeconds(30))
        {
            _wifi = await ReadWifiAsync(ct);
            _lastWifiCheck = DateTimeOffset.Now;
        }

        await TryRestoreAsync(running);
        var foreground = ForegroundProcessName();
        foreach (var scene in AppServices.Scenes)
        {
            if (scene.Id == AppServices.ActiveSceneId) continue;
            foreach (var trigger in scene.Triggers.Where(t => t.Enabled))
            {
                if (!Matches(trigger, running, foreground)) continue;
                var key = scene.Id + ":" + trigger.Kind + ":" + trigger.Value;
                var cooldown = TimeSpan.FromSeconds(Math.Max(3, trigger.CooldownSeconds));
                if (_lastFired.TryGetValue(key, out var last) && DateTimeOffset.Now - last < cooldown) continue;
                _lastFired[key] = DateTimeOffset.Now;
                await ActivateAsync(scene);
                break;
            }
        }
        _previous = new HashSet<string>(running, StringComparer.OrdinalIgnoreCase);
    }

    private bool Matches(SceneTrigger trigger, IReadOnlySet<string> running, string? foreground)
    {
        var value = trigger.Value?.Trim().ToLowerInvariant();
        return trigger.Kind switch
        {
            SceneTriggerKind.ProcessStarted => !string.IsNullOrEmpty(value) && running.Contains(value) && !_previous.Contains(value),
            SceneTriggerKind.ProcessStopped => !string.IsNullOrEmpty(value) && !running.Contains(value) && _previous.Contains(value),
            SceneTriggerKind.ForegroundProcess => !string.IsNullOrEmpty(value) && string.Equals(value, foreground, StringComparison.OrdinalIgnoreCase),
            SceneTriggerKind.AcPower => PowerLineStatus() == 1,
            SceneTriggerKind.BatteryPower => PowerLineStatus() == 0,
            SceneTriggerKind.WifiSsid => !string.IsNullOrEmpty(value) && string.Equals(value, _wifi, StringComparison.OrdinalIgnoreCase),
            SceneTriggerKind.TimeRange => IsInTimeRange(trigger.Value, trigger.EndValue),
            SceneTriggerKind.ExternalMonitor => GetSystemMetrics(80) > 1,
            _ => false,
        };
    }

    private async Task TryRestoreAsync(IReadOnlySet<string> running)
    {
        var settings = SettingsService.Current;
        if (string.IsNullOrEmpty(settings.PendingRestoreSceneId) || settings.PendingRestoreApps.Count == 0) return;
        if (settings.PendingRestoreApps.Any(running.Contains))
        {
            settings.PendingRestoreObservedRunning = true;
            SettingsService.Save();
            return;
        }
        if (!settings.PendingRestoreObservedRunning) return;
        var target = AppServices.Scenes.FirstOrDefault(x => x.Id == settings.PendingRestoreSceneId);
        settings.PendingRestoreSceneId = null;
        settings.PendingRestoreApps = [];
        settings.PendingRestoreObservedRunning = false;
        SettingsService.Save();
        if (target is not null) await ActivateAsync(target);
    }

    private async Task ActivateAsync(Scene scene)
    {
        if (Interlocked.Exchange(ref _switching, 1) != 0) return;
        try { await _activate(scene); }
        finally { Volatile.Write(ref _switching, 0); }
    }

    private static bool IsInTimeRange(string? start, string? end)
    {
        if (!TimeOnly.TryParse(start, out var from) || !TimeOnly.TryParse(end, out var to)) return false;
        var now = TimeOnly.FromDateTime(DateTime.Now);
        return from <= to ? now >= from && now <= to : now >= from || now <= to;
    }

    private static async Task<string?> ReadWifiAsync(CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo("netsh", "wlan show interfaces") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var process = Process.Start(psi)!;
            var text = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            return text.Split('\n').Select(x => x.Trim()).FirstOrDefault(x => x.StartsWith("SSID", StringComparison.OrdinalIgnoreCase) && !x.StartsWith("SSID BSSID", StringComparison.OrdinalIgnoreCase))?.Split(':', 2).ElementAtOrDefault(1)?.Trim();
        }
        catch { return null; }
    }

    private static string? ForegroundProcessName()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero) return null;
        GetWindowThreadProcessId(window, out var pid);
        try { using var process = Process.GetProcessById((int)pid); return process.ProcessName.ToLowerInvariant() + ".exe"; }
        catch { return null; }
    }

    private static byte PowerLineStatus() { var status = new SYSTEM_POWER_STATUS(); return GetSystemPowerStatus(ref status) ? status.ACLineStatus : (byte)255; }
    public void Dispose() { _stop.Cancel(); _stop.Dispose(); }
    [StructLayout(LayoutKind.Sequential)] private struct SYSTEM_POWER_STATUS { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public int BatteryLifeTime, BatteryFullLifeTime; }
    [DllImport("kernel32.dll")] private static extern bool GetSystemPowerStatus(ref SYSTEM_POWER_STATUS status);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
}
