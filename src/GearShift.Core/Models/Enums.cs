namespace GearShift.Core.Models;

/// <summary>What a scene wants a program's run-state to be when the scene is applied.</summary>
public enum AppDisposition
{
    /// <summary>The program should be running; launch it if it isn't.</summary>
    EnsureRunning,

    /// <summary>The program should be closed; terminate it if it's running.</summary>
    EnsureClosed,

    /// <summary>
    /// The program should be running but frozen — every thread suspended (via NtSuspendProcess), so it
    /// keeps its RAM and session but consumes no CPU. Only applies to an already-running process;
    /// switching to a scene that wants it <see cref="EnsureRunning"/> thaws it again.
    /// </summary>
    EnsureSuspended,
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
