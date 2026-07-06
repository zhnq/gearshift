using System.Diagnostics;
using GearShift.Core.Engine;

namespace GearShift.Core.Actions;

/// <summary>
/// Runs plugin actions by resolving their manifest from the <see cref="ActionLibrary"/> and executing
/// the apply command (typically a PowerShell script). Output and exit code become the step outcome, so
/// a failing script degrades to a warning/failure instead of throwing.
/// </summary>
public sealed class ScriptActionRunner : IActionRunner
{
    private readonly ActionLibrary _library;
    private readonly Func<string, bool>? _isEnabled;

    public ScriptActionRunner(ActionLibrary library, Func<string, bool>? isEnabled = null)
    {
        _library = library;
        _isEnabled = isEnabled;
    }

    public async Task<StepOutcome> RunAsync(SwitchStep step, CancellationToken ct = default)
    {
        var invocation = step.Action;
        if (invocation is null)
            return new StepOutcome(step, StepStatus.Skipped, "无动作调用");

        if (_isEnabled is not null && !_isEnabled(invocation.ActionId))
            return new StepOutcome(step, StepStatus.Skipped, $"动作 {invocation.ActionId} 已禁用");

        var resolved = _library.Resolve(invocation.ActionId);
        if (resolved is null)
            return new StepOutcome(step, StepStatus.Skipped, $"未找到动作插件 {invocation.ActionId}");

        if (resolved.Manifest.Apply is null)
            return new StepOutcome(step, StepStatus.Skipped, $"{resolved.Manifest.Name} 未定义 apply");

        var (exitCode, output) = await ExecuteAsync(resolved.Manifest.Apply, invocation.Params, resolved.Directory, ct);
        var text = Summarize(output);

        return exitCode == 0
            ? new StepOutcome(step, StepStatus.Ok,
                string.IsNullOrEmpty(text) ? $"{resolved.Manifest.Name} 已执行" : $"{resolved.Manifest.Name}：{text}")
            : new StepOutcome(step, StepStatus.Failed, $"{resolved.Manifest.Name} 失败({exitCode}）{text}");
    }

    /// <summary>Builds the command-line arguments, substituting <c>{key}</c> placeholders from params.</summary>
    public static string BuildArguments(ActionCommand command, string directory, IReadOnlyDictionary<string, string> parameters)
    {
        var args = command.Args ?? string.Empty;
        foreach (var (key, value) in parameters)
            args = args.Replace("{" + key + "}", value);

        if (!string.IsNullOrWhiteSpace(command.Script))
        {
            var scriptPath = Path.Combine(directory, command.Script);
            return $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" {args}".TrimEnd();
        }

        return args;
    }

    private static async Task<(int ExitCode, string Output)> ExecuteAsync(
        ActionCommand command, IReadOnlyDictionary<string, string> parameters, string directory, CancellationToken ct)
    {
        var exe = command.Run.Equals("powershell", StringComparison.OrdinalIgnoreCase) ? "powershell" : command.Run;

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = BuildArguments(command, directory, parameters),
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        return (process.ExitCode, (stdout + stderr).Trim());
    }

    private static string Summarize(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return string.Empty;

        var line = output.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim() ?? string.Empty;
        return line.Length > 80 ? line[..80] : line;
    }
}
