using Microsoft.UI.Xaml;

namespace GearShift.App.Services;

/// <summary>Applies the app theme at runtime to the window root.</summary>
public static class ThemeService
{
    public static ElementTheme Parse(string value) => value switch
    {
        "Light" => ElementTheme.Light,
        "Dark" => ElementTheme.Dark,
        _ => ElementTheme.Default,
    };

    public static void Apply(string value)
    {
        if (App.MainWindow?.Content is FrameworkElement root)
            root.RequestedTheme = Parse(value);
    }
}
