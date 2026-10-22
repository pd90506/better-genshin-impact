using System;

namespace BetterGenshinImpact.Platform.Mac;

/// <summary>
/// 计算游戏画面在窗口中的位置。
/// <para>
/// 坐标约定：原版的"桌面坐标"是物理像素，这里定义为「屏幕 point × 主显示器缩放」，
/// 与窗口截图的像素一一对应。键鼠模拟时再除以缩放换回 point。
/// </para>
/// </summary>
public static class MacGameGeometry
{
    public const double DefaultTitleBarHeight = 28;

    /// <summary>
    /// 游戏画面在窗口内的区域（point，相对窗口左上角）
    /// </summary>
    public static (double X, double Y, double Width, double Height) GameRectInWindow(MacWindowInfo window, MacPlatformConfig config)
    {
        if (config.ManualCropWidth > 0 && config.ManualCropHeight > 0)
        {
            return (config.ManualCropX, config.ManualCropY, config.ManualCropWidth, config.ManualCropHeight);
        }

        var top = config.TopInset >= 0
            ? config.TopInset
            : MacWindows.IsFullScreen(window) ? 0 : DefaultTitleBarHeight;
        top = Math.Min(top, window.Height / 2);
        double x = 0, y = top, w = window.Width, h = window.Height - top;
        if (config.AutoFit16By9)
        {
            (x, y, w, h) = Fit16By9(x, y, w, h);
        }

        return (x, y, w, h);
    }

    public static (double X, double Y, double Width, double Height) Fit16By9(double x, double y, double w, double h)
    {
        double gw, gh;
        if (w * 9 > h * 16)
        {
            gh = h;
            gw = h * 16 / 9;
        }
        else
        {
            gw = w;
            gh = w * 9 / 16;
        }

        return (x + (w - gw) / 2, y + (h - gh) / 2, gw, gh);
    }
}
