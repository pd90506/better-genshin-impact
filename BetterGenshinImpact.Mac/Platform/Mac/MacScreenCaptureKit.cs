using System;
using System.Runtime.InteropServices;
using System.Threading;
using static BetterGenshinImpact.Platform.Mac.MacNative;

namespace BetterGenshinImpact.Platform.Mac;

/// <summary>
/// 用 ScreenCaptureKit（macOS 14+ 的 SCScreenshotManager）截取单个窗口。
/// <para>
/// ScreenCaptureKit 只有 Objective-C 接口且回调是 block。这里手工构造「全局 block」（不捕获变量、
/// 永不释放），invoke 指向静态的 UnmanagedCallersOnly 函数，再用信号量把异步回调转成同步调用。
/// 同一时间只允许一个请求，调用方已经在截图线程上串行。
/// </para>
/// </summary>
public sealed unsafe class MacScreenCaptureKit : IDisposable
{
    private const int BlockIsGlobal = 1 << 28;
    private const int BlockHasSignature = 1 << 30;

    [StructLayout(LayoutKind.Sequential)]
    private struct BlockDescriptor
    {
        public nuint Reserved;
        public nuint Size;
        public IntPtr Signature;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlockLiteral
    {
        public IntPtr Isa;
        public int Flags;
        public int Reserved;
        public IntPtr Invoke;
        public BlockDescriptor* Descriptor;
    }

    private static readonly object RequestLock = new();
    private static readonly SemaphoreSlim Completed = new(0, 1);
    private static IntPtr _contentResult;
    private static IntPtr _imageResult;

    private static BlockLiteral* _contentBlock;
    private static BlockLiteral* _imageBlock;

    private uint _filterWindowId;
    private IntPtr _filter;

    static MacScreenCaptureKit()
    {
        if (!IsMacOS)
        {
            return;
        }

        NativeLibrary.Load("/System/Library/Frameworks/ScreenCaptureKit.framework/ScreenCaptureKit");
        var libSystem = NativeLibrary.Load("/usr/lib/libSystem.B.dylib");
        var globalBlockIsa = NativeLibrary.GetExport(libSystem, "_NSConcreteGlobalBlock");

        _contentBlock = CreateBlock(globalBlockIsa, (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&OnContent, "v24@?0@8@16");
        _imageBlock = CreateBlock(globalBlockIsa, (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&OnImage,
            "v24@?0^{CGImage=}8@16");
    }

    private static BlockLiteral* CreateBlock(IntPtr isa, IntPtr invoke, string signature)
    {
        var descriptor = (BlockDescriptor*)NativeMemory.AllocZeroed((nuint)sizeof(BlockDescriptor));
        descriptor->Size = (nuint)sizeof(BlockLiteral);
        descriptor->Signature = Marshal.StringToCoTaskMemUTF8(signature);
        var block = (BlockLiteral*)NativeMemory.AllocZeroed((nuint)sizeof(BlockLiteral));
        block->Isa = isa;
        block->Flags = BlockIsGlobal | BlockHasSignature;
        block->Invoke = invoke;
        block->Descriptor = descriptor;
        return block;
    }

    [UnmanagedCallersOnly]
    private static void OnContent(IntPtr block, IntPtr content, IntPtr error)
    {
        try
        {
            _contentResult = content == IntPtr.Zero ? IntPtr.Zero : objc_msgSend(content, Sel("retain"));
            Completed.Release();
        }
        catch
        {
            // 超时后迟到的回调：信号量已满，忽略。回调中的异常不能传回原生代码
        }
    }

    [UnmanagedCallersOnly]
    private static void OnImage(IntPtr block, IntPtr image, IntPtr error)
    {
        try
        {
            _imageResult = image == IntPtr.Zero ? IntPtr.Zero : CFRetain(image);
            Completed.Release();
        }
        catch
        {
            // 同上
        }
    }

    [DllImport(ObjC)]
    private static extern IntPtr objc_autoreleasePoolPush();

    [DllImport(ObjC)]
    private static extern void objc_autoreleasePoolPop(IntPtr pool);

    /// <summary>
    /// 截取窗口，返回已 retain 的 CGImageRef（调用方负责 CGImageRelease），失败返回 IntPtr.Zero
    /// </summary>
    public IntPtr CaptureWindow(MacWindowInfo window)
    {
        if (!IsMacOS || !OperatingSystem.IsMacOSVersionAtLeast(14))
        {
            return IntPtr.Zero;
        }

        lock (RequestLock)
        {
            var pool = objc_autoreleasePoolPush();
            try
            {
                if (_filter == IntPtr.Zero || _filterWindowId != window.WindowId)
                {
                    ReleaseFilter();
                    _filter = CreateFilter(window.WindowId);
                    _filterWindowId = window.WindowId;
                    if (_filter == IntPtr.Zero)
                    {
                        return IntPtr.Zero;
                    }
                }

                var scale = MacWindows.MainScreenScale();
                var config = objc_msgSend(objc_msgSend(Class("SCStreamConfiguration"), Sel("alloc")), Sel("init"));
                objc_msgSend_ULong(config, Sel("setWidth:"), (ulong)Math.Round(window.Width * scale));
                objc_msgSend_ULong(config, Sel("setHeight:"), (ulong)Math.Round(window.Height * scale));
                objc_msgSend_ULong(config, Sel("setShowsCursor:"), 0);
                // kCVPixelFormatType_32BGRA
                objc_msgSend_ULong(config, Sel("setPixelFormat:"), 0x42475241);

                _imageResult = IntPtr.Zero;
                objc_msgSend_IntPtr_IntPtr_IntPtr(Class("SCScreenshotManager"),
                    Sel("captureImageWithFilter:configuration:completionHandler:"), _filter, config, (IntPtr)_imageBlock);
                var ok = Completed.Wait(TimeSpan.FromSeconds(2));
                objc_msgSend(config, Sel("release"));
                return ok ? _imageResult : IntPtr.Zero;
            }
            finally
            {
                objc_autoreleasePoolPop(pool);
            }
        }
    }

    private static IntPtr CreateFilter(uint windowId)
    {
        _contentResult = IntPtr.Zero;
        objc_msgSend_IntPtr(Class("SCShareableContent"), Sel("getShareableContentWithCompletionHandler:"), (IntPtr)_contentBlock);
        if (!Completed.Wait(TimeSpan.FromSeconds(3)) || _contentResult == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var content = _contentResult;
        try
        {
            var windows = objc_msgSend(content, Sel("windows"));
            var count = objc_msgSend_RetLong(windows, Sel("count"));
            for (long i = 0; i < count; i++)
            {
                var w = objc_msgSend_Long(windows, Sel("objectAtIndex:"), i);
                if (objc_msgSend_RetUInt(w, Sel("windowID")) == windowId)
                {
                    return objc_msgSend_IntPtr(objc_msgSend(Class("SCContentFilter"), Sel("alloc")),
                        Sel("initWithDesktopIndependentWindow:"), w);
                }
            }
        }
        finally
        {
            objc_msgSend(content, Sel("release"));
        }

        return IntPtr.Zero;
    }

    private void ReleaseFilter()
    {
        if (_filter != IntPtr.Zero)
        {
            objc_msgSend(_filter, Sel("release"));
            _filter = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        lock (RequestLock)
        {
            ReleaseFilter();
        }
    }
}
