using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using BetterGenshinImpact.MacApp.Infrastructure;
using BetterGenshinImpact.MacApp.Views;
using BetterGenshinImpact.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BetterGenshinImpact.MacApp;

public partial class MacApplication : Avalonia.Application
{
    private IHost? _host;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 原版代码依赖的 UI 线程、对话框、剪贴板等接到 Avalonia
            UiPlatform.UiThread = new AvaloniaUiThread();
            UiPlatform.Dialogs = new AvaloniaDialogHost();
            System.Windows.Application.Current = new App();
            System.Windows.Application.ShutdownHandler = code => Dispatcher.UIThread.Post(() => desktop.Shutdown(code));

            _host = App.Initialize();
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;
            System.Windows.Application.Current.MainWindow = new MainWindowProxy(mainWindow);
            System.Windows.Clipboard.SetTextHandler = text => Dispatcher.UIThread.Post(async () =>
            {
                if (mainWindow.Clipboard != null)
                {
                    await mainWindow.Clipboard.SetTextAsync(text);
                }
            });
            System.Windows.Clipboard.GetTextHandler = () => Dispatcher.UIThread.Invoke(() =>
                mainWindow.Clipboard?.GetTextAsync().GetAwaiter().GetResult());

            desktop.ShutdownRequested += (_, _) => OnShutdown();
            mainWindow.Opened += (_, _) => CommandLineActivation.Handle();
            _ = Task.Run(() => _host.StartAsync());
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnShutdown()
    {
        try
        {
            App.GetService<Service.Interface.IConfigService>()?.Flush();
            _host?.StopAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
        }
        catch
        {
            // 退出时忽略
        }
    }
}
