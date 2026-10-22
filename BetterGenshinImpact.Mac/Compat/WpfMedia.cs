using System.Globalization;
using System.IO;

namespace System.Windows.Media
{
    public struct Color : IEquatable<Color>
    {
        public byte A { get; set; }

        public byte R { get; set; }

        public byte G { get; set; }

        public byte B { get; set; }

        public static Color FromArgb(byte a, byte r, byte g, byte b) => new() { A = a, R = r, G = g, B = b };

        public static Color FromRgb(byte r, byte g, byte b) => FromArgb(255, r, g, b);

        public bool Equals(Color other) => A == other.A && R == other.R && G == other.G && B == other.B;

        public override bool Equals(object? obj) => obj is Color other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(A, R, G, B);

        public static bool operator ==(Color a, Color b) => a.Equals(b);

        public static bool operator !=(Color a, Color b) => !a.Equals(b);

        public override string ToString() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";
    }

    public static class ColorConverter
    {
        public static object ConvertFromString(string value)
        {
            if (TryParse(value, out var color))
            {
                return color;
            }

            throw new FormatException($"无法识别的颜色：{value}");
        }

        public static bool TryParse(string? value, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var text = value.Trim();
            if (text.StartsWith('#'))
            {
                var hex = text[1..];
                if (hex.Length == 3 || hex.Length == 4)
                {
                    hex = string.Concat(Array.ConvertAll(hex.ToCharArray(), c => new string(c, 2)));
                }

                if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
                {
                    return false;
                }

                switch (hex.Length)
                {
                    case 6:
                        color = Color.FromArgb(255, (byte)(v >> 16), (byte)(v >> 8), (byte)v);
                        return true;
                    case 8:
                        color = Color.FromArgb((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
                        return true;
                    default:
                        return false;
                }
            }

            return Colors.ByName.TryGetValue(text, out color);
        }
    }

    public abstract class Brush
    {
        public double Opacity { get; set; } = 1;

        public Brush Clone() => (Brush)MemberwiseClone();

        public void Freeze()
        {
        }

        public bool IsFrozen => false;

        public bool CanFreeze => true;
    }

    public sealed class SolidColorBrush : Brush
    {
        public SolidColorBrush()
        {
        }

        public SolidColorBrush(Color color)
        {
            Color = color;
        }

        public Color Color { get; set; }

        public override string ToString() => Color.ToString();
    }

    public sealed class BrushConverter
    {
        public object? ConvertFromString(string value) => ColorConverter.TryParse(value, out var c) ? new SolidColorBrush(c) : null;

        public object? ConvertFrom(object value) => ConvertFromString(value?.ToString() ?? string.Empty);
    }

    public enum Stretch
    {
        None = 0,
        Fill = 1,
        Uniform = 2,
        UniformToFill = 3,
    }

    /// <summary>
    /// 图像源。macOS 版统一用编码后的图片字节（PNG）或文件路径表示，由界面层解码显示
    /// </summary>
    public abstract class ImageSource
    {
        public virtual double Width => 0;

        public virtual double Height => 0;

        public void Freeze()
        {
        }

        public bool IsFrozen => false;
    }

    public enum PixelFormatKind
    {
        Bgr24,
        Bgra32,
        Bgr32,
        Gray8,
        Rgb24,
        Pbgra32,
    }

    public readonly struct PixelFormat : IEquatable<PixelFormat>
    {
        public PixelFormat(PixelFormatKind kind, int bitsPerPixel)
        {
            Kind = kind;
            BitsPerPixel = bitsPerPixel;
        }

        public PixelFormatKind Kind { get; }

        public int BitsPerPixel { get; }

        public bool Equals(PixelFormat other) => Kind == other.Kind;

        public override bool Equals(object? obj) => obj is PixelFormat other && Equals(other);

        public override int GetHashCode() => (int)Kind;

        public static bool operator ==(PixelFormat a, PixelFormat b) => a.Equals(b);

        public static bool operator !=(PixelFormat a, PixelFormat b) => !a.Equals(b);
    }

    public static class PixelFormats
    {
        public static PixelFormat Bgr24 { get; } = new(PixelFormatKind.Bgr24, 24);

        public static PixelFormat Bgra32 { get; } = new(PixelFormatKind.Bgra32, 32);

        public static PixelFormat Bgr32 { get; } = new(PixelFormatKind.Bgr32, 32);

        public static PixelFormat Pbgra32 { get; } = new(PixelFormatKind.Pbgra32, 32);

        public static PixelFormat Gray8 { get; } = new(PixelFormatKind.Gray8, 8);

        public static PixelFormat Rgb24 { get; } = new(PixelFormatKind.Rgb24, 24);
    }

    public class FontFamily
    {
        public FontFamily(string familyName)
        {
            Source = familyName;
        }

        public string Source { get; }
    }

    public sealed class RenderOptions
    {
        public static RenderMode ProcessRenderMode { get; set; }
    }

    public enum RenderMode
    {
        Default,
        SoftwareOnly,
    }
}

namespace System.Windows.Media.Imaging
{
    public enum BitmapCacheOption
    {
        Default = 0,
        OnDemand = 0,
        OnLoad = 1,
        None = 2,
    }

