using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Gma.System.MouseKeyHook;
using static BetterGenshinImpact.Platform.Mac.MacNative;

namespace BetterGenshinImpact.Platform.Mac;

/// <summary>
/// 全局键鼠监听（CGEventTap，只监听不拦截）。
/// 用于：全局热键、键鼠录制、相对鼠标位移。需要「输入监控」权限。
/// 本程序自己模拟的事件带有 <see cref="SyntheticMarker"/> 标记，监听时会被忽略。
/// </summary>
public sealed class MacEventTap
{
    public const long SyntheticMarker = 0x42474931; // "BGI1"

    private static readonly Lazy<MacEventTap> LazyShared = new(() => new MacEventTap());

    public static MacEventTap Shared => LazyShared.Value;

    private readonly object _lock = new();
    private readonly List<KeyboardMouseEventsBase> _listeners = new();
    private readonly List<Action<int, int>> _deltaListeners = new();
    private Thread? _thread;
    private IntPtr _tap;
    private IntPtr _runLoop;
    private ulong _lastFlags;

    public bool IsRunning => _tap != IntPtr.Zero;

    /// <summary>
    /// 启动失败的原因（通常是没有输入监控权限）
    /// </summary>
    public string? LastError { get; private set; }

    public static void Install()
    {
        Hook.Factory = () => Shared.CreateListener();
        Hook.ModifiersProvider = CurrentModifiers;
        Hook.MousePositionProvider = () =>
        {
            if (!IsMacOS)
            {
                return Point.Empty;
            }

            var evt = CGEventCreate(IntPtr.Zero);
            var p = CGEventGetLocation(evt);
            CFRelease(evt);
            return new Point((int)p.X, (int)p.Y);
        };
    }

    public static Keys CurrentModifiers()
    {
        if (!IsMacOS)
        {
            return Keys.None;
        }

        return FlagsToModifiers(CGEventSourceFlagsState(kCGEventSourceStateCombinedSessionState));
    }

    private static Keys FlagsToModifiers(ulong flags)
    {
        var keys = Keys.None;
        if ((flags & kCGEventFlagMaskShift) != 0)
        {
            keys |= Keys.Shift;
        }

        if ((flags & kCGEventFlagMaskControl) != 0)
        {
            keys |= Keys.Control;
        }

        if ((flags & kCGEventFlagMaskAlternate) != 0)
        {
            keys |= Keys.Alt;
        }

        return keys;
    }

    public IKeyboardMouseEvents CreateListener()
    {
        var listener = new Listener(this);
        lock (_lock)
        {
            _listeners.Add(listener);
        }

        EnsureStarted();
        return listener;
    }

    public IDisposable SubscribeMouseDelta(Action<int, int> handler)
    {
        lock (_lock)
        {
            _deltaListeners.Add(handler);
        }

        EnsureStarted();
        return new ActionDisposable(() =>
        {
            lock (_lock)
            {
                _deltaListeners.Remove(handler);
            }
        });
    }

    private void Remove(KeyboardMouseEventsBase listener)
    {
        lock (_lock)
        {
            _listeners.Remove(listener);
        }
    }

    private void EnsureStarted()
    {
        if (!IsMacOS)
        {
            return;
        }

        lock (_lock)
        {
            if (_thread != null)
            {
                return;
            }

            using var ready = new ManualResetEventSlim();
            _thread = new Thread(() => Run(ready)) { IsBackground = true, Name = "BetterGI EventTap" };
            _thread.Start();
            ready.Wait(TimeSpan.FromSeconds(3));
        }
    }

    private unsafe void Run(ManualResetEventSlim ready)
    {
        try
        {
            ulong mask = 0;
            foreach (var t in new[]
                     {
                         kCGEventLeftMouseDown, kCGEventLeftMouseUp, kCGEventRightMouseDown, kCGEventRightMouseUp,
                         kCGEventMouseMoved, kCGEventLeftMouseDragged, kCGEventRightMouseDragged, kCGEventKeyDown,
                         kCGEventKeyUp, kCGEventFlagsChanged, kCGEventScrollWheel, kCGEventOtherMouseDown,
                         kCGEventOtherMouseUp, kCGEventOtherMouseDragged,
                     })
            {
                mask |= 1UL << (int)t;
            }

            _tap = CGEventTapCreate(kCGSessionEventTap, kCGHeadInsertEventTap, kCGEventTapOptionListenOnly, mask, &OnEvent, IntPtr.Zero);
            if (_tap == IntPtr.Zero)
            {
                LastError = "无法创建全局键鼠监听，请在「系统设置 → 隐私与安全性 → 输入监控」中允许 BetterGI";
                CGRequestListenEventAccess();
                ready.Set();
                lock (_lock)
                {
                    _thread = null;
                }

                return;
            }

            var source = CFMachPortCreateRunLoopSource(IntPtr.Zero, _tap, 0);
            _runLoop = CFRunLoopGetCurrent();
            CFRunLoopAddSource(_runLoop, source, GetConstant(CoreFoundation, "kCFRunLoopCommonModes"));
            CGEventTapEnable(_tap, true);
            ready.Set();
            CFRunLoopRun();
        }
        catch (Exception e)
        {
            LastError = e.Message;
            ready.Set();
        }
    }

