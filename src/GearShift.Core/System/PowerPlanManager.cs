using System.Diagnostics;
using System.Text.RegularExpressions;
using GearShift.Core.Engine;

namespace GearShift.Core.System;

/// <summary>
/// Reads and switches the active Windows power plan via <c>powercfg</c>. Accepts friendly keys
/// (<c>high</c> / <c>balanced</c> / <c>saver</c>) or a raw scheme GUID.
/// </summary>
public sealed partial class PowerPlanManager : IPowerPlanManager
{
    private static readonly Dictionary<string, string> WellKnown = new(StringComparer.OrdinalIgnoreCase)
    {
        ["high"] = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
        ["balanced"] = "381b4222-f694-41f0-9685-ff5bb260df2e",
        ["saver"] = "a1841308-3541-4fab-bc81-f71556f20b4a",
    };

    public string? Active()
    {
        var output = Run("/getactivescheme");
        var match = GuidRegex().Match(output);
        if (!match.Success)
            return null;

        var guid = match.Value.ToLowerInvariant();
        foreach (var (key, value) in WellKnown)
            if (string.Equals(value, guid, StringComparison.OrdinalIgnoreCase))
                return key;

        return guid;
    }

    public void SetActive(string key)
    {
        var guid = WellKnown.TryGetValue(key, out var known) ? known : key;
        Run($"/setactive {guid}");
    }

    private static string Run(string args)
    {
        var psi = new ProcessStartInfo("powercfg", args)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit(3000);
        return output;
    }

    [GeneratedRegex(@"[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}")]
    private static partial Regex GuidRegex();
}
