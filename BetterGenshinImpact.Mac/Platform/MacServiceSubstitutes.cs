using System;
using System.Diagnostics;
using BetterGenshinImpact.Service.Model.OverlayMetric;

// 原版中依赖 Windows 专属组件的服务，macOS 版以同名类型替换（原文件在 MacExclude.props 中排除）。

namespace BetterGenshinImpact.Service
{
    /// <summary>
    /// 性能指标遮罩。原版依赖 LibreHardwareMonitor 与 PDH 采样硬件，macOS 版暂不提供，只保留接口
    /// </summary>
    public sealed class OverlayMetricsService : IDisposable
    {
        public event EventHandler<OverlayMetricsSnapshot>? MetricsUpdated;

        public OverlayMetricsSnapshot CurrentSnapshot { get; private set; } = OverlayMetricsSnapshot.Empty;

        public bool IsEnabled => false;

        public void UpdateGameFps(double fps)
        {
        }

        public void RecordSkippedTick()
        {
        }

        public void RecordDispatcherTick(double processingCostMs, double captureCostMs, double triggerCostMs)
        {
        }

        public void Refresh() => MetricsUpdated?.Invoke(this, CurrentSnapshot);

        public void Dispose()
        {
        }
    }
}

namespace Meziantou.Framework.Win32
{
    public enum CredentialPersistence
    {
        Session = 1,
        LocalMachine = 2,
        Enterprise = 3,
    }

    public sealed class Credential
    {
        public Credential(string applicationName, string? userName, string? password, string? comment)
        {
            ApplicationName = applicationName;
            UserName = userName;
            Password = password;
            Comment = comment;
        }

        public string ApplicationName { get; }

        public string? UserName { get; }

        public string? Password { get; }

        public string? Comment { get; }
    }
}

namespace BetterGenshinImpact.Helpers.Win32
{
    using Meziantou.Framework.Win32;

    /// <summary>
    /// Windows 凭据管理器的替代：macOS 上保存到「钥匙串」（security 命令行）
    /// </summary>
    public static class CredentialManagerHelper
    {
        private const string Account = "BetterGI";

        public static void SaveCredential(string applicationName, string userName, string secret, string comment,
            CredentialPersistence persistence)
        {
            Run("add-generic-password", "-U", "-a", userName ?? Account, "-s", applicationName, "-j", comment ?? string.Empty, "-w", secret);
        }

        public static Credential? ReadCredential(string applicationName)
        {
            var (code, output) = Run("find-generic-password", "-s", applicationName, "-w");
            if (code != 0)
            {
                return null;
            }

            return new Credential(applicationName, Account, output.TrimEnd('\n', '\r'), null);
        }

        public static void UpdateCredential(string applicationName, string newUserName, string newSecret, string newComment)
        {
            SaveCredential(applicationName, newUserName, newSecret, newComment, CredentialPersistence.LocalMachine);
        }

        public static void DeleteCredential(string applicationName)
        {
            Run("delete-generic-password", "-s", applicationName);
        }

        private static (int Code, string Output) Run(params string[] args)
        {
            try
            {
                var psi = new ProcessStartInfo("/usr/bin/security")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                foreach (var a in args)
                {
                    psi.ArgumentList.Add(a);
                }

                using var p = Process.Start(psi)!;
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(10000);
                return (p.ExitCode, output);
            }
            catch (Exception)
            {
                return (-1, string.Empty);
            }
        }
    }
}

namespace Fischless.GameCapture.Graphics
{
}

namespace Fischless.GameCapture.BitBlt
{
    public static class BitBltRegistryHelper
    {
        public static void SetDirectXUserGlobalSettings()
        {
        }
    }
}

namespace BetterGenshinImpact.View.Controls.Markdown
{
    public enum MarkdownLinkNavigationMode
    {
        SystemDefault,
        Internal,
        None,
    }
}

namespace BetterGenshinImpact.Service.ChildSession
{
    /// <summary>
    /// Windows 远程桌面子会话（多开）在 macOS 上不可用，保留类型供 ViewModel 编译
    /// </summary>
    public sealed class ChildSessionService
    {
        public bool HasActiveChildSession() => false;

        public void ShowWindow() => Wpf.Ui.Violeta.Controls.Toast.Warning("macOS 版不支持子会话多开");

        public void HideWindow()
        {
        }

