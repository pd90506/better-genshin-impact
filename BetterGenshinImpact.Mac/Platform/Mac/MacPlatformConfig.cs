using System;
using System.Collections.Generic;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using BetterGenshinImpact.Core.Config;
using Newtonsoft.Json;

namespace BetterGenshinImpact.Platform.Mac;

/// <summary>
/// macOS 版特有的设置，保存在 User/mac_config.json，与原版 config.json 分开，避免影响 Windows 版
/// </summary>
public partial class MacPlatformConfig : ObservableObject
{
    private static readonly object FileLock = new();
    private static MacPlatformConfig? _instance;

    public static string FilePath => Path.Combine(Global.Absolute("User"), "mac_config.json");

    public static MacPlatformConfig Instance
    {
        get
        {
            if (_instance != null)
            {
                return _instance;
            }

            lock (FileLock)
            {
                _instance ??= Load();
            }

            return _instance;
        }
    }

    /// <summary>
    /// 游戏窗口所属应用名包含的关键字（任一匹配即可）。默认 GeForce NOW 客户端；用浏览器玩时可加入 "Google Chrome" / "Safari"
    /// </summary>
    [ObservableProperty]
    private List<string> _windowOwnerKeywords = ["GeForce NOW"];

    /// <summary>
    /// 窗口标题需包含的关键字，留空不限制。用浏览器玩时建议填 "GeForce NOW"
    /// </summary>
    [ObservableProperty]
    private List<string> _windowTitleKeywords = [];

    /// <summary>
    /// 窗口顶部需要裁掉的高度（point）。-1 表示自动：全屏为 0，窗口模式为标题栏高度 28
    /// </summary>
    [ObservableProperty]
    private double _topInset = -1;

    /// <summary>
    /// 是否在内容区域中自动裁出居中的 16:9 画面（GeForce NOW 在非 16:9 窗口中会补黑边）
    /// </summary>
    [ObservableProperty]
    private bool _autoFit16By9 = true;

    /// <summary>
    /// 手动指定游戏画面在窗口内的区域（point，相对窗口左上角）。宽或高为 0 时不启用
    /// </summary>
    [ObservableProperty]
    private double _manualCropX;

    [ObservableProperty]
    private double _manualCropY;

    [ObservableProperty]
    private double _manualCropWidth;

    [ObservableProperty]
    private double _manualCropHeight;

    /// <summary>
    /// 相对鼠标移动（视角转动）的倍率。GeForce NOW 对鼠标增量有自己的灵敏度，视角转动不准时调整
    /// </summary>
    [ObservableProperty]
    private double _mouseMoveMultiplier = 1.0;

    /// <summary>
    /// 后台输入：游戏不在前台时，把按键直接投递给游戏进程（CGEventPostToPid）。部分情况下 GeForce NOW 可能不响应
    /// </summary>
    [ObservableProperty]
    private bool _backgroundInputEnabled = true;

    /// <summary>
    /// 按键按下与抬起之间的间隔（毫秒）。串流有延迟，过短的按键可能被游戏忽略
    /// </summary>
    [ObservableProperty]
    private int _keyPressHoldMs = 40;

    /// <summary>
    /// 截图帧率上限
    /// </summary>
    [ObservableProperty]
    private int _captureMaxFps = 30;

    private static MacPlatformConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var config = JsonConvert.DeserializeObject<MacPlatformConfig>(File.ReadAllText(FilePath));
                if (config != null)
                {
                    config.PropertyChanged += (_, _) => config.Save();
                    return config;
                }
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"读取 mac_config.json 失败：{e.Message}");
        }

        var created = new MacPlatformConfig();
        created.Save();
        created.PropertyChanged += (_, _) => created.Save();
        return created;
    }

    public void Save()
    {
        lock (FileLock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}
