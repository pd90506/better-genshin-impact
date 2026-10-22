using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using BetterGenshinImpact.Platform;
using OpenCvSharp;

// System.Drawing.Common（GDI+）在非 Windows 平台上会直接抛出 PlatformNotSupportedException，
// macOS 版不引用它，改为在这里提供原版用到的 Pen / Bitmap / Graphics 等少量类型，底层用 OpenCV 实现。
// Color / Point / Rectangle 等基础类型来自跨平台的 System.Drawing.Primitives。
namespace System.Drawing
{
    public sealed class Pen : IDisposable
    {
        public Pen(Color color, float width = 1)
        {
            Color = color;
            Width = width;
        }

        public Pen(Brush brush, float width = 1) : this(brush is SolidBrush sb ? sb.Color : Color.Black, width)
        {
        }

        public Color Color { get; set; }

        public float Width { get; set; }

        public void Dispose()
        {
        }
    }

    public static class Pens
    {
        public static Pen Red => new(Color.Red);

        public static Pen Lime => new(Color.Lime);

        public static Pen White => new(Color.White);

        public static Pen Black => new(Color.Black);

        public static Pen LightPink => new(Color.LightPink);

        public static Pen Yellow => new(Color.Yellow);

        public static Pen Green => new(Color.Green);

        public static Pen Blue => new(Color.Blue);

        public static Pen Cyan => new(Color.Cyan);

        public static Pen Orange => new(Color.Orange);

        public static Pen Coral => new(Color.Coral);

        public static Pen Gold => new(Color.Gold);
    }

    public abstract class Brush : IDisposable
    {
        public void Dispose()
        {
        }
    }

    public sealed class SolidBrush : Brush
    {
        public SolidBrush(Color color)
        {
            Color = color;
        }

        public Color Color { get; set; }
    }

    public enum FontStyle
    {
        Regular = 0,
        Bold = 1,
        Italic = 2,
        Underline = 4,
        Strikeout = 8,
    }

    public enum GraphicsUnit
    {
        World = 0,
        Display = 1,
        Pixel = 2,
        Point = 3,
    }

    public sealed class Font : IDisposable
    {
        public Font(string familyName, float emSize, FontStyle style = FontStyle.Regular, GraphicsUnit unit = GraphicsUnit.Point)
        {
            Name = familyName;
            Size = emSize;
            Style = style;
        }

        public string Name { get; }

        public float Size { get; }

        public FontStyle Style { get; }

        public void Dispose()
        {
        }
    }

    public enum StringAlignment
    {
        Near = 0,
        Center = 1,
        Far = 2,
    }

    public sealed class StringFormat : IDisposable
    {
        public StringAlignment Alignment { get; set; }

        public StringAlignment LineAlignment { get; set; }

        public void Dispose()
        {
        }
    }

    public abstract class Image : IDisposable
    {
        public abstract int Width { get; }

        public abstract int Height { get; }

        public abstract void Dispose();
    }

    /// <summary>
    /// 以 OpenCV Mat（BGR/BGRA）保存像素的位图
    /// </summary>
    public sealed class Bitmap : Image
    {
        public Bitmap(Mat mat)
        {
            Mat = mat;
        }

        public Bitmap(int width, int height)
        {
            Mat = new Mat(height, width, MatType.CV_8UC4, Scalar.All(0));
        }

        public Bitmap(string filename)
        {
            Mat = Cv2.ImRead(filename, ImreadModes.Unchanged);
        }

        public Bitmap(Stream stream)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            Mat = Cv2.ImDecode(ms.ToArray(), ImreadModes.Unchanged);
        }

        public Mat Mat { get; }

        public override int Width => Mat.Width;

        public override int Height => Mat.Height;

        public void Save(string filename, Imaging.ImageFormat format) => Cv2.ImWrite(filename, Mat);

        public void Save(string filename) => Cv2.ImWrite(filename, Mat);

        public void Save(Stream stream, Imaging.ImageFormat format)
        {
            var bytes = Mat.ToBytes(format.Extension);
            stream.Write(bytes, 0, bytes.Length);
        }

