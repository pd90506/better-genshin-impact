using System;

namespace BetterGenshinImpact.Platform;

/// <summary>
/// 临时入口：后续由 Avalonia 宿主替换
/// </summary>
internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        App.Initialize();
        Console.WriteLine("BetterGI macOS core initialized.");
        return 0;
    }
}
