using GearShift.Core.Models;
using GearShift.Core.Storage;
using Xunit;

namespace GearShift.Core.Tests;

public class SceneStoreTests
{
    [Fact]
    public void Load_returns_empty_document_when_file_missing()
    {
        var store = new SceneStore(Path.Combine(Path.GetTempPath(), $"ss-missing-{Guid.NewGuid():N}.json"));

        var doc = store.Load();

        Assert.Empty(doc.Scenes);
        Assert.Null(doc.ActiveSceneId);
    }

    [Fact]
    public void Save_then_load_round_trips_scene_content()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ss-rt-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SceneStore(path);
            var doc = new SceneDocument
            {
                ActiveSceneId = "game",
                Scenes =
                [
                    new Scene
                    {
                        Id = "game", Name = "游戏模式", Icon = "🎮", Proxy = TriState.Off, PowerPlan = "high",
                        Apps =
                        [
                            new AppRef { Match = "steam.exe", Disposition = AppDisposition.EnsureRunning, Path = @"C:\steam.exe" },
                            new AppRef { Match = "outlook.exe", Disposition = AppDisposition.EnsureClosed },
                        ],
                        Actions = [new ActionInvocation { ActionId = "focus-assist", Params = { } }],
                    },
                ],
            };

            store.Save(doc);
            var loaded = store.Load();

            Assert.True(File.Exists(path));
            var scene = Assert.Single(loaded.Scenes);
            Assert.Equal("游戏模式", scene.Name);
            Assert.Equal("🎮", scene.Icon);
            Assert.Equal(TriState.Off, scene.Proxy);
            Assert.Equal("high", scene.PowerPlan);
            Assert.Equal(2, scene.Apps.Count);
            Assert.Equal(AppDisposition.EnsureClosed, scene.Apps[1].Disposition);
            Assert.Equal("game", loaded.ActiveSceneId);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
