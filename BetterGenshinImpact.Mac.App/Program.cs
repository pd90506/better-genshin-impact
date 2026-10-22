using System;
using Avalonia;

namespace BetterGenshinImpact.MacApp;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<MacApplication>()
            .UsePlatformDetect()
            .With(new MacOSPlatformOptions { ShowInDock = true });
        // 界面冒烟测试时把绑定错误输出到控制台
        return Environment.GetEnvironmentVariable("BGI_SMOKE_DIR") != null
            ? builder.LogToTextWriter(Console.Out, Avalonia.Logging.LogEventLevel.Warning, Avalonia.Logging.LogArea.Binding, Avalonia.Logging.LogArea.Property, Avalonia.Logging.LogArea.Control)
            : builder.LogToTrace();
    }
}
