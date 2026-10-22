using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using BetterGenshinImpact.Platform;

// WPF-UI（Wpf.Ui / Wpf.Ui.Violeta）在 macOS 上的替代：只保留核心代码与 ViewModel 用到的类型，
// 提示和导航转发给 Avalonia 宿主。
namespace Wpf.Ui.Controls
{
    public enum ControlAppearance
    {
        Primary,
        Secondary,
        Info,
        Dark,
        Light,
        Danger,
        Success,
        Caution,
        Transparent,
    }

    public enum InfoBarSeverity
    {
        Informational,
        Success,
        Warning,
        Error,
    }

    public enum WindowBackdropType
    {
        None,
        Auto,
        Mica,
        Acrylic,
        Tabbed,
    }

    public enum ContentDialogResult
    {
        None,
        Primary,
        Secondary,
    }

    public enum SymbolRegular
    {
        Empty = 0,
        Play24,
        Pause24,
        Stop24,
        ArrowWrap20,
        ArrowShuffle24,
        ArrowRepeatAll24,
        Info24,
        Warning24,
        ErrorCircle24,
        CheckmarkCircle24,
        Dismiss24,
    }

    public class SymbolIcon
    {
        public SymbolIcon()
        {
        }

        public SymbolIcon(SymbolRegular symbol)
        {
            Symbol = symbol;
        }

        public SymbolRegular Symbol { get; set; }

        public bool Filled { get; set; }
    }

    public class IconElement
    {
    }

    /// <summary>
    /// 对话框：macOS 版用 Prompt 风格的消息框替代
    /// </summary>
    public class ContentDialog
    {
        public ContentDialog()
        {
        }

        public ContentDialog(object? contentPresenter)
        {
        }

        public object? Title { get; set; }

        public object? Content { get; set; }

        public string PrimaryButtonText { get; set; } = string.Empty;

        public string SecondaryButtonText { get; set; } = string.Empty;

        public string CloseButtonText { get; set; } = "取消";

        public bool IsPrimaryButtonEnabled { get; set; } = true;

        public bool IsSecondaryButtonEnabled { get; set; } = true;

        public ControlAppearance PrimaryButtonAppearance { get; set; }

        public ControlAppearance CloseButtonAppearance { get; set; }

        public double DialogWidth { get; set; }

        public double DialogHeight { get; set; }

        public double DialogMaxWidth { get; set; }

        public double DialogMaxHeight { get; set; }

        public async Task<ContentDialogResult> ShowAsync(CancellationToken cancellationToken = default)
        {
            var hasPrimary = !string.IsNullOrEmpty(PrimaryButtonText);
            var result = await UiPlatform.RunOnUiAsync(() => UiPlatform.Dialogs.ShowMessageAsync(
                Content?.ToString() ?? string.Empty,
                Title?.ToString() ?? string.Empty,
                hasPrimary ? MessageBoxButton.YesNo : MessageBoxButton.OK,
                Wpf.Ui.Violeta.Controls.MessageBoxIcon.Question,
                System.Windows.MessageBoxResult.None));
            return result is System.Windows.MessageBoxResult.Yes or System.Windows.MessageBoxResult.OK && hasPrimary ? ContentDialogResult.Primary : ContentDialogResult.None;
        }

        public void Hide()
        {
        }
    }

    public class FluentWindow : Window
    {
        public WindowBackdropType WindowBackdropType { get; set; }

        public bool ExtendsContentIntoTitleBar { get; set; }
    }

    public class TitleBar : System.Windows.Controls.Control
    {
        public object? Icon { get; set; }

        public string Title { get; set; } = string.Empty;
    }

    public class ImageIcon : IconElement
    {
        public System.Windows.Media.ImageSource? Source { get; set; }
    }

    public interface INavigableView<out T>
    {
        T ViewModel { get; }
    }
}

namespace Wpf.Ui.Abstractions.Controls
{
    public interface INavigationAware
    {
        Task OnNavigatedToAsync();

        Task OnNavigatedFromAsync();
    }
}

namespace Wpf.Ui.Appearance
{
    public enum ApplicationTheme
    {
        Unknown,
        Dark,
        Light,
        HighContrast,
    }

    public static class ApplicationThemeManager
    {
        public static ApplicationTheme Current { get; private set; } = ApplicationTheme.Dark;

        public static event Action<ApplicationTheme>? Changed;

        public static void Apply(ApplicationTheme theme, Wpf.Ui.Controls.WindowBackdropType backgroundEffect = Wpf.Ui.Controls.WindowBackdropType.Mica,
            bool updateAccent = true)
        {
            Current = theme;
            Changed?.Invoke(theme);
        }

