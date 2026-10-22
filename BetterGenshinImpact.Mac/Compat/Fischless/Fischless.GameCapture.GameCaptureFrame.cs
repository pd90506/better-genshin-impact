// 复制自 Fischless.GameCapture/GameCaptureFrame.cs，补充 ImplicitUsings 隐式引用的命名空间
using System;
using System.Collections.Generic;

using OpenCvSharp;
using Vanara.PInvoke;

namespace Fischless.GameCapture;

public sealed class GameCaptureFrame : IDisposable
{
    public GameCaptureFrame(Mat frame, RECT? captureRect = null)
    {
        Frame = frame;
        CaptureRect = captureRect;
    }

    public Mat Frame { get; }

    public RECT? CaptureRect { get; }

    public void Dispose()
    {
        Frame.Dispose();
        GC.SuppressFinalize(this);
    }
}