        public override void Dispose() => Mat.Dispose();
    }

    public sealed class Graphics : IDisposable
    {
        private readonly Mat _mat;

        private Graphics(Mat mat)
        {
            _mat = mat;
        }

        public static Graphics FromImage(Image image) => new(((Bitmap)image).Mat);

        private static Scalar ToScalar(Color c) => new(c.B, c.G, c.R, c.A);

        public void FillRectangle(Brush brush, Rectangle rect) =>
            Cv2.Rectangle(_mat, new OpenCvSharp.Rect(rect.X, rect.Y, rect.Width, rect.Height),
                ToScalar(brush is SolidBrush sb ? sb.Color : Color.Black), -1);

        public void FillRectangle(Brush brush, RectangleF rect) => FillRectangle(brush, Rectangle.Round(rect));

        public void DrawRectangle(Pen pen, Rectangle rect) =>
            Cv2.Rectangle(_mat, new OpenCvSharp.Rect(rect.X, rect.Y, rect.Width, rect.Height), ToScalar(pen.Color), Math.Max(1, (int)pen.Width));

        public void DrawRectangle(Pen pen, int x, int y, int width, int height) => DrawRectangle(pen, new Rectangle(x, y, width, height));

        public void DrawLine(Pen pen, int x1, int y1, int x2, int y2) =>
            Cv2.Line(_mat, x1, y1, x2, y2, ToScalar(pen.Color), Math.Max(1, (int)pen.Width));

        public void DrawString(string s, Font font, Brush brush, RectangleF layoutRectangle, StringFormat? format = null)
        {
            // OpenCV 自带字体不支持中文，调试用标注只保留 ASCII 部分
            var ascii = new string(Array.FindAll(s.ToCharArray(), c => c < 128));
            var scale = Math.Max(0.3, font.Size / 24.0);
            var size = Cv2.GetTextSize(ascii, HersheyFonts.HersheySimplex, scale, 1, out _);
            var x = (int)(layoutRectangle.X + (layoutRectangle.Width - size.Width) / 2);
            var y = (int)(layoutRectangle.Y + (layoutRectangle.Height + size.Height) / 2);
            Cv2.PutText(_mat, ascii, new OpenCvSharp.Point(x, y), HersheyFonts.HersheySimplex, scale,
                ToScalar(brush is SolidBrush sb ? sb.Color : Color.White));
        }

        public void DrawString(string s, Font font, Brush brush, float x, float y) =>
            DrawString(s, font, brush, new RectangleF(x, y, 0, 0));

        public void Dispose()
        {
        }
    }
}

namespace System.Drawing.Imaging
{
    public sealed class ImageFormat
    {
        private ImageFormat(string extension)
        {
            Extension = extension;
        }

        public string Extension { get; }

        public static ImageFormat Png { get; } = new(".png");

        public static ImageFormat Jpeg { get; } = new(".jpg");

        public static ImageFormat Bmp { get; } = new(".bmp");
    }
}

namespace OpenCvSharp.Extensions
{
    public static class BitmapConverter
    {
        public static System.Drawing.Bitmap ToBitmap(this Mat src) => new(src.Clone());

        public static Mat ToMat(this System.Drawing.Bitmap src) => src.Mat.Clone();
    }
}

namespace OpenCvSharp.WpfExtensions
{
    using System.Windows.Media.Imaging;

    public static class WriteableBitmapConverter
    {
        public static WriteableBitmap ToWriteableBitmap(this Mat src)
        {
            var wb = new WriteableBitmap(src.Width, src.Height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
            ToWriteableBitmap(src, wb);
            return wb;
        }

        public static void ToWriteableBitmap(Mat src, WriteableBitmap dst)
        {
            using var bgra = src.Channels() switch
            {
                1 => src.CvtColor(ColorConversionCodes.GRAY2BGRA),
                3 => src.CvtColor(ColorConversionCodes.BGR2BGRA),
                _ => src.Clone(),
            };
            bgra.CopyTo(dst.Mat);
            dst.Unlock();
        }
    }

    public static class BitmapSourceConverter
    {
        public static BitmapSource ToBitmapSource(this Mat src) => BitmapSource.FromMat(src);
    }
}

namespace Windows.System
{
    /// <summary>
    /// WinRT Launcher：macOS 上用 open 命令打开链接
    /// </summary>
    public static class Launcher
    {
        public static Task<bool> LaunchUriAsync(Uri uri)
        {
            UiPlatform.Dialogs.OpenUrl(uri.ToString());
            return Task.FromResult(true);
        }

        public static Task<bool> LaunchFolderPathAsync(string path)
        {
            UiPlatform.Dialogs.OpenUrl(new Uri(Path.GetFullPath(path)).AbsoluteUri);
            return Task.FromResult(true);
        }
    }
}

namespace Windows.Networking.PushNotifications
{
}

namespace System.Windows.Interop
{
    public sealed class WindowInteropHelper
    {
        public WindowInteropHelper(Window? window)
        {
            Owner = window;
        }

        public Window? Owner { get; }

        /// <summary>
        /// BetterGI 自身窗口的句柄。macOS 版没有 HWND，返回 0
        /// </summary>
        public IntPtr Handle => IntPtr.Zero;

        public IntPtr EnsureHandle() => IntPtr.Zero;
    }
}

namespace System.Windows.Resources
{
    public sealed class StreamResourceInfo
    {
        public StreamResourceInfo(Stream stream, string contentType)
        {
            Stream = stream;
            ContentType = contentType;
        }

        public Stream Stream { get; }

