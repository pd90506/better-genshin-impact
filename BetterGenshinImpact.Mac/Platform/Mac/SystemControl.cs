using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BetterGenshinImpact.GameTask.Runtime.Win32;
using BetterGenshinImpact.Platform;
using BetterGenshinImpact.Platform.Mac;
using BetterGenshinImpact.Service.Interface;
using Microsoft.Extensions.Logging;
using Vanara.PInvoke;

namespace BetterGenshinImpact.GameTask;

/// <summary>
/// 原版 SystemControl 的 macOS 实现，公开接口保持一致。
/// 窗口句柄（nint）在 macOS 上是 CGWindowID；坐标为「屏幕 point × 缩放比例」的桌面像素。
/// </summary>
public class SystemControl
{
    private static readonly ILogger Logger = App.GetLogger<SystemControl>();

    /// <summary>
    /// macOS 上"游戏进程名"即游戏窗口所属应用名
    /// </summary>
    public static List<string> GetGenshinGameProcessNameList()
    {
        if (TaskContext.Instance().Runtime is { Window: Win32GameWindow window })
        {
            return [window.ProcessName];
        }

        return MacPlatformConfig.Instance.WindowOwnerKeywords.ToList();
    }

    public static nint FindGenshinImpactHandle()
    {
        var config = MacPlatformConfig.Instance;
        var window = MacWindows.Find(config.WindowOwnerKeywords, config.WindowTitleKeywords);
        return window == null ? 0 : (nint)window.WindowId;
    }

    /// <summary>
    /// "关联启动"：macOS 版打开 GeForce NOW 客户端并等待窗口出现。
    /// <paramref name="path"/> 为 .app 路径或应用名，留空时使用 "GeForce NOW"
    /// </summary>
    public static async Task<nint> StartFromLocalAsync(string path)
    {
        var app = string.IsNullOrWhiteSpace(path) ? "GeForce NOW" : path;
        try
        {
            var psi = new ProcessStartInfo("/usr/bin/open") { UseShellExecute = false };
            if (app.EndsWith(".app", StringComparison.OrdinalIgnoreCase) || app.Contains('/'))
            {
                psi.ArgumentList.Add(app);
            }
            else
            {
                psi.ArgumentList.Add("-a");
                psi.ArgumentList.Add(app);
            }

            using var _ = Process.Start(psi);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "启动 {App} 失败", app);
            return 0;
        }

        for (var i = 0; i < 12; i++)
        {
            var handle = FindGenshinImpactHandle();
            if (handle != 0)
            {
                await Task.Delay(3000);
                return FindGenshinImpactHandle();
            }

            await Task.Delay(5000);
        }

