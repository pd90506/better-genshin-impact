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
using Microsoft.Extensions.Logging;

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
            // 与原版相同：推理运行时、Host、服务器时间初始化完成后，再执行命令行任务和启动检查
            var started = Task.Run(App.StartAsync);
            mainWindow.Opened += async (_, _) =>
            {
                try
                {
                    await started;
                }
                catch (Exception e)
                {
                    App.GetLogger<MacApplication>().LogError(e, "启动失败");
                    Wpf.Ui.Violeta.Controls.Toast.Error("启动失败：" + e.Message);
                    return;
                }

                CommandLineActivation.Handle();
                await App.RunPostStartupAsync();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnShutdown()
    {
        try
        {
            App.GetService<Service.Interface.IConfigService>()?.Flush();
            Task.Run(App.StopAsync).Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // 退出时忽略
        }
    }
}
