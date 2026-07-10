using GearShift.Core.Models;

namespace GearShift.Core.Engine;

/// <summary>Result of trying to close a program.</summary>
public enum CloseOutcome
{
    NotRunning,
    ClosedGracefully,
    ForceKilled,
    Failed,
}

/// <summary>Starts and stops programs. The only component allowed to terminate processes.</summary>
public interface IProcessController
{
    void Start(AppRef app);
    CloseOutcome Close(string match);
}

public interface IWindowLayoutController
{
    void Restore(IReadOnlyList<WindowLayout> layouts);
}

/// <summary>Reads and writes the Windows system proxy.</summary>
public interface ISystemProxy
{
    bool? IsEnabled();
    void SetEnabled(bool on);
}

/// <summary>Reads and switches the active Windows power plan (friendly key or GUID).</summary>
public interface IPowerPlanManager
{
    string? Active();
    void SetActive(string key);
}

public interface IDisplayManager { void SetMode(string mode); }
public interface IAudioDeviceManager { void SetDefaultPlayback(string endpointId); }

/// <summary>Runs a library/plugin action step (e.g. a PowerShell script).</summary>
public interface IActionRunner
{
    Task<StepOutcome> RunAsync(SwitchStep step, CancellationToken ct = default);
}
