# BetterGI macOS 版

在 Mac 上通过 **GeForce NOW** 云游戏玩原神时使用的 BetterGI。界面和功能与 Windows 版相同：
一条龙（清日常、刷体力）、地图追踪（采集、挖矿）、调度器、自动拾取 / 剧情 / 钓鱼等实时触发、独立任务、JS 脚本、键鼠录制、演奏……

> 目前只提供 Apple Silicon（M1 及更新的芯片）版本，需要 macOS 12 或更高版本。

## 安装

1. 在 GitHub Actions 的 **macOS** 工作流（或 Release）中下载 `BetterGI-macOS.zip`，解压得到 `BetterGI.app`，拖到「应用程序」文件夹。
2. 应用没有经过 Apple 公证，第一次打开前在「终端」中执行一次：

   ```bash
   xattr -cr /Applications/BetterGI.app
   ```

   然后双击打开。如果仍提示「无法验证开发者」，到「系统设置 → 隐私与安全性」底部点击「仍要打开」。

## 第一次使用

打开后在「启动」页按顺序完成：

1. **授予权限**：点击三个「去授权」，在系统设置中打开 BetterGI 的开关。
   | 权限 | 用途 |
   | --- | --- |
   | 屏幕录制 | 截取游戏画面进行识别 |
   | 辅助功能 | 模拟键盘鼠标操作 |
   | 输入监控 | 全局快捷键、键鼠录制 |

   授权后需要**退出并重新打开** BetterGI 才会生效。
2. **打开 GeForce NOW 并进入原神**，然后点击「▶ 启动」。状态栏显示「已绑定」即表示成功。
3. 点击「📷 截图预览」检查截取到的画面：应该正好是游戏画面，没有标题栏或黑边，并显示「16:9 ✓」。

## GeForce NOW 设置建议

- 游戏内分辨率选 **1920×1080**（16:9）；GeForce NOW 窗口尽量大，或者全屏。
- BetterGI 运行任务时**不要移动鼠标、不要遮挡游戏窗口**，也不要把游戏窗口最小化。
- 网络延迟会影响操作，建议使用有线网络或 5GHz Wi-Fi。
- 用浏览器（Safari / Chrome）玩 GeForce NOW 时，在「启动 → 高级设置」中把「窗口所属应用」改为浏览器名称，
  并在「窗口标题包含」中填写标签页标题的关键字；也可以点「刷新」后手动选择窗口再点「绑定此窗口」。

## 常用功能

| 想做的事 | 在哪里 |
| --- | --- |
| 清日常（每日委托、领奖励、合成树脂、刷秘境、尘歌壶等） | 「一条龙」：点 ＋ 添加任务，再点 ▶ 一键执行 |
| 刷体力（秘境、地脉花、首领） | 「独立任务」或「一条龙」中的秘境配置 |
| 采集 / 挖矿 / 锄地 | 先在「脚本仓库」更新仓库并订阅 `pathing` 中的路线，再到「调度器」新建配置组、添加路线后运行 |
| 自动拾取、自动剧情、自动钓鱼等 | 「实时触发」中打开对应开关（启动后生效） |
| 快捷键 | 「快捷键」页；Mac 键盘上的 F1–F12 需要同时按 fn |

### 命令行启动（定时自动清日常）

与 Windows 版相同的启动参数也可以在 macOS 上使用，例如每天定时执行一条龙：

```bash
open -a BetterGI --args startOneDragon
```

可以配合「快捷指令」的自动化或 `launchd` 定时运行。

## 调整

在「启动 → 高级设置」中可以调整：

- **顶部裁剪**：截图预览中出现标题栏或黑边时调整（-1 为自动）。
- **鼠标移动倍率**：视角转动过快或过慢时调整（地图追踪依赖视角转动的准确度）。
- **按键按住时长**：云游戏偶尔丢失按键时调大。

这些设置保存在 `~/Library/Application Support/BetterGI/User/mac_config.json`。

## 数据位置

配置、脚本、日志都在 `~/Library/Application Support/BetterGI`：

- `User/`：配置、一条龙、调度器、订阅的脚本（地图追踪 `User/AutoPathing`，JS 脚本 `User/JsScript`）
- `log/`：运行日志，反馈问题时请附上
- `Repos/`：脚本仓库

## 与 Windows 版的差异

- 游戏画面上的遮罩（识别框、日志浮窗）暂不显示，日志在主界面下方查看。
- 依赖 WebView2 的页面（如在线脚本仓库网页）改为原生页面或用浏览器打开。
- AI 识别使用 CPU 推理（Apple Silicon 上速度足够）。
- 不支持启动 / 关闭本地原神客户端、桌面分身等 Windows 专属功能。
- 每次更新应用后签名会变化，系统可能要求重新授予权限：在系统设置中先删除旧的 BetterGI 再重新添加。

## 从源码编译

需要 .NET 8 SDK、Xcode 命令行工具、CMake 和 Ninja（`brew install cmake ninja`）。

```bash
# 1. 编译 OpenCvSharp 原生库（约 30 分钟，只需一次）
BetterGenshinImpact.Mac/native/build-opencvsharp.sh /tmp/opencv BetterGenshinImpact.Mac/native/osx-arm64

# 2. 发布并打包
dotnet publish BetterGenshinImpact.Mac.App -c Release -r osx-arm64 --self-contained -o /tmp/bgi-publish
BetterGenshinImpact.Mac.App/packaging/make-app.sh /tmp/bgi-publish dist
```

工程结构：

- `BetterGenshinImpact.Mac`：直接编译原版的核心代码（识别、任务、ViewModel），用 macOS 实现替换 Windows 专属部分（截图、键鼠、窗口、热键）。
- `BetterGenshinImpact.Mac.App`：Avalonia 图形界面，页面由 `tools/wpf2avalonia.py` 从原版 WPF 页面转换生成（原版页面更新后运行 `tools/generate-pages.sh`）。
- `Test/BetterGenshinImpact.Mac.UnitTest`：在 macOS / Linux 上运行原版的识别单元测试。
