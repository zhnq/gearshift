# GearShift

GearShift 是一个 Windows 场景切换工具。你可以为办公、游戏、会议等使用场景预先设置需要运行或关闭的程序、系统代理、电源计划、显示器模式和音频设备，然后从托盘菜单一键切换。

![GearShift 图标](src/GearShift.App/Assets/app-icon.png)

## 主要功能

- 一键切换场景，自动启动、关闭或挂起指定程序
- 切换系统代理、电源计划、显示器拓扑和默认播放设备
- 记录并恢复窗口布局
- 支持程序、前台窗口、电源、Wi-Fi、时间和外接显示器触发
- 支持临时场景自动恢复、桌面快捷方式和 `Ctrl+Alt+1..9` 热键
- 支持 PowerShell 动作插件，可按需扩展场景操作

GearShift 使用声明式场景配置：每个场景只描述目标状态，切换时仅执行当前状态与目标状态之间的差异。未写入场景的程序和设置不会被修改，重复切换同一场景也不会重复执行无用操作。

## 安装

从 [Releases](https://github.com/zhnq/gearshift/releases/latest) 下载最新版安装程序。

运行要求：

- Windows 10 1809 或更高版本 / Windows 11
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Windows App SDK 2.2 Runtime](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads)

配置和场景数据保存在 `%AppData%\GearShift`。

## 开发

项目使用 .NET 10、WinUI 3 和 Windows App SDK。

```powershell
dotnet build GearShift.slnx
dotnet test tests/GearShift.Core.Tests/GearShift.Core.Tests.csproj
dotnet run --project src/GearShift.App -p:Platform=x64
```

生成自包含版本：

```powershell
dotnet publish src/GearShift.App/GearShift.App.csproj -c Release -r win-x64 `
  -p:Platform=x64 -p:WindowsAppSDKSelfContained=true --self-contained true
```

## License

[MIT](LICENSE)