        public string ContentType { get; }
    }
}

namespace System.Windows
{
    using System.Windows.Resources;

    public partial class ApplicationResources
    {
        /// <summary>
        /// 把 pack://application:,,,/Resources/... 映射到程序目录下的 Resources 文件
        /// </summary>
        public static StreamResourceInfo? GetResourceStream(Uri uri)
        {
            var path = uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString;
            path = Uri.UnescapeDataString(path).TrimStart('/');
            var idx = path.IndexOf(";component/", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                path = path[(idx + ";component/".Length)..];
            }

            var file = Path.Combine(AppContext.BaseDirectory, path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(file))
            {
                return null;
            }

            return new StreamResourceInfo(File.OpenRead(file), "application/octet-stream");
        }
    }
}

namespace Microsoft.Web.WebView2.Core
{
    public enum CoreWebView2HostResourceAccessKind
    {
        Deny,
        Allow,
        DenyCors,
    }

    public class CoreWebView2NewWindowRequestedEventArgs : EventArgs
    {
        public string Uri { get; init; } = string.Empty;

        public bool Handled { get; set; }
    }

    public class CoreWebView2NavigationCompletedEventArgs : EventArgs
    {
        public bool IsSuccess { get; init; }
    }

    public class CoreWebView2WebMessageReceivedEventArgs : EventArgs
    {
        public string WebMessageAsJson { get; init; } = string.Empty;

        public string TryGetWebMessageAsString() => WebMessageAsJson;
    }

    /// <summary>
    /// macOS 版没有内嵌 WebView2，网页统一交给系统浏览器
    /// </summary>
    public class CoreWebView2
    {
        public void SetVirtualHostNameToFolderMapping(string hostName, string folderPath, CoreWebView2HostResourceAccessKind accessKind)
        {
            VirtualHosts[hostName] = folderPath;
        }

        public Dictionary<string, string> VirtualHosts { get; } = new();

        public void AddHostObjectToScript(string name, object rawObject)
        {
        }

        public void Navigate(string uri) => UiPlatform.Dialogs.OpenUrl(uri);

        public void NavigateToString(string html)
        {
        }

        public void PostWebMessageAsJson(string json)
        {
        }

        public void PostWebMessageAsString(string message)
        {
        }

        public Task<string> ExecuteScriptAsync(string script) => Task.FromResult("null");

        public CoreWebView2Settings Settings { get; } = new();

        public event EventHandler<CoreWebView2NewWindowRequestedEventArgs>? NewWindowRequested;

        public event EventHandler<CoreWebView2WebMessageReceivedEventArgs>? WebMessageReceived;

        public event EventHandler<CoreWebView2NavigationCompletedEventArgs>? NavigationCompleted;
    }

    public class CoreWebView2Settings
    {
        public bool AreDevToolsEnabled { get; set; }

        public bool AreDefaultContextMenusEnabled { get; set; }

        public bool IsWebMessageEnabled { get; set; }
    }
}

namespace Microsoft.Web.WebView2.Wpf
{
    public class WebView2 : System.Windows.Controls.Control, IDisposable
    {
        public Core.CoreWebView2 CoreWebView2 { get; } = new();

        public Uri? Source { get; set; }

        public Task EnsureCoreWebView2Async(object? environment = null) => Task.CompletedTask;

        public Task<string> ExecuteScriptAsync(string javaScript) => Task.FromResult("null");

        public void Dispose()
        {
        }
    }
}

namespace PresentMonFps
{
    public sealed class FpsRequest
    {
        public FpsRequest(uint pid)
        {
            Pid = pid;
        }

        public FpsRequest(int pid) : this((uint)pid)
        {
        }

        public uint Pid { get; }
    }

    public sealed class FpsResult
    {
        public double Fps { get; init; }
    }

    /// <summary>
    /// PresentMon 只支持 Windows，macOS 版不采样游戏帧率
    /// </summary>
    public static class FpsInspector
    {
        public static Task StartForeverAsync(FpsRequest request, Action<FpsResult> callback, System.Threading.CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public static bool IsAvailable => false;
    }
}

namespace Wpf.Ui.Violeta.Win32
{
    public sealed class OpenFolderDialog
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get => Title; set => Title = value; }

        public bool UseDescriptionForTitle { get; set; }

        public string SelectedPath { get => FolderName; set => FolderName = value; }

        public string InitialDirectory { get; set; } = string.Empty;

        public string FolderName { get; set; } = string.Empty;

        public bool Multiselect { get; set; }

        public bool? ShowDialog() => ShowDialog(IntPtr.Zero);

        public bool? ShowDialog(IntPtr owner)
        {
            var path = UiPlatform.RunSync(() => UiPlatform.Dialogs.PickFolderAsync(Title));
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            FolderName = path;
            return true;
        }
    }
}
