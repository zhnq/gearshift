namespace GearShift.Core.Models;

/// <summary>What a scene wants a program's run-state to be when the scene is applied.</summary>
public enum AppDisposition
{
    /// <summary>The program should be running; launch it if it isn't.</summary>
    EnsureRunning,

    /// <summary>The program should be closed; terminate it if it's running.</summary>
    EnsureClosed,
}

/// <summary>How a program should appear when GearShift starts it.</summary>
public enum AppLaunchMode
{
    /// <summary>Ask Windows to open the program normally in the foreground.</summary>
    Normal,

    /// <summary>Ask Windows to start the program minimized. Applications may choose to minimize to their tray.</summary>
    Minimized,
}

/// <summary>
/// A three-valued switch used for global settings. <see cref="Unchanged"/> means the scene
/// does not touch this setting at all — the cornerstone of the "never touch what wasn't asked" rule.
/// </summary>
public enum TriState
{
    Unchanged,
    On,
    Off,
}

/// <summary>Events and environmental conditions that can activate a scene automatically.</summary>
public enum SceneTriggerKind
{
    ProcessStarted,
    ProcessStopped,
    ForegroundProcess,
    AcPower,
    BatteryPower,
    WifiSsid,
    TimeRange,
    ExternalMonitor,
}
