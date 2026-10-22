// 复制自 Fischless.HotkeyCapture/KeyPressedEventArgs.cs，补充 ImplicitUsings 隐式引用的命名空间
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Vanara.PInvoke;

namespace Fischless.HotkeyCapture;

public class KeyPressedEventArgs : EventArgs
{
    public User32.HotKeyModifiers Modifier { get; }
    public Keys Key { get; }

    public KeyPressedEventArgs(User32.HotKeyModifiers modifier, Keys key)
    {
        Modifier = modifier;
        Key = key;
    }
}
