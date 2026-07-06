using Microsoft.UI.Xaml;

namespace GearShift.App;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Services.AppServices.Initialize();
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
