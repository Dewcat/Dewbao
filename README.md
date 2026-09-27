<h1 align="center">DewBao</h1>

<h3 align="center">真正是您家庭的一员</h3>

一个基于 **C# / .NET 8 / Windows Forms / WebView2** 的 Windows 桌面悬浮播放器，默认打开哔哩哔哩。通过置顶窗口、透明度调节和全局快捷键，在使用其他应用时控制网页视频。

## 功能

- **悬浮置顶**：拖动标题栏移动窗口，拖动边缘调整大小，可锁定视频区域为 16:9。
- **网页播放**：在地址栏输入视频页面地址，按 Enter 打开。
- **全局快捷键**：显示或隐藏窗口、播放或暂停、快进快退、调节音量和透明度。
- **鼠标穿透**：隐藏窗口外框，让鼠标操作落到下方应用。
- **沉浸模式**：隐藏标题栏与外框，同时启用鼠标穿透，锁定窗口交互。
- **托盘常驻**：关闭窗口时隐藏到托盘，通过托盘菜单恢复、打开设置或退出。
- **保存配置**：记住窗口位置、大小、上次网址、透明度和快捷键；每次启动会关闭沉浸模式与鼠标穿透，便于直接操作。
- **单实例运行**：重复启动时提示检查系统托盘。

## 运行

运行环境：Windows 10 / 11、.NET 8 Desktop Runtime，以及 Microsoft Edge WebView2 Runtime。

当前仓库包含 Debug 和 Release 构建目录。下载或克隆整个仓库后，可从稳定版本目录启动：

```text
DewBao/bin/Release/net8.0-windows/DewBao.exe
```

请保留同目录下的 DLL、JSON 文件和 `runtimes` 文件夹，不要只复制 EXE。程序清单要求管理员权限，启动时会出现 Windows UAC 提示。

1. 启动后在地址栏输入 B 站视频链接，按 Enter。
2. 在网页中开始播放，调整窗口位置、大小与透明度。
3. 通过标题栏设置按钮或托盘的「打开设置」自定义快捷键。
4. 点击关闭按钮仅隐藏窗口；需要彻底退出时，在托盘菜单选择「退出」。

## 默认快捷键

| 操作 | 快捷键 |
| --- | --- |
| 显示 / 隐藏窗口 | `Ctrl + Alt + H` |
| 播放 / 暂停 | `Ctrl + Alt + P` |
| 快进 5 秒 | `Ctrl + Alt + →` |
| 快退 5 秒 | `Ctrl + Alt + ←` |
| 音量增加 10 个百分点 | `Ctrl + Alt + ↑` |
| 音量减少 10 个百分点 | `Ctrl + Alt + ↓` |
| 提高不透明度（更不透明） | `Ctrl + Alt + ]` |
| 降低不透明度（更透明） | `Ctrl + Alt + [` |
| 切换鼠标穿透 | `Ctrl + Alt + Shift + T` |
| 切换沉浸模式 | `Ctrl + Alt + Shift + I` |

开启鼠标穿透或沉浸模式后，使用对应快捷键退出。如果两者都已开启，需要分别关闭才能恢复窗口交互。快捷键可在设置中修改，修改后以自己的配置为准。

## 从源码构建

安装 .NET 8 SDK，在 Windows 上执行：

```powershell
git clone https://github.com/Dewcat/Dewbao.git
cd Dewbao
dotnet restore .\Dewbao.sln
dotnet build .\Dewbao.sln -c Debug
Start-Process .\DewBao\bin\Debug\net8.0-windows\DewBao.exe -Verb RunAs
```

也可以用支持 .NET 8 的 Visual Studio 打开 `Dewbao.sln`，使用 Windows Forms 开发环境构建。

### 版本与发布约定

日常更改先在 Debug 版本验证，功能确认稳定后再构建 Release：

```powershell
dotnet build .\Dewbao.sln -c Release
```

| 用途 | 目录 |
| --- | --- |
| 开发验证 | `DewBao/bin/Debug/net8.0-windows` |
| 稳定版本 | `DewBao/bin/Release/net8.0-windows` |

分发时从当前稳定的 Release 目录复制完整运行文件。根目录 `artifacts` 不作为当前版本来源。

## 配置与缓存

| 内容 | 路径 |
| --- | --- |
| 用户配置 | `%APPDATA%\DewBao\settings.json` |
| WebView2 浏览器数据 | `%APPDATA%\DewBao\WebView2Cache` |

需要重置配置时，先从托盘退出程序，再备份并删除 `settings.json`，下次启动会使用默认设置。浏览器数据可能包含登录状态，请勿将其作为发布文件上传。

## 常见问题

- **WebView2 初始化失败**：按程序提示安装 WebView2 Runtime 后重新启动。
- **提示程序已在运行，但找不到窗口**：检查系统托盘，双击图标或按显示 / 隐藏快捷键。
- **鼠标无法操作窗口**：检查鼠标穿透和沉浸模式是否开启，用对应快捷键关闭。
- **播放快捷键无效**：先确认页面的视频已经加载。当前控制逻辑针对主页面中的第一个 HTML `video` 元素，嵌套播放器或特殊页面不一定适用。
- **游戏中无法显示或响应快捷键**：独占全屏、游戏输入处理或快捷键冲突可能影响效果，可尝试窗口化 / 无边框窗口模式及其他快捷键组合。

## 源码结构

```text
Dewbao.sln                  解决方案
DewBao/
  Program.cs                程序入口与单实例检查
  MainForm.cs               悬浮窗口、网页播放、托盘与交互模式
  SettingsForm.cs           快捷键和窗口设置界面
  AppSettings.cs            配置模型与 JSON 持久化
  GlobalKeyboardHook.cs     全局键盘钩子
  NativeMethods.cs          Windows 原生接口
  app.manifest              管理员权限与系统兼容性声明
```
