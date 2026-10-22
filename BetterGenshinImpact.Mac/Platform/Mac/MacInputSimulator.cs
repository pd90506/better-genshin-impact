using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Fischless.WindowsInput;
using Vanara.PInvoke;
using static BetterGenshinImpact.Platform.Mac.MacNative;

namespace BetterGenshinImpact.Platform.Mac;

/// <summary>
/// 用 CGEvent 实现 Fischless.WindowsInput 的键鼠模拟接口，供原版的 Simulation.SendInput 与输入通道复用。
/// <para>
/// 键码使用原版的 Windows VK，在这里转换为 macOS 键码；绝对坐标与原版一致，是按主屏幕归一化到 0~65535 的桌面坐标。
/// 所有模拟事件都带有 <see cref="MacEventTap.SyntheticMarker"/>，全局键鼠监听会忽略它们。
/// </para>
/// </summary>
public sealed class MacInputSimulator : IKeyboardSimulator, IMouseSimulator, IInputDeviceStateAdaptor
{
    private readonly object _stateLock = new();
    private readonly HashSet<int> _heldModifiers = new();
    private bool _leftDown;
    private bool _rightDown;
    private bool _otherDown;

    /// <param name="targetProcessId">大于 0 时把事件直接投递给该进程（后台输入），否则投递到 HID 层（前台输入）</param>
    public MacInputSimulator(int targetProcessId = 0)
    {
        TargetProcessId = targetProcessId;
    }

    public int TargetProcessId { get; set; }

    public IMouseSimulator Mouse => this;

    public IKeyboardSimulator Keyboard => this;

    private static int HoldMs => Math.Clamp(MacPlatformConfig.Instance.KeyPressHoldMs, 10, 500);

    // ---------------- 事件投递 ----------------

    private void Post(IntPtr evt)
    {
        if (evt == IntPtr.Zero)
        {
            return;
        }

        try
        {
            CGEventSetIntegerValueField(evt, kCGEventSourceUserData, MacEventTap.SyntheticMarker);
            if (TargetProcessId > 0)
            {
                CGEventPostToPid(TargetProcessId, evt);
            }
            else
            {
                CGEventPost(kCGHIDEventTap, evt);
            }
        }
        finally
        {
            CFRelease(evt);
        }
    }

    private ulong CurrentFlags()
    {
        ulong flags = 0;
        lock (_stateLock)
        {
            foreach (var vk in _heldModifiers)
            {
                flags |= MacKeyMap.ModifierFlag(vk);
            }
        }

        return flags;
    }

    private static CGPoint CursorLocation()
    {
        var evt = CGEventCreate(IntPtr.Zero);
        var p = CGEventGetLocation(evt);
        CFRelease(evt);
        return p;
    }

    // ---------------- 键盘 ----------------

    private void SendKey(User32.VK keyCode, bool down)
    {
        if (!IsMacOS)
        {
            return;
        }

        var vk = (int)keyCode;
        switch (vk)
        {
            // 鼠标按键也可能以 VK 的形式出现
            case 0x01:
                SendMouseButton(kCGMouseButtonLeft, down);
                return;
            case 0x02:
                SendMouseButton(kCGMouseButtonRight, down);
                return;
            case 0x04:
                SendMouseButton(kCGMouseButtonCenter, down);
                return;
            case 0x05:
                SendMouseButton(3, down);
                return;
            case 0x06:
                SendMouseButton(4, down);
                return;
        }

        if (!MacKeyMap.TryGetMacKeyCode(vk, out var macCode))
        {
            return;
        }

        var modifierFlag = MacKeyMap.ModifierFlag(vk);
        if (modifierFlag != 0)
        {
            lock (_stateLock)
            {
                if (down)
                {
                    _heldModifiers.Add(vk);
                }
                else
                {
                    _heldModifiers.Remove(vk);
                }
            }
        }

        var evt = CGEventCreateKeyboardEvent(IntPtr.Zero, macCode, down);
        CGEventSetFlags(evt, CurrentFlags());
        Post(evt);
    }

    public IKeyboardSimulator KeyDown(User32.VK keyCode)
    {
        SendKey(keyCode, true);
        return this;
    }

