using GearShift.Core.Actions;
using Xunit;

namespace GearShift.Core.Tests;

public class ActionPluginTests
{
    [Fact]
    public void BuildArguments_substitutes_placeholders_and_wraps_script()
    {
        var cmd = new ActionCommand { Run = "powershell", Script = "apply.ps1", Args = "-Mode {mode}" };

        var args = ScriptActionRunner.BuildArguments(cmd, @"C:\actions\focus",
            new Dictionary<string, string> { ["mode"] = "off" });

        Assert.Contains("-File", args);
        Assert.Contains("apply.ps1", args);
        Assert.Contains("-Mode off", args);
        Assert.DoesNotContain("{mode}", args);
    }

    [Fact]
    public void Library_discovers_and_resolves_manifests()
    {
        var root = Path.Combine(Path.GetTempPath(), "ss-actions-" + Guid.NewGuid().ToString("N"));
        try
        {
            var dir = Path.Combine(root, "focus-assist");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "action.json"),
                """
                { "id": "focus-assist", "name": "专注助手",
                  "apply": { "run": "powershell", "script": "apply.ps1", "args": "-Mode {mode}" } }
                """);

            var library = new ActionLibrary(root);
            var resolved = library.Resolve("focus-assist");

            Assert.NotNull(resolved);
            Assert.Equal("专注助手", resolved!.Manifest.Name);
            Assert.Equal("apply.ps1", resolved.Manifest.Apply!.Script);
            Assert.Single(library.All);
            Assert.Null(library.Resolve("does-not-exist"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Library_on_missing_root_is_empty()
    {
        var library = new ActionLibrary(Path.Combine(Path.GetTempPath(), "ss-missing-" + Guid.NewGuid().ToString("N")));
        Assert.Empty(library.All);
    }
}
