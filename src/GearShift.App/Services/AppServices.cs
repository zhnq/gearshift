using GearShift.Core.Actions;
using GearShift.Core.Engine;
using GearShift.Core.Models;
using GearShift.Core.Safety;
using GearShift.Core.Storage;
using GearShift.Core.System;

namespace GearShift.App.Services;

/// <summary>
/// Simple composition root: wires the Core backend to real Windows implementations and holds the
/// in-memory scene list. Seeds sensible defaults on first run so the UI is never empty.
/// </summary>
public static class AppServices
{
    private static readonly SceneStore Store = new();
    private static SceneDocument _document = new();

    public static List<Scene> Scenes { get; private set; } = [];
    public static string? ActiveSceneId { get; private set; }
    public static SceneSwitcher Switcher { get; private set; } = null!;
    public static ProcessManager Processes { get; private set; } = null!;
    public static ActionLibrary Actions { get; private set; } = null!;
    public static string ActionsRoot { get; private set; } = "";
    public static WindowLayoutManager Windows { get; private set; } = null!;
    public static AudioDeviceManager Audio { get; private set; } = null!;

    public static void Initialize()
    {
        _document = Store.Load();

        if (_document.Scenes.Count == 0)
        {
            _document = _document with { Scenes = DefaultScenes.Build() };
            Store.Save(_document);
        }

        Scenes = [.. _document.Scenes];
        ActiveSceneId = _document.ActiveSceneId;

        var baseDir = Path.GetDirectoryName(Store.FilePath)!;
        ActionsRoot = Path.Combine(baseDir, "actions");
        ExampleActions.SeedIfEmpty(ActionsRoot);
        Actions = new ActionLibrary(ActionsRoot);
        _ = ProgramCatalog.WarmAsync();

        Processes = new ProcessManager();
        Windows = new WindowLayoutManager();
        Audio = new AudioDeviceManager();
        var proxy = new SystemProxy();
        var power = new PowerPlanManager();
        var engine = new DiffEngine(new SafetyList(_document.ExtraProtectedProcesses));

        Switcher = new SceneSwitcher(
            engine,
            () => new WindowsSystemProbe(Processes, proxy, power),
            Processes, proxy, power,
            new ScriptActionRunner(Actions, ActionState.IsEnabled), Windows, new DisplayManager(), Audio);
    }

    public static async Task<SwitchResult> SwitchAsync(Scene scene, IProgress<StepOutcome>? progress = null)
    {
        var previous = ActiveSceneId;
        var result = await Switcher.SwitchAsync(scene, progress);
        ActiveSceneId = scene.Id;
        Persist();
        SceneRunHistory.Append(new SceneRunRecord(DateTimeOffset.Now, scene.Id, scene.Name, result.Outcomes));
        if (scene.RestoreWhenStopped.Count > 0 && !string.IsNullOrWhiteSpace(previous) && previous != scene.Id)
        {
            SettingsService.Current.PendingRestoreSceneId = previous;
            SettingsService.Current.PendingRestoreApps = scene.RestoreWhenStopped.ToList();
            SettingsService.Current.PendingRestoreObservedRunning = false;
            SettingsService.Save();
        }
        return result;
    }

    public static IReadOnlyList<SwitchStep> Preview(Scene scene)
        => new DiffEngine(new SafetyList(_document.ExtraProtectedProcesses))
            .BuildPlan(scene, new WindowsSystemProbe(Processes, new SystemProxy(), new PowerPlanManager()));

    public static void UpsertScene(Scene scene)
    {
        var index = Scenes.FindIndex(s => s.Id == scene.Id);
        if (index >= 0)
            Scenes[index] = scene;
        else
            Scenes.Add(scene);
        Persist();
    }

    public static void DeleteScene(string id)
    {
        Scenes.RemoveAll(s => s.Id == id);
        if (ActiveSceneId == id)
            ActiveSceneId = null;
        Persist();
    }

    public static Scene NewScene() => new()
    {
        Id = "scene-" + Guid.NewGuid().ToString("N")[..8],
        Name = "新场景",
        Icon = "🗂",
    };

    private static void Persist()
    {
        _document = _document with { Scenes = [.. Scenes], ActiveSceneId = ActiveSceneId };
        Store.Save(_document);
    }
}