    public IKeyboardSimulator KeyDown(bool? isExtendedKey, User32.VK keyCode) => KeyDown(keyCode);

    public IKeyboardSimulator KeyUp(User32.VK keyCode)
    {
        SendKey(keyCode, false);
        return this;
    }

    public IKeyboardSimulator KeyUp(bool? isExtendedKey, User32.VK keyCode) => KeyUp(keyCode);

    public IKeyboardSimulator KeyPress(User32.VK keyCode)
    {
        SendKey(keyCode, true);
        Thread.Sleep(HoldMs);
        SendKey(keyCode, false);
        return this;
    }

    public IKeyboardSimulator KeyPress(bool? isExtendedKey, User32.VK keyCode) => KeyPress(keyCode);

    public IKeyboardSimulator KeyPress(params User32.VK[] keyCodes)
    {
        foreach (var key in keyCodes)
        {
            KeyPress(key);
        }

        return this;
    }

    public IKeyboardSimulator KeyPress(bool? isExtendedKey, params User32.VK[] keyCodes) => KeyPress(keyCodes);

    public IKeyboardSimulator ModifiedKeyStroke(IEnumerable<User32.VK> modifierKeyCodes, IEnumerable<User32.VK> keyCodes)
    {
        var modifiers = modifierKeyCodes.ToList();
        foreach (var m in modifiers)
        {
            KeyDown(m);
        }

        foreach (var k in keyCodes)
        {
            KeyPress(k);
        }

        foreach (var m in Enumerable.Reverse(modifiers))
        {
            KeyUp(m);
        }

        return this;
    }

    public IKeyboardSimulator ModifiedKeyStroke(IEnumerable<User32.VK> modifierKeyCodes, User32.VK keyCode) =>
        ModifiedKeyStroke(modifierKeyCodes, [keyCode]);

    public IKeyboardSimulator ModifiedKeyStroke(User32.VK modifierKey, IEnumerable<User32.VK> keyCodes) =>
        ModifiedKeyStroke([modifierKey], keyCodes);

    public IKeyboardSimulator ModifiedKeyStroke(User32.VK modifierKeyCode, User32.VK keyCode) =>
        ModifiedKeyStroke([modifierKeyCode], [keyCode]);

    public IKeyboardSimulator TextEntry(string text)
    {
        foreach (var c in text)
        {
            TextEntry(c);
        }

        return this;
    }

    /// <summary>
    /// 输入任意 Unicode 字符：创建空键码事件并附带字符串
    /// </summary>
    public IKeyboardSimulator TextEntry(char character)
    {
        if (!IsMacOS)
        {
            return this;
        }

        foreach (var down in new[] { true, false })
        {
            var evt = CGEventCreateKeyboardEvent(IntPtr.Zero, 0, down);
            var chars = new[] { character };
            CGEventKeyboardSetUnicodeString(evt, 1, chars);
            Post(evt);
        }

        return this;
    }

    [DllImport(CoreGraphics)]
    private static extern void CGEventKeyboardSetUnicodeString(IntPtr evt, nuint stringLength, char[] unicodeString);

    IKeyboardSimulator IKeyboardSimulator.Sleep(int millsecondsTimeout)
    {
        Thread.Sleep(millsecondsTimeout);
        return this;
    }

    IKeyboardSimulator IKeyboardSimulator.Sleep(TimeSpan timeout)
    {
        Thread.Sleep(timeout);
        return this;
    }

    // ---------------- 鼠标 ----------------

    /// <summary>
    /// 把按主屏归一化（0~65535）的桌面坐标换算为屏幕 point
    /// </summary>
    internal static CGPoint NormalizedToPoint(double absoluteX, double absoluteY)
    {
        var (w, h) = MacWindows.MainDisplaySize();
        return new CGPoint(absoluteX / 65535d * w, absoluteY / 65535d * h);
    }

    private uint MoveEventType()
    {
        lock (_stateLock)
        {
            if (_leftDown)
            {
                return kCGEventLeftMouseDragged;
            }

            if (_rightDown)
            {
                return kCGEventRightMouseDragged;
            }

            if (_otherDown)
            {
                return kCGEventOtherMouseDragged;
            }
        }

        return kCGEventMouseMoved;
    }

