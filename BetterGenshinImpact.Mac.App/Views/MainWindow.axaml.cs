using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.MacApp.Views.Generated;
using BetterGenshinImpact.MacApp.Views.Pages;
using BetterGenshinImpact.ViewModel.Pages;
using Serilog.Events;
using Wpf.Ui;

namespace BetterGenshinImpact.MacApp.Views;

/// <summary>
/// 导航项：页面在第一次打开时创建，DataContext 为原版 ViewModel
/// </summary>
public sealed record NavItem(string Icon, string Title, Func<Control> Create, params Type[] WpfPageTypes)
{
    public Control? Page { get; set; }
}

public partial class MainWindow : Window
{
    private const int MaxLogChars = 60_000;
    private readonly List<NavItem> _items;
    private NavItem? _current;
    private readonly StringBuilder _log = new();
    private bool _logDirty;

    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = "macOS · " + Global.Version.Split('+')[0];

        _items =
        [
            new("🏠", "启动", () => new MacHomePage(), typeof(View.Pages.HomePage)),
            new("⚡", "实时触发", () => Page<TriggerSettingsPage, TriggerSettingsPageViewModel>(), typeof(View.Pages.TriggerSettingsPage)),
            new("🎯", "独立任务", () => Page<TaskSettingsPage, TaskSettingsPageViewModel>(), typeof(View.Pages.TaskSettingsPage)),
            new("🐉", "一条龙", () => Page<OneDragonFlowPage, OneDragonFlowViewModel>(), typeof(View.Pages.OneDragonFlowPage)),
            new("📋", "调度器", () => Page<ScriptControlPage, ScriptControlViewModel>(scroll: false), typeof(View.Pages.ScriptControlPage)),
            new("🗺️", "地图追踪", () => Page<MapPathingPage, MapPathingViewModel>(scroll: false), typeof(View.Pages.MapPathingPage)),
            new("📜", "JS 脚本", () => Page<JsListPage, JsListViewModel>(scroll: false), typeof(View.Pages.JsListPage)),
            new("📦", "脚本仓库", () => new ScriptRepoPage(), typeof(View.Windows.ScriptRepoWindow)),
            new("🖱️", "键鼠录制", () => Page<KeyMouseRecordPage, KeyMouseRecordPageViewModel>(scroll: false), typeof(View.Pages.KeyMouseRecordPage)),
            new("🎵", "演奏", () => Page<MusicPage, MusicPageViewModel>(scroll: false)),
            new("🧰", "操控辅助", () => Page<MacroSettingsPage, MacroSettingsPageViewModel>()),
            new("⌨️", "快捷键", () => Page<HotKeyPage, HotKeyPageViewModel>(scroll: false), typeof(View.Pages.HotKeyPage)),
            new("🎮", "游戏按键", () => Page<KeyBindingsSettingsPage, KeyBindingsSettingsPageViewModel>(), typeof(View.Pages.KeyBindingsSettingsPage)),
            new("🔔", "通知", () => Page<NotificationSettingsPage, NotificationSettingsPageViewModel>()),
            new("⚙️", "设置", () => Page<CommonSettingsPage, CommonSettingsPageViewModel>(), typeof(View.Pages.CommonSettingsPage)),
        ];

        NavList.ItemsSource = _items;
        NavList.SelectionChanged += (_, _) => ShowSelected();
        NavList.SelectedIndex = 0;

        NavigationService.NavigateHandler = Navigate;
        ToastHub.Shown += (kind, message) => Dispatcher.UIThread.Post(() => ShowToast(kind, message));

        ClearLogButton.Click += (_, _) =>
        {
            _log.Clear();
            LogText.Text = string.Empty;
        };
        foreach (var (level, text) in App.LogSink.Snapshot())
        {
            AppendLog(level, text);
        }

        App.LogSink.Emitted += (level, text) =>
        {
            lock (_log)
            {
                AppendLog(level, text);
            }
        };
        DispatcherTimer.Run(() =>
        {
            FlushLog();
            return true;
        }, TimeSpan.FromMilliseconds(200));

