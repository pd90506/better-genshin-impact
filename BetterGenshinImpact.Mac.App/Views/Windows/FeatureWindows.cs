using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using BetterGenshinImpact.MacApp.Infrastructure;
using BetterGenshinImpact.Platform;
using BetterGenshinImpact.ViewModel.Windows;
using Wpf.Ui;
using Gen = BetterGenshinImpact.MacApp.Views.Generated;
using W = System.Windows;
using WV = BetterGenshinImpact.View.Windows;

namespace BetterGenshinImpact.MacApp.Views.Windows;

/// <summary>
/// 原版代码中 new XxxWindow().Show()/ShowDialog() 的 macOS 实现：
/// 兼容层窗口对象 → 由原版 XAML 转换生成的 Avalonia 界面 + 原版 ViewModel
/// </summary>
public static class FeatureWindows
{
    public static bool Open(W.Window window, bool modal)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            return Dispatcher.UIThread.Invoke(() => Open(window, modal));
        }

        switch (window)
        {
            // 这两个功能在 macOS 版是独立页面
            case WV.ScriptRepoWindow:
                window.Close();
                return NavigationService.NavigateHandler?.Invoke(typeof(WV.ScriptRepoWindow)) ?? false;
            case WV.KeyBindingsWindow:
                window.Close();
                return NavigationService.NavigateHandler?.Invoke(typeof(View.Pages.KeyBindingsSettingsPage)) ?? false;
        }

        var (content, width, height) = Create(window);
        if (content == null)
        {
            return false;
        }

        var host = new Window
        {
            Title = string.IsNullOrEmpty(window.Title) ? "BetterGI" : window.Title,
            Content = content,
            Width = width,
            Height = height,
            MinWidth = 360,
            MinHeight = 240,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Icon = MainOwner?.Icon,
        };
        host.Bind(Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty, host.GetResourceObservable("ApplicationBackgroundBrush"));

        // 兼容层窗口关闭（ViewModel 调用 Close）→ 关闭 Avalonia 窗口；反之亦然
        var closing = false;
        window.Closed += (_, _) =>
        {
            if (!closing)
            {
                closing = true;
                Dispatcher.UIThread.Post(host.Close);
            }
        };
        // 原版窗口 Closing 事件绑定的 ClosingCommand（例如保存设置）
        host.Closing += (_, e) =>
        {
            if (content.DataContext?.GetType().GetProperty("ClosingCommand")?.GetValue(content.DataContext) is System.Windows.Input.ICommand command)
            {
                var args = new System.ComponentModel.CancelEventArgs();
                if (command.CanExecute(args))
                {
                    command.Execute(args);
                }

                e.Cancel = args.Cancel;
            }
        };
        host.Closed += (_, _) =>
        {
            if (!closing)
            {
                closing = true;
                window.Close();
            }
        };

        if (modal && MainOwner != null)
        {
            using var cts = new CancellationTokenSource();
            host.Closed += (_, _) => cts.Cancel();
            _ = host.ShowDialog(MainOwner);
            UiPlatform.UiThread.RunNestedLoop(cts.Token);
        }
        else if (MainOwner != null)
        {
            host.Show(MainOwner);
        }
        else
        {
            host.Show();
        }

        return true;
    }

    private static Window? MainOwner => (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    private static (Control? Content, double Width, double Height) Create(W.Window window)
    {
        switch (window)
        {
            case WV.AutoPickBlacklistConfigWindow b:
            {
                var vm = new AutoPickBlacklistConfigViewModel(b.Config);
                vm.CloseRequested += result => Close(b, result);
                return (new Gen.AutoPickBlacklistConfigWindow { DataContext = vm }, 820, 640);
            }
            case WV.AutoPickWhitelistConfigWindow w:
            {
                var vm = new AutoPickWhitelistConfigViewModel(w.Config);
                vm.CloseRequested += result => Close(w, result);
                return (new Gen.AutoPickWhitelistConfigWindow { DataContext = vm }, 820, 640);
            }
            case WV.JsonMonoDialog j:
                return (new Gen.JsonMonoDialog { DataContext = new JsonMonoViewModel(j.Path) }, 900, 640);
            case WV.SkillCdConfigWindow s:
                return (new Gen.SkillCdConfigWindow { DataContext = s }, 560, 520);
            case View.Pages.View.PathingConfigView p:
                return (new Gen.PathingConfigView { DataContext = p.DataContext }, 560, 720);
            case WV.MusicSettingsWindow m:
                return (new Gen.MusicSettingsWindow { DataContext = m.DataContext }, 720, 640);
            case WV.AboutWindow:
                return (About(), 480, 300);
            case WV.WpfUiWindow ui:
                return (new ScrollViewer { Content = ControlTreeRenderer.Render(ui.Content), Padding = new Thickness(16) }, 640, 480);
            default:
                return (null, 0, 0);
        }
    }

    private static void Close(W.Window window, bool? result)
    {
        window.DialogResult = result;
        window.Close();
    }

    private static Control About() => new StackPanel
    {
        Margin = new Thickness(24),
        Spacing = 8,
        Children =
        {
            new TextBlock { Text = "BetterGI · 更好的原神", FontSize = 20, FontWeight = FontWeight.SemiBold },
            new TextBlock { Text = "macOS 版 " + Core.Config.Global.Version },
            new TextBlock { Text = "基于 babalae/better-genshin-impact，GPL-3.0 开源", TextWrapping = TextWrapping.Wrap },
            new TextBlock { Text = "https://github.com/babalae/better-genshin-impact", Foreground = Brushes.SteelBlue },
        },
    };
}
