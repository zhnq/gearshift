using GearShift.Core.Models;

namespace GearShift.App.Services;

/// <summary>First-run seed data mirroring the design prototype. Users edit these to real programs.</summary>
public static class DefaultScenes
{
    public static IReadOnlyList<Scene> Build() =>
    [
        new Scene
        {
            Id = "game",
            Name = "游戏模式",
            Icon = "🎮",
            Proxy = TriState.Off,
            PowerPlan = "high",
            Apps =
            [
                new AppRef { Match = "steam.exe", Disposition = AppDisposition.EnsureRunning, DisplayName = "Steam" },
                new AppRef { Match = "outlook.exe", Disposition = AppDisposition.EnsureClosed, DisplayName = "Outlook" },
                new AppRef { Match = "wxwork.exe", Disposition = AppDisposition.EnsureClosed, DisplayName = "企业微信" },
            ],
        },
        new Scene
        {
            Id = "office",
            Name = "办公模式",
            Icon = "💼",
            Proxy = TriState.On,
            PowerPlan = "balanced",
            Apps =
            [
                new AppRef { Match = "steam.exe", Disposition = AppDisposition.EnsureClosed, DisplayName = "Steam" },
                new AppRef { Match = "ts3client_win64.exe", Disposition = AppDisposition.EnsureClosed, DisplayName = "TeamSpeak" },
            ],
        },
        new Scene
        {
            Id = "meeting",
            Name = "会议模式",
            Icon = "🎧",
            Apps =
            [
                new AppRef { Match = "steam.exe", Disposition = AppDisposition.EnsureClosed, DisplayName = "Steam" },
            ],
        },
    ];
}
