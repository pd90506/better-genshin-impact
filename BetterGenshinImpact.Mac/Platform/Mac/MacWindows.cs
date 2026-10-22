using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static BetterGenshinImpact.Platform.Mac.MacNative;

namespace BetterGenshinImpact.Platform.Mac;

/// <summary>
/// 一个可截图的 macOS 窗口。坐标单位为 point（屏幕逻辑坐标，左上角为原点）
/// </summary>
public sealed record MacWindowInfo(uint WindowId, int ProcessId, string OwnerName, string Title, double X, double Y, double Width,
    double Height, int Layer, bool IsOnScreen)
{
    public override string ToString() => string.IsNullOrEmpty(Title) ? OwnerName : $"{OwnerName} - {Title}";
}

/// <summary>
/// 窗口查询与控制
/// </summary>
public static class MacWindows
{
    private static IntPtr _kWindowNumber;
    private static IntPtr _kWindowOwnerPid;
    private static IntPtr _kWindowOwnerName;
    private static IntPtr _kWindowName;
    private static IntPtr _kWindowBounds;
    private static IntPtr _kWindowLayer;
    private static IntPtr _kWindowIsOnscreen;

    private static void EnsureKeys()
    {
        if (_kWindowNumber != IntPtr.Zero)
        {
            return;
        }

        _kWindowNumber = GetConstant(CoreGraphics, "kCGWindowNumber");
        _kWindowOwnerPid = GetConstant(CoreGraphics, "kCGWindowOwnerPID");
        _kWindowOwnerName = GetConstant(CoreGraphics, "kCGWindowOwnerName");
        _kWindowName = GetConstant(CoreGraphics, "kCGWindowName");
        _kWindowBounds = GetConstant(CoreGraphics, "kCGWindowBounds");
        _kWindowLayer = GetConstant(CoreGraphics, "kCGWindowLayer");
        _kWindowIsOnscreen = GetConstant(CoreGraphics, "kCGWindowIsOnscreen");
    }

    /// <summary>
    /// 列出窗口，按 Z 序从前到后
    /// </summary>
    public static List<MacWindowInfo> List(bool onScreenOnly = true, uint? windowId = null)
    {
        var result = new List<MacWindowInfo>();
        if (!IsMacOS)
        {
            return result;
        }

        EnsureKeys();
        var option = windowId.HasValue
            ? kCGWindowListOptionIncludingWindow
            : (onScreenOnly ? kCGWindowListOptionOnScreenOnly : kCGWindowListOptionAll) | kCGWindowListExcludeDesktopElements;
        var array = CGWindowListCopyWindowInfo(option, windowId ?? 0);
        if (array == IntPtr.Zero)
        {
            return result;
        }

        try
        {
            var count = CFArrayGetCount(array);
            for (long i = 0; i < count; i++)
            {
                var dict = CFArrayGetValueAtIndex(array, i);
                var id = (uint)CFNumberToLong(CFDictionaryGetValue(dict, _kWindowNumber));
                var pid = (int)CFNumberToLong(CFDictionaryGetValue(dict, _kWindowOwnerPid));
                var owner = CFStringToString(CFDictionaryGetValue(dict, _kWindowOwnerName)) ?? string.Empty;
                var title = CFStringToString(CFDictionaryGetValue(dict, _kWindowName)) ?? string.Empty;
                var layer = (int)CFNumberToLong(CFDictionaryGetValue(dict, _kWindowLayer));
                var onScreenValue = CFDictionaryGetValue(dict, _kWindowIsOnscreen);
                var onScreen = onScreenValue != IntPtr.Zero && CFBooleanGetValue(onScreenValue);
                CGRectMakeWithDictionaryRepresentation(CFDictionaryGetValue(dict, _kWindowBounds), out var rect);
                result.Add(new MacWindowInfo(id, pid, owner, title, rect.Origin.X, rect.Origin.Y, rect.Size.Width, rect.Size.Height, layer,
                    onScreen));
            }
        }
        finally
        {
            CFRelease(array);
        }

        return result;
    }

    public static MacWindowInfo? Get(uint windowId) => List(false, windowId).FirstOrDefault(w => w.WindowId == windowId);

    /// <summary>
    /// 普通应用窗口（排除菜单栏、程序坞、过小的窗口与本程序自己）
    /// </summary>
    public static List<MacWindowInfo> ListCapturable()
    {
        var self = Environment.ProcessId;
        return List()
            .Where(w => w.Layer == 0 && w.Width >= 320 && w.Height >= 180 && w.ProcessId != self)
            .ToList();
    }

