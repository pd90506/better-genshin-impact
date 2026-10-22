using System;
using System.Threading;
using System.Threading.Tasks;
using BetterGenshinImpact.Core.Input.Backends.Win32;
using BetterGenshinImpact.Core.Mask;
using BetterGenshinImpact.Platform.Mac;
using BetterGenshinImpact.Service.Interface;
using BetterGenshinImpact.View.Windows;
using Microsoft.Extensions.Logging;
using Vanara.PInvoke;

namespace BetterGenshinImpact.GameTask.Runtime.Win32
{
    /// <summary>
    /// macOS 上的游戏窗口：GeForce NOW（或浏览器）中的原神画面。
    /// 沿用原版类型名，<see cref="IGameWindow.Handle"/> 为 CGWindowID。
    /// </summary>
    public sealed class Win32GameWindow : IGameWindow
    {
        private readonly Timer _watchTimer;
        private (double X, double Y, double W, double H) _lastBounds;
        private bool _disposed;

        public Win32GameWindow(nint hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                throw new ArgumentException("游戏窗口句柄为空", nameof(hWnd));
            }

            var info = MacWindows.Get((uint)hWnd) ?? throw new ArgumentException("找不到游戏窗口");
            Handle = hWnd;
            ProcessId = info.ProcessId;
            ProcessName = info.OwnerName;
            _lastBounds = (info.X, info.Y, info.Width, info.Height);
            // macOS 没有窗口移动事件可订阅，轮询窗口位置
            _watchTimer = new Timer(_ => CheckBounds(), null, 500, 500);
        }

        public nint Handle { get; }

        public int ProcessId { get; }

        /// <summary>
        /// 窗口所属应用名，用于按进程判断前台
        /// </summary>
        public string ProcessName { get; }

        public MacWindowInfo? Info => MacWindows.Get((uint)Handle);

        public GameViewport Viewport => new(SystemControl.GetCaptureRect(Handle), 1f);

        public bool IsAlive => MacWindows.IsProcessAlive(ProcessId) && Info != null;

        public bool IsForeground => MacWindows.FrontmostProcessId() == ProcessId;

        public bool IsMinimized => Info is { IsOnScreen: false };

        public bool RequiresForeground => true;

        public void Activate() => MacWindows.Activate(ProcessId);

        public event EventHandler? ViewportChanged;

        private void CheckBounds()
        {
            if (_disposed)
            {
                return;
            }

            var info = Info;
            if (info == null)
            {
                return;
            }

            var bounds = (info.X, info.Y, info.Width, info.Height);
            if (bounds != _lastBounds)
            {
                _lastBounds = bounds;
                ViewportChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _watchTimer.Dispose();
            ViewportChanged = null;
        }
    }

    /// <summary>
    /// macOS 运行环境：查找 GeForce NOW 窗口，截图用 <see cref="MacScreenCapture"/>，键鼠用 CGEvent。
    /// 沿用原版类型名，首页的手动选窗等逻辑无需改动
    /// </summary>
    public sealed class Win32RuntimeProvider(
        IConfigService configService,
        IMaskWindowDrawingBoard maskWindowDrawingBoard,
        IMaskWindowMapState maskWindowMapState,
        ILogger<Win32RuntimeProvider> logger)
        : IGameRuntimeProvider
    {
        public GameRuntimeKind Kind => GameRuntimeKind.Win32Window;

        public async Task<GameRuntime?> AcquireAsync(CancellationToken ct)
        {
            var hWnd = SystemControl.FindGenshinImpactHandle();
            if (hWnd == IntPtr.Zero)
            {
                await ThemedMessageBox.ErrorAsync(
                    "未找到游戏窗口，请先打开 GeForce NOW 并进入原神。\n\n如果使用浏览器游玩，请在「Mac 设置」中把窗口所属应用改为浏览器名称。");
                return null;
            }

            ct.ThrowIfCancellationRequested();
            return AttachTo(hWnd);
        }

        public GameRuntime AttachTo(nint hWnd)
        {
            SystemControl.ActivateWindow(hWnd);
            var window = new Win32GameWindow(hWnd);
            MacScreenCapture? capture = null;
            try
            {
                if (window.IsMinimized)
                {
                    throw new ArgumentException("游戏窗口不能最小化");
                }

                capture = new MacScreenCapture();
                capture.Start(hWnd);
                var viewport = window.Viewport;
                logger.LogInformation("已绑定游戏窗口：{Owner}，游戏画面 {Width}x{Height}", window.ProcessName, viewport.Width, viewport.Height);
                return new GameRuntime(Kind, window, capture, new Win32InputBackend(hWnd), maskWindowDrawingBoard, maskWindowMapState);
            }
            catch
            {
                capture?.Dispose();
                window.Dispose();
                throw;
            }
        }

        public void CloseGame() => SystemControl.CloseGameProcesses();
    }
}