        return FindGenshinImpactHandle();
    }

    internal static string BuildGenshinStartArguments(string? configuredArguments, bool isChildSession) => configuredArguments ?? string.Empty;

    public static bool IsGenshinImpactActiveByProcess()
    {
        if (TaskContext.Instance().Runtime?.Window is { RequiresForeground: false })
        {
            return true;
        }

        var name = GetActiveProcessName();
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        return GetGenshinGameProcessNameList().Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    public static string GetActiveByProcess() => GetActiveProcessName() ?? "Unknown";

    public static bool IsGenshinImpactActive()
    {
        var handle = TaskContext.Instance().GameHandle;
        if (handle == 0)
        {
            return false;
        }

        var window = MacWindows.Get((uint)handle);
        return window != null && MacWindows.FrontmostProcessId() == window.ProcessId;
    }

    public static bool IsGenshinImpactMinimized()
    {
        var handle = TaskContext.Instance().GameHandle;
        return handle != 0 && MacWindows.Get((uint)handle) is { IsOnScreen: false };
    }

    /// <summary>
    /// 前台应用中最靠前的窗口
    /// </summary>
    public static nint GetForegroundWindowHandle()
    {
        var pid = MacWindows.FrontmostProcessId();
        var window = MacWindows.List().FirstOrDefault(w => w.ProcessId == pid && w.Layer == 0);
        return window == null ? 0 : (nint)window.WindowId;
    }

    public static nint FindHandleByProcessName(params string[] names)
    {
        var window = MacWindows.Find(names);
        return window == null ? 0 : (nint)window.WindowId;
    }

    public static nint FindHandleByWindowName()
    {
        var window = MacWindows.ListCapturable()
            .FirstOrDefault(w => w.Title.Contains("原神") || w.Title.Contains("Genshin", StringComparison.OrdinalIgnoreCase));
        return window == null ? 0 : (nint)window.WindowId;
    }

    public static string? GetActiveProcessName() => MacWindows.FrontmostProcessName();

    public static Process? GetProcessByHandle(nint hWnd)
    {
        try
        {
            var window = MacWindows.Get((uint)hWnd);
            return window == null ? null : Process.GetProcessById(window.ProcessId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return null;
        }
    }

    private static RECT ToPixelRect(double x, double y, double w, double h)
    {
        var scale = MacWindows.MainScreenScale();
        var left = (int)Math.Round(x * scale);
        var top = (int)Math.Round(y * scale);
        return new RECT(left, top, left + (int)Math.Round(w * scale), top + (int)Math.Round(h * scale));
    }

    /// <summary>
    /// 整个窗口在桌面上的区域（像素）
    /// </summary>
    public static RECT GetWindowRect(nint hWnd)
    {
        var window = MacWindows.Get((uint)hWnd);
        return window == null ? new RECT() : ToPixelRect(window.X, window.Y, window.Width, window.Height);
    }

    /// <summary>
    /// 游戏画面大小（像素），左上角为 0
    /// </summary>
    public static RECT GetGameScreenRect(nint hWnd)
    {
        var rect = GetCaptureRect(hWnd);
        return new RECT(0, 0, rect.Width, rect.Height);
    }

    /// <summary>
    /// 游戏画面在桌面上的区域（像素）：窗口内裁掉标题栏与黑边后的 16:9 区域
    /// </summary>
    public static RECT GetCaptureRect(nint hWnd)
    {
        var window = MacWindows.Get((uint)hWnd);
        if (window == null)
        {
            return new RECT();
        }

        var (gx, gy, gw, gh) = MacGameGeometry.GameRectInWindow(window, MacPlatformConfig.Instance);
        return ToPixelRect(window.X + gx, window.Y + gy, gw, gh);
    }

    public static void ActivateWindow(nint hWnd)
    {
        var window = MacWindows.Get((uint)hWnd);
        if (window != null)
        {
            MacWindows.Activate(window.ProcessId);
        }
    }

    public static void ActivateWindow()
    {
        var runtime = TaskContext.Instance().Runtime ?? throw new Exception("请先启动BetterGI");
        runtime.Window.Activate();
    }

    public static void RestartApplication(string[] newArgs, bool graceful = false)
    {
        App.GetService<IConfigService>()?.Flush();
        var bundle = MacAppEnvironment.AppBundlePath;
        var psi = new ProcessStartInfo("/usr/bin/open") { UseShellExecute = false };
        if (bundle != null)
        {
            psi.ArgumentList.Add("-n");
            psi.ArgumentList.Add(bundle);
            if (newArgs.Length > 0)
            {
                psi.ArgumentList.Add("--args");
                foreach (var a in newArgs)
                {
                    psi.ArgumentList.Add(a);
                }
            }
        }
        else
        {
            psi.FileName = Environment.ProcessPath!;
            foreach (var a in newArgs)
            {
                psi.ArgumentList.Add(a);
            }
        }

        using var _ = Process.Start(psi);
        if (graceful)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => System.Windows.Application.Current.Shutdown()));
        }
        else
        {
            Environment.Exit(0);
        }
    }

    public static void FocusWindow(nint hWnd) => ActivateWindow(hWnd);

    public static void MinimizeAndActivateWindow(nint hWnd) => ActivateWindow(hWnd);

    public static void RestoreWindow(nint hWnd) => ActivateWindow(hWnd);

    public static bool IsFullScreenMode(IntPtr hWnd)
    {
        var window = MacWindows.Get((uint)hWnd);
        return window != null && MacWindows.IsFullScreen(window);
    }

    public static void CloseGame()
    {
        var service = App.GetService<Runtime.GameRuntimeService>();
        if (service != null)
        {
            service.CloseGame();
        }
        else
        {
            CloseGameProcesses();
        }
    }

    /// <summary>
    /// 退出游戏所在的应用（GeForce NOW 会结束云端会话）
    /// </summary>
    public static void CloseGameProcesses()
    {
        try
        {
            var handle = TaskContext.Instance().GameHandle;
            var window = handle != 0 ? MacWindows.Get((uint)handle) : null;
            var names = window != null ? [window.OwnerName] : GetGenshinGameProcessNameList();
            foreach (var name in names)
            {
                using var p = Process.Start(new ProcessStartInfo("/usr/bin/osascript")
                {
                    ArgumentList = { "-e", $"tell application \"{name.Replace("\"", "")}\" to quit" },
                    UseShellExecute = false,
                    RedirectStandardError = true,
                });
                p?.WaitForExit(10000);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"关闭游戏时出错: {ex.Message}");
        }
    }

    public static void Shutdown()
    {
        try
        {
            using var _ = Process.Start(new ProcessStartInfo("/usr/bin/osascript")
            {
                ArgumentList = { "-e", "tell application \"System Events\" to shut down" },
                UseShellExecute = false,
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Shutdown方法执行出错: {ex.Message}");
        }
    }
}
