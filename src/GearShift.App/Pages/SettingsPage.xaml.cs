using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using GearShift.App.Services;
using GearShift.Core.Safety;

namespace GearShift.App.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _loaded;
    private readonly List<string?> _sceneIds = [];

    public SettingsPage()
    {
        InitializeComponent();
        LoadState();
    }

    private void LoadState()
    {
        var s = SettingsService.Current;

        AutoStartToggle.IsOn = AutoStartHelper.IsEnabled();
        StartMinToggle.IsOn = s.StartMinimized;
        NotifyToggle.IsOn = s.NotifyOnSwitch;
        ConfirmPluginToggle.IsOn = s.ConfirmPluginScripts;
        AutoUpdateToggle.IsOn = s.AutoCheckUpdates;
        AutomationToggle.IsOn = s.EnableAutomation;
        HotkeyToggle.IsOn = s.EnableHotkeys;

        ThemeCombo.SelectedIndex = s.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 };

        // Default-scene combo: index 0 = none, then one item per scene.
        DefaultSceneCombo.Items.Clear();
        _sceneIds.Clear();
        DefaultSceneCombo.Items.Add("无（不自动应用）");
        _sceneIds.Add(null);
        foreach (var scene in AppServices.Scenes)
        {
            DefaultSceneCombo.Items.Add(scene.Name);
            _sceneIds.Add(scene.Id);
        }
        var idx = _sceneIds.IndexOf(s.DefaultSceneId);
        DefaultSceneCombo.SelectedIndex = idx < 0 ? 0 : idx;

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is null
            ? "声明式场景切换工具"
            : $"版本 {version.Major}.{version.Minor}.{version.Build} · 声明式场景切换工具";

        if (ElevationHelper.IsElevated())
        {
            ElevStatus.Text = "已以管理员身份运行";
            ElevButton.IsEnabled = false;
        }
        else
        {
            ElevStatus.Text = "关闭部分程序或改服务需要提权";
        }

        _loaded = true;
    }

    private void OnRestartElevated(object sender, RoutedEventArgs e) => ElevationHelper.RestartElevated();

    private void OnAutoStartToggled(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        try { AutoStartHelper.SetEnabled(AutoStartToggle.IsOn); }
        catch { /* registry may be locked down; ignore for now */ }
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        var theme = ThemeCombo.SelectedIndex switch { 1 => "Light", 2 => "Dark", _ => "Default" };
        SettingsService.Current.Theme = theme;
        SettingsService.Save();
        ThemeService.Apply(theme);
    }

    private void OnDefaultSceneChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        var i = DefaultSceneCombo.SelectedIndex;
        SettingsService.Current.DefaultSceneId = i >= 0 && i < _sceneIds.Count ? _sceneIds[i] : null;
        SettingsService.Save();
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        SettingsService.Current.StartMinimized = StartMinToggle.IsOn;
        SettingsService.Current.NotifyOnSwitch = NotifyToggle.IsOn;
        SettingsService.Current.ConfirmPluginScripts = ConfirmPluginToggle.IsOn;
        SettingsService.Current.AutoCheckUpdates = AutoUpdateToggle.IsOn;
        SettingsService.Current.EnableAutomation = AutomationToggle.IsOn;
        SettingsService.Current.EnableHotkeys = HotkeyToggle.IsOn;
        SettingsService.Save();
    }

    private async void OnCheckUpdate(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = "正在连接 GitHub…";
        try
        {
            var update = await UpdateService.CheckAsync();
            if (update is null)
            {
                UpdateStatusText.Text = $"已是最新版 {UpdateService.CurrentVersion.ToString(3)}";
                return;
            }
            UpdateStatusText.Text = $"发现 {update.Tag}";
            await PromptAndApplyUpdateAsync(update);
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = "检查失败";
            await new ContentDialog
            {
                Title = "无法检查更新",
                Content = ex.Message,
                CloseButtonText = "完成",
                XamlRoot = XamlRoot,
            }.ShowAsync();
        }
        finally { CheckUpdateButton.IsEnabled = true; }
    }

    private async Task PromptAndApplyUpdateAsync(UpdateInfo update)
    {
        var notes = string.IsNullOrWhiteSpace(update.Notes) ? "此版本未提供更新说明。" : update.Notes;
        if (notes.Length > 1800) notes = notes[..1800] + "…";
        var dialog = new ContentDialog
        {
            Title = $"更新到 {update.Tag}？",
            Content = new ScrollViewer
            {
                MaxHeight = 360,
                Content = new TextBlock { Text = notes, TextWrapping = TextWrapping.Wrap },
            },
            PrimaryButtonText = "下载并更新",
            CloseButtonText = "稍后",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        UpdateStatusText.Text = "正在下载更新…";
        await UpdateService.ApplyAsync(update);
        Application.Current.Exit();
    }

    private async void OnViewSafetyList(object sender, RoutedEventArgs e)
    {
        var names = string.Join("   ", SafetyList.Defaults);
        var dialog = new ContentDialog
        {
            Title = "关键进程安全名单",
            Content = new TextBlock { Text = names, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "完成",
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }
}
