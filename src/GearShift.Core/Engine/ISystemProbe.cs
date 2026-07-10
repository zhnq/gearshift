namespace GearShift.Core.Engine;

/// <summary>
/// Read-only snapshot of the parts of the system a scene can affect. The engine depends only on
/// this abstraction, which keeps <see cref="DiffEngine"/> pure and unit-testable with a fake.
/// </summary>
public interface ISystemProbe
{
    /// <summary>Currently running executable names, lower-cased (e.g. <c>steam.exe</c>).</summary>
    IReadOnlySet<string> RunningProcessNames();

    /// <summary>Whether the Windows system proxy is enabled, or <c>null</c> if unknown.</summary>
    bool? ProxyEnabled { get; }

    /// <summary>Active power-plan key (GUID), or <c>null</c> if unknown.</summary>
    string? ActivePowerPlan { get; }
}