        public static ApplicationTheme GetAppTheme() => Current;

        public static bool IsMatchedDark() => Current == ApplicationTheme.Dark;
    }
}

namespace Wpf.Ui
{
    /// <summary>
    /// 页面导航。macOS 宿主按页面类型名切换界面
    /// </summary>
    public interface INavigationService
    {
        bool Navigate(Type pageType);

        bool Navigate(Type pageType, object? dataContext);
    }

    public interface INavigationWindow
    {
        bool Navigate(Type pageType);

        void ShowWindow();

        void CloseWindow();
    }

    public interface IPageService
    {
        T? GetPage<T>() where T : class;

        FrameworkElement? GetPage(Type pageType);
    }

    public interface ISnackbarService
    {
        void Show(string title, string message, Wpf.Ui.Controls.ControlAppearance appearance, Wpf.Ui.Controls.IconElement? icon, TimeSpan timeout);
    }

    public interface IContentDialogService
    {
    }

    public sealed class NavigationService : INavigationService
    {
        public static Func<Type, bool>? NavigateHandler { get; set; }

        public bool Navigate(Type pageType) => NavigateHandler?.Invoke(pageType) ?? false;

        public bool Navigate(Type pageType, object? dataContext) => Navigate(pageType);
    }

    public sealed class SnackbarService : ISnackbarService
    {
        public void Show(string title, string message, Wpf.Ui.Controls.ControlAppearance appearance, Wpf.Ui.Controls.IconElement? icon, TimeSpan timeout)
        {
            var kind = appearance switch
            {
                Wpf.Ui.Controls.ControlAppearance.Danger => ToastKind.Error,
                Wpf.Ui.Controls.ControlAppearance.Caution => ToastKind.Warning,
                Wpf.Ui.Controls.ControlAppearance.Success => ToastKind.Success,
                _ => ToastKind.Information,
            };
            ToastHub.Show(kind, string.IsNullOrEmpty(title) ? message : $"{title}\n{message}");
        }
    }

    public enum ToastKind
    {
        Information,
        Success,
        Warning,
        Error,
    }

    /// <summary>
    /// 轻提示的汇总出口。宿主订阅 <see cref="Shown"/> 后在界面上显示
    /// </summary>
    public static class ToastHub
    {
        public static event Action<ToastKind, string>? Shown;

        public static void Show(ToastKind kind, string message)
        {
            if (Shown == null)
            {
                Console.WriteLine($"[{kind}] {message}");
                return;
            }

            Shown.Invoke(kind, message);
        }
    }
}

namespace Wpf.Ui.Violeta.Controls
{
    public enum MessageBoxIcon
    {
        None,
        Information,
        Question,
        Warning,
        Error,
        Success,
        Info = Information,
    }

    public enum ToastLocation
    {
        TopCenter,
        TopLeft,
        TopRight,
        BottomCenter,
        BottomLeft,
        BottomRight,
        Center,
    }

    public static class Toast
    {
        public static void Information(string message, ToastLocation location = ToastLocation.TopCenter, Thickness offset = default, int time = 4000) =>
            Wpf.Ui.ToastHub.Show(Wpf.Ui.ToastKind.Information, message);

        public static void Success(string message, ToastLocation location = ToastLocation.TopCenter, Thickness offset = default, int time = 4000) =>
            Wpf.Ui.ToastHub.Show(Wpf.Ui.ToastKind.Success, message);

        public static void Warning(string message, ToastLocation location = ToastLocation.TopCenter, Thickness offset = default, int time = 4000) =>
            Wpf.Ui.ToastHub.Show(Wpf.Ui.ToastKind.Warning, message);

        public static void Error(string message, ToastLocation location = ToastLocation.TopCenter, Thickness offset = default, int time = 4000) =>
            Wpf.Ui.ToastHub.Show(Wpf.Ui.ToastKind.Error, message);

        public static void Show(string message) => Information(message);
    }

    /// <summary>
    /// 对应原版 GlobalUsing 中 MessageBox = Wpf.Ui.Violeta.Controls.MessageBox
    /// </summary>
    public static class MessageBox
    {
        public static MessageBoxResult Show(string messageBoxText) => Show(messageBoxText, string.Empty);

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxIcon icon = MessageBoxIcon.None, MessageBoxResult defaultResult = MessageBoxResult.None) =>
            UiPlatform.RunSync(() => UiPlatform.Dialogs.ShowMessageAsync(messageBoxText, caption, button, icon, defaultResult));

