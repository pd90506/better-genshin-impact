using System;
using System.Diagnostics;
using static BetterGenshinImpact.Platform.Mac.MacNative;

namespace BetterGenshinImpact.Platform.Mac;

/// <summary>
/// macOS 隐私权限：屏幕录制（截图）、辅助功能（模拟键鼠）、输入监控（全局热键）
/// </summary>
public static class MacPermissions
{
    public static bool ScreenRecording => !IsMacOS || SafeCall(CGPreflightScreenCaptureAccess);

    public static bool Accessibility => !IsMacOS || SafeCall(AXIsProcessTrusted);

    public static bool InputMonitoring => !IsMacOS || SafeCall(CGPreflightListenEventAccess);

    public static void RequestScreenRecording()
    {
        if (IsMacOS && !ScreenRecording)
        {
            SafeCall(CGRequestScreenCaptureAccess);
            OpenSettings("Privacy_ScreenCapture");
        }
    }

    public static void RequestAccessibility()
    {
        if (IsMacOS && !Accessibility)
        {
            SafeCall(CGRequestPostEventAccess);
            OpenSettings("Privacy_Accessibility");
        }
    }

    public static void RequestInputMonitoring()
    {
        if (IsMacOS && !InputMonitoring)
        {
            SafeCall(CGRequestListenEventAccess);
            OpenSettings("Privacy_ListenEvent");
        }
    }

    public static void OpenSettings(string anchor)
    {
        try
        {
            var psi = new ProcessStartInfo("/usr/bin/open") { UseShellExecute = false };
            psi.ArgumentList.Add($"x-apple.systempreferences:com.apple.preference.security?{anchor}");
            using var _ = Process.Start(psi);
        }
        catch
        {
            // ignored
        }
    }

    private static bool SafeCall(Func<bool> func)
    {
        try
        {
            return func();
        }
        catch
        {
            return false;
        }
    }
}
