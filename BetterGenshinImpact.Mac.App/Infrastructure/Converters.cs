using System;
using System.IO;
using Avalonia.Media.Imaging;

namespace BetterGenshinImpact.MacApp.Infrastructure;

/// <summary>
/// 原版的图片（System.Windows.Media.ImageSource 替身，内部保存 PNG 字节）转换为 Avalonia 位图
/// </summary>
public static class ImageSourceConverter
{
    public static Bitmap? ToBitmap(System.Windows.Media.ImageSource? source)
    {
        if (source is System.Windows.Media.Imaging.BitmapSource { EncodedBytes: { Length: > 0 } bytes })
        {
            try
            {
                return new Bitmap(new MemoryStream(bytes));
            }
            catch
            {
                return null;
            }
        }

        return null;
    }
}

/// <summary>
/// Avalonia 按键与 WPF Key 枚举的转换（两者大部分名称相同）
/// </summary>
public static class KeyMapper
{
    public static System.Windows.Input.Key ToWpf(Avalonia.Input.Key key)
    {
        var name = key.ToString();
        if (name.StartsWith("Oem", StringComparison.Ordinal) || Enum.TryParse<System.Windows.Input.Key>(name, out _))
        {
            return Enum.TryParse<System.Windows.Input.Key>(name, out var k) ? k : System.Windows.Input.Key.None;
        }

        return key switch
        {
            Avalonia.Input.Key.LeftShift => System.Windows.Input.Key.LeftShift,
            Avalonia.Input.Key.RightShift => System.Windows.Input.Key.RightShift,
            Avalonia.Input.Key.LeftCtrl => System.Windows.Input.Key.LeftCtrl,
            Avalonia.Input.Key.RightCtrl => System.Windows.Input.Key.RightCtrl,
            Avalonia.Input.Key.LeftAlt => System.Windows.Input.Key.LeftAlt,
            Avalonia.Input.Key.RightAlt => System.Windows.Input.Key.RightAlt,
            _ => System.Windows.Input.Key.None,
        };
    }
}