        var smokeDir = Environment.GetEnvironmentVariable("BGI_SMOKE_DIR");
        if (!string.IsNullOrEmpty(smokeDir))
        {
            Opened += (_, _) => _ = RunSmokeTestAsync(smokeDir);
        }
    }

    /// <summary>
    /// 界面冒烟测试：依次打开每个页面并截图，用于在没有 Mac 的环境中检查页面能否正常加载
    /// </summary>
    private async System.Threading.Tasks.Task RunSmokeTestAsync(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        await System.Threading.Tasks.Task.Delay(1500);
        for (var i = 0; i < _items.Count; i++)
        {
            NavList.SelectedIndex = i;
            await System.Threading.Tasks.Task.Delay(1200);
            var failed = PageHost.Content is TextBlock tb && tb.Text?.StartsWith("页面加载失败") == true;
            Console.WriteLine($"[smoke] {i:00} {_items[i].Title}: {(failed ? "FAILED" : "ok")}");
            if (failed)
            {
                Console.WriteLine(((TextBlock)PageHost.Content!).Text);
            }

            var size = new PixelSize((int)Bounds.Width, (int)Bounds.Height);
            using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(size);
            bitmap.Render(this);
            bitmap.Save(System.IO.Path.Combine(dir, $"{i:00}.png"));
        }

        Console.WriteLine("[smoke] done");
        Close();
    }

    private static Control Page<TPage, TViewModel>(bool scroll = true) where TPage : Control, new() where TViewModel : class
    {
        var page = new TPage { DataContext = App.GetService<TViewModel>() };
        return scroll ? new ScrollViewer { Content = page, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled } : page;
    }

    private void ShowSelected()
    {
        if (NavList.SelectedItem is not NavItem item)
        {
            return;
        }

        try
        {
            item.Page ??= item.Create();
            NotifyNavigation(_current, item);
            _current = item;
            PageHost.Content = item.Page;
        }
        catch (Exception e)
        {
            PageHost.Content = new TextBlock
            {
                Text = $"页面加载失败：{e.Message}\n\n{e}",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(24),
            };
        }
    }

    /// <summary>
    /// 原版 WPF-UI 导航时调用 ViewModel 的 OnNavigatedTo / OnNavigatedFrom（很多页面在这里加载数据）
    /// </summary>
    private static void NotifyNavigation(NavItem? from, NavItem to)
    {
        if (from != null && from != to && ViewModelOf(from) is Wpf.Ui.Abstractions.Controls.INavigationAware fromVm)
        {
            _ = fromVm.OnNavigatedFromAsync();
        }

        if (from != to && ViewModelOf(to) is Wpf.Ui.Abstractions.Controls.INavigationAware toVm)
        {
            _ = toVm.OnNavigatedToAsync();
        }
    }

    private static object? ViewModelOf(NavItem item) => item.Page switch
    {
        ScrollViewer { Content: Control page } => page.DataContext,
        Control page => page.DataContext,
        _ => null,
    };

    /// <summary>
    /// 原版 ViewModel 中的 _navigationService.Navigate(typeof(XxxPage))
    /// </summary>
    private bool Navigate(Type pageType)
    {
        var index = _items.FindIndex(i => i.WpfPageTypes.Contains(pageType));
        if (index < 0)
        {
            return false;
        }

        Dispatcher.UIThread.Post(() => NavList.SelectedIndex = index);
        return true;
    }

    private void ShowToast(ToastKind kind, string message)
    {
        var (glyph, color) = kind switch
        {
            ToastKind.Success => ("✅", Color.FromRgb(0x2E, 0x7D, 0x32)),
            ToastKind.Warning => ("⚠️", Color.FromRgb(0xB2, 0x6A, 0x00)),
            ToastKind.Error => ("⛔", Color.FromRgb(0xC6, 0x28, 0x28)),
            _ => ("ℹ️", Color.FromRgb(0x15, 0x65, 0xC0)),
        };
        var toast = new Border
        {
            Background = new SolidColorBrush(color, 0.92),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = glyph },
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White, MaxWidth = 360 },
                },
            },
        };
        ToastHost.Children.Add(toast);
        DispatcherTimer.RunOnce(() => ToastHost.Children.Remove(toast), TimeSpan.FromSeconds(4));
    }

    private void AppendLog(LogEventLevel level, string text)
    {
        var prefix = level switch
        {
            LogEventLevel.Error or LogEventLevel.Fatal => "❌ ",
            LogEventLevel.Warning => "⚠️ ",
            _ => string.Empty,
        };
        _log.Append(prefix).AppendLine(text);
        if (_log.Length > MaxLogChars)
        {
            _log.Remove(0, _log.Length - MaxLogChars / 2);
        }

        _logDirty = true;
    }

    private void FlushLog()
    {
        if (!_logDirty)
        {
            return;
        }

        lock (_log)
        {
            LogText.Text = _log.ToString();
            _logDirty = false;
        }

        LogScroll.ScrollToEnd();
    }
}
