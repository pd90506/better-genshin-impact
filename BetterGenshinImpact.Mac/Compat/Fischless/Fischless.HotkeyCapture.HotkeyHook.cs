using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Gma.System.MouseKeyHook;
using Vanara.PInvoke;

namespace Fischless.HotkeyCapture;

/// <summary>
/// 全局热键。Windows 版使用 RegisterHotKey，macOS 版改为监听全局键盘事件（CGEventTap）后自行匹配
/// </summary>
public sealed class HotkeyHook : IDisposable
{
    private readonly List<(User32.HotKeyModifiers Modifier, Keys Key)> _hotkeys = new();
    private IKeyboardMouseEvents? _events;

    public event EventHandler<KeyPressedEventArgs>? KeyPressed = null;

    public void RegisterHotKey(User32.HotKeyModifiers modifier, Keys key)
    {
        lock (_hotkeys)
        {
            if (_hotkeys.Contains((modifier, key)))
            {
                throw new InvalidOperationException("Hotkey already registered");
            }

            _hotkeys.Add((modifier, key));
        }

        if (_events == null)
        {
            _events = Hook.GlobalEvents();
            _events.KeyDown += OnKeyDown;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var modifier = User32.HotKeyModifiers.MOD_NONE;
        if (e.Alt)
        {
            modifier |= User32.HotKeyModifiers.MOD_ALT;
        }

        if (e.Control)
        {
            modifier |= User32.HotKeyModifiers.MOD_CONTROL;
        }

        if (e.Shift)
        {
            modifier |= User32.HotKeyModifiers.MOD_SHIFT;
        }

        bool matched;
        lock (_hotkeys)
        {
            matched = _hotkeys.Exists(h => h.Key == e.KeyCode && (h.Modifier & ~User32.HotKeyModifiers.MOD_NOREPEAT) == modifier);
        }

        if (matched)
        {
            KeyPressed?.Invoke(this, new KeyPressedEventArgs(modifier, e.KeyCode));
        }
    }

    public void UnregisterHotKey()
    {
        lock (_hotkeys)
        {
            _hotkeys.Clear();
        }
    }

    public void Dispose()
    {
        UnregisterHotKey();
        if (_events != null)
        {
            _events.KeyDown -= OnKeyDown;
            _events.Dispose();
            _events = null;
        }
    }
}