    /// <summary>
    /// 按匹配规则查找游戏窗口：owner 名包含任一关键字；标题关键字为空表示不限制。多个结果取面积最大的
    /// </summary>
    public static MacWindowInfo? Find(IEnumerable<string> ownerKeywords, IEnumerable<string>? titleKeywords = null)
    {
        var owners = ownerKeywords.Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
        var titles = titleKeywords?.Where(k => !string.IsNullOrWhiteSpace(k)).ToList() ?? [];
        return ListCapturable()
            .Where(w => owners.Any(k => w.OwnerName.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .Where(w => titles.Count == 0 || titles.Any(k => w.Title.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(w => w.Width * w.Height)
            .FirstOrDefault();
    }

    // ---------------- 前台应用 ----------------

    public static int FrontmostProcessId()
    {
        if (!IsMacOS)
        {
            return 0;
        }

        var workspace = objc_msgSend(Class("NSWorkspace"), Sel("sharedWorkspace"));
        var app = objc_msgSend(workspace, Sel("frontmostApplication"));
        return app == IntPtr.Zero ? 0 : objc_msgSend_RetInt(app, Sel("processIdentifier"));
    }

    public static string? FrontmostProcessName()
    {
        if (!IsMacOS)
        {
            return null;
        }

        var workspace = objc_msgSend(Class("NSWorkspace"), Sel("sharedWorkspace"));
        var app = objc_msgSend(workspace, Sel("frontmostApplication"));
        return app == IntPtr.Zero ? null : NSStringToString(objc_msgSend(app, Sel("localizedName")));
    }

    /// <summary>
    /// 激活应用（带到前台）。先用 NSRunningApplication，失败时用 AppleScript 兜底
    /// </summary>
    public static void Activate(int processId)
    {
        if (!IsMacOS || processId <= 0)
        {
            return;
        }

        var app = objc_msgSend_Int(Class("NSRunningApplication"), Sel("runningApplicationWithProcessIdentifier:"), processId);
        if (app != IntPtr.Zero)
        {
            // 未隐藏时先取消隐藏，再激活。NSApplicationActivateIgnoringOtherApps = 1 << 1
            objc_msgSend(app, Sel("unhide"));
            objc_msgSend_RetBool_ULong(app, Sel("activateWithOptions:"), 1UL << 1);
        }

        if (FrontmostProcessId() == processId)
        {
            return;
        }

        try
        {
            using var p = Process.Start(new ProcessStartInfo("/usr/bin/osascript")
            {
                ArgumentList =
                {
                    "-e",
                    $"tell application \"System Events\" to set frontmost of (first process whose unix id is {processId}) to true"
                },
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            });
            p?.WaitForExit(3000);
        }
        catch
        {
            // ignored
        }
    }

    /// <summary>
    /// 主显示器的缩放比例（Retina 为 2）
    /// </summary>
    public static double MainScreenScale()
    {
        if (!IsMacOS)
        {
            return 1;
        }

        var screen = objc_msgSend(Class("NSScreen"), Sel("mainScreen"));
        if (screen == IntPtr.Zero)
        {
            return 1;
        }

        var scale = objc_msgSend_RetDouble(screen, Sel("backingScaleFactor"));
        return scale > 0 ? scale : 1;
    }

    /// <summary>
    /// 主显示器大小（point）
    /// </summary>
    public static (double Width, double Height) MainDisplaySize()
    {
        if (!IsMacOS)
        {
            return (1920, 1080);
        }

        var rect = CGDisplayBounds(CGMainDisplayID());
        return (rect.Size.Width, rect.Size.Height);
    }

    /// <summary>
    /// 窗口是否占满所在显示器（全屏模式没有标题栏）
    /// </summary>
    public static unsafe bool IsFullScreen(MacWindowInfo window)
    {
        if (!IsMacOS)
        {
            return false;
        }

        var ids = stackalloc uint[16];
        CGGetActiveDisplayList(16, ids, out var count);
        for (var i = 0; i < count; i++)
        {
            var r = CGDisplayBounds(ids[i]);
            if (Math.Abs(r.Origin.X - window.X) < 1 && Math.Abs(r.Origin.Y - window.Y) < 1 &&
                Math.Abs(r.Size.Width - window.Width) < 1 && Math.Abs(r.Size.Height - window.Height) < 1)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsProcessAlive(int processId)
    {
        try
        {
            using var p = Process.GetProcessById(processId);
            return !p.HasExited;
        }
        catch
        {
            return false;
        }
    }
}
