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
    private readonly List<SceneTrigger> _triggers = [];
    private readonly List<WindowLayout> _layouts = [];

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
            switch (app.Disposition)
            {
                case AppDisposition.EnsureRunning: LaunchApps.Add(entry); break;
                default: CloseApps.Add(entry); break;
            }
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

        _triggers.AddRange(_scene.Triggers);
        _layouts.AddRange(_scene.WindowLayouts);
        RestoreToggle.IsOn = _scene.RestoreWhenStopped.Count > 0;
        RefreshTriggerSummary();

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
        DisplayCombo.SelectedIndex = _scene.DisplayMode switch { "extend" => 1, "clone" => 2, "external" => 3, "internal" => 4, _ => 0 };
        AudioCombo.Items.Clear();
        AudioCombo.Items.Add(new GearShift.Core.System.AudioEndpoint("", "不改变"));
        foreach (var endpoint in AppServices.Audio.PlaybackDevices()) AudioCombo.Items.Add(endpoint);
        AudioCombo.SelectedValue = _scene.AudioDeviceId ?? "";
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
        var candidates = ProgramCatalog.GetAll();
        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Multiple,
            MaxHeight = 360,
            ItemsSource = candidates.Select(x => new ProgramPickerItem(x)).ToList(),
        };
        var search = new TextBox { PlaceholderText = "搜索程序名称", MinWidth = 420 };
        var source = new ComboBox { MinWidth = 140, SelectedIndex = 0 };
        foreach (var label in new[] { "全部来源", "开始菜单", "桌面", "已安装程序", "Microsoft Store", "Steam 游戏" }) source.Items.Add(label);

        void Refresh()
        {
            var term = search.Text.Trim();
            var sourceIndex = source.SelectedIndex - 1;
            list.ItemsSource = candidates
                .Where(x => (sourceIndex < 0 || (int)x.Source == sourceIndex)
                            && (term.Length == 0 || x.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)))
                .Select(x => new ProgramPickerItem(x))
                .ToList();
        }
        search.TextChanged += (_, _) => Refresh();
        source.SelectionChanged += (_, _) => Refresh();

        var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        filterRow.Children.Add(search);
        filterRow.Children.Add(source);
        var panel = new StackPanel { Spacing = 10, MinWidth = 620 };
        panel.Children.Add(new TextBlock { Text = "可多选。找不到时使用“浏览文件”，支持 exe、快捷方式和脚本。" });
        panel.Children.Add(filterRow);
        panel.Children.Add(list);
        var dialog = new ContentDialog
        {
            Title = "添加要启动的程序",
            Content = panel,
            PrimaryButtonText = "添加所选",
            SecondaryButtonText = "浏览文件",
            CloseButtonText = "取消",
            XamlRoot = XamlRoot,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            foreach (var item in list.SelectedItems.Cast<ProgramPickerItem>())
                AddLaunchCandidate(item.Candidate.Path, item.Candidate.Name);
            return;
        }
        if (result != ContentDialogResult.Secondary) return;

        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        // 桌面上的程序多是 .lnk 快捷方式，一并放行；.lnk 会解析到真实 exe 后再入库。
        picker.FileTypeFilter.Add(".exe");
        picker.FileTypeFilter.Add(".lnk");
        picker.FileTypeFilter.Add(".url");
        picker.FileTypeFilter.Add(".bat");
        picker.FileTypeFilter.Add(".cmd");

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        AddLaunchCandidate(file.Path, Path.GetFileNameWithoutExtension(file.Name));
    }

    private void AddLaunchCandidate(string path, string display)
    {
        var targetPath = path;

        // 快捷方式：解析出目标 exe，这样启动的是真实程序、且进程名能匹配上（引擎靠进程名判断“是否已运行”）。
        if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            var resolved = ShortcutResolver.ResolveTarget(path);
            if (!string.IsNullOrWhiteSpace(resolved))
                targetPath = resolved;
        }

        // Match 用目标文件名（如 chrome.exe），供差异引擎与运行中进程比对。
        var match = targetPath.StartsWith("steam:", StringComparison.OrdinalIgnoreCase)
                    || targetPath.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
            ? display + ".app"
            : Path.GetFileName(targetPath);
        if (LaunchApps.Any(a => a.Match.Equals(match, StringComparison.OrdinalIgnoreCase))) return;
        LaunchApps.Add(new AppEntry(match, AppDisposition.EnsureRunning, targetPath, display));
    }

    private void OnToggleLaunchMode(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not AppEntry entry) return;
        entry.LaunchMode = entry.LaunchMode == AppLaunchMode.Normal
            ? AppLaunchMode.Minimized
            : AppLaunchMode.Normal;
        LaunchList.ItemsSource = null;
        LaunchList.ItemsSource = LaunchApps;
    }

    private void OnRemoveAction(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is ActionEntry entry)
            SceneActions.Remove(entry);
    }

    private async void OnConfigureTriggers(object sender, RoutedEventArgs e)
    {
        var kind = new ComboBox { MinWidth = 280, SelectedIndex = 0 };
        foreach (var item in new[] { "程序启动", "程序退出", "前台程序", "接通电源", "使用电池", "连接 Wi-Fi", "时间范围", "连接外接显示器" }) kind.Items.Add(item);
        var value = new TextBox { PlaceholderText = "程序名 / Wi-Fi 名 / 开始时间 HH:mm", MinWidth = 280 };
        var end = new TextBox { PlaceholderText = "结束时间 HH:mm（仅时间范围）", MinWidth = 280 };
        var list = new ListView { ItemsSource = _triggers.Select(DescribeTrigger).ToList(), MaxHeight = 180 };
        var panel = new StackPanel { Spacing = 10, MinWidth = 400 };
        panel.Children.Add(new TextBlock { Text = "自动触发有 30 秒防抖。程序名称请填写 xxx.exe。" });
        panel.Children.Add(kind); panel.Children.Add(value); panel.Children.Add(end); panel.Children.Add(list);
        var dialog = new ContentDialog { Title = "自动触发", Content = panel, PrimaryButtonText = "添加", SecondaryButtonText = "移除所选", CloseButtonText = "完成", XamlRoot = XamlRoot };
        var choice = await dialog.ShowAsync();
        if (choice == ContentDialogResult.Primary)
        {
            var triggerKind = (SceneTriggerKind)kind.SelectedIndex;
            var requiresValue = triggerKind is SceneTriggerKind.ProcessStarted or SceneTriggerKind.ProcessStopped or SceneTriggerKind.ForegroundProcess or SceneTriggerKind.WifiSsid or SceneTriggerKind.TimeRange;
            if (!requiresValue || !string.IsNullOrWhiteSpace(value.Text))
                _triggers.Add(new SceneTrigger { Kind = triggerKind, Value = value.Text.Trim(), EndValue = end.Text.Trim() });
            RefreshTriggerSummary();
        }
        else if (choice == ContentDialogResult.Secondary && list.SelectedIndex >= 0)
        {
            _triggers.RemoveAt(list.SelectedIndex);
            RefreshTriggerSummary();
        }
    }

    private void RefreshTriggerSummary() => TriggerSummary.Text = _triggers.Count == 0 ? "未配置自动触发" : string.Join(" · ", _triggers.Select(DescribeTrigger));
    private static string DescribeTrigger(SceneTrigger trigger) => trigger.Kind switch
    {
        SceneTriggerKind.ProcessStarted => $"启动 {trigger.Value}",
        SceneTriggerKind.ProcessStopped => $"退出 {trigger.Value}",
        SceneTriggerKind.ForegroundProcess => $"前台 {trigger.Value}",
        SceneTriggerKind.AcPower => "接通电源",
        SceneTriggerKind.BatteryPower => "使用电池",
        SceneTriggerKind.WifiSsid => $"Wi-Fi {trigger.Value}",
        SceneTriggerKind.TimeRange => $"{trigger.Value}-{trigger.EndValue}",
        SceneTriggerKind.ExternalMonitor => "外接显示器",
        _ => trigger.Kind.ToString(),
    };

    private async void OnCaptureLayout(object sender, RoutedEventArgs e)
    {
        var captured = AppServices.Windows.CaptureVisible();
        _layouts.Clear();
        _layouts.AddRange(captured);
        CaptureLayoutButton.Content = captured.Count == 0 ? "未检测到可记录窗口" : $"已记录 {captured.Count} 个窗口布局";
        if (captured.Count == 0)
            await new ContentDialog { Title = "未记录窗口", Content = "请先打开需要记录的程序窗口。", CloseButtonText = "完成", XamlRoot = XamlRoot }.ShowAsync();
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
        var display = DisplayCombo.SelectedIndex switch { 1 => "extend", 2 => "clone", 3 => "external", 4 => "internal", _ => (string?)null };
        var audio = AudioCombo.SelectedValue?.ToString();

        var updated = _scene with
        {
            Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "未命名场景" : NameBox.Text.Trim(),
            Icon = string.IsNullOrWhiteSpace(IconBox.Text) ? "🗂" : IconBox.Text.Trim(),
            Apps =
            [
                .. LaunchApps.Select(a => a.ToRef()),
                .. CloseApps.Select(a => a.ToRef()),
            ],
            Proxy = proxy,
            PowerPlan = power,
            DisplayMode = display,
            AudioDeviceId = string.IsNullOrWhiteSpace(audio) ? null : audio,
            Actions = [.. SceneActions.Select(a => new ActionInvocation { ActionId = a.ActionId, Params = a.Params })],
            Triggers = [.. _triggers],
            WindowLayouts = [.. _layouts],
            RestoreWhenStopped = RestoreToggle.IsOn
                ? LaunchApps.Select(a => a.Match).ToList()
                : [],
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
        var manual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new ListView { SelectionMode = ListViewSelectionMode.Multiple, MaxHeight = 300 };

        void AddRow(RunningApp app, bool selected)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            var image = new Image { Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(image);
            row.Children.Add(new TextBlock { Text = app.Name, VerticalAlignment = VerticalAlignment.Center });
            var item = new ListViewItem { Content = row, Tag = app.Name };
            list.Items.Add(item);
            _ = SetIconAsync(image, app.Path);
            if (selected) list.SelectedItems.Add(item);
        }

        void Populate(int sourceMode)
        {
            list.Items.Clear();
            list.SelectedItems.Clear();

            // 手动/路径添加的项固定在最前、默认勾选。
            foreach (var m in manual.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                AddRow(new RunningApp(m, null), selected: true);

            IEnumerable<RunningApp> apps = sourceMode switch
            {
                1 => AppServices.Processes.RunningProcessNames().Select(n => new RunningApp(n, null)),
                2 => ProgramCatalog.GetAll().Select(x => new RunningApp(Path.GetFileName(x.Path), x.Path)),
                _ => AppServices.Processes.VisibleWindowApps(),
            };
            var materialized = apps.ToList();
            if (sourceMode == 0 && materialized.Count == 0)
                materialized = AppServices.Processes.RunningProcessNames().Select(n => new RunningApp(n, null)).ToList();

            var ordered = materialized
                .Where(a => !safety.IsProtected(a.Name) && !manual.Contains(a.Name))
                .GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var a in ordered)
                AddRow(a, selected: false);
        }

        var input = new TextBox { PlaceholderText = "手动输入进程名或路径，如 wechat.exe", Width = 320 };
        var addBtn = new Button { Content = "添加" };
        var sourceCombo = new ComboBox { MinWidth = 150, SelectedIndex = 0 };
        foreach (var label in new[] { "当前窗口", "所有进程", "已安装程序" }) sourceCombo.Items.Add(label);

        void CommitManual()
        {
            var name = NormalizeManualEntry(input.Text);
            if (name is null || safety.IsProtected(name)) { input.Text = string.Empty; return; }
            manual.Add(name);
            input.Text = string.Empty;
            Populate(sourceCombo.SelectedIndex);
        }

        addBtn.Click += (_, _) => CommitManual();
        input.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter) CommitManual(); };
        sourceCombo.SelectionChanged += (_, _) => Populate(sourceCombo.SelectedIndex);

        var inputRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        inputRow.Children.Add(input);
        inputRow.Children.Add(addBtn);

        var header = new Grid { ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(inputRow, 0);
        Grid.SetColumn(sourceCombo, 1);
        header.Children.Add(inputRow);
        header.Children.Add(sourceCombo);

        var panel = new StackPanel { Spacing = 10, MinWidth = 440 };
        panel.Children.Add(header);
        panel.Children.Add(list);

        Populate(sourceMode: 0);

        var dialog = new ContentDialog
        {
            Title = "选择要关闭的程序",
            Content = panel,
            PrimaryButtonText = "添加",
            CloseButtonText = "取消",
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            return list.SelectedItems.Cast<ListViewItem>().Select(i => (string)i.Tag).ToList();
        return [];
    }

    /// <summary>
    /// 规范化手动输入：接受进程名或完整路径（.lnk 会解析到目标 exe），统一成 <c>xxx.exe</c> 形式；空输入返回 null。
    /// </summary>
    private static string? NormalizeManualEntry(string? raw)
    {
        var text = raw?.Trim().Trim('"');
        if (string.IsNullOrEmpty(text)) return null;

        if (text.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            var target = ShortcutResolver.ResolveTarget(text);
            if (!string.IsNullOrWhiteSpace(target)) text = target;
        }

        var name = Path.GetFileName(text);
        if (string.IsNullOrEmpty(name)) name = text;
        if (string.IsNullOrEmpty(Path.GetExtension(name)))
            name += ".exe";
        return name;
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
        LaunchMode = app.LaunchMode;
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
    public AppLaunchMode LaunchMode { get; set; }
    public string LaunchModeLabel => LaunchMode == AppLaunchMode.Minimized ? "最小化" : "正常打开";

    public AppRef ToRef() => new()
    {
        Match = Match,
        DisplayName = DisplayName,
        Path = Path,
        Disposition = Disposition,
        LaunchMode = LaunchMode,
    };
}

public sealed class ProgramPickerItem
{
    public ProgramPickerItem(ProgramCandidate candidate) => Candidate = candidate;
    public ProgramCandidate Candidate { get; }
    public string Name => Candidate.Name;
    public override string ToString() => $"{Candidate.Name}    · {Candidate.SourceLabel}";
}
