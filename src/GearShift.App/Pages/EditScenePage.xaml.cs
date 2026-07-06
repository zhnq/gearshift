using System.Collections.ObjectModel;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using GearShift.App.Services;
using GearShift.Core.Actions;
using GearShift.Core.Models;
using GearShift.Core.Safety;
using GearShift.Core.System;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace GearShift.App.Pages;

public sealed partial class EditScenePage : Page
{
    private Scene _scene = null!;

    private ObservableCollection<AppEntry> LaunchApps { get; } = [];
    private ObservableCollection<AppEntry> CloseApps { get; } = [];
    private ObservableCollection<ActionEntry> SceneActions { get; } = [];

    public EditScenePage()
    {
        InitializeComponent();
        LaunchList.ItemsSource = LaunchApps;
        CloseList.ItemsSource = CloseApps;
        ActionList.ItemsSource = SceneActions;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _scene = e.Parameter as Scene ?? AppServices.NewScene();

        NameBox.Text = _scene.Name;
        IconBox.Text = string.IsNullOrEmpty(_scene.Icon) ? "🗂" : _scene.Icon;

        foreach (var app in _scene.Apps)
        {
            var entry = new AppEntry(app);
            if (app.Disposition == AppDisposition.EnsureRunning)
                LaunchApps.Add(entry);
            else
                CloseApps.Add(entry);
        }

        foreach (var invocation in _scene.Actions)
        {
            var name = AppServices.Actions.Resolve(invocation.ActionId)?.Manifest.Name ?? invocation.ActionId;
            SceneActions.Add(new ActionEntry
            {
                ActionId = invocation.ActionId,
                Name = name,
                Params = new Dictionary<string, string>(invocation.Params),
            });
        }

        ProxyCombo.SelectedIndex = _scene.Proxy switch
        {
            TriState.On => 1,
            TriState.Off => 2,
            _ => 0,
        };
        PowerCombo.SelectedIndex = _scene.PowerPlan switch
        {
            "high" => 1,
            "balanced" => 2,
            "saver" => 3,
            _ => 0,
        };
    }

    private void OnBack(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(ScenesPage));

