// 复制自 Fischless.GameCapture/CaptureModeExtensions.cs，补充 ImplicitUsings 隐式引用的命名空间
using System;
using System.Collections.Generic;

namespace Fischless.GameCapture;

public static class CaptureModeExtensions
{
    public static CaptureModes ToCaptureMode(this string modeName)
    {
        return (CaptureModes)Enum.Parse(typeof(CaptureModes), modeName);
    }
}