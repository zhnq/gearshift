namespace GearShift.Core.System;

/// <summary>A running program with a visible window: its executable name and (if accessible) path.</summary>
public sealed record RunningApp(string Name, string? Path);