    private void OnRemoveApp(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not AppEntry entry) return;
        LaunchApps.Remove(entry);
        CloseApps.Remove(entry);
    }

    private async void OnAddClose(object sender, RoutedEventArgs e)
    {
        foreach (var name in await PickRunningProcessesAsync())
        {
            if (CloseApps.Any(a => a.Match.Equals(name, StringComparison.OrdinalIgnoreCase)))
                continue;
            CloseApps.Add(new AppEntry(name, AppDisposition.EnsureClosed, null, name));
        }
    }

    private async void OnAddLaunch(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        picker.FileTypeFilter.Add(".exe");

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        var display = Path.GetFileNameWithoutExtension(file.Name);
        LaunchApps.Add(new AppEntry(file.Name, AppDisposition.EnsureRunning, file.Path, display));
    }

    private void OnRemoveAction(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is ActionEntry entry)
            SceneActions.Remove(entry);
    }

    private async void OnAddAction(object sender, RoutedEventArgs e)
    {
        var manifests = AppServices.Actions.All.Where(m => ActionState.IsEnabled(m.Id)).ToList();
        if (manifests.Count == 0)
        {
            await new ContentDialog
            {
                Title = "没有可用动作",
                Content = "还没有已启用的动作插件，可在「动作库」页导入或启用。",
                CloseButtonText = "好的",
                XamlRoot = XamlRoot,
            }.ShowAsync();
            return;
        }

        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            ItemsSource = manifests.Select(m => m.Name).ToList(),
            MaxHeight = 320,
        };
        var dialog = new ContentDialog
        {
            Title = "添加动作",
            Content = list,
            PrimaryButtonText = "添加",
            CloseButtonText = "取消",
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary && list.SelectedIndex >= 0)
        {
            var manifest = manifests[list.SelectedIndex];
            if (SceneActions.Any(a => a.ActionId == manifest.Id))
                return;

            var parameters = await ConfigureParamsAsync(manifest);
            if (parameters is null)
                return; // cancelled the param step

            SceneActions.Add(new ActionEntry
            {
                ActionId = manifest.Id,
                Name = manifest.Name,
                Params = parameters,
            });
        }
    }

    /// <summary>Prompts for the action's parameters. Returns an empty dict when there are none, null on cancel.</summary>
    private async Task<Dictionary<string, string>?> ConfigureParamsAsync(ActionManifest manifest)
    {
        if (manifest.Params.Count == 0)
            return [];

        var panel = new StackPanel { Spacing = 12 };
        var getters = new List<(string Key, Func<string> Value)>();

        foreach (var param in manifest.Params)
        {
            var row = new StackPanel { Spacing = 4 };
            row.Children.Add(new TextBlock { Text = param.Key });

            if (param.Type == "enum" && param.Values.Count > 0)
            {
                var combo = new ComboBox
                {
                    ItemsSource = param.Values.ToList(),
                    SelectedItem = param.Default ?? param.Values[0],
                    MinWidth = 200,
                };
                row.Children.Add(combo);
                getters.Add((param.Key, () => combo.SelectedItem?.ToString() ?? param.Default ?? ""));
            }
            else
            {
                var box = new TextBox { Text = param.Default ?? "", MinWidth = 200 };
                row.Children.Add(box);
                getters.Add((param.Key, () => box.Text));
            }

            panel.Children.Add(row);
        }

        var dialog = new ContentDialog
        {
            Title = $"配置 · {manifest.Name}",
            Content = panel,
            PrimaryButtonText = "添加",
            CloseButtonText = "取消",
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return null;

        var result = new Dictionary<string, string>();
        foreach (var (key, value) in getters)
            result[key] = value();
        return result;
    }

    private void OnDone(object sender, RoutedEventArgs e)
    {
        var proxy = ProxyCombo.SelectedIndex switch { 1 => TriState.On, 2 => TriState.Off, _ => TriState.Unchanged };
        var power = PowerCombo.SelectedIndex switch { 1 => "high", 2 => "balanced", 3 => "saver", _ => (string?)null };

        var updated = _scene with
        {
            Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "未命名场景" : NameBox.Text.Trim(),
            Icon = string.IsNullOrWhiteSpace(IconBox.Text) ? "🗂" : IconBox.Text.Trim(),
            Apps = [.. LaunchApps.Select(a => a.ToRef()), .. CloseApps.Select(a => a.ToRef())],
            Proxy = proxy,
            PowerPlan = power,
            Actions = [.. SceneActions.Select(a => new ActionInvocation { ActionId = a.ActionId, Params = a.Params })],
        };

        AppServices.UpsertScene(updated);
        Frame.Navigate(typeof(ScenesPage));
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "删除场景？",
            Content = $"确定删除「{_scene.Name}」吗？此操作无法撤销。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            AppServices.DeleteScene(_scene.Id);
            Frame.Navigate(typeof(ScenesPage));
        }
    }

    private async Task<IReadOnlyList<string>> PickRunningProcessesAsync()
    {
        var safety = new SafetyList();
        var apps = AppServices.Processes.VisibleWindowApps()
            .Where(a => !safety.IsProtected(a.Name))
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (apps.Count == 0)
            apps = AppServices.Processes.RunningProcessNames()
                .Where(n => !safety.IsProtected(n))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Select(n => new RunningApp(n, null))
                .ToList();

        var list = new ListView { SelectionMode = ListViewSelectionMode.Multiple, MaxHeight = 360 };
        foreach (var app in apps)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            var image = new Image { Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(image);
            row.Children.Add(new TextBlock { Text = app.Name, VerticalAlignment = VerticalAlignment.Center });
            list.Items.Add(new ListViewItem { Content = row, Tag = app.Name });
            _ = SetIconAsync(image, app.Path);
        }

        var dialog = new ContentDialog
        {
            Title = "选择要关闭的程序",
            Content = list,
            PrimaryButtonText = "添加",
            CloseButtonText = "取消",
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            return list.SelectedItems.Cast<ListViewItem>().Select(i => (string)i.Tag).ToList();
        return [];
    }

    private static async Task SetIconAsync(Image image, string? path)
    {
        var source = await IconLoader.FromExeAsync(path);
        if (source is not null)
            image.Source = source;
    }
}

/// <summary>Editing row for a plugin action referenced by the scene.</summary>
public sealed class ActionEntry
{
    public required string ActionId { get; init; }
    public required string Name { get; init; }
    public IReadOnlyDictionary<string, string> Params { get; init; } = new Dictionary<string, string>();
}

/// <summary>Mutable editing row for a program inside the scene editor.</summary>
public sealed class AppEntry
{
    public AppEntry(AppRef app)
    {
        Match = app.Match;
        DisplayName = app.Label;
        Path = app.Path;
        Disposition = app.Disposition;
    }

    public AppEntry(string match, AppDisposition disposition, string? path, string? displayName)
    {
        Match = match;
        DisplayName = string.IsNullOrEmpty(displayName) ? match : displayName;
        Path = path;
        Disposition = disposition;
    }

    public string Match { get; }
    public string DisplayName { get; }
    public string? Path { get; }
    public AppDisposition Disposition { get; }

    public AppRef ToRef() => new()
    {
        Match = Match,
        DisplayName = DisplayName,
        Path = Path,
        Disposition = Disposition,
    };
}
