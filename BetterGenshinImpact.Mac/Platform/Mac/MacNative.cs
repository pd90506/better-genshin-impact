using System;
using System.Runtime.InteropServices;
using System.Text;

namespace BetterGenshinImpact.Platform.Mac;

/// <summary>
/// CoreGraphics / CoreFoundation / Objective-C 运行时的 P/Invoke 声明。
/// 只使用 C 接口，不依赖额外的原生库，由 .NET 直接加载系统框架。
/// </summary>
internal static unsafe class MacNative
{
    public const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    public const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    public const string ApplicationServices = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    public const string ObjC = "/usr/lib/libobjc.A.dylib";

    public static bool IsMacOS => OperatingSystem.IsMacOS();

    // ---------------- 基础结构 ----------------

    [StructLayout(LayoutKind.Sequential)]
    public struct CGPoint
    {
        public double X;
        public double Y;

        public CGPoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CGSize
    {
        public double Width;
        public double Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CGRect
    {
        public CGPoint Origin;
        public CGSize Size;

        public static CGRect Null => new() { Origin = new CGPoint(double.PositiveInfinity, double.PositiveInfinity) };
    }

    // ---------------- CoreFoundation ----------------

    [DllImport(CoreFoundation)]
    public static extern void CFRelease(IntPtr cf);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFRetain(IntPtr cf);

    [DllImport(CoreFoundation)]
    public static extern long CFArrayGetCount(IntPtr theArray);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFArrayGetValueAtIndex(IntPtr theArray, long idx);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFDictionaryGetValue(IntPtr theDict, IntPtr key);

    [DllImport(CoreFoundation)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFNumberGetValue(IntPtr number, long theType, out long valuePtr);

    [DllImport(CoreFoundation)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFNumberGetValue(IntPtr number, long theType, out double valuePtr);

    public const long kCFNumberSInt64Type = 4;
    public const long kCFNumberFloat64Type = 6;

    [DllImport(CoreFoundation)]
    public static extern long CFStringGetLength(IntPtr theString);

    [DllImport(CoreFoundation)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFStringGetCString(IntPtr theString, byte* buffer, long bufferSize, uint encoding);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string cStr, uint encoding);

    public const uint kCFStringEncodingUTF8 = 0x08000100;

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFDataGetBytePtr(IntPtr theData);

    [DllImport(CoreFoundation)]
    public static extern long CFDataGetLength(IntPtr theData);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFRunLoopGetCurrent();

    [DllImport(CoreFoundation)]
    public static extern void CFRunLoopRun();

    [DllImport(CoreFoundation)]
    public static extern void CFRunLoopStop(IntPtr rl);

    [DllImport(CoreFoundation)]
    public static extern void CFRunLoopAddSource(IntPtr rl, IntPtr source, IntPtr mode);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator, IntPtr port, long order);

    [DllImport(CoreFoundation)]
    public static extern void CFMachPortInvalidate(IntPtr port);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFGetTypeID(IntPtr cf);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFStringGetTypeID();

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFNumberGetTypeID();

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFBooleanGetTypeID();

    [DllImport(CoreFoundation)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CFBooleanGetValue(IntPtr boolean);

    /// <summary>
    /// 读取框架导出的常量（例如 kCGWindowNumber、kCFRunLoopCommonModes 都是 CFStringRef 全局变量）
    /// </summary>
    public static IntPtr GetConstant(string library, string symbol)
    {
        var handle = NativeLibrary.Load(library);
        var address = NativeLibrary.GetExport(handle, symbol);
        return Marshal.ReadIntPtr(address);
    }

    public static string? CFStringToString(IntPtr cfString)
    {
        if (cfString == IntPtr.Zero)
        {
            return null;
        }

        var length = CFStringGetLength(cfString);
        var bufferSize = length * 4 + 1;
        var buffer = new byte[bufferSize];
        fixed (byte* p = buffer)
        {
            if (!CFStringGetCString(cfString, p, bufferSize, kCFStringEncodingUTF8))
            {
                return null;
            }
        }

        var end = Array.IndexOf(buffer, (byte)0);
        return Encoding.UTF8.GetString(buffer, 0, end < 0 ? buffer.Length : end);
    }

    public static long CFNumberToLong(IntPtr number)
    {
        if (number == IntPtr.Zero)
        {
            return 0;
        }

        CFNumberGetValue(number, kCFNumberSInt64Type, out long value);
        return value;
    }

    public static double CFNumberToDouble(IntPtr number)
    {
        if (number == IntPtr.Zero)
        {
            return 0;
        }

        CFNumberGetValue(number, kCFNumberFloat64Type, out double value);
        return value;
    }

    // ---------------- CoreGraphics：窗口 ----------------

    public const uint kCGWindowListOptionAll = 0;
    public const uint kCGWindowListOptionOnScreenOnly = 1 << 0;
    public const uint kCGWindowListOptionOnScreenAboveWindow = 1 << 1;
    public const uint kCGWindowListOptionOnScreenBelowWindow = 1 << 2;
    public const uint kCGWindowListOptionIncludingWindow = 1 << 3;
    public const uint kCGWindowListExcludeDesktopElements = 1 << 4;

    public const uint kCGWindowImageDefault = 0;
    public const uint kCGWindowImageBoundsIgnoreFraming = 1 << 0;
    public const uint kCGWindowImageShouldBeOpaque = 1 << 1;
    public const uint kCGWindowImageOnlyShadows = 1 << 2;
    public const uint kCGWindowImageBestResolution = 1 << 3;
    public const uint kCGWindowImageNominalResolution = 1 << 4;

    [DllImport(CoreGraphics)]
    public static extern IntPtr CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);

    [DllImport(CoreGraphics)]
    public static extern IntPtr CGWindowListCreateImage(CGRect screenBounds, uint listOption, uint windowId, uint imageOption);

    [DllImport(CoreGraphics)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CGRectMakeWithDictionaryRepresentation(IntPtr dict, out CGRect rect);

    [DllImport(CoreGraphics)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CGPreflightScreenCaptureAccess();

    [DllImport(CoreGraphics)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CGRequestScreenCaptureAccess();

    // ---------------- CoreGraphics：图像 ----------------

    [DllImport(CoreGraphics)]
    public static extern nuint CGImageGetWidth(IntPtr image);

    [DllImport(CoreGraphics)]
    public static extern nuint CGImageGetHeight(IntPtr image);

    [DllImport(CoreGraphics)]
    public static extern nuint CGImageGetBytesPerRow(IntPtr image);

    [DllImport(CoreGraphics)]
    public static extern nuint CGImageGetBitsPerPixel(IntPtr image);

    [DllImport(CoreGraphics)]
    public static extern uint CGImageGetBitmapInfo(IntPtr image);

    [DllImport(CoreGraphics)]
    public static extern IntPtr CGImageGetDataProvider(IntPtr image);

    [DllImport(CoreGraphics)]
    public static extern IntPtr CGDataProviderCopyData(IntPtr provider);

    [DllImport(CoreGraphics)]
    public static extern void CGImageRelease(IntPtr image);

    // ---------------- CoreGraphics：显示器 ----------------

    [DllImport(CoreGraphics)]
    public static extern uint CGMainDisplayID();

    [DllImport(CoreGraphics)]
    public static extern CGRect CGDisplayBounds(uint display);

    [DllImport(CoreGraphics)]
    public static extern int CGGetActiveDisplayList(uint maxDisplays, uint* activeDisplays, out uint displayCount);

    // ---------------- CoreGraphics：事件 ----------------

    public const int kCGHIDEventTap = 0;
    public const int kCGSessionEventTap = 1;
    public const int kCGAnnotatedSessionEventTap = 2;

    public const int kCGHeadInsertEventTap = 0;
    public const int kCGEventTapOptionDefault = 0;
    public const int kCGEventTapOptionListenOnly = 1;

    public const uint kCGEventNull = 0;
    public const uint kCGEventLeftMouseDown = 1;
    public const uint kCGEventLeftMouseUp = 2;
    public const uint kCGEventRightMouseDown = 3;
    public const uint kCGEventRightMouseUp = 4;
    public const uint kCGEventMouseMoved = 5;
    public const uint kCGEventLeftMouseDragged = 6;
    public const uint kCGEventRightMouseDragged = 7;
    public const uint kCGEventKeyDown = 10;
    public const uint kCGEventKeyUp = 11;
    public const uint kCGEventFlagsChanged = 12;
    public const uint kCGEventScrollWheel = 22;
    public const uint kCGEventOtherMouseDown = 25;
    public const uint kCGEventOtherMouseUp = 26;
    public const uint kCGEventOtherMouseDragged = 27;
    public const uint kCGEventTapDisabledByTimeout = 0xFFFFFFFE;
    public const uint kCGEventTapDisabledByUserInput = 0xFFFFFFFF;

    public const int kCGMouseEventClickState = 1;
    public const int kCGMouseEventButtonNumber = 3;
    public const int kCGMouseEventDeltaX = 4;
    public const int kCGMouseEventDeltaY = 5;
    public const int kCGKeyboardEventAutorepeat = 8;
    public const int kCGKeyboardEventKeycode = 9;
    public const int kCGScrollWheelEventDeltaAxis1 = 11;
    public const int kCGScrollWheelEventDeltaAxis2 = 12;
    public const int kCGEventSourceUserData = 42;

    public const ulong kCGEventFlagMaskAlphaShift = 0x00010000;
    public const ulong kCGEventFlagMaskShift = 0x00020000;
    public const ulong kCGEventFlagMaskControl = 0x00040000;
    public const ulong kCGEventFlagMaskAlternate = 0x00080000;
    public const ulong kCGEventFlagMaskCommand = 0x00100000;

    public const int kCGScrollEventUnitPixel = 0;
    public const int kCGScrollEventUnitLine = 1;

    public const int kCGMouseButtonLeft = 0;
    public const int kCGMouseButtonRight = 1;
    public const int kCGMouseButtonCenter = 2;

    public const int kCGEventSourceStateCombinedSessionState = 0;
    public const int kCGEventSourceStateHIDSystemState = 1;

    [DllImport(CoreGraphics)]
    public static extern IntPtr CGEventCreate(IntPtr source);

    [DllImport(CoreGraphics)]
    public static extern IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort virtualKey, [MarshalAs(UnmanagedType.U1)] bool keyDown);

    [DllImport(CoreGraphics)]
    public static extern IntPtr CGEventCreateMouseEvent(IntPtr source, uint mouseType, CGPoint mouseCursorPosition, uint mouseButton);

    /// <summary>
    /// 非可变参数版本（macOS 10.13+）。CGEventCreateScrollWheelEvent 是可变参数函数，arm64 上不能直接 P/Invoke
    /// </summary>
    [DllImport(CoreGraphics)]
    public static extern IntPtr CGEventCreateScrollWheelEvent2(IntPtr source, int units, uint wheelCount, int wheel1, int wheel2, int wheel3);

    [DllImport(CoreGraphics)]
    public static extern void CGEventPost(int tap, IntPtr evt);

    [DllImport(CoreGraphics)]
    public static extern void CGEventPostToPid(int pid, IntPtr evt);

    [DllImport(CoreGraphics)]
    public static extern void CGEventSetIntegerValueField(IntPtr evt, int field, long value);

    [DllImport(CoreGraphics)]
    public static extern long CGEventGetIntegerValueField(IntPtr evt, int field);

    [DllImport(CoreGraphics)]
    public static extern void CGEventSetFlags(IntPtr evt, ulong flags);

    [DllImport(CoreGraphics)]
    public static extern ulong CGEventGetFlags(IntPtr evt);

    [DllImport(CoreGraphics)]
    public static extern CGPoint CGEventGetLocation(IntPtr evt);

    [DllImport(CoreGraphics)]
    public static extern void CGEventSetType(IntPtr evt, uint type);

    [DllImport(CoreGraphics)]
    public static extern int CGWarpMouseCursorPosition(CGPoint newCursorPosition);

    [DllImport(CoreGraphics)]
    public static extern int CGAssociateMouseAndMouseCursorPosition([MarshalAs(UnmanagedType.U1)] bool connected);

    [DllImport(CoreGraphics)]
    public static extern ulong CGEventSourceFlagsState(int stateId);

    [DllImport(CoreGraphics)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CGEventSourceKeyState(int stateId, ushort key);

    [DllImport(CoreGraphics)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CGEventSourceButtonState(int stateId, uint button);

    [DllImport(CoreGraphics)]
    public static extern IntPtr CGEventTapCreate(int tap, int place, int options, ulong eventsOfInterest,
        delegate* unmanaged<IntPtr, uint, IntPtr, IntPtr, IntPtr> callback, IntPtr userInfo);

    [DllImport(CoreGraphics)]
    public static extern void CGEventTapEnable(IntPtr tap, [MarshalAs(UnmanagedType.U1)] bool enable);

    [DllImport(CoreGraphics)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CGPreflightListenEventAccess();

    [DllImport(CoreGraphics)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CGRequestListenEventAccess();

    [DllImport(CoreGraphics)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CGPreflightPostEventAccess();

    [DllImport(CoreGraphics)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool CGRequestPostEventAccess();

    // ---------------- ApplicationServices：辅助功能权限 ----------------

    [DllImport(ApplicationServices)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool AXIsProcessTrusted();

    // ---------------- Objective-C 运行时 ----------------

    [DllImport(ObjC)]
    public static extern IntPtr objc_getClass(string name);

    [DllImport(ObjC)]
    public static extern IntPtr sel_registerName(string name);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    public static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    public static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    public static extern IntPtr objc_msgSend_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    public static extern IntPtr objc_msgSend_IntPtr_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2, IntPtr arg3);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    public static extern IntPtr objc_msgSend_Int(IntPtr receiver, IntPtr selector, int arg1);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    public static extern IntPtr objc_msgSend_Long(IntPtr receiver, IntPtr selector, long arg1);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    public static extern IntPtr objc_msgSend_ULong(IntPtr receiver, IntPtr selector, ulong arg1);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    public static extern int objc_msgSend_RetInt(IntPtr receiver, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    public static extern long objc_msgSend_RetLong(IntPtr receiver, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    public static extern uint objc_msgSend_RetUInt(IntPtr receiver, IntPtr selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool objc_msgSend_RetBool_ULong(IntPtr receiver, IntPtr selector, ulong arg1);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    public static extern double objc_msgSend_RetDouble(IntPtr receiver, IntPtr selector);

    public static IntPtr Class(string name) => objc_getClass(name);

    public static IntPtr Sel(string name) => sel_registerName(name);

    public static IntPtr NSString(string value)
    {
        var cls = objc_getClass("NSString");
        var ptr = Marshal.StringToCoTaskMemUTF8(value);
        try
        {
            return objc_msgSend_IntPtr(cls, sel_registerName("stringWithUTF8String:"), ptr);
        }
        finally
        {
            Marshal.FreeCoTaskMem(ptr);
        }
    }

    public static string? NSStringToString(IntPtr nsString)
    {
        if (nsString == IntPtr.Zero)
        {
            return null;
        }

        var utf8 = objc_msgSend(nsString, sel_registerName("UTF8String"));
        return Marshal.PtrToStringUTF8(utf8);
    }
}
