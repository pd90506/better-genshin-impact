using System;
using System.Collections.Generic;
using System.Diagnostics;
using Fischless.GameCapture;
using OpenCvSharp;
using Vanara.PInvoke;
using static BetterGenshinImpact.Platform.Mac.MacNative;

namespace BetterGenshinImpact.Platform.Mac;

/// <summary>
/// macOS 窗口截图：截取游戏窗口（GeForce NOW）并裁出游戏画面，输出与 BitBlt 一致的 BGR 三通道 Mat。
/// <para>
/// 优先使用 CGWindowListCreateImage（同步、无额外依赖）。新版 macOS 若移除该接口，
/// 自动改用 ScreenCaptureKit（见 <see cref="MacScreenCaptureKit"/>）。
/// 需要「屏幕与系统录音」权限。
/// </para>
/// </summary>
public sealed class MacScreenCapture : IGameCapture
{
    private uint _windowId;
    private bool _useScreenCaptureKit;
    private MacScreenCaptureKit? _sck;
    private long _lastCaptureTicks;

    public bool IsCapturing { get; private set; }

    /// <summary>
    /// 最近一次截图的游戏画面区域（桌面像素坐标）
    /// </summary>
    public RECT? LastCaptureRect { get; private set; }

    public void Start(nint hWnd, Dictionary<string, object>? settings = null)
    {
        _windowId = (uint)hWnd;
        IsCapturing = _windowId != 0;
        if (IsMacOS && !CGPreflightScreenCaptureAccess())
        {
            CGRequestScreenCaptureAccess();
        }
    }

    public GameCaptureFrame? Capture()
    {
        if (!IsCapturing || !IsMacOS)
        {
            return null;
        }

        var config = MacPlatformConfig.Instance;
        var window = MacWindows.Get(_windowId);
        if (window == null || window.Width < 10 || window.Height < 10)
        {
            return null;
        }

        using var full = GrabWindow(window);
        if (full == null || full.Empty())
        {
            return null;
        }

        // 窗口截图的像素与 point 之比（Retina 为 2）
        var pxPerPt = full.Width / window.Width;
        var (gx, gy, gw, gh) = MacGameGeometry.GameRectInWindow(window, config);
        var crop = new Rect(
            (int)Math.Round(gx * pxPerPt),
            (int)Math.Round(gy * pxPerPt),
            (int)Math.Round(gw * pxPerPt),
            (int)Math.Round(gh * pxPerPt));
        crop = crop.Intersect(new Rect(0, 0, full.Width, full.Height));
        if (crop.Width <= 0 || crop.Height <= 0)
        {
            return null;
        }

        var bgr = new Mat();
        using (var roi = new Mat(full, crop))
        {
            if (roi.Channels() == 4)
            {
                Cv2.CvtColor(roi, bgr, ColorConversionCodes.BGRA2BGR);
            }
            else
            {
                roi.CopyTo(bgr);
            }
        }

        var screenScale = MacWindows.MainScreenScale();
        var left = (int)Math.Round((window.X + gx) * screenScale);
        var top = (int)Math.Round((window.Y + gy) * screenScale);
        LastCaptureRect = new RECT(left, top, left + bgr.Width, top + bgr.Height);
        _lastCaptureTicks = Stopwatch.GetTimestamp();
        return new GameCaptureFrame(bgr, LastCaptureRect);
    }

    private Mat? GrabWindow(MacWindowInfo window)
    {
        if (!_useScreenCaptureKit)
        {
            try
            {
                var image = CGWindowListCreateImage(CGRect.Null, kCGWindowListOptionIncludingWindow, window.WindowId,
                    kCGWindowImageBoundsIgnoreFraming | kCGWindowImageBestResolution);
                if (image != IntPtr.Zero)
                {
                    try
                    {
                        return CGImageToMat(image);
                    }
                    finally
                    {
                        CGImageRelease(image);
                    }
                }
            }
            catch (EntryPointNotFoundException)
            {
                _useScreenCaptureKit = true;
            }
        }

        if (!_useScreenCaptureKit && !OperatingSystem.IsMacOSVersionAtLeast(15))
        {
            return null;
        }

        // CGWindowListCreateImage 在 macOS 15 之后可能返回空图，改用 ScreenCaptureKit
        _useScreenCaptureKit = true;
        _sck ??= new MacScreenCaptureKit();
        var sckImage = _sck.CaptureWindow(window);
        if (sckImage == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return CGImageToMat(sckImage);
        }
        finally
        {
            CGImageRelease(sckImage);
        }
    }

    /// <summary>
    /// CGImage（32 位 BGRA，小端序）复制为 OpenCV Mat
    /// </summary>
    public static Mat? CGImageToMat(IntPtr image)
    {
        var width = (int)CGImageGetWidth(image);
        var height = (int)CGImageGetHeight(image);
        var bytesPerRow = (long)CGImageGetBytesPerRow(image);
        var bitsPerPixel = (int)CGImageGetBitsPerPixel(image);
        if (width <= 0 || height <= 0 || bitsPerPixel != 32)
        {
            return null;
        }

        var data = CGDataProviderCopyData(CGImageGetDataProvider(image));
        if (data == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            using var wrapped = Mat.FromPixelData(height, width, MatType.CV_8UC4, CFDataGetBytePtr(data), bytesPerRow);
            return wrapped.Clone();
        }
        finally
        {
            CFRelease(data);
        }
    }

    public void Stop()
    {
        IsCapturing = false;
        _sck?.Dispose();
        _sck = null;
    }

    public void Dispose() => Stop();
}
