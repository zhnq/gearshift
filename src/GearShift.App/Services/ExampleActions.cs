namespace GearShift.App.Services;

/// <summary>
/// Writes a couple of example plugin actions to the actions folder on first run, so the plugin
/// pipeline is demonstrable out of the box and users have a template to copy.
/// </summary>
public static class ExampleActions
{
    public static void SeedIfEmpty(string root)
    {
        if (Directory.Exists(root) && Directory.EnumerateDirectories(root).Any())
            return;

        Write(root, "focus-assist",
            manifest: """
            {
              "id": "focus-assist",
              "name": "专注助手",
              "description": "进入场景时设置勿扰 / 专注模式",
              "experimental": true,
              "params": [{ "key": "mode", "type": "enum", "values": ["on", "off"], "default": "on" }],
              "apply": { "run": "powershell", "script": "apply.ps1", "args": "-Mode {mode}" }
            }
            """,
            script: """
            param([string]$Mode = "on")
            # 示例脚本：真实的专注助手切换可在此实现（WNF / 快速设置）。
            # 这里仅演示插件管线可用，输出会显示在切换结果里。
            Write-Output "专注助手已设置为 $Mode"
            """);

        Write(root, "clean-ram",
            manifest: """
            {
              "id": "clean-ram",
              "name": "清理内存",
              "description": "释放后台占用的工作集内存",
              "apply": { "run": "powershell", "script": "apply.ps1" }
            }
            """,
            script: """
            $sig = '[System.Runtime.InteropServices.DllImport("psapi.dll")] public static extern int EmptyWorkingSet(System.IntPtr h);'
            $mem = Add-Type -MemberDefinition $sig -Name 'Mem' -Namespace 'Win' -PassThru
            Get-Process | ForEach-Object {
                try { $mem::EmptyWorkingSet($_.Handle) | Out-Null } catch {}
            }
            Write-Output "已清理各进程工作集内存"
            """);
    }

    private static void Write(string root, string id, string manifest, string script)
    {
        var dir = Path.Combine(root, id);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "action.json"), manifest);
        File.WriteAllText(Path.Combine(dir, "apply.ps1"), script);
    }
}
