// 复制自 Fischless.GameCapture/IGameCapture.cs，补充 ImplicitUsings 隐式引用的命名空间
using System;
using System.Collections.Generic;

namespace Fischless.GameCapture;

public interface IGameCapture : IDisposable
{
    public bool IsCapturing { get; }

    public void Start(nint hWnd, Dictionary<string, object>? settings = null);

    public GameCaptureFrame? Capture();

    public void Stop();
}
