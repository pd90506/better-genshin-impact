using System;
using BetterGenshinImpact.Core.Input;
using BetterGenshinImpact.GameTask;
using BetterGenshinImpact.Platform.Mac;
using Fischless.WindowsInput;
using Microsoft.Extensions.Logging;
using Vanara.PInvoke;

namespace BetterGenshinImpact.Platform.Mac
{
    /// <summary>
    /// macOS 输入通道：所有操作转给 <see cref="MacInputSimulator"/>
    /// </summary>
    internal sealed class MacInputChannel(MacInputSimulator simulator) : InputChannelBase
    {
        public MacInputSimulator Simulator { get; } = simulator;

        protected override void OnKeyDown(User32.VK key) => Simulator.KeyDown(key);

        protected override void OnKeyUp(User32.VK key) => Simulator.KeyUp(key);

        protected override void OnKeyPress(User32.VK key) => Simulator.KeyPress(key);

        protected override void OnMouseButton(InputMouseButton button, bool down)
        {
            IMouseSimulator mouse = Simulator;
            switch (button)
            {
                case InputMouseButton.Left:
                    _ = down ? mouse.LeftButtonDown() : mouse.LeftButtonUp();
                    break;
                case InputMouseButton.Right:
                    _ = down ? mouse.RightButtonDown() : mouse.RightButtonUp();
                    break;
                case InputMouseButton.Middle:
                    _ = down ? mouse.MiddleButtonDown() : mouse.MiddleButtonUp();
                    break;
                case InputMouseButton.X1:
                    _ = down ? mouse.XButtonDown(1) : mouse.XButtonUp(1);
                    break;
                case InputMouseButton.X2:
                    _ = down ? mouse.XButtonDown(2) : mouse.XButtonUp(2);
                    break;
            }
        }

        protected override void OnMouseClick(InputMouseButton button)
        {
            IMouseSimulator mouse = Simulator;
            switch (button)
            {
                case InputMouseButton.Left:
                    mouse.LeftButtonClick();
                    break;
                case InputMouseButton.Right:
                    mouse.RightButtonClick();
                    break;
                case InputMouseButton.Middle:
                    mouse.MiddleButtonClick();
                    break;
                case InputMouseButton.X1:
                    mouse.XButtonClick(1);
                    break;
                case InputMouseButton.X2:
                    mouse.XButtonClick(2);
                    break;
            }
        }

        protected override void OnMoveBy(int dx, int dy) => Simulator.MoveMouseBy(dx, dy);

        protected override void OnMoveTo(double absX, double absY) => Simulator.MoveMouseTo(absX, absY);

        protected override void OnScroll(int clicks) => Simulator.VerticalScroll(clicks);

        public override bool IsKeyDown(User32.VK key) => MacInputSimulator.IsKeyDownNow(key);
    }
}

namespace BetterGenshinImpact.Core.Input.Backends.Win32
{
    /// <summary>
    /// macOS 版沿用原版的类型名，原版各处 new Win32InputBackend(hWnd) 无需改动。
    /// 前台通道：CGEventPost 到 HID 层，由当前前台应用（GeForce NOW）接收；
    /// 后台通道：CGEventPostToPid 直接投递给游戏窗口所属进程，游戏不在前台时也能操作（可在 Mac 设置中关闭）。
    /// </summary>
    public sealed class Win32InputBackend : IInputBackend
    {
        /// <param name="hWnd">macOS 上为游戏窗口的 CGWindowID。为 0 时后台通道与前台通道相同</param>
        public Win32InputBackend(IntPtr hWnd)
        {
            Foreground = new MacInputChannel(new MacInputSimulator());
            var pid = 0;
            if (hWnd != IntPtr.Zero && MacPlatformConfig.Instance.BackgroundInputEnabled)
            {
                pid = MacWindows.Get((uint)hWnd)?.ProcessId ?? 0;
            }

            Background = pid > 0 ? new MacInputChannel(new MacInputSimulator(pid)) : Foreground;
        }

        public InputBackendKind Kind => InputBackendKind.Win32;

        public IInputChannel Foreground { get; }

        public IInputChannel Background { get; }

        public void ReleaseAll()
        {
            if (!ReferenceEquals(Background, Foreground))
            {
                Background.ReleaseAll();
            }

            Foreground.ReleaseAll();
        }

        public void Dispose()
        {
        }
    }
}

namespace BetterGenshinImpact.Core.Simulator
{
    /// <summary>
    /// 原版 Simulation 的 macOS 实现：SendInput 换成 CGEvent
    /// </summary>
    public class Simulation
    {
        private static readonly MacInputSimulator MacSimulator = new();

        public static InputSimulator SendInput { get; } = new(MacSimulator, MacSimulator, MacSimulator);

        public static void ReleaseAllKey()
        {
            foreach (User32.VK key in Enum.GetValues(typeof(User32.VK)))
            {
                if ((int)key is 0x01 or 0x02 or 0x04 or 0x05 or 0x06)
                {
                    continue;
                }

                if (IsKeyDown(key))
                {
                    GameTask.Common.TaskControl.Logger.LogDebug($"解除{key}的按下状态.");
                    SendInput.Keyboard.KeyUp(key);
                }
            }

            SendInput.Mouse.LeftButtonUp();
            SendInput.Mouse.RightButtonUp();
            SendInput.Mouse.MiddleButtonUp();
        }

        public static bool IsKeyDown(User32.VK key) => MacInputSimulator.IsKeyDownNow(key);
    }
}

namespace BetterGenshinImpact.Helpers
{
    /// <summary>
    /// 主屏幕的"物理像素"尺寸。macOS 版定义为主显示器 point × 缩放比例，与窗口截图像素一致
    /// </summary>
    public class PrimaryScreen
    {
        public static System.Drawing.Size WorkingArea
        {
            get
            {
                var (w, h) = MacWindows.MainDisplaySize();
                var scale = MacWindows.MainScreenScale();
                return new System.Drawing.Size((int)Math.Round(w * scale), (int)Math.Round(h * scale));
            }
        }
    }

    public class DpiHelper
    {
        /// <summary>
        /// macOS 版桌面坐标已经是像素，输入时由 MacInputSimulator 换算，不再额外缩放
        /// </summary>
        public static float ScaleY => 1f;

        public static DpiScaleF GetScale(nint hWnd = 0) => new(1f, 1f);
    }

    public readonly struct DpiScaleF(float x = 1f, float y = 1f)
    {
        public float X => x;

        public float Y => y;

        public DpiScaleF Reserve() => new(1f / X, 1f / Y);

        public override string ToString() => $"{X},{Y}";
    }
}