        public static Task<MessageBoxResult> ShowAsync(string messageBoxText, string caption = "", MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxIcon icon = MessageBoxIcon.None, MessageBoxResult defaultResult = MessageBoxResult.None) =>
            UiPlatform.RunOnUiAsync(() => UiPlatform.Dialogs.ShowMessageAsync(messageBoxText, caption, button, icon, defaultResult));

        public static MessageBoxResult Information(string messageBoxText, string caption = "信息") =>
            Show(messageBoxText, caption, MessageBoxButton.OK, MessageBoxIcon.Information);

        public static MessageBoxResult Warning(string messageBoxText, string caption = "警告") =>
            Show(messageBoxText, caption, MessageBoxButton.OK, MessageBoxIcon.Warning);

        public static MessageBoxResult Error(string messageBoxText, string caption = "错误") =>
            Show(messageBoxText, caption, MessageBoxButton.OK, MessageBoxIcon.Error);

        public static MessageBoxResult Question(string messageBoxText, string caption = "确认") =>
            Show(messageBoxText, caption, MessageBoxButton.YesNo, MessageBoxIcon.Question);
    }
}

namespace Wpf.Ui.Violeta.Controls
{
    using System.Collections.Generic;
    using System.Linq;

    public interface ICascadingItem
    {
        string Header { get; }

        object? Tag { get; set; }

        IReadOnlyList<ICascadingItem> Children { get; }
    }

    public class CascadingItem : ICascadingItem
    {
        public CascadingItem(string header)
        {
            Header = header;
            Children = [];
        }

        public CascadingItem(string header, IEnumerable<ICascadingItem> children)
        {
            Header = header;
            Children = children.ToList();
        }

        public string Header { get; }

        public object? Tag { get; set; }

        public IReadOnlyList<ICascadingItem> Children { get; }

        public override string ToString() => Header;
    }
}

namespace Wpf.Ui.Controls
{
    /// <summary>
    /// Wpf.Ui 的实例式消息框（new Wpf.Ui.Controls.MessageBox { Title, Content, ... }.ShowDialogAsync()）
    /// </summary>
    public class MessageBox : FluentWindow
    {

        public string PrimaryButtonText { get; set; } = string.Empty;

        public string SecondaryButtonText { get; set; } = string.Empty;

        public string CloseButtonText { get; set; } = "关闭";

        public bool IsPrimaryButtonEnabled { get; set; } = true;

        public bool IsSecondaryButtonEnabled { get; set; } = true;

        public bool IsCloseButtonEnabled { get; set; } = true;

        public ControlAppearance PrimaryButtonAppearance { get; set; }

        public ControlAppearance CloseButtonAppearance { get; set; }

        public System.Threading.Tasks.Task<MessageBoxResult> ShowDialogAsync(bool showAsDialog = true, System.Threading.CancellationToken cancellationToken = default) =>
            BetterGenshinImpact.Platform.UiPlatform.RunOnUiAsync(() => BetterGenshinImpact.Platform.UiPlatform.Dialogs.ShowContentDialogAsync(this));
    }

    public enum MessageBoxResult
    {
        None,
        Primary,
        Secondary,
    }

    public class TextBox : System.Windows.Controls.TextBox
    {
        public string PlaceholderText { get; set; } = string.Empty;

        public bool ClearButtonEnabled { get; set; }
    }

    public class TextBlock : System.Windows.Controls.TextBlock
    {
        public FontTypography FontTypography { get; set; }
    }

    public enum FontTypography
    {
        Caption,
        Body,
        BodyStrong,
        Subtitle,
        Title,
        TitleLarge,
        Display,
    }

    public class Button : System.Windows.Controls.Button
    {
        public ControlAppearance Appearance { get; set; }

        public object? Icon { get; set; }
    }

    public class StackPanel : System.Windows.Controls.StackPanel
    {
    }

    public class ToggleSwitch : System.Windows.Controls.CheckBox
    {
        public object? OnContent { get; set; }

        public object? OffContent { get; set; }
    }

    public class NumberBox : System.Windows.Controls.Control
    {
        public double? Value { get; set; }

        public double Minimum { get; set; } = double.MinValue;

        public double Maximum { get; set; } = double.MaxValue;

        public string PlaceholderText { get; set; } = string.Empty;
    }

    public class CardControl : System.Windows.Controls.ContentControl
    {
        public object? Header { get; set; }

        public object? Icon { get; set; }
    }

    public class CardExpander : System.Windows.Controls.ContentControl
    {
        public object? Header { get; set; }

        public bool IsExpanded { get; set; }
    }
}