    public IMouseSimulator MoveMouseBy(int pixelDeltaX, int pixelDeltaY)
    {
        if (!IsMacOS)
        {
            return this;
        }

        var multiplier = MacPlatformConfig.Instance.MouseMoveMultiplier;
        var dx = (long)Math.Round(pixelDeltaX * multiplier);
        var dy = (long)Math.Round(pixelDeltaY * multiplier);
        var location = CursorLocation();
        // 游戏锁定鼠标（隐藏光标）时只发送位移量，避免把光标移出窗口；否则与真实鼠标一样同时移动光标
        if (CGCursorIsVisible())
        {
            location = new CGPoint(location.X + dx, location.Y + dy);
        }

        var evt = CGEventCreateMouseEvent(IntPtr.Zero, MoveEventType(), location, kCGMouseButtonLeft);
        CGEventSetIntegerValueField(evt, kCGMouseEventDeltaX, dx);
        CGEventSetIntegerValueField(evt, kCGMouseEventDeltaY, dy);
        Post(evt);
        return this;
    }

    [DllImport(CoreGraphics)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool CGCursorIsVisible();

    public IMouseSimulator MoveMouseTo(double absoluteX, double absoluteY)
    {
        if (!IsMacOS)
        {
            return this;
        }

        var target = NormalizedToPoint(absoluteX, absoluteY);
        var current = CursorLocation();
        var evt = CGEventCreateMouseEvent(IntPtr.Zero, MoveEventType(), target, kCGMouseButtonLeft);
        CGEventSetIntegerValueField(evt, kCGMouseEventDeltaX, (long)Math.Round(target.X - current.X));
        CGEventSetIntegerValueField(evt, kCGMouseEventDeltaY, (long)Math.Round(target.Y - current.Y));
        Post(evt);
        return this;
    }

    public IMouseSimulator MoveMouseToPositionOnVirtualDesktop(double absoluteX, double absoluteY) => MoveMouseTo(absoluteX, absoluteY);

    private void SendMouseButton(int button, bool down)
    {
        if (!IsMacOS)
        {
            return;
        }

        uint type = button switch
        {
            kCGMouseButtonLeft => down ? kCGEventLeftMouseDown : kCGEventLeftMouseUp,
            kCGMouseButtonRight => down ? kCGEventRightMouseDown : kCGEventRightMouseUp,
            _ => down ? kCGEventOtherMouseDown : kCGEventOtherMouseUp,
        };
        lock (_stateLock)
        {
            switch (button)
            {
                case kCGMouseButtonLeft:
                    _leftDown = down;
                    break;
                case kCGMouseButtonRight:
                    _rightDown = down;
                    break;
                default:
                    _otherDown = down;
                    break;
            }
        }

        var evt = CGEventCreateMouseEvent(IntPtr.Zero, type, CursorLocation(), (uint)button);
        CGEventSetIntegerValueField(evt, kCGMouseEventButtonNumber, button);
        CGEventSetIntegerValueField(evt, kCGMouseEventClickState, 1);
        CGEventSetFlags(evt, CurrentFlags());
        Post(evt);
    }

    private IMouseSimulator Click(int button)
    {
        SendMouseButton(button, true);
        Thread.Sleep(HoldMs);
        SendMouseButton(button, false);
        return this;
    }

    private IMouseSimulator DoubleClick(int button)
    {
        Click(button);
        Thread.Sleep(60);
        return Click(button);
    }

    public IMouseSimulator LeftButtonDown()
    {
        SendMouseButton(kCGMouseButtonLeft, true);
        return this;
    }

    public IMouseSimulator LeftButtonUp()
    {
        SendMouseButton(kCGMouseButtonLeft, false);
        return this;
    }

    public IMouseSimulator LeftButtonClick() => Click(kCGMouseButtonLeft);

    public IMouseSimulator LeftButtonDoubleClick() => DoubleClick(kCGMouseButtonLeft);

    public IMouseSimulator MiddleButtonDown()
    {
        SendMouseButton(kCGMouseButtonCenter, true);
        return this;
    }

    public IMouseSimulator MiddleButtonUp()
    {
        SendMouseButton(kCGMouseButtonCenter, false);
        return this;
    }

    public IMouseSimulator MiddleButtonClick() => Click(kCGMouseButtonCenter);

    public IMouseSimulator MiddleButtonDoubleClick() => DoubleClick(kCGMouseButtonCenter);

    public IMouseSimulator RightButtonDown()
    {
        SendMouseButton(kCGMouseButtonRight, true);
        return this;
    }

    public IMouseSimulator RightButtonUp()
    {
        SendMouseButton(kCGMouseButtonRight, false);
        return this;
    }

    public IMouseSimulator RightButtonClick() => Click(kCGMouseButtonRight);

    public IMouseSimulator RightButtonDoubleClick() => DoubleClick(kCGMouseButtonRight);

    /// <summary>
    /// Windows XBUTTON1 = 1、XBUTTON2 = 2，对应 macOS 的第 3、4 号按键
    /// </summary>
    private static int XButton(int buttonId) => buttonId == 2 ? 4 : 3;

    public IMouseSimulator XButtonDown(int buttonId)
    {
        SendMouseButton(XButton(buttonId), true);
        return this;
    }

    public IMouseSimulator XButtonUp(int buttonId)
    {
        SendMouseButton(XButton(buttonId), false);
        return this;
    }

    public IMouseSimulator XButtonClick(int buttonId) => Click(XButton(buttonId));

    public IMouseSimulator XButtonDoubleClick(int buttonId) => DoubleClick(XButton(buttonId));

    public IMouseSimulator VerticalScroll(int scrollAmountInClicks)
    {
        if (IsMacOS)
        {
            Post(CGEventCreateScrollWheelEvent2(IntPtr.Zero, kCGScrollEventUnitLine, 1, scrollAmountInClicks, 0, 0));
        }

        return this;
    }

    public IMouseSimulator HorizontalScroll(int scrollAmountInClicks)
    {
        if (IsMacOS)
        {
            Post(CGEventCreateScrollWheelEvent2(IntPtr.Zero, kCGScrollEventUnitLine, 2, 0, scrollAmountInClicks, 0));
        }

        return this;
    }

    IMouseSimulator IMouseSimulator.Sleep(int millsecondsTimeout)
    {
        Thread.Sleep(millsecondsTimeout);
        return this;
    }

    IMouseSimulator IMouseSimulator.Sleep(TimeSpan timeout)
    {
        Thread.Sleep(timeout);
        return this;
    }

    // ---------------- 按键状态 ----------------

    public static bool IsKeyDownNow(User32.VK keyCode)
    {
        if (!IsMacOS)
        {
            return false;
        }

        var vk = (int)keyCode;
        switch (vk)
        {
            case 0x01:
                return CGEventSourceButtonState(kCGEventSourceStateCombinedSessionState, kCGMouseButtonLeft);
            case 0x02:
                return CGEventSourceButtonState(kCGEventSourceStateCombinedSessionState, kCGMouseButtonRight);
            case 0x04:
                return CGEventSourceButtonState(kCGEventSourceStateCombinedSessionState, kCGMouseButtonCenter);
            case 0x05:
                return CGEventSourceButtonState(kCGEventSourceStateCombinedSessionState, 3);
            case 0x06:
                return CGEventSourceButtonState(kCGEventSourceStateCombinedSessionState, 4);
        }

        return MacKeyMap.TryGetMacKeyCode(vk, out var macCode) && CGEventSourceKeyState(kCGEventSourceStateCombinedSessionState, macCode);
    }

    public bool IsKeyDown(User32.VK keyCode) => IsKeyDownNow(keyCode);

    public bool IsKeyUp(User32.VK keyCode) => !IsKeyDown(keyCode);

    public bool IsHardwareKeyDown(User32.VK keyCode) => IsKeyDown(keyCode);

    public bool IsHardwareKeyUp(User32.VK keyCode) => !IsKeyDown(keyCode);

    public bool IsTogglingKeyInEffect(User32.VK keyCode) =>
        keyCode == User32.VK.VK_CAPITAL && IsMacOS && (CGEventSourceFlagsState(kCGEventSourceStateCombinedSessionState) & kCGEventFlagMaskAlphaShift) != 0;
}
