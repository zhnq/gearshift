using GearShift.Core.Engine;

namespace GearShift.Core.System;

/// <summary>
/// Live snapshot of the real Windows system, composed from the concrete managers. A fresh instance
/// should be taken at the start of every switch so the diff reflects current reality.
/// </summary>
public sealed class WindowsSystemProbe : ISystemProbe
{
    private readonly ProcessManager _processes;
    private readonly ISystemProxy _proxy;
    private readonly IPowerPlanManager _power;
    private IReadOnlySet<string>? _cachedNames;
    private IReadOnlySet<string>? _cachedSuspended;

    public WindowsSystemProbe(ProcessManager processes, ISystemProxy proxy, IPowerPlanManager power)
    {
        _processes = processes;
        _proxy = proxy;
        _power = power;
    }

    public IReadOnlySet<string> RunningProcessNames()
        => _cachedNames ??= _processes.RunningProcessNames();

    public IReadOnlySet<string> SuspendedProcessNames()
        => _cachedSuspended ??= _processes.SuspendedProcessNames();

    public bool? ProxyEnabled => _proxy.IsEnabled();

    public string? ActivePowerPlan => _power.Active();
}
