# GearShift

**v0.1.0** · 一个 Windows 桌面「场景切换」工具：右键任务栏托盘图标，一键在**游戏 / 办公 / 会议**等场景间切换——自动关闭/启动程序、开关系统代理、切电源计划，切回时自动恢复。

## 直接运行（发布版）

从 [**Releases**](https://github.com/zhnq/gearshift/releases/latest) 下载 `GearShift-0.1.0-win-x64.zip`，解压后双击 **`GearShift.exe`**。

首次运行前需各装一次以下两个运行时（一次安装，后续版本通用）：
- [.NET 10 桌面运行时 (x64)](https://dotnet.microsoft.com/download/dotnet/10.0) — 页面选 **.NET Desktop Runtime**
- [Windows App SDK 运行时 (x64)](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads) — 下载 **2.2 的 Runtime（Redistributable）**

要求 Windows 10 1809+ / Windows 11（x64）。

> 想要免安装、双击即跑的版本？用自包含方式自行发布：
> `dotnet publish src/GearShift.App -c Release -r win-x64 -p:Platform=x64 -p:WindowsAppSDKSelfContained=true --self-contained true`（体积约 85MB）。

## 核心理念：声明式目标状态

一个场景不是"一串进入动作 + 一串退出动作"，而是一份**期望的系统状态**描述。
切换 = 引擎对比「当前状态」与「目标状态」，只执行差异。

- **恢复零成本**：从游戏切回办公，就是声明办公的目标状态，该开的开、该关的关，无需 undo 记账。
- **绝对安全**：场景没提到的进程/设置一律不碰。
- **幂等**：目标已满足时切换是空操作。

详见 `src/GearShift.Core/Engine/DiffEngine.cs`。

## 技术栈

- **.NET 10**（`net10.0-windows`）
- **GearShift.Core** —— 纯后端类库，不依赖 UI，单元测试覆盖
- **WinUI 3 / Windows App SDK**（应用外壳，开发中）+ H.NotifyIcon（托盘）
- 系统代理：注册表 + WinINet 广播；电源计划：`powercfg`；进程：`System.Diagnostics`

## 目录结构

```
src/GearShift.Core/          纯逻辑，可单测
  Models/       Scene, AppRef, 枚举              —— 场景数据模型
  Engine/       DiffEngine, SceneSwitcher, 抽象  —— 声明式差异引擎 + 切换编排
  Safety/       SafetyList                       —— 关键进程永不可杀名单
  System/       ProcessManager, SystemProxy,     —— Windows 系统动作实现
                PowerPlanManager, WindowsSystemProbe
  Actions/      NullActionRunner                 —— 动作插件运行器（占位，待接入）
  Storage/      SceneStore                       —— scenes.json 读写
tests/GearShift.Core.Tests/  xunit 测试（引擎/切换器/存储）
```

配置文件：`%AppData%\GearShift\scenes.json`

## 构建与测试

```powershell
dotnet build
dotnet test        # 引擎正确性、安全名单、代理比对、存储往返
```

## 设计原型

完整 UI 原型（16 个界面 × 浅/深 = 32 画板，WinUI + Fluent Design）在 Figma：
文件 key `REDACTED`。

## 进度

- [x] Core 领域模型 + 声明式引擎（13 项测试通过）
- [x] 系统动作：进程启停 / 系统代理 / 电源计划
- [x] **动作插件系统**：manifest + 库扫描 + PowerShell 运行器（脚本动作真实执行；首运行种入示例插件）
- [x] JSON 配置存储（scenes.json / settings.json / actions/）
- [x] WinUI 3 应用：托盘(当前场景勾选、关窗到托盘、切换气泡通知)
- [x] 核心页面（原型保真）：场景总览 / 场景编辑(增删程序·代理·电源·动作插件·存盘) / 动作库 / 设置
- [x] 设置可用项：主题切换、开机自启、默认场景、安全名单查看、**以管理员身份重启**
- [x] 打磨：单实例(不重复托盘)、关窗到托盘、真实可见窗口过滤、快照新建场景、默认场景启动自动应用、clean-ram 真实实现(EmptyWorkingSet)
- [x] 插件生态 UI 闭环：**导入插件**(.zip 包 + 信任确认脚本预览)、**插件动作参数配置**、场景图标可编辑。示例包见 `examples/plugins/hello-plugin.zip`
- [x] **v0.1.0 发布就绪**：动作启用/禁用开关落地、版本号、LICENSE、应用图标、**自包含打包**(`dist/`)
- [ ] 后续(0.2+)：真实专注助手/音频脚本、插件 read 相位状态比对、布局细节精修、多分辨率图标

## 分发（可选）

自包含发布（免装 Windows App SDK 运行时）：
```powershell
dotnet publish src/GearShift.App/GearShift.App.csproj -c Release -r win-x64 `
  -p:Platform=x64 -p:WindowsAppSDKSelfContained=true --self-contained true
```

## 运行

```powershell
dotnet run --project src/GearShift.App -p:Platform=x64
```
或直接运行 `src/GearShift.App/bin/x64/Debug/net10.0-windows10.0.19041.0/GearShift.App.exe`。
非打包 WinUI 3 应用运行需 **Windows App SDK 运行时**；若报缺运行时，安装后重试（或后续改为 self-contained 打包免安装）。

## 运行要求

杀部分程序 / 改服务需管理员权限（`app.manifest` 将设 `requireAdministrator`）。
WinUI 3 非打包应用运行需安装 Windows App SDK 运行时。
