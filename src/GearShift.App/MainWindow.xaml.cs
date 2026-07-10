using H.NotifyIcon.Core;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using GearShift.App.Pages;
using GearShift.App.Services;
using GearShift.Core.Models;
using Windows.Graphics;

namespace GearShift.App;

public sealed partial class MainWindow : Window
{
    private bool _exiting;

    public MainWindow()
    {
        InitializeComponent();
        Title = "GearShift";

        // Fluent window material + a sensible default size, matching the prototype.
        SystemBackdrop = new MicaBackdrop();
        try { AppWindow.Resize(new SizeInt32(1240, 820)); } catch { /* headless / unsupported */ }

        // Closing the window hides to tray instead of quitting — the app lives in the tray.
        AppWindow.Closing += OnWindowClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ThemeService.Apply(SettingsService.Current.Theme);
        ContentFrame.Navigate(typeof(ScenesPage));
        BuildTrayMenu();
        Tray.ForceCreate();
        UpdateTrayTooltip();

        if (SettingsService.Current.StartMinimized)
            AppWindow.Hide();

        ApplyStartupScene();
        _ = CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        if (!SettingsService.Current.AutoCheckUpdates) return;
        try
        {
            var update = await UpdateService.CheckAsync();
            if (update is null || Content?.XamlRoot is null) return;
            AppWindow.Show();
            Activate();
            var notes = string.IsNullOrWhiteSpace(update.Notes) ? "此版本未提供更新说明。" : update.Notes;
            if (notes.Length > 1800) notes = notes[..1800] + "…";
            var dialog = new ContentDialog
            {
                Title = $"GearShift {update.Tag} 可用",
                Content = new ScrollViewer
                {
                    MaxHeight = 360,
                    Content = new TextBlock { Text = notes, TextWrapping = TextWrapping.Wrap },
                },
                PrimaryButtonText = "下载并更新",
                CloseButtonText = "稍后",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            try
            {
                await UpdateService.ApplyAsync(update);
                _exiting = true;
                Tray.Dispose();
                Application.Current.Exit();
            }
            catch (Exception ex)
            {
                await new ContentDialog
                {
                    Title = "更新失败",
                    Content = ex.Message,
                    CloseButtonText = "完成",
                    XamlRoot = Content.XamlRoot,
                }.ShowAsync();
            }
        }
        catch
        {
            // Automatic checks are intentionally quiet. Manual checks in Settings surface errors.
        }
    }

    private async void ApplyStartupScene()
    {
        var id = SettingsService.Current.PendingElevatedSceneId ?? SettingsService.Current.DefaultSceneId;
        if (string.IsNullOrEmpty(id)) return;

        if (SettingsService.Current.PendingElevatedSceneId is not null)
        {
            SettingsService.Current.PendingElevatedSceneId = null;
            SettingsService.Save();
        }

        var scene = AppServices.Scenes.FirstOrDefault(s => s.Id == id);
        if (scene is null) return;

        await AppServices.SwitchAsync(scene);
        BuildTrayMenu();
        UpdateTrayTooltip();
    }

    private void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_exiting) return;
        args.Cancel = true;
        AppWindow.Hide();
    }

    private void ShowFromTray()
    {
        AppWindow.Show();
        Activate();
    }

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        var tag = (args.SelectedItem as NavigationViewItem)?.Tag as string;
        switch (tag)
        {
            case "scenes":
                ContentFrame.Navigate(typeof(ScenesPage));
                break;
            case "actions":
                ContentFrame.Navigate(typeof(ActionLibraryPage));
                break;
        }
    }

    // ---- Tray ----

    private void BuildTrayMenu()
    {
        TrayMenu.Items.Clear();

        foreach (var scene in AppServices.Scenes)
        {
            var label = string.IsNullOrEmpty(scene.Icon) ? scene.Name : $"{scene.Icon}  {scene.Name}";
            if (scene.Id == AppServices.ActiveSceneId)
                label += "（再次启用）";
            var item = new MenuFlyoutItem { Text = label, Tag = scene };
            if (scene.Id == AppServices.ActiveSceneId)
                item.Icon = new FontIcon { Glyph = ((char)0xE73E).ToString() }; // checkmark on the active scene
            item.Click += OnTraySceneClick;
            TrayMenu.Items.Add(item);
        }

        TrayMenu.Items.Add(new MenuFlyoutSeparator());

        var open = new MenuFlyoutItem { Text = "打开主窗口" };
        open.Click += (_, _) => ShowFromTray();
        TrayMenu.Items.Add(open);

        var exit = new MenuFlyoutItem { Text = "退出 GearShift" };
        exit.Click += (_, _) =>
        {
            _exiting = true;
            Tray.Dispose();
            Application.Current.Exit();
        };
        TrayMenu.Items.Add(exit);
    }

    private async void OnTraySceneClick(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuFlyoutItem)?.Tag is not Scene scene)
            return;

        var result = await AppServices.SwitchAsync(scene);
        BuildTrayMenu();
        UpdateTrayTooltip();

        if (SettingsService.Current.NotifyOnSwitch)
        {
            var summary = result.WasNoOp
                ? "已处于目标状态"
                : $"{result.OkCount} 项完成" + (result.HadTrouble ? $" · {result.WarningCount + result.FailedCount} 项提示" : "");
            try { Tray.ShowNotification($"已切换到 {scene.Name}", summary, NotificationIcon.Info); }
            catch { /* notifications best-effort */ }
        }
    }

    private void UpdateTrayTooltip()
    {
        var active = AppServices.Scenes.FirstOrDefault(s => s.Id == AppServices.ActiveSceneId);
        Tray.ToolTipText = active is null ? "GearShift" : $"GearShift — 当前：{active.Name}";
    }
}
