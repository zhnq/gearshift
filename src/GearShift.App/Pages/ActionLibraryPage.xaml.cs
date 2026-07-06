using GearShift.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace GearShift.App.Pages;

public sealed partial class ActionLibraryPage : Page
{
    public ActionLibraryPage()
    {
        InitializeComponent();
        RefreshList();
    }

    private void RefreshList() => ActionsList.ItemsSource = ActionCatalog.Build(AppServices.Actions);

    private void OnActionToggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch { DataContext: ActionDescriptor { Id: { } id } } toggle)
            ActionState.SetEnabled(id, toggle.IsOn);
    }

    private async void OnImport(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        picker.FileTypeFilter.Add(".zip");

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        PluginInstaller.Candidate? candidate;
        try { candidate = PluginInstaller.Inspect(file.Path); }
        catch { candidate = null; }

        if (candidate is null)
        {
            await Info("无法导入", "这不是有效的插件包（缺少 action.json）。");
            return;
        }

        if (await ConfirmTrustAsync(candidate))
        {
            try
            {
                PluginInstaller.Install(candidate, AppServices.ActionsRoot);
                AppServices.Actions.Reload();
                RefreshList();
                await Info("已导入", $"「{candidate.Name}」已加入动作库，可在场景编辑页引用。");
            }
            catch (Exception ex)
            {
                await Info("导入失败", ex.Message);
            }
        }
    }

    private async Task<bool> ConfirmTrustAsync(PluginInstaller.Candidate candidate)
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = $"{candidate.Name} · {candidate.Id}",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        if (!string.IsNullOrWhiteSpace(candidate.Description))
            panel.Children.Add(new TextBlock
            {
                Text = candidate.Description,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });

        panel.Children.Add(new TextBlock
        {
            Text = "脚本内容 · 运行前请确认你信任来源：",
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
        });
        panel.Children.Add(new TextBox
        {
            Text = string.IsNullOrEmpty(candidate.ScriptText) ? "（无脚本 / 触发型动作）" : candidate.ScriptText,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12.5,
            MaxHeight = 240,
        });
        panel.Children.Add(new TextBlock
        {
            Text = "⚠ 运行第三方脚本存在风险，仅导入你信任的插件。默认以当前用户权限运行。",
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });

        var dialog = new ContentDialog
        {
            Title = "导入动作插件",
            Content = new ScrollViewer { Content = panel },
            PrimaryButtonText = "信任并导入",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task Info(string title, string message)
    {
        await new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "好的",
            XamlRoot = XamlRoot,
        }.ShowAsync();
    }
}
