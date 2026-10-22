using System;
using System.Collections.Generic;
using BetterGenshinImpact.Platform.Mac;
using Microsoft.Extensions.Logging;

namespace BetterGenshinImpact.Core.Recognition.ONNX
{
    public interface IInferenceDeviceDiscovery
    {
        IReadOnlyList<InferenceDeviceInfo> GetDevices(InferenceDeviceType engine, string runtimeDirectory);
    }

    /// <summary>
    /// macOS 版只使用 ONNX Runtime 自带的 CPU 推理（Apple Silicon 上已足够快），不探测 DirectML / CUDA / OpenVINO
    /// </summary>
    public sealed class InferenceDeviceDiscovery : IInferenceDeviceDiscovery
    {
        public IReadOnlyList<InferenceDeviceInfo> GetDevices(InferenceDeviceType engine, string runtimeDirectory) => engine switch
        {
            InferenceDeviceType.Cpu => [ResolvedInferenceRuntime.CpuDevice],
            _ => [],
        };
    }
}

namespace BetterGenshinImpact.Core.Monitor
{
    /// <summary>
    /// 相对鼠标移动监听。macOS 上 CGEventTap 的鼠标移动事件自带 DeltaX / DeltaY，
    /// 不论游戏是否锁定鼠标都能拿到相对位移，DirectInput 与 RawInput 两种来源都用它实现
    /// </summary>
    public abstract class MacRelativeMouseMonitor(ILogger logger) : RelativeMouseInputMonitorBase(logger)
    {
        private IDisposable? _subscription;

        protected override void StartCore()
        {
            _subscription = MacEventTap.Shared.SubscribeMouseDelta((dx, dy) =>
                Publish(new RelativeMouseMoveEventArgs(dx, dy, DateTime.Now)));
        }

        protected override void StopCore()
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }

    public sealed class DirectInputMonitor(ILogger<DirectInputMonitor> logger) : MacRelativeMouseMonitor(logger);

    public sealed class RawInputMonitor(ILogger<RawInputMonitor> logger) : MacRelativeMouseMonitor(logger);
}

namespace BetterGenshinImpact.GameTask.Runtime.WebPage
{
    /// <summary>
    /// 云原神网页版依赖 WebView2，macOS 版不提供。保留类型名供 GameRuntimeService 编译
    /// </summary>
    public sealed class WebPageRuntimeProvider : IGameRuntimeProvider
    {
        public GameRuntimeKind Kind => GameRuntimeKind.WebPage;

        public System.Threading.Tasks.Task<GameRuntime?> AcquireAsync(System.Threading.CancellationToken ct) =>
            System.Threading.Tasks.Task.FromResult<GameRuntime?>(null);

        public void CloseGame()
        {
        }

        public Func<System.Threading.Tasks.Task>? StopCaptureBeforeCloseAsync { private get; set; }
    }
}

namespace System.Windows.Forms
{
    public enum MessageBoxIcon
    {
        None = 0,
        Hand = 16,
        Error = 16,
        Stop = 16,
        Question = 32,
        Exclamation = 48,
        Warning = 48,
        Asterisk = 64,
        Information = 64,
    }

    public enum MessageBoxButtons
    {
        OK = 0,
        OKCancel = 1,
        YesNo = 4,
    }

    public static class MessageBox
    {
        public static DialogResult Show(string text, string caption = "", MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None)
        {
            var result = System.Windows.MessageBox.Show(text, caption, (System.Windows.MessageBoxButton)(int)buttons);
            return result switch
            {
                System.Windows.MessageBoxResult.OK => DialogResult.OK,
                System.Windows.MessageBoxResult.Yes => DialogResult.Yes,
                System.Windows.MessageBoxResult.No => DialogResult.No,
                _ => DialogResult.Cancel,
            };
        }
    }
}

namespace BetterGenshinImpact.Core.Monitor
{
    /// <summary>
    /// 原版 Wine/MouseKeyMonitor.Wine.cs 的空实现：macOS 上全局键鼠事件由 CGEventTap 提供，不需要轮询
    /// </summary>
    public partial class MouseKeyMonitor
    {
        private void TrySubscribeWinePolling()
        {
        }

        private void DisposeWineAddon()
        {
        }
    }
}
