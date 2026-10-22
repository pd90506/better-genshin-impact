using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using BetterGenshinImpact.Platform;

// WinForms 输入事件类型 + MouseKeyHook（Gma.System.MouseKeyHook）在 macOS 上的替代。
// 全局键鼠事件由 macOS 的 CGEventTap 实现（见 Platform/Mac/MacGlobalHook.cs），通过 Hook.Factory 注入。
namespace System.Windows.Forms
{
    [Flags]
    public enum MouseButtons
    {
        None = 0,
        Left = 0x100000,
        Right = 0x200000,
        Middle = 0x400000,
        XButton1 = 0x800000,
        XButton2 = 0x1000000,
    }

    public class KeyEventArgs : EventArgs
    {
        public KeyEventArgs(Keys keyData)
        {
            KeyData = keyData;
        }

        public Keys KeyData { get; }

        public Keys KeyCode => KeyData & Keys.KeyCode;

        public Keys Modifiers => KeyData & Keys.Modifiers;

        public int KeyValue => (int)KeyCode;

        public bool Alt => (KeyData & Keys.Alt) == Keys.Alt;

        public bool Control => (KeyData & Keys.Control) == Keys.Control;

        public bool Shift => (KeyData & Keys.Shift) == Keys.Shift;

        public bool Handled { get; set; }

        public bool SuppressKeyPress { get; set; }
    }

    public delegate void KeyEventHandler(object? sender, KeyEventArgs e);

    public class KeyPressEventArgs : EventArgs
    {
        public KeyPressEventArgs(char keyChar)
        {
            KeyChar = keyChar;
        }

        public char KeyChar { get; set; }

        public bool Handled { get; set; }
    }

    public delegate void KeyPressEventHandler(object? sender, KeyPressEventArgs e);

    public class MouseEventArgs : EventArgs
    {
        public MouseEventArgs(MouseButtons button, int clicks, int x, int y, int delta)
        {
            Button = button;
            Clicks = clicks;
            X = x;
            Y = y;
            Delta = delta;
        }

        public MouseButtons Button { get; }

        public int Clicks { get; }

        public int X { get; }

        public int Y { get; }

        public int Delta { get; }

        public System.Drawing.Point Location => new(X, Y);
    }

    public delegate void MouseEventHandler(object? sender, MouseEventArgs e);

    public static class Control
    {
        public static Keys ModifierKeys => Gma.System.MouseKeyHook.Hook.CurrentModifiers;

        public static MouseButtons MouseButtons => MouseButtons.None;

        public static System.Drawing.Point MousePosition => Gma.System.MouseKeyHook.Hook.CurrentMousePosition();
    }

    public sealed class Screen
    {
        public static Screen PrimaryScreen { get; } = new();

        public static Screen[] AllScreens => [PrimaryScreen];

        public Rectangle Bounds => new(0, 0, (int)System.Windows.SystemParameters.PrimaryScreenWidth, (int)System.Windows.SystemParameters.PrimaryScreenHeight);

        public Rectangle WorkingArea => Bounds;

        public bool Primary => true;

        public string DeviceName => "Main";
    }

    public static class Clipboard
    {
        public static void SetText(string text) => System.Windows.Clipboard.SetText(text);

        public static void SetDataObject(object data) => System.Windows.Clipboard.SetDataObject(data);

        public static void SetDataObject(object data, bool copy) => System.Windows.Clipboard.SetDataObject(data);

        public static string GetText() => System.Windows.Clipboard.GetText();

        public static bool ContainsText() => System.Windows.Clipboard.ContainsText();

        public static void Clear() => System.Windows.Clipboard.Clear();
    }

    public enum DialogResult
    {
        None = 0,
        OK = 1,
        Cancel = 2,
        Abort = 3,
        Retry = 4,
        Ignore = 5,
        Yes = 6,
        No = 7,
    }

    public abstract class FileDialogBase : IDisposable
    {
        public string Title { get; set; } = string.Empty;

        public string Filter { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public string InitialDirectory { get; set; } = string.Empty;

        public string DefaultExt { get; set; } = string.Empty;

        public bool RestoreDirectory { get; set; }

        public bool AddExtension { get; set; } = true;

        public bool CheckFileExists { get; set; }

        public bool Multiselect { get; set; }

        public int FilterIndex { get; set; }

        public string[] FileNames => string.IsNullOrEmpty(FileName) ? [] : [FileName];

        protected abstract bool IsSave { get; }

        public DialogResult ShowDialog() => ShowDialog(null) == true ? DialogResult.OK : DialogResult.Cancel;

        public bool? ShowDialog(object? owner)
        {
            var path = UiPlatform.RunSync(() => UiPlatform.Dialogs.PickFileAsync(Title, Filter, IsSave, FileName));
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            FileName = path;
            return true;
        }

        public void Dispose()
        {
        }
    }