        public bool TryGetRelativeMouseCaptureBounds(out System.Drawing.Rectangle bounds)
        {
            bounds = default;
            return false;
        }
    }
}

namespace BetterGenshinImpact.Service.Notifier
{
    using System.Threading.Tasks;
    using BetterGenshinImpact.Service.Notification.Model;
    using BetterGenshinImpact.Service.Notifier.Interface;

    /// <summary>
    /// 系统通知。沿用原版类型名（配置项仍叫 Windows 通知），macOS 上通过「通知中心」显示
    /// </summary>
    public class WindowsUwpNotifier : INotifier
    {
        public string Name => "系统通知";

        public Task SendAsync(BaseNotificationData data)
        {
            var message = (data.Message ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
            var title = $"BetterGI · {data.Event}".Replace("\"", "\\\"");
            try
            {
                using var p = Process.Start(new ProcessStartInfo("/usr/bin/osascript")
                {
                    ArgumentList = { "-e", $"display notification \"{message}\" with title \"{title}\"" },
                    UseShellExecute = false,
                });
            }
            catch
            {
                // 通知失败不影响任务
            }

            return Task.CompletedTask;
        }
    }
}

namespace BetterGenshinImpact.Helpers
{
    /// <summary>
    /// 设备标识：macOS 使用 IOPlatformUUID 的哈希
    /// </summary>
    public class DeviceIdHelper
    {
        private static readonly Lazy<string> LazyDeviceId = new(Initialize);

        public static string DeviceId => LazyDeviceId.Value;

        private static string Initialize()
        {
            string raw = Environment.MachineName;
            try
            {
                using var p = Process.Start(new ProcessStartInfo("/usr/sbin/ioreg", "-rd1 -c IOPlatformExpertDevice")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                });
                var output = p!.StandardOutput.ReadToEnd();
                var match = System.Text.RegularExpressions.Regex.Match(output, "\"IOPlatformUUID\"\\s*=\\s*\"([^\"]+)\"");
                if (match.Success)
                {
                    raw = match.Groups[1].Value;
                }
            }
            catch
            {
                // 使用机器名
            }

            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash)[..32];
        }
    }
}

namespace BetterGenshinImpact.Helpers.Ui
{
    /// <summary>
    /// WPF 窗口外观工具（Mica/Acrylic 背景、居中）。macOS 版界面由 Avalonia 负责，这里为空实现
    /// </summary>
    public static class WindowHelper
    {
        public static void TryApplySystemBackdrop(System.Windows.Window window)
        {
        }

        public static void ApplyThemeToWindow(System.Windows.Window window, BetterGenshinImpact.Core.Config.ThemeType themeType)
        {
        }

        public static void CenterOnVisibleOwner(System.Windows.Window window)
        {
        }
    }
}

namespace Wpf.Ui.Violeta.Appearance
{
    public enum SystemMenuTheme
    {
        Default,
        Light,
        Dark,
    }

    public static class SystemMenuThemeManager
    {
        public static void Apply(SystemMenuTheme theme)
        {
        }
    }
}

namespace Wpf.Ui.Violeta.Win32
{
    public static class IconManager
    {
        public static string[] CacheExcludeExtensions { get; set; } = [];
    }
}

namespace BetterGenshinImpact.Helpers.Extensions
{
    using OpenCvSharp;
    using System.Windows.Media.Imaging;

    /// <summary>
    /// 原版 BitmapExtension 依赖 GDI+（LockBits），macOS 版基于 OpenCV Mat 实现
    /// </summary>
    public static class BitmapExtension
    {
        public static BitmapSource ToBitmapSource(this System.Drawing.Bitmap bitmap) => BitmapSource.FromMat(bitmap.Mat);

        public static BitmapSource ToBitmapSource(this System.Drawing.Bitmap bitmap, out bool bottomUp)
        {
            bottomUp = false;
            return BitmapSource.FromMat(bitmap.Mat);
        }

        public static BitmapImage ToBitmapImage(this System.Drawing.Bitmap bitmap)
        {
            var image = new BitmapImage
            {
                StreamSource = new System.IO.MemoryStream(bitmap.Mat.ToBytes(".png")),
            };
            image.EndInit();
            return image;
        }

        public static Scalar ToScalar(this System.Drawing.Color color) => new(color.R, color.G, color.B);
    }
}
