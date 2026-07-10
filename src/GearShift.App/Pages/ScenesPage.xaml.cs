using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using GearShift.App.Services;
using GearShift.Core.Engine;
using GearShift.Core.Models;
using GearShift.Core.Safety;

namespace GearShift.App.Pages;

public sealed partial class ScenesPage : Page
{
    public ScenesPage()
    {
        InitializeComponent();
        RefreshCards();
    }

    private void RefreshCards()
    {
        ScenesGrid.ItemsSource = AppServices.Scenes
            .Select(s => new SceneItem(s, s.Id == AppServices.ActiveSceneId))
            .ToList();
    }

    private async void OnSwitchClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not Scene scene)
            return;

        var result = await AppServices.SwitchAsync(scene);
        RefreshCards();
        await ShowResultAsync(scene, result);
    }

    private void OnNewScene(object sender, RoutedEventArgs e)
        => Frame.Navigate(typeof(EditScenePage), AppServices.NewScene());

    private async void OnSnapshotScene(object sender, RoutedEventArgs e)
    {
        var safety = new SafetyList();
        var apps = AppServices.Processes.VisibleWindowProcessNames()
            .Where(n => !safety.IsProtected(n))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Select(n => new AppRef { Match = n, Disposition = AppDisposition.EnsureClosed, DisplayName = n })
            .ToList();

        if (apps.Count == 0)
        {
            await new ContentDialog
            {
                Title = "快照新建",
                Content = "没有检测到打开的程序窗口。",
                CloseButtonText = "好的",
                XamlRoot = XamlRoot,
            }.ShowAsync();
            return;
        }

        var scene = AppServices.NewScene() with { Name = "快照场景", Icon = "📸", Apps = apps };
        Frame.Navigate(typeof(EditScenePage), scene);
    }

    private void OnEditScene(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuFlyoutItem)?.Tag is Scene scene)
            Frame.Navigate(typeof(EditScenePage), scene);
    }

    private async void OnEnableScene(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuFlyoutItem)?.Tag is not Scene scene) return;
        var result = await AppServices.SwitchAsync(scene);
        RefreshCards();
        await ShowResultAsync(scene, result);
    }

    private async void OnPreviewScene(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuFlyoutItem)?.Tag is not Scene scene) return;
        var plan = AppServices.Preview(scene);
        var content = plan.Count == 0
            ? "当前系统已符合场景目标。"
            : string.Join(Environment.NewLine, plan.Select(x => $"• {x.Reason}：{x.Target}"));
        await new ContentDialog
        {
            Title = $"{scene.Name} 的执行预览",
            Content = new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "完成",
            XamlRoot = XamlRoot,
        }.ShowAsync();
    }

    private async void OnCreateShortcut(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuFlyoutItem)?.Tag is not Scene scene) return;
        try
        {
            var path = DesktopShortcutService.CreateSceneShortcut(scene.Id, scene.Name);
            await new ContentDialog { Title = "已创建快捷方式", Content = path, CloseButtonText = "完成", XamlRoot = XamlRoot }.ShowAsync();
        }
        catch (Exception ex)
        {
            await new ContentDialog { Title = "无法创建快捷方式", Content = ex.Message, CloseButtonText = "完成", XamlRoot = XamlRoot }.ShowAsync();
        }
    }

    private async void OnDeleteScene(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuFlyoutItem)?.Tag is not Scene scene) return;

        var dialog = new ContentDialog
        {
            Title = "删除场景？",
            Content = $"确定删除「{scene.Name}」吗？此操作无法撤销。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            AppServices.DeleteScene(scene.Id);
            RefreshCards();
        }
    }

    private async void OnShowHistory(object sender, RoutedEventArgs e)
    {
        var records = SceneRunHistory.Load().Take(20).ToList();
        var text = records.Count == 0
            ? "还没有执行记录。"
            : string.Join(Environment.NewLine + Environment.NewLine, records.Select(r =>
                $"{r.StartedAt:MM-dd HH:mm} · {r.SceneName} · {r.Outcomes.Count} 步 · {r.Outcomes.Sum(x => x.Duration.TotalMilliseconds):0} ms" +
                Environment.NewLine + string.Join(Environment.NewLine, r.Outcomes.Select(x => $"  {x.Status}  {x.Message}  {x.Duration.TotalMilliseconds:0} ms"))));
        await new ContentDialog
        {
            Title = "最近执行记录",
            Content = new ScrollViewer { MaxHeight = 420, Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap } },
            CloseButtonText = "完成",
            XamlRoot = XamlRoot,
        }.ShowAsync();
    }

    private async Task ShowResultAsync(Scene scene, SwitchResult result)
    {
        var body = new StringBuilder();
        if (result.WasNoOp)
        {
            body.Append("已处于目标状态，无需改动。");
        }
        else
        {
            foreach (var o in result.Outcomes)
            {
                var mark = o.Status switch
                {
                    StepStatus.Ok => "✓",
                    StepStatus.Warning => "⚠",
                    StepStatus.Failed => "✕",
                    _ => "·",
                };
                body.AppendLine($"{mark} {o.Message}");
            }
        }

        var dialog = new ContentDialog
        {
            Title = $"已切换到 {scene.Name}",
            Content = body.ToString().TrimEnd(),
            PrimaryButtonText = result.FailedCount > 0 && !ElevationHelper.IsElevated() ? "管理员重试" : null,
            SecondaryButtonText = result.HadTrouble ? "再次执行" : null,
            CloseButtonText = "完成",
            XamlRoot = XamlRoot,
        };
        var choice = await dialog.ShowAsync();
        if (choice == ContentDialogResult.Primary) ElevationHelper.RestartElevated(scene.Id);
        if (choice == ContentDialogResult.Secondary)
        {
            var retried = await AppServices.SwitchAsync(scene);
            RefreshCards();
            await ShowResultAsync(scene, retried);
        }
    }
}

/// <summary>Lightweight view model for a scene card.</summary>
public sealed class SceneItem
{
    public SceneItem(Scene scene, bool isActive)
    {
        Scene = scene;
        IsActive = isActive;
        Icon = string.IsNullOrEmpty(scene.Icon) ? "🗂" : scene.Icon;
        Name = scene.Name;
        Summary = BuildSummary(scene);
    }

    public Scene Scene { get; }
    public bool IsActive { get; }
    public string Icon { get; }
    public string Name { get; }
    public string Summary { get; }

    public Visibility ActiveVisibility => IsActive ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SwitchVisibility => IsActive ? Visibility.Collapsed : Visibility.Visible;

    private static string BuildSummary(Scene scene)
    {
        var start = scene.Apps.Count(a => a.Disposition == AppDisposition.EnsureRunning);
        var close = scene.Apps.Count(a => a.Disposition == AppDisposition.EnsureClosed);
        var parts = new List<string> { $"{start} 项启动", $"{close} 项关闭" };
        if (scene.Proxy != TriState.Unchanged)
            parts.Add($"代理 {(scene.Proxy == TriState.On ? "开" : "关")}");
        return string.Join(" · ", parts);
    }
}
