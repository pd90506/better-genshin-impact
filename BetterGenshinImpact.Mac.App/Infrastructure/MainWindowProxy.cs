using Avalonia.Threading;
using W = System.Windows;

namespace BetterGenshinImpact.MacApp.Infrastructure;

/// <summary>
/// 原版代码通过 Application.Current.MainWindow 显示、隐藏、激活主窗口，这里转发给 Avalonia 主窗口
/// </summary>
public sealed class MainWindowProxy : View.MainWindow
{
    private readonly Avalonia.Controls.Window _window;

    public MainWindowProxy(Avalonia.Controls.Window window)
    {
        _window = window;
        Title = window.Title ?? "BetterGI";
    }

    public override W.WindowState WindowState
    {
        get => Dispatcher.UIThread.Invoke(() => _window.WindowState switch
        {
            Avalonia.Controls.WindowState.Minimized => W.WindowState.Minimized,
            Avalonia.Controls.WindowState.Maximized or Avalonia.Controls.WindowState.FullScreen => W.WindowState.Maximized,
            _ => W.WindowState.Normal,
        });
        set => Dispatcher.UIThread.Post(() => _window.WindowState = value switch
        {
            W.WindowState.Minimized => Avalonia.Controls.WindowState.Minimized,
            W.WindowState.Maximized => Avalonia.Controls.WindowState.Maximized,
            _ => Avalonia.Controls.WindowState.Normal,
        });
    }

    public override bool Topmost
    {
        get => _window.Topmost;
        set => Dispatcher.UIThread.Post(() => _window.Topmost = value);
    }

    public override void Show() => Dispatcher.UIThread.Post(() => _window.Show());

    public override void Hide() => Dispatcher.UIThread.Post(() => _window.Hide());

    public override bool Activate()
    {
        Dispatcher.UIThread.Post(() => _window.Activate());
        return true;
    }

    public override void Close() => Dispatcher.UIThread.Post(() => _window.Close());

    public override void NavigateTo(System.Type pageType) => Wpf.Ui.NavigationService.NavigateHandler?.Invoke(pageType);
}