    [UnmanagedCallersOnly]
    private static IntPtr OnEvent(IntPtr proxy, uint type, IntPtr evt, IntPtr userInfo)
    {
        try
        {
            Shared.Handle(type, evt);
        }
        catch
        {
            // 回调中不能抛出异常到原生代码
        }

        return evt;
    }

    private void Handle(uint type, IntPtr evt)
    {
        if (type is kCGEventTapDisabledByTimeout or kCGEventTapDisabledByUserInput)
        {
            CGEventTapEnable(_tap, true);
            return;
        }

        if (CGEventGetIntegerValueField(evt, kCGEventSourceUserData) == SyntheticMarker)
        {
            return;
        }

        KeyboardMouseEventsBase[] listeners;
        Action<int, int>[] deltaListeners;
        lock (_lock)
        {
            listeners = _listeners.ToArray();
            deltaListeners = _deltaListeners.ToArray();
        }

        var flags = CGEventGetFlags(evt);
        var location = CGEventGetLocation(evt);
        var point = new Point((int)location.X, (int)location.Y);
        var timestamp = Environment.TickCount;

        switch (type)
        {
            case kCGEventKeyDown:
            case kCGEventKeyUp:
            {
                var vk = MacKeyMap.ToVirtualKey((ushort)CGEventGetIntegerValueField(evt, kCGKeyboardEventKeycode));
                if (vk == 0)
                {
                    return;
                }

                var args = new KeyEventArgs((Keys)vk | FlagsToModifiers(flags));
                foreach (var l in listeners)
                {
                    if (type == kCGEventKeyDown)
                    {
                        l.RaiseKeyDown(args);
                    }
                    else
                    {
                        l.RaiseKeyUp(args);
                    }
                }

                break;
            }
            case kCGEventFlagsChanged:
            {
                var vk = MacKeyMap.ToVirtualKey((ushort)CGEventGetIntegerValueField(evt, kCGKeyboardEventKeycode));
                var bit = MacKeyMap.ModifierFlag(vk);
                if (vk == 0 || bit == 0)
                {
                    _lastFlags = flags;
                    return;
                }

                var isDown = (flags & bit) != 0 && (_lastFlags & bit) == 0;
                var isUp = (flags & bit) == 0 && (_lastFlags & bit) != 0;
                _lastFlags = flags;
                var args = new KeyEventArgs((Keys)vk | FlagsToModifiers(flags));
                foreach (var l in listeners)
                {
                    if (isDown)
                    {
                        l.RaiseKeyDown(args);
                    }
                    else if (isUp)
                    {
                        l.RaiseKeyUp(args);
                    }
                }

                break;
            }
            case kCGEventMouseMoved:
            case kCGEventLeftMouseDragged:
            case kCGEventRightMouseDragged:
            case kCGEventOtherMouseDragged:
            {
                var dx = (int)CGEventGetIntegerValueField(evt, kCGMouseEventDeltaX);
                var dy = (int)CGEventGetIntegerValueField(evt, kCGMouseEventDeltaY);
                foreach (var d in deltaListeners)
                {
                    d(dx, dy);
                }

                var args = new MouseEventExtArgs(MouseButtons.None, 0, point, 0, timestamp, false, false);
                foreach (var l in listeners)
                {
                    l.RaiseMouseMove(args);
                }

                break;
            }
            case kCGEventLeftMouseDown:
            case kCGEventRightMouseDown:
            case kCGEventOtherMouseDown:
            case kCGEventLeftMouseUp:
            case kCGEventRightMouseUp:
            case kCGEventOtherMouseUp:
            {
                var isDown = type is kCGEventLeftMouseDown or kCGEventRightMouseDown or kCGEventOtherMouseDown;
                var button = type switch
                {
                    kCGEventLeftMouseDown or kCGEventLeftMouseUp => MouseButtons.Left,
                    kCGEventRightMouseDown or kCGEventRightMouseUp => MouseButtons.Right,
                    _ => CGEventGetIntegerValueField(evt, kCGMouseEventButtonNumber) switch
                    {
                        2 => MouseButtons.Middle,
                        3 => MouseButtons.XButton1,
                        4 => MouseButtons.XButton2,
                        _ => MouseButtons.Middle,
                    },
                };
                var args = new MouseEventExtArgs(button, 1, point, 0, timestamp, isDown, !isDown);
                foreach (var l in listeners)
                {
                    if (isDown)
                    {
                        l.RaiseMouseDown(args);
                    }
                    else
                    {
                        l.RaiseMouseUp(args);
                    }
                }

                break;
            }
            case kCGEventScrollWheel:
            {
                // Windows 一格滚轮为 120
                var delta = (int)CGEventGetIntegerValueField(evt, kCGScrollWheelEventDeltaAxis1) * 120;
                if (delta == 0)
                {
                    return;
                }

                var args = new MouseEventExtArgs(MouseButtons.None, 0, point, delta, timestamp, false, false);
                foreach (var l in listeners)
                {
                    l.RaiseMouseWheel(args);
                }

                break;
            }
        }
    }

    private sealed class Listener(MacEventTap owner) : KeyboardMouseEventsBase
    {
        public override void Dispose()
        {
            owner.Remove(this);
            base.Dispose();
        }
    }

    private sealed class ActionDisposable(Action action) : IDisposable
    {
        private Action? _action = action;

        public void Dispose() => Interlocked.Exchange(ref _action, null)?.Invoke();
    }
}
