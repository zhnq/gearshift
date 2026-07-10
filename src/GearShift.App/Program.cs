using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using WinRT;
using GearShift.App.Services;

namespace GearShift.App;

/// <summary>
/// Custom entry point that enforces a single running instance. A second launch hands its activation
/// to the first instance (which surfaces its window) and exits, so there's only ever one tray icon.
/// </summary>
public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ComWrappersSupport.InitializeComWrappers();
        var requestedSceneId = TryGetSceneId(args);

        var mainInstance = AppInstance.FindOrRegisterForKey("GearShift-SingleInstance");
        if (!mainInstance.IsCurrent)
        {
            if (!string.IsNullOrWhiteSpace(requestedSceneId)) SceneActivationRequest.Write(requestedSceneId);
            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            mainInstance.RedirectActivationToAsync(activation).AsTask().GetAwaiter().GetResult();
            return;
        }

        mainInstance.Activated += OnRedirected;

        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }

    public static string? InitialSceneId { get; private set; }

    private static string? TryGetSceneId(string[] args)
    {
        var index = Array.FindIndex(args, x => string.Equals(x, "--scene", StringComparison.OrdinalIgnoreCase));
        var id = index >= 0 && index + 1 < args.Length ? args[index + 1].Trim() : null;
        if (!string.IsNullOrWhiteSpace(id)) InitialSceneId = id;
        return id;
    }

    private static void OnRedirected(object? sender, AppActivationArguments args)
    {
        var window = App.MainWindow;
        window?.DispatcherQueue.TryEnqueue(() =>
        {
            window.AppWindow.Show();
            window.Activate();
        });
    }
}
