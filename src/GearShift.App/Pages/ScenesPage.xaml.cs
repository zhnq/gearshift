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
            CloseButtonText = "完成",
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
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
        var freeze = scene.Apps.Count(a => a.Disposition == AppDisposition.EnsureSuspended);
        var parts = new List<string> { $"{start} 项启动", $"{close} 项关闭" };
        if (freeze > 0)
            parts.Add($"{freeze} 项冻结");
        if (scene.Proxy != TriState.Unchanged)
            parts.Add($"代理 {(scene.Proxy == TriState.On ? "开" : "关")}");
        return string.Join(" · ", parts);
    }
}
