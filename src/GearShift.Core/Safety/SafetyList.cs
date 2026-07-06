namespace GearShift.Core.Safety;

/// <summary>
/// The guardrail that stops the app from ever terminating a process critical to Windows.
/// Any <c>EnsureClosed</c> whose target is on this list is silently skipped by the engine.
/// </summary>
public sealed class SafetyList
{
    /// <summary>Processes that must never be closed. Names are compared case-insensitively.</summary>
    public static readonly IReadOnlyList<string> Defaults =
    [
        "csrss.exe", "winlogon.exe", "wininit.exe", "services.exe", "lsass.exe",
        "svchost.exe", "smss.exe", "dwm.exe", "explorer.exe", "fontdrvhost.exe",
        "ctfmon.exe", "conhost.exe", "taskhostw.exe", "searchhost.exe",
        "system", "registry", "memory compression", "gearshift.exe",
    ];

    private readonly HashSet<string> _protected;

    public SafetyList(IEnumerable<string>? extra = null)
    {
        _protected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in Defaults) _protected.Add(name);
        if (extra is not null)
            foreach (var name in extra) _protected.Add(name.Trim());
    }

    public bool IsProtected(string processName)
        => _protected.Contains(processName.Trim());

    public void Add(string processName) => _protected.Add(processName.Trim());

    public IReadOnlyCollection<string> All => _protected;
}
