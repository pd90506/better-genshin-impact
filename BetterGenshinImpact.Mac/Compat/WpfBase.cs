using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using BetterGenshinImpact.Platform;

// WPF 基础类型（WindowsBase / PresentationCore / PresentationFramework）在 macOS 上的最小替代实现。
// 只覆盖原版核心代码与 ViewModel 实际用到的成员，界面相关的行为转发给 Avalonia 宿主。
namespace System.Windows
{
    public struct Point : IEquatable<Point>
    {
        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; set; }

        public double Y { get; set; }

        public void Offset(double offsetX, double offsetY)
        {
            X += offsetX;
            Y += offsetY;
        }

        public static Point operator +(Point point, Vector vector) => new(point.X + vector.X, point.Y + vector.Y);

        public static Point operator -(Point point, Vector vector) => new(point.X - vector.X, point.Y - vector.Y);

        public static Vector operator -(Point point1, Point point2) => new(point1.X - point2.X, point1.Y - point2.Y);

        public static bool operator ==(Point a, Point b) => a.Equals(b);

        public static bool operator !=(Point a, Point b) => !a.Equals(b);

        public bool Equals(Point other) => X.Equals(other.X) && Y.Equals(other.Y);

        public override bool Equals(object? obj) => obj is Point other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{X},{Y}");
    }

    public struct Vector
    {
        public Vector(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; set; }

        public double Y { get; set; }

        public double Length => Math.Sqrt(X * X + Y * Y);
    }

    public struct Size : IEquatable<Size>
    {
        public Size(double width, double height)
        {
            Width = width;
            Height = height;
        }

        public double Width { get; set; }

        public double Height { get; set; }

        public bool IsEmpty => Width <= 0 && Height <= 0;

        public static Size Empty => new(0, 0);

        public bool Equals(Size other) => Width.Equals(other.Width) && Height.Equals(other.Height);

        public override bool Equals(object? obj) => obj is Size other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Width, Height);

        public static bool operator ==(Size a, Size b) => a.Equals(b);

        public static bool operator !=(Size a, Size b) => !a.Equals(b);
    }

    public struct Rect : IEquatable<Rect>
    {
        public Rect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public Rect(Point location, Size size) : this(location.X, location.Y, size.Width, size.Height)
        {
        }

        public Rect(Point point1, Point point2) : this(Math.Min(point1.X, point2.X), Math.Min(point1.Y, point2.Y),
            Math.Abs(point2.X - point1.X), Math.Abs(point2.Y - point1.Y))
        {
        }

        public Rect(Size size) : this(0, 0, size.Width, size.Height)
        {
        }

        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public double Left => X;

        public double Top => Y;

        public double Right => X + Width;

        public double Bottom => Y + Height;

        public Point Location
        {
            get => new(X, Y);
            set
            {
                X = value.X;
                Y = value.Y;
            }
        }

        public Size Size
        {
            get => new(Width, Height);
            set
            {
                Width = value.Width;
                Height = value.Height;
            }
        }

        public Point TopLeft => new(Left, Top);

        public Point TopRight => new(Right, Top);

        public Point BottomLeft => new(Left, Bottom);

        public Point BottomRight => new(Right, Bottom);

        public static Rect Empty => new(double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity);

        public bool IsEmpty => Width < 0 || double.IsNegativeInfinity(Width);

        public bool Contains(Point point) => Contains(point.X, point.Y);

        public bool Contains(double x, double y) => !IsEmpty && x >= X && x - Width <= X && y >= Y && y - Height <= Y;

        public bool IntersectsWith(Rect rect) =>
            !IsEmpty && !rect.IsEmpty && rect.Left <= Right && rect.Right >= Left && rect.Top <= Bottom && rect.Bottom >= Top;

        public void Intersect(Rect rect)
        {
            if (!IntersectsWith(rect))
            {
                this = Empty;
                return;
            }

            var left = Math.Max(Left, rect.Left);
            var top = Math.Max(Top, rect.Top);
            Width = Math.Max(Math.Min(Right, rect.Right) - left, 0);
            Height = Math.Max(Math.Min(Bottom, rect.Bottom) - top, 0);
            X = left;
            Y = top;
        }

        public static Rect Intersect(Rect rect1, Rect rect2)
        {
            rect1.Intersect(rect2);
            return rect1;
        }

        public void Offset(double offsetX, double offsetY)
        {
            X += offsetX;
            Y += offsetY;
        }

        public void Inflate(double width, double height)
        {
            X -= width;
            Y -= height;
            Width += width * 2;
            Height += height * 2;
        }

        public bool Equals(Rect other) => X.Equals(other.X) && Y.Equals(other.Y) && Width.Equals(other.Width) && Height.Equals(other.Height);

        public override bool Equals(object? obj) => obj is Rect other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

        public static bool operator ==(Rect a, Rect b) => a.Equals(b);

        public static bool operator !=(Rect a, Rect b) => !a.Equals(b);

        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{X},{Y},{Width},{Height}");
    }

    public struct Int32Rect
    {
        public Int32Rect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int X { get; set; }

        public int Y { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public static Int32Rect Empty => default;
    }

    public struct Thickness
    {
        public Thickness(double uniformLength) : this(uniformLength, uniformLength, uniformLength, uniformLength)
        {
        }

        public Thickness(double left, double top, double right, double bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public double Left { get; set; }

        public double Top { get; set; }

        public double Right { get; set; }

        public double Bottom { get; set; }
    }

    public struct CornerRadius
    {
        public CornerRadius(double uniformRadius)
        {
            TopLeft = TopRight = BottomRight = BottomLeft = uniformRadius;
        }

        public double TopLeft { get; set; }

        public double TopRight { get; set; }

        public double BottomRight { get; set; }

        public double BottomLeft { get; set; }
    }

    public enum Visibility
    {
        Visible = 0,
        Hidden = 1,
        Collapsed = 2,
    }

    public enum WindowState
    {
        Normal = 0,
        Minimized = 1,
        Maximized = 2,
    }

    public enum WindowStartupLocation
    {
        Manual = 0,
        CenterScreen = 1,
        CenterOwner = 2,
    }

    public enum ResizeMode
    {
        NoResize = 0,
        CanMinimize = 1,
        CanResize = 2,
        CanResizeWithGrip = 3,
    }

    public enum SizeToContent
    {
        Manual = 0,
        Width = 1,
        Height = 2,
        WidthAndHeight = 3,
    }

    public enum HorizontalAlignment
    {
        Left = 0,
        Center = 1,
        Right = 2,
        Stretch = 3,
    }

    public enum VerticalAlignment
    {
        Top = 0,
        Center = 1,
        Bottom = 2,
        Stretch = 3,
    }

    public enum TextAlignment
    {
        Left = 0,
        Right = 1,
        Center = 2,
        Justify = 3,
    }

    public enum TextWrapping
    {
        WrapWithOverflow = 0,
        NoWrap = 1,
        Wrap = 2,
    }

    public enum FlowDirection
    {
        LeftToRight = 0,
        RightToLeft = 1,
    }

    public enum MessageBoxButton
    {
        OK = 0,
        OKCancel = 1,
        YesNoCancel = 3,
        YesNo = 4,
    }

    public enum MessageBoxResult
    {
        None = 0,
        OK = 1,
        Cancel = 2,
        Yes = 6,
        No = 7,
    }

    public enum MessageBoxImage
    {
        None = 0,
        Hand = 16,
        Stop = 16,
        Error = 16,
        Question = 32,
        Exclamation = 48,
        Warning = 48,
        Asterisk = 64,
        Information = 64,
    }

    public enum MessageBoxOptions
    {
        None = 0,
    }

    public class DependencyObject
    {
        public Dispatcher Dispatcher => Dispatcher.Instance;

        public bool CheckAccess() => Dispatcher.CheckAccess();

        public void VerifyAccess() => Dispatcher.VerifyAccess();
    }

    public sealed class DependencyProperty
    {
        private DependencyProperty(string name, Type propertyType, Type ownerType)
        {
            Name = name;
            PropertyType = propertyType;
            OwnerType = ownerType;
        }

        public string Name { get; }

        public Type PropertyType { get; }

        public Type OwnerType { get; }

        public static DependencyProperty Register(string name, Type propertyType, Type ownerType, object? typeMetadata = null) =>
            new(name, propertyType, ownerType);
    }

    public readonly struct DependencyPropertyChangedEventArgs
    {
        public DependencyPropertyChangedEventArgs(DependencyProperty property, object? oldValue, object? newValue)
        {
            Property = property;
            OldValue = oldValue;
            NewValue = newValue;
        }

        public DependencyProperty Property { get; }

        public object? OldValue { get; }

        public object? NewValue { get; }
    }

    public delegate void DependencyPropertyChangedEventHandler(object sender, DependencyPropertyChangedEventArgs e);

    public class RoutedEventArgs : EventArgs
    {
        public bool Handled { get; set; }

        public object? Source { get; set; }

        public object? OriginalSource { get; set; }
    }

    public delegate void RoutedEventHandler(object sender, RoutedEventArgs e);

    public class UIElement : DependencyObject
    {
        /// <summary>
        /// 逻辑父元素，由 Panel / ContentControl 维护，供 LogicalTreeHelper 使用
        /// </summary>
        public DependencyObject? Parent { get; internal set; }

        public Visibility Visibility { get; set; }

        public bool IsEnabled { get; set; } = true;

        public bool IsVisible => Visibility == Visibility.Visible;

        public double Opacity { get; set; } = 1;

        public bool Focus() => true;
    }

    public class FrameworkElement : UIElement
    {
        public object? DataContext { get; set; }

        public object? Tag { get; set; }

        public string Name { get; set; } = string.Empty;

        public double Width { get; set; } = double.NaN;

        public double Height { get; set; } = double.NaN;

        public double ActualWidth => double.IsNaN(Width) ? 0 : Width;

        public double ActualHeight => double.IsNaN(Height) ? 0 : Height;

        public double MinWidth { get; set; }

        public double MinHeight { get; set; }

        public double MaxWidth { get; set; } = double.PositiveInfinity;

        public double MaxHeight { get; set; } = double.PositiveInfinity;

        public Thickness Margin { get; set; }

        public HorizontalAlignment HorizontalAlignment { get; set; }

        public VerticalAlignment VerticalAlignment { get; set; }

        public ResourceDictionary Resources { get; set; } = new();

        public object? ToolTip { get; set; }

        public object? FindResource(object resourceKey) => Resources.Contains(resourceKey) ? Resources[resourceKey] : null;

        public object? TryFindResource(object resourceKey) => FindResource(resourceKey);

        public event RoutedEventHandler? Loaded;

        public event RoutedEventHandler? Unloaded;

        protected void RaiseLoaded() => Loaded?.Invoke(this, new RoutedEventArgs());

        protected void RaiseUnloaded() => Unloaded?.Invoke(this, new RoutedEventArgs());
    }

    public class ResourceDictionary : Dictionary<object, object?>
    {
        public bool Contains(object key) => ContainsKey(key);

        public new object? this[object key]
        {
            get => TryGetValue(key, out var value) ? value : null;
            set => base[key] = value;
        }

        public System.Collections.ObjectModel.Collection<ResourceDictionary> MergedDictionaries { get; } = new();

        public Uri? Source { get; set; }
    }

    /// <summary>
    /// WPF Window 的替代实现。核心代码对窗口只做激活、隐藏、最小化等操作，
    /// 主窗口由 Avalonia 宿主提供子类把这些操作转发到真实窗口
    /// </summary>
    public class Window : FrameworkElement
    {
        public virtual string Title { get; set; } = string.Empty;

        public object? Content { get; set; }

        public System.Windows.Media.Brush? Background { get; set; }

        public System.Windows.Media.Brush? Foreground { get; set; }

        public System.Windows.Media.ImageSource? Icon { get; set; }

        public Point PointToScreen(Point point) => new(Left + point.X, Top + point.Y);

        public virtual WindowState WindowState { get; set; }

        public Window? Owner { get; set; }

        public virtual bool Topmost { get; set; }

        public bool ShowInTaskbar { get; set; } = true;

        public bool ShowActivated { get; set; } = true;

        public bool? DialogResult { get; set; }

        public double Left { get; set; }

        public double Top { get; set; }

        public WindowStartupLocation WindowStartupLocation { get; set; }

        public ResizeMode ResizeMode { get; set; }

        public SizeToContent SizeToContent { get; set; }

        public bool IsActive { get; protected set; }

        public bool IsLoaded { get; protected set; }

        public virtual void Show()
        {
            Visibility = Visibility.Visible;
            if (!Application.Current.Windows.Contains(this))
            {
                Application.Current.Windows.Add(this);
            }
        }

        public virtual bool? ShowDialog()
        {
            Show();
            return DialogResult;
        }

        public virtual void Hide()
        {
            Visibility = Visibility.Hidden;
        }

        public virtual void Close()
        {
            Application.Current.Windows.Remove(this);
            Closing?.Invoke(this, new CancelEventArgs());
            Closed?.Invoke(this, EventArgs.Empty);
        }

        public virtual bool Activate() => true;

        public event EventHandler? Closed;

        public event CancelEventHandler? Closing;

        public event EventHandler? Activated;

        public event EventHandler? Deactivated;

        public event EventHandler? StateChanged;

        public event EventHandler? SourceInitialized;

        public event EventHandler? LocationChanged;

        public event SizeChangedEventHandler? SizeChanged;

        protected void RaiseActivated() => Activated?.Invoke(this, EventArgs.Empty);

        protected void RaiseDeactivated() => Deactivated?.Invoke(this, EventArgs.Empty);

        protected void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

        public static Window? GetWindow(DependencyObject dependencyObject) => Application.Current.MainWindow;
    }

    public class SizeChangedEventArgs : RoutedEventArgs
    {
        public Size NewSize { get; init; }

        public Size PreviousSize { get; init; }
    }

    public delegate void SizeChangedEventHandler(object sender, SizeChangedEventArgs e);

    public sealed class WindowCollection : IEnumerable<Window>
    {
        private readonly List<Window> _windows = new();

        public int Count
        {
            get
            {
                lock (_windows)
                {
                    return _windows.Count;
                }
            }
        }

        public void Add(Window window)
        {
            lock (_windows)
            {
                _windows.Add(window);
            }
        }

        public void Remove(Window window)
        {
            lock (_windows)
            {
                _windows.Remove(window);
            }
        }

        public bool Contains(Window window)
        {
            lock (_windows)
            {
                return _windows.Contains(window);
            }
        }

        public IEnumerator<Window> GetEnumerator()
        {
            lock (_windows)
            {
                return _windows.ToList().GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class StartupEventArgs : EventArgs
    {
        public string[] Args { get; init; } = [];
    }

    public class ExitEventArgs : EventArgs
    {
        public int ApplicationExitCode { get; set; }
    }

    public enum ShutdownMode
    {
        OnLastWindowClose = 0,
        OnMainWindowClose = 1,
        OnExplicitShutdown = 2,
    }

    /// <summary>
    /// WPF Application 的替代实现
    /// </summary>
    public class Application : DependencyObject
    {
        private static Application? _current;

        public static Application Current
        {
            get => _current ??= new Application();
            set => _current = value;
        }

        /// <summary>
        /// 由宿主设置：真正退出程序的逻辑
        /// </summary>
        public static Action<int>? ShutdownHandler { get; set; }

        public Window? MainWindow { get; set; }

        public WindowCollection Windows { get; } = new();

        public ResourceDictionary Resources { get; set; } = new();

        public ShutdownMode ShutdownMode { get; set; }

        public IDictionary Properties { get; } = new Hashtable();

        public object? TryFindResource(object resourceKey) => Resources.Contains(resourceKey) ? Resources[resourceKey] : null;

        public object? FindResource(object resourceKey) => TryFindResource(resourceKey);

        public void Shutdown() => Shutdown(0);

        public void Shutdown(int exitCode)
        {
            Dispatcher.InvokeShutdown();
            Exit?.Invoke(this, new ExitEventArgs { ApplicationExitCode = exitCode });
            if (ShutdownHandler != null)
            {
                ShutdownHandler(exitCode);
            }
            else
            {
                Environment.Exit(exitCode);
            }
        }

        public event EventHandler<ExitEventArgs>? Exit;

        public static System.Windows.Resources.StreamResourceInfo? GetResourceStream(Uri uri) => ApplicationResources.GetResourceStream(uri);

        public static System.Windows.Resources.StreamResourceInfo? GetContentStream(Uri uri) => ApplicationResources.GetResourceStream(uri);

        public static Uri? ResourceAssembly { get; set; }
    }

    public static class Clipboard
    {
        /// <summary>
        /// 由宿主设置
        /// </summary>
        public static Action<string>? SetTextHandler { get; set; }

        public static Func<string?>? GetTextHandler { get; set; }

        public static void SetText(string text) => SetTextHandler?.Invoke(text);

        public static void SetDataObject(object data) => SetTextHandler?.Invoke(data?.ToString() ?? string.Empty);

        public static void SetDataObject(object data, bool copy) => SetDataObject(data);

        public static string GetText() => GetTextHandler?.Invoke() ?? string.Empty;

        public static bool ContainsText() => !string.IsNullOrEmpty(GetText());

        public static void Clear() => SetTextHandler?.Invoke(string.Empty);
    }

    public static class SystemParameters
    {
        public static double PrimaryScreenWidth { get; set; } = 1920;

        public static double PrimaryScreenHeight { get; set; } = 1080;

        public static Rect WorkArea => new(0, 0, PrimaryScreenWidth, PrimaryScreenHeight);

        public static double VirtualScreenLeft => 0;

        public static double VirtualScreenTop => 0;

        public static double VirtualScreenWidth => PrimaryScreenWidth;

        public static double VirtualScreenHeight => PrimaryScreenHeight;
    }

    /// <summary>
    /// 系统原生 MessageBox 的替代，转发到 ThemedMessageBox
    /// </summary>
    public static class MessageBox
    {
        public static MessageBoxResult Show(string messageBoxText) => Show(messageBoxText, string.Empty);

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None) =>
            UiPlatform.RunSync(() => UiPlatform.Dialogs.ShowMessageAsync(messageBoxText, caption, button,
                Wpf.Ui.Violeta.Controls.MessageBoxIcon.Information, defaultResult));
    }
}
