using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using BetterGenshinImpact.GameTask;
using BetterGenshinImpact.GameTask.Runtime;
using BetterGenshinImpact.GameTask.Runtime.Win32;
using BetterGenshinImpact.Platform.Mac;
using BetterGenshinImpact.ViewModel.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

namespace BetterGenshinImpact.MacApp.Views.Pages;

/// <summary>
/// macOS 版首页：启动截图器、选择 GeForce NOW 窗口、检查权限、macOS 专属设置
/// </summary>
public partial class MacHomeViewModel : ObservableObject
{
    private readonly DispatcherTimer _timer;

    public MacHomeViewModel()
    {
        Home = App.GetService<HomePageViewModel>()!;
        Config = MacPlatformConfig.Instance;
        OwnerKeywords = string.Join(", ", Config.WindowOwnerKeywords);
        TitleKeywords = string.Join(", ", Config.WindowTitleKeywords);
        Refresh();
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) => Refresh());
        _timer.Start();
    }

    /// <summary>
    /// 原版首页 ViewModel：启动 / 停止截图器的命令与状态
    /// </summary>
    public HomePageViewModel Home { get; }

    public MacPlatformConfig Config { get; }

    [ObservableProperty]
    private string _windowStatus = "正在查找游戏窗口…";

    [ObservableProperty]
    private bool _windowFound;

    [ObservableProperty]
    private bool _screenRecordingGranted;

    [ObservableProperty]
    private bool _accessibilityGranted;

    [ObservableProperty]
    private bool _inputMonitoringGranted;

    [ObservableProperty]
    private Bitmap? _preview;

    [ObservableProperty]
    private string _previewInfo = string.Empty;

    [ObservableProperty]
    private string _ownerKeywords;

    [ObservableProperty]
    private string _titleKeywords;

    [ObservableProperty]
    private ObservableCollection<MacWindowInfo> _windows = [];

    [ObservableProperty]
    private MacWindowInfo? _selectedWindow;

    public bool AllPermissionsGranted => ScreenRecordingGranted && AccessibilityGranted && InputMonitoringGranted;

    partial void OnOwnerKeywordsChanged(string value) =>
        Config.WindowOwnerKeywords = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    partial void OnTitleKeywordsChanged(string value) =>
        Config.WindowTitleKeywords = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private void Refresh()
    {
        ScreenRecordingGranted = MacPermissions.ScreenRecording;
        AccessibilityGranted = MacPermissions.Accessibility;
        InputMonitoringGranted = MacPermissions.InputMonitoring;
        OnPropertyChanged(nameof(AllPermissionsGranted));

        var handle = TaskContext.Instance().IsInitialized ? TaskContext.Instance().GameHandle : 0;
        var window = handle != 0 ? MacWindows.Get((uint)handle) : null;
        if (window != null)
        {
            WindowFound = true;
            var rect = SystemControl.GetCaptureRect(handle);
            WindowStatus = $"已绑定：{window}（游戏画面 {rect.Width}×{rect.Height}）";
            return;
        }

        var hWnd = SystemControl.FindGenshinImpactHandle();
        window = hWnd != 0 ? MacWindows.Get((uint)hWnd) : null;
        WindowFound = window != null;
        WindowStatus = window != null
            ? $"已找到：{window}，点击「启动」开始"
            : "未找到游戏窗口：请先打开 GeForce NOW 并进入原神（浏览器游玩请修改下方的窗口匹配规则）";
    }

    [RelayCommand]
    private void RequestScreenRecording() => MacPermissions.RequestScreenRecording();

    [RelayCommand]
    private void RequestAccessibility() => MacPermissions.RequestAccessibility();

    [RelayCommand]
    private void RequestInputMonitoring() => MacPermissions.RequestInputMonitoring();

    [RelayCommand]
    private void RefreshWindows()
    {
        Windows = new ObservableCollection<MacWindowInfo>(MacWindows.ListCapturable());
        SelectedWindow = Windows.FirstOrDefault(w => w.WindowId == (uint)SystemControl.FindGenshinImpactHandle());
    }

    /// <summary>
    /// 手动绑定所选窗口（不按名称匹配时使用）
    /// </summary>
    [RelayCommand]
    private void BindSelectedWindow()
    {
        if (SelectedWindow == null)
        {
            return;
        }

        var service = App.GetService<GameRuntimeService>()!;
        if (service.IsRunning)
        {
            Wpf.Ui.Violeta.Controls.Toast.Warning("请先停止截图器");
            return;
        }

        var provider = App.GetService<Win32RuntimeProvider>()!;
        service.Start(provider.AttachTo((nint)SelectedWindow.WindowId));
    }

    /// <summary>
    /// 截一张游戏画面预览，确认裁剪区域是否正好是游戏画面（没有标题栏和黑边）
    /// </summary>
    [RelayCommand]
    private async Task CapturePreviewAsync()
    {
        var hWnd = TaskContext.Instance().IsInitialized && TaskContext.Instance().GameHandle != 0
            ? TaskContext.Instance().GameHandle
            : SelectedWindow != null ? (nint)SelectedWindow.WindowId : SystemControl.FindGenshinImpactHandle();
        if (hWnd == 0)
        {
            PreviewInfo = "没有可截图的窗口";
            return;
        }

        var result = await Task.Run(() =>
        {
            using var capture = new MacScreenCapture();
            capture.Start(hWnd);
            using var frame = capture.Capture();
            if (frame == null)
            {
                return ((byte[]?)null, "截图失败：请确认已授予「屏幕录制」权限，且窗口没有最小化");
            }

            using var small = frame.Frame.Width > 960 ? frame.Frame.Resize(new Size(960, 960 * frame.Frame.Height / frame.Frame.Width)) : frame.Frame.Clone();
            var info = $"游戏画面 {frame.Frame.Width}×{frame.Frame.Height}" +
                       (frame.Frame.Width * 9 == frame.Frame.Height * 16 ? "（16:9 ✓）" : "（不是 16:9，请调整裁剪设置）");
            return (small.ToBytes(".png"), info);
        });

        PreviewInfo = result.Item2;
        Preview = result.Item1 == null ? null : new Bitmap(new MemoryStream(result.Item1));
    }
}
