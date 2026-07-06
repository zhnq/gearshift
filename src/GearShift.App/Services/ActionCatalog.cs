using GearShift.Core.Actions;

namespace GearShift.App.Services;

/// <summary>A row in the action library page.</summary>
public sealed class ActionDescriptor
{
    public required string Icon { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<string> Chips { get; init; }
    public bool Enabled { get; init; }

    /// <summary>Plugin id, or null for engine-native built-ins that can't be toggled.</summary>
    public string? Id { get; init; }

    public bool CanToggle => Id is not null;
}

/// <summary>Composes the display list: engine-native built-ins plus discovered script plugins.</summary>
public static class ActionCatalog
{
    public static IReadOnlyList<ActionDescriptor> Build(ActionLibrary library)
    {
        var list = new List<ActionDescriptor>
        {
            new() { Icon = "🌐", Name = "系统代理", Description = "关闭或开启 Windows 系统代理", Chips = ["内置", "有状态"], Enabled = true },
            new() { Icon = "⚡", Name = "电源计划", Description = "切换 Windows 电源计划（高性能 / 平衡）", Chips = ["内置", "有状态"], Enabled = true },
        };

        foreach (var manifest in library.All)
        {
            var chips = new List<string> { "脚本 · PowerShell" };
            if (manifest.Apply?.Script is null) chips.Add("触发");
            if (manifest.Experimental) chips.Add("实验性");

            list.Add(new ActionDescriptor
            {
                Icon = "🧩",
                Name = manifest.Name,
                Description = manifest.Description,
                Chips = chips,
                Id = manifest.Id,
                Enabled = ActionState.IsEnabled(manifest.Id),
            });
        }

        return list;
    }
}