    public sealed class SaveFileDialog : FileDialogBase
    {
        public bool OverwritePrompt { get; set; } = true;

        protected override bool IsSave => true;
    }

    public sealed class OpenFileDialog : FileDialogBase
    {
        protected override bool IsSave => false;
    }

    public sealed class FolderBrowserDialog : IDisposable
    {
        public string Description { get; set; } = string.Empty;

        public string SelectedPath { get; set; } = string.Empty;

        public bool UseDescriptionForTitle { get; set; }

        public bool ShowNewFolderButton { get; set; } = true;

        public DialogResult ShowDialog()
        {
            var path = UiPlatform.RunSync(() => UiPlatform.Dialogs.PickFolderAsync(Description));
            if (string.IsNullOrEmpty(path))
            {
                return DialogResult.Cancel;
            }

            SelectedPath = path;
            return DialogResult.OK;
        }

        public void Dispose()
        {
        }
    }
}

namespace Gma.System.MouseKeyHook
{
    using global::System;
    using global::System.Windows.Forms;

    public class MouseEventExtArgs : MouseEventArgs
    {
        public MouseEventExtArgs(MouseButtons buttons, int clicks, Point point, int delta, int timestamp, bool isMouseButtonDown,
            bool isMouseButtonUp, bool isHorizontalWheel = false)
            : base(buttons, clicks, point.X, point.Y, delta)
        {
            Timestamp = timestamp;
            IsMouseButtonDown = isMouseButtonDown;
            IsMouseButtonUp = isMouseButtonUp;
            IsHorizontalWheel = isHorizontalWheel;
        }

        public bool Handled { get; set; }

        public bool WheelScrolled => Delta != 0;

        public bool Clicked => Clicks > 0;

        public bool IsMouseButtonDown { get; }

        public bool IsMouseButtonUp { get; }

        public bool IsHorizontalWheel { get; }

        public int Timestamp { get; }

        public global::System.Drawing.Point Point => Location;
    }

    public interface IKeyboardEvents
    {
        event KeyEventHandler KeyDown;

        event KeyPressEventHandler KeyPress;

        event KeyEventHandler KeyUp;
    }

    public interface IMouseEvents
    {
        event MouseEventHandler MouseMove;

        event EventHandler<MouseEventExtArgs> MouseMoveExt;

        event MouseEventHandler MouseClick;

        event MouseEventHandler MouseDown;

        event EventHandler<MouseEventExtArgs> MouseDownExt;

        event MouseEventHandler MouseUp;

        event EventHandler<MouseEventExtArgs> MouseUpExt;

        event MouseEventHandler MouseWheel;

        event EventHandler<MouseEventExtArgs> MouseWheelExt;

        event MouseEventHandler MouseDoubleClick;
    }

    public interface IKeyboardMouseEvents : IKeyboardEvents, IMouseEvents, IDisposable
    {
    }

    /// <summary>
    /// 全局键鼠事件的公共实现，平台层调用 Raise* 方法发出事件
    /// </summary>
    public class KeyboardMouseEventsBase : IKeyboardMouseEvents
    {
        public event KeyEventHandler? KeyDown;

        public event KeyPressEventHandler? KeyPress;

        public event KeyEventHandler? KeyUp;

        public event MouseEventHandler? MouseMove;

        public event EventHandler<MouseEventExtArgs>? MouseMoveExt;

        public event MouseEventHandler? MouseClick;

        public event MouseEventHandler? MouseDown;

        public event EventHandler<MouseEventExtArgs>? MouseDownExt;

        public event MouseEventHandler? MouseUp;

        public event EventHandler<MouseEventExtArgs>? MouseUpExt;

        public event MouseEventHandler? MouseWheel;

        public event EventHandler<MouseEventExtArgs>? MouseWheelExt;

        public event MouseEventHandler? MouseDoubleClick;

        public void RaiseKeyDown(KeyEventArgs e) => KeyDown?.Invoke(this, e);