    public enum BitmapCreateOptions
    {
        None = 0,
        PreservePixelFormat = 1,
        DelayCreation = 2,
        IgnoreColorProfile = 4,
        IgnoreImageCache = 8,
    }

    /// <summary>
    /// 位图。以 PNG 字节保存，界面层用 Avalonia Bitmap 解码
    /// </summary>
    public class BitmapSource : ImageSource
    {
        private int _pixelWidth;
        private int _pixelHeight;

        public byte[]? EncodedBytes { get; protected set; }

        public int PixelWidth => _pixelWidth;

        public int PixelHeight => _pixelHeight;

        public override double Width => _pixelWidth;

        public override double Height => _pixelHeight;

        public PixelFormat Format { get; protected set; } = PixelFormats.Bgra32;

        public double DpiX => 96;

        public double DpiY => 96;

        protected void SetEncoded(byte[] bytes)
        {
            EncodedBytes = bytes;
            using var mat = OpenCvSharp.Cv2.ImDecode(bytes, OpenCvSharp.ImreadModes.Unchanged);
            _pixelWidth = mat.Width;
            _pixelHeight = mat.Height;
        }

        public static BitmapSource FromEncoded(byte[] bytes)
        {
            var source = new BitmapSource();
            source.SetEncoded(bytes);
            return source;
        }

        public static BitmapSource FromMat(OpenCvSharp.Mat mat)
        {
            var source = new BitmapSource();
            source.EncodedBytes = mat.ToBytes(".png");
            source._pixelWidth = mat.Width;
            source._pixelHeight = mat.Height;
            return source;
        }

        public static BitmapSource Create(int pixelWidth, int pixelHeight, double dpiX, double dpiY, PixelFormat pixelFormat,
            object? palette, Array pixels, int stride)
        {
            var type = pixelFormat.BitsPerPixel switch
            {
                8 => OpenCvSharp.MatType.CV_8UC1,
                24 => OpenCvSharp.MatType.CV_8UC3,
                _ => OpenCvSharp.MatType.CV_8UC4,
            };
            using var mat = OpenCvSharp.Mat.FromPixelData(pixelHeight, pixelWidth, type, pixels, stride);
            return FromMat(mat);
        }
    }

    public sealed class BitmapImage : BitmapSource
    {
        public BitmapImage()
        {
        }

        public BitmapImage(Uri uriSource)
        {
            UriSource = uriSource;
            EndInit();
        }

        public Uri? UriSource { get; set; }

        public Stream? StreamSource { get; set; }

        public BitmapCacheOption CacheOption { get; set; }

        public BitmapCreateOptions CreateOptions { get; set; }

        public int DecodePixelWidth { get; set; }

        public int DecodePixelHeight { get; set; }

        public void BeginInit()
        {
        }

        public void EndInit()
        {
            if (StreamSource != null)
            {
                using var ms = new MemoryStream();
                StreamSource.CopyTo(ms);
                SetEncoded(ms.ToArray());
            }
            else if (UriSource != null && UriSource.IsFile && File.Exists(UriSource.LocalPath))
            {
                SetEncoded(File.ReadAllBytes(UriSource.LocalPath));
            }
        }
    }

    public sealed class WriteableBitmap : BitmapSource
    {
        private readonly OpenCvSharp.Mat _mat;

        public WriteableBitmap(int pixelWidth, int pixelHeight, double dpiX, double dpiY, PixelFormat pixelFormat, object? palette)
        {
            var type = pixelFormat.BitsPerPixel switch
            {
                8 => OpenCvSharp.MatType.CV_8UC1,
                24 => OpenCvSharp.MatType.CV_8UC3,
                _ => OpenCvSharp.MatType.CV_8UC4,
            };
            _mat = new OpenCvSharp.Mat(pixelHeight, pixelWidth, type, OpenCvSharp.Scalar.All(0));
            Format = pixelFormat;
            SetEncoded(_mat.ToBytes(".png"));
        }

        public OpenCvSharp.Mat Mat => _mat;

        public IntPtr BackBuffer => _mat.Data;

        public int BackBufferStride => (int)_mat.Step();

        public void Lock()
        {
        }

        public void Unlock()
        {
            EncodedBytes = _mat.ToBytes(".png");
        }

        public void AddDirtyRect(Int32Rect dirtyRect)
        {
        }

        public void WritePixels(Int32Rect sourceRect, IntPtr buffer, int bufferSize, int stride)
        {
            unsafe
            {
                var src = (byte*)buffer;
                var dst = (byte*)_mat.Data;
                var rowBytes = Math.Min(stride, BackBufferStride);
                for (var y = 0; y < sourceRect.Height && y < _mat.Rows; y++)
                {
                    Buffer.MemoryCopy(src + (long)y * stride, dst + (long)(y + sourceRect.Y) * BackBufferStride, BackBufferStride, rowBytes);
                }
            }

            EncodedBytes = _mat.ToBytes(".png");
        }

        public void WritePixels(Int32Rect sourceRect, Array pixels, int stride, int offset)
        {
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                WritePixels(sourceRect, handle.AddrOfPinnedObject() + offset, pixels.Length, stride);
            }
            finally
            {
                handle.Free();
            }
        }
    }
}