        public void RaiseKeyUp(KeyEventArgs e) => KeyUp?.Invoke(this, e);

        public void RaiseKeyPress(KeyPressEventArgs e) => KeyPress?.Invoke(this, e);

        public void RaiseMouseMove(MouseEventExtArgs e)
        {
            MouseMove?.Invoke(this, e);
            MouseMoveExt?.Invoke(this, e);
        }

        public void RaiseMouseDown(MouseEventExtArgs e)
        {
            MouseDown?.Invoke(this, e);
            MouseDownExt?.Invoke(this, e);
        }

        public void RaiseMouseUp(MouseEventExtArgs e)
        {
            MouseUp?.Invoke(this, e);
            MouseUpExt?.Invoke(this, e);
            MouseClick?.Invoke(this, e);
        }

        public void RaiseMouseWheel(MouseEventExtArgs e)
        {
            MouseWheel?.Invoke(this, e);
            MouseWheelExt?.Invoke(this, e);
        }

        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

    public static class Hook
    {
        /// <summary>
        /// 平台层注入：创建全局键鼠监听
        /// </summary>
        public static Func<IKeyboardMouseEvents>? Factory { get; set; }

        /// <summary>
        /// 平台层注入：查询当前修饰键与鼠标位置
        /// </summary>
        public static Func<Keys>? ModifiersProvider { get; set; }

        public static Func<Point>? MousePositionProvider { get; set; }

        public static Keys CurrentModifiers => ModifiersProvider?.Invoke() ?? Keys.None;

        public static Point CurrentMousePosition() => MousePositionProvider?.Invoke() ?? Point.Empty;

        public static IKeyboardMouseEvents GlobalEvents() => Factory?.Invoke() ?? new KeyboardMouseEventsBase();

        public static IKeyboardMouseEvents AppEvents() => GlobalEvents();
    }
}

namespace Microsoft.Win32
{
    using global::System.Windows.Forms;

    /// <summary>
    /// WPF 的 Microsoft.Win32 文件对话框，与 WinForms 版本行为一致，只是 ShowDialog 返回 bool?
    /// </summary>
    public sealed class SaveFileDialog
    {
        private readonly global::System.Windows.Forms.SaveFileDialog _inner = new();

        public string Title { get => _inner.Title; set => _inner.Title = value; }

        public string Filter { get => _inner.Filter; set => _inner.Filter = value; }

        public string FileName { get => _inner.FileName; set => _inner.FileName = value; }

        public string InitialDirectory { get => _inner.InitialDirectory; set => _inner.InitialDirectory = value; }

        public string DefaultExt { get => _inner.DefaultExt; set => _inner.DefaultExt = value; }

        public bool AddExtension { get; set; } = true;

        public bool OverwritePrompt { get; set; } = true;

        public int FilterIndex { get; set; }

        public bool? ShowDialog() => _inner.ShowDialog(null);

        public bool? ShowDialog(object? owner) => _inner.ShowDialog(owner);
    }

    public sealed class OpenFileDialog
    {
        private readonly global::System.Windows.Forms.OpenFileDialog _inner = new();

        public string Title { get => _inner.Title; set => _inner.Title = value; }

        public string Filter { get => _inner.Filter; set => _inner.Filter = value; }

        public string FileName { get => _inner.FileName; set => _inner.FileName = value; }

        public string[] FileNames => _inner.FileNames;

        public string InitialDirectory { get => _inner.InitialDirectory; set => _inner.InitialDirectory = value; }

        public string DefaultExt { get => _inner.DefaultExt; set => _inner.DefaultExt = value; }

        public bool Multiselect { get; set; }

        public bool CheckFileExists { get; set; }

        public int FilterIndex { get; set; }

        public bool? ShowDialog() => _inner.ShowDialog(null);

        public bool? ShowDialog(object? owner) => _inner.ShowDialog(owner);
    }

    public sealed class OpenFolderDialog
    {
        public string Title { get; set; } = string.Empty;

        public string InitialDirectory { get; set; } = string.Empty;

        public string FolderName { get; set; } = string.Empty;

        public bool Multiselect { get; set; }

        public bool? ShowDialog() => ShowDialog(null);

        public bool? ShowDialog(object? owner)
        {
            var path = BetterGenshinImpact.Platform.UiPlatform.RunSync(() => BetterGenshinImpact.Platform.UiPlatform.Dialogs.PickFolderAsync(Title));
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            FolderName = path;
            return true;
        }
    }
}
