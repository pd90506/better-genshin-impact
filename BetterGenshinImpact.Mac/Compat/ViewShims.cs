using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BetterGenshinImpact.Platform;
using Wpf.Ui.Violeta.Controls;

// 原版 View 层（XAML 窗口、页面、控件）中被核心代码和 ViewModel 直接引用的类型。
// macOS 版不编译 XAML，这里提供同名替身：
// - 简单对话框（ThemedMessageBox / PromptDialog）转发到 Avalonia 宿主的对话框；
// - 功能窗口在 Show 时请求宿主打开对应的 macOS 界面，宿主没有实现时给出提示；
// - 依赖 WebView2 的网页窗口改为用系统浏览器打开。

namespace BetterGenshinImpact.View.Windows
{

    /// <summary>
    /// 功能窗口替身的基类
    /// </summary>
    public class FeatureWindow : Wpf.Ui.Controls.FluentWindow
    {
        public override void Show()
        {
            base.Show();
            if (!UiPlatform.Dialogs.OpenFeatureWindow(this, false))
            {
                Wpf.Ui.Violeta.Controls.Toast.Warning($"macOS 版暂未提供「{DisplayName}」界面");
                base.Close();
            }
        }

        public override bool? ShowDialog()
        {
            base.Show();
            if (!UiPlatform.Dialogs.OpenFeatureWindow(this, true))
            {
                Wpf.Ui.Violeta.Controls.Toast.Warning($"macOS 版暂未提供「{DisplayName}」界面");
                base.Close();
                return false;
            }

            return DialogResult;
        }

        public virtual string DisplayName => string.IsNullOrEmpty(Title) ? GetType().Name : Title;
    }

    public partial class ThemedMessageBox
    {
        public enum MessageBoxIcon
        {
            None,
            Information,
            Warning,
            Error,
            Question,
            Success,
        }

        private static Wpf.Ui.Violeta.Controls.MessageBoxIcon ToViolet(MessageBoxIcon icon) => icon switch
        {
            MessageBoxIcon.Information => Wpf.Ui.Violeta.Controls.MessageBoxIcon.Information,
            MessageBoxIcon.Warning => Wpf.Ui.Violeta.Controls.MessageBoxIcon.Warning,
            MessageBoxIcon.Error => Wpf.Ui.Violeta.Controls.MessageBoxIcon.Error,
            MessageBoxIcon.Question => Wpf.Ui.Violeta.Controls.MessageBoxIcon.Question,
            MessageBoxIcon.Success => Wpf.Ui.Violeta.Controls.MessageBoxIcon.Success,
            _ => Wpf.Ui.Violeta.Controls.MessageBoxIcon.None,
        };

        public static MessageBoxResult Show(string content, string title = "提示", MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxIcon icon = MessageBoxIcon.Information, MessageBoxResult defaultResult = MessageBoxResult.None, Window? owner = null)
        {
            var result = UiPlatform.RunSync(() => UiPlatform.Dialogs.ShowMessageAsync(content, title, button, ToViolet(icon), defaultResult));
            return result == MessageBoxResult.None ? defaultResult : result;
        }

        public static Task<MessageBoxResult> ShowAsync(string content, string title = "提示", MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxIcon icon = MessageBoxIcon.Information, MessageBoxResult defaultResult = MessageBoxResult.None, Window? owner = null) =>
            UiPlatform.RunOnUiAsync(async () =>
            {
                var result = await UiPlatform.Dialogs.ShowMessageAsync(content, title, button, ToViolet(icon), defaultResult);
                return result == MessageBoxResult.None ? defaultResult : result;
            });

        public static void Error(string message, string title = "错误") => Show(message, title, MessageBoxButton.OK, MessageBoxIcon.Error);

        public static MessageBoxResult Error(string message, string title, MessageBoxButton button, MessageBoxResult defaultResult = MessageBoxResult.None) =>
            Show(message, title, button, MessageBoxIcon.Error, defaultResult);

        public static Task<MessageBoxResult> ErrorAsync(string message, string title = "错误") =>
            ShowAsync(message, title, MessageBoxButton.OK, MessageBoxIcon.Error);

        public static Task<MessageBoxResult> ErrorAsync(string message, string title, MessageBoxButton button, MessageBoxResult defaultResult = MessageBoxResult.None) =>
            ShowAsync(message, title, button, MessageBoxIcon.Error, defaultResult);

        public static void Warning(string message, string title = "警告") => Show(message, title, MessageBoxButton.OK, MessageBoxIcon.Warning);

        public static MessageBoxResult Warning(string message, string title, MessageBoxButton button, MessageBoxResult defaultResult = MessageBoxResult.None) =>
            Show(message, title, button, MessageBoxIcon.Warning, defaultResult);

        public static Task<MessageBoxResult> WarningAsync(string message, string title = "警告") =>
            ShowAsync(message, title, MessageBoxButton.OK, MessageBoxIcon.Warning);

        public static Task<MessageBoxResult> WarningAsync(string message, string title, MessageBoxButton button, MessageBoxResult defaultResult = MessageBoxResult.None) =>
            ShowAsync(message, title, button, MessageBoxIcon.Warning, defaultResult);

        public static void Information(string message, string title = "信息") => Show(message, title, MessageBoxButton.OK, MessageBoxIcon.Information);

        public static MessageBoxResult Information(string message, string title, MessageBoxButton button, MessageBoxResult defaultResult = MessageBoxResult.None) =>
            Show(message, title, button, MessageBoxIcon.Information, defaultResult);

        public static Task<MessageBoxResult> InformationAsync(string message, string title = "信息") =>
            ShowAsync(message, title, MessageBoxButton.OK, MessageBoxIcon.Information);

        public static Task<MessageBoxResult> InformationAsync(string message, string title, MessageBoxButton button, MessageBoxResult defaultResult = MessageBoxResult.None) =>
            ShowAsync(message, title, button, MessageBoxIcon.Information, defaultResult);

        public static void Success(string message, string title = "成功") => Show(message, title, MessageBoxButton.OK, MessageBoxIcon.Success);

        public static MessageBoxResult Success(string message, string title, MessageBoxButton button, MessageBoxResult defaultResult = MessageBoxResult.None) =>
            Show(message, title, button, MessageBoxIcon.Success, defaultResult);

        public static Task<MessageBoxResult> SuccessAsync(string message, string title = "成功") =>
            ShowAsync(message, title, MessageBoxButton.OK, MessageBoxIcon.Success);

        public static Task<MessageBoxResult> SuccessAsync(string message, string title, MessageBoxButton button, MessageBoxResult defaultResult = MessageBoxResult.None) =>
            ShowAsync(message, title, button, MessageBoxIcon.Success, defaultResult);

        public static MessageBoxResult Question(string message, string title, MessageBoxButton button, MessageBoxResult defaultResult = MessageBoxResult.None) =>
            Show(message, title, button, MessageBoxIcon.Question, defaultResult);

        public static Task<MessageBoxResult> QuestionAsync(string message, string title = "确认") =>
            ShowAsync(message, title, MessageBoxButton.YesNo, MessageBoxIcon.Question);

        public static Task<MessageBoxResult> QuestionAsync(string message, string title, MessageBoxButton button, MessageBoxResult defaultResult = MessageBoxResult.None) =>
            ShowAsync(message, title, button, MessageBoxIcon.Question, defaultResult);
    }

    public class PromptDialogConfig
    {
        public bool ShowLeftButton { get; set; } = false;

        public string LeftButtonText { get; set; } = "左下角按钮";

        public RoutedEventHandler? LeftButtonClick { get; set; }
    }

    /// <summary>
    /// 输入对话框。原版读取的是自定义控件里的 Text / SelectedItem，这里把控件模型交给宿主渲染后再读取
    /// </summary>
    public class PromptDialog : Window
    {
        private readonly string _question;
        private readonly UIElement _content;
        private readonly PromptDialogConfig? _config;

        public PromptDialog(string question, string title, UIElement uiElement, string? defaultValue, PromptDialogConfig? config = null)
        {
            _question = question;
            Title = title;
            _content = uiElement;
            _config = config;
            if (uiElement is System.Windows.Controls.TextBox tb && string.IsNullOrEmpty(tb.Text) && defaultValue != null)
            {
                tb.Text = defaultValue;
            }
        }

        public string ResponseText => _content is System.Windows.Controls.TextBox t ? t.Text : string.Empty;

        public override bool? ShowDialog()
        {
            var size = new Size(double.IsNaN(Width) ? 500 : Width, double.IsNaN(Height) ? 0 : Height);
            DialogResult = UiPlatform.RunSync(() => UiPlatform.Dialogs.ShowContentAsync(_question, Title, _content, size, _config));
            return DialogResult;
        }

        public static string Prompt(string question, string title, string defaultValue = "", PromptDialogConfig? config = null)
        {
            var textBox = new System.Windows.Controls.TextBox { Text = defaultValue };
            return Prompt(question, title, textBox, defaultValue, config);
        }

        public static string Prompt(string question, string title, UIElement uiElement, string defaultValue = "", PromptDialogConfig? config = null)
        {
            if (uiElement is System.Windows.Controls.TextBox tb && string.IsNullOrEmpty(tb.Text))
            {
                tb.Text = defaultValue;
            }
            else if (uiElement is ComboBox cb && cb.SelectedItem == null && !string.IsNullOrEmpty(defaultValue))
            {
                cb.SelectedItem = cb.EffectiveItems.FirstOrDefault(i => i?.ToString() == defaultValue);
            }

            return ShowCore(question, title, uiElement, new Size(400, 0), config, defaultValue);
        }

        public static string Prompt(string question, string title, UIElement uiElement, Size size, PromptDialogConfig? config = null) =>
            ShowCore(question, title, uiElement, size, config, string.Empty);

        private static string ShowCore(string question, string title, UIElement uiElement, Size size, PromptDialogConfig? config, string defaultValue)
        {
            var ok = UiPlatform.RunSync(() => UiPlatform.Dialogs.ShowContentAsync(question, title, uiElement, size, config));
            if (!ok)
            {
                return string.Empty;
            }

            return uiElement switch
            {
                System.Windows.Controls.TextBox t => t.Text,
                ComboBox c => c.SelectedItem?.ToString() ?? string.Empty,
                _ => defaultValue,
            };
        }
    }

    public class AboutWindow : FeatureWindow
    {
        public override string DisplayName => "关于";
    }

    public class ArtifactOcrDialog : FeatureWindow
    {
        public Task<bool> CaptureAsync()
        {
            Wpf.Ui.Violeta.Controls.Toast.Warning("macOS 版暂不支持圣遗物 OCR 测试窗口");
            return Task.FromResult(false);
        }

        public ArtifactOcrDialog(double x, double y, double width, double height, string windowTitle, string? javaScript)
        {
            Title = windowTitle;
            JavaScript = javaScript;
            RegionX = x;
            RegionY = y;
            RegionWidth = width;
            RegionHeight = height;
        }

        public string? JavaScript { get; }

        public double RegionX { get; }

        public double RegionY { get; }

        public double RegionWidth { get; }

        public double RegionHeight { get; }
    }

    public class AutoComboTreeWindow : FeatureWindow
    {
        public override string DisplayName => "自动连招树";
    }

    public class AutoPickBlacklistConfigWindow : FeatureWindow
    {
        public AutoPickBlacklistConfigWindow(GameTask.AutoPick.AutoPickConfig config)
        {
            Config = config;
            Title = "自动拾取黑名单";
        }

        public GameTask.AutoPick.AutoPickConfig Config { get; }
    }

    public class AutoPickWhitelistConfigWindow : FeatureWindow
    {
        public AutoPickWhitelistConfigWindow(GameTask.AutoPick.AutoPickConfig config)
        {
            Config = config;
            Title = "自动拾取白名单";
        }

        public GameTask.AutoPick.AutoPickConfig Config { get; }
    }

    public class CheckUpdateWindow : FeatureWindow
    {
        public CheckUpdateWindow(Model.UpdateOption option)
        {
            Option = option;
            Title = "检查更新";
        }

        public Model.UpdateOption Option { get; }

        public Func<object, CheckUpdateWindowButton, Task>? UserInteraction = null!;

        public bool ShowUpdateStatus { get; set; } = true;

        public string SelectedGitSource { get; set; } = "Github";

        public void NavigateToHtml(string html)
        {
        }

        public enum CheckUpdateWindowButton
        {
            BackgroundUpdate,
            OtherUpdate,
            Update,
            Ignore,
            Cancel,
        }
    }

    public class ChildSessionWindow : FeatureWindow
    {
    }

    public class CustomHtmlMaskEditorWindow : FeatureWindow
    {
        public const string WindowTag = nameof(CustomHtmlMaskEditorWindow);

        public static void ShowEditor(Service.CustomHtmlMaskService customHtmlMaskService, Core.Config.MaskWindowConfig maskWindowConfig) =>
            Wpf.Ui.Violeta.Controls.Toast.Warning("macOS 版暂不支持自定义 HTML 遮罩");
    }

    public class DiscordTargetWindow : FeatureWindow
    {
        public List<Service.Notifier.DiscordBotTarget> Result { get; private set; } = [];

        public DiscordTargetWindow(params object?[] args)
        {
            Title = "Discord 目标";
        }
    }

    public class FeedWindow : FeatureWindow
    {
        public FeedWindow(ViewModel.Windows.FeedWindowViewModel viewModel)
        {
            ViewModel = viewModel;
            Title = "反馈";
        }

        public ViewModel.Windows.FeedWindowViewModel ViewModel { get; }
    }

    public class ImageEditWindow : FeatureWindow
    {
        public ImageEditWindow(string imagePath)
        {
            ImagePath = imagePath;
            Title = "图片编辑";
        }

        /// <summary>
        /// macOS 版不提供编辑，直接使用原图
        /// </summary>
        public override bool? ShowDialog()
        {
            ResultImagePath = ImagePath;
            return true;
        }

        public string? ResultImagePath { get; private set; }

        public string ImagePath { get; }
    }

    public class JsonMonoDialog : FeatureWindow
    {
        public JsonMonoDialog(string path)
        {
            Path = path;
            Title = System.IO.Path.GetFileName(path);
            Tag = nameof(JsonMonoDialog);
        }

        public string Path { get; }

        public static new void Show(string path) => new JsonMonoDialog(path).ShowEditor();

        private void ShowEditor() => base.Show();
    }

    public class KeyBindingsWindow : FeatureWindow
    {
        private static KeyBindingsWindow? _instance;

        public static KeyBindingsWindow Instance => _instance ??= new KeyBindingsWindow();

        public KeyBindingsWindow()
        {
            Title = "按键绑定";
        }
    }

    public class MapPathingDevWindow : FeatureWindow
    {
        public MapPathingDevWindow()
        {
            Title = "地图追踪开发者工具";
        }
    }

    public class MapViewer : FeatureWindow
    {
        public MapViewer(string mapName)
        {
            MapName = mapName;
            Title = "地图查看器";
        }

        public string MapName { get; }
    }

    public class MusicSettingsWindow : FeatureWindow
    {
        public MusicSettingsWindow(object viewModel)
        {
            DataContext = viewModel;
            Title = "音乐设置";
        }
    }

    public class PictureInPictureWindow : FeatureWindow
    {
        public PictureInPictureWindow()
        {
            Title = "画中画";
        }

        public event Action? ClosedByUser;

        public void SetFrame(OpenCvSharp.Mat? frame)
        {
        }

        internal void RaiseClosedByUser() => ClosedByUser?.Invoke();
    }

    public class RecognitionTemplateEditorWindow : FeatureWindow
    {
        public RecognitionTemplateEditorWindow(ViewModel.Windows.RecognitionTemplateEditorViewModel viewModel)
        {
            ViewModel = viewModel;
            Title = "识别模板编辑器";
        }

        public ViewModel.Windows.RecognitionTemplateEditorViewModel ViewModel { get; }
    }

    public class ScriptRepoWindow : FeatureWindow
    {
        public ScriptRepoWindow()
        {
            Title = "脚本仓库";
        }
    }

    public class SkillCdConfigWindow : FeatureWindow
    {
        public System.Collections.ObjectModel.ObservableCollection<GameTask.SkillCd.SkillCdRule> Rules { get; set; }

        public SkillCdConfigWindow(IEnumerable<GameTask.SkillCd.SkillCdRule> initialRules)
        {
            // 深度复制列表，避免直接修改原配置
            Rules = new System.Collections.ObjectModel.ObservableCollection<GameTask.SkillCd.SkillCdRule>(
                (initialRules ?? []).Select(r => new GameTask.SkillCd.SkillCdRule { RoleName = r.RoleName, CdValueText = r.CdValueText }));
            if (Rules.Count == 0)
            {
                Rules.Add(new GameTask.SkillCd.SkillCdRule());
            }

            DataContext = this;
            Title = "技能冷却设置";
        }

        public List<GameTask.SkillCd.SkillCdRule> GetValidRules() => Rules
            .Where(r => !string.IsNullOrWhiteSpace(r.RoleName))
            .Select(r => new GameTask.SkillCd.SkillCdRule { RoleName = r.RoleName.Trim(), CdValueText = r.CdValueText?.Trim() })
            .ToList();
    }

    public class WebImageInput : FeatureWindow
    {
        public WebImageInput(object viewModel)
        {
            DataContext = viewModel;
            Title = "网页图片输入";
        }
    }

    public class WelcomeDialog : FeatureWindow
    {
        public WelcomeDialog()
        {
            Title = "欢迎";
        }
    }

    public class WpfUiWindow : FeatureWindow
    {
        public WpfUiWindow(object content)
        {
            Content = content;
        }
    }
}

namespace BetterGenshinImpact.View.Windows.Editable
{
    public class ScriptGroupProjectEditor : BetterGenshinImpact.View.Windows.FeatureWindow
    {
        public ScriptGroupProjectEditor(object viewModel)
        {
            DataContext = viewModel;
            Title = "脚本组项目编辑";
        }
    }
}

namespace BetterGenshinImpact.View
{
    /// <summary>
    /// 主窗口：由 Avalonia 宿主创建并赋给 Application.Current.MainWindow，这里只承载原版对 MainWindow 的引用
    /// </summary>
    public class MainWindow : Wpf.Ui.Controls.FluentWindow
    {
        public static MainWindow? Instance => Application.Current.MainWindow as MainWindow;

        public virtual void NavigateTo(Type pageType)
        {
            Wpf.Ui.NavigationService.NavigateHandler?.Invoke(pageType);
        }
    }

    public class MaskWindow : Window
    {
        private static MaskWindow? _instance;

        public static MaskWindow Instance() => _instance ??= new MaskWindow();

        public static MaskWindow? InstanceNullable() => _instance;

        public void Refresh()
        {
        }

        public void RefreshPosition()
        {
        }
    }

    public class CaptureTestWindow : BetterGenshinImpact.View.Windows.FeatureWindow
    {
        public CaptureTestWindow()
        {
            Title = "截图测试";
        }

        public void StartCapture(IntPtr hWnd, Fischless.GameCapture.CaptureModes captureMode)
        {
        }
    }

    public class CapturableWindow
    {
        public CapturableWindow(IntPtr handle, string name, string processName, ImageSource? icon)
        {
            Handle = handle;
            Name = name;
            ProcessName = processName;
            Icon = icon;
        }

        public IntPtr Handle { get; }

        public string Name { get; }

        public string ProcessName { get; }

        public ImageSource? Icon { get; }
    }

    /// <summary>
    /// 窗口选择器：macOS 版由宿主列出可截图的窗口（GeForce NOW 等），选择结果通过 <see cref="PickHandler"/> 返回
    /// </summary>
    public class PickerWindow : BetterGenshinImpact.View.Windows.FeatureWindow
    {
        public static Func<bool, IntPtr?>? PickHandler { get; set; }

        public PickerWindow(bool captureTest = false)
        {
            CaptureTest = captureTest;
            Title = "选择窗口";
        }

        public bool CaptureTest { get; }

        public bool PickCaptureTarget(IntPtr hWnd, out IntPtr pickedWindow)
        {
            var picked = UiPlatform.RunSync(() => Task.FromResult(PickHandler?.Invoke(CaptureTest)));
            pickedWindow = picked ?? IntPtr.Zero;
            return picked.HasValue && picked.Value != IntPtr.Zero;
        }
    }

    /// <summary>
    /// 脚本用的 HTML 遮罩窗口。macOS 版暂不支持在游戏上叠加网页
    /// </summary>
    public class HtmlMaskWindow
    {
        public static string Show(string url, string? id, string workDir)
        {
            Wpf.Ui.Violeta.Controls.Toast.Warning("macOS 版暂不支持脚本 HTML 遮罩");
            return id ?? Guid.NewGuid().ToString("N");
        }

        public static bool Close(string id) => true;

        public static bool Reload(string id) => false;

        public static void CloseAll()
        {
        }

        public static void HideAll()
        {
        }

        public static void ShowAll()
        {
        }

        public static void UpdateAllPositions()
        {
        }

        public static string[] GetWindowIds() => [];

        public static bool Exists(string id) => false;

        public static void SetClickThrough(string windowId, bool enabled)
        {
        }

        public static bool GetClickThrough(string windowId) => false;

        public static void ToggleClickThrough(string windowId)
        {
        }

        public static void NotifyFlush(string windowId)
        {
        }
    }
}

namespace BetterGenshinImpact.View.Pages
{
    public class HomePage : Page
    {
    }

    public class OneDragonFlowPage : Page
    {
        public static readonly List<string> SereniteaPotSchedule = ["每天重复", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日"];
    }

    public class ScriptControlPage : Page
    {
    }

    public class KeyBindingsSettingsPage : Page
    {
    }

    public class HotKeyPage : Page
    {
    }

    public class TaskSettingsPage : Page
    {
    }

    public class TriggerSettingsPage : Page
    {
    }

    public class MapPathingPage : Page
    {
    }

    public class JsListPage : Page
    {
    }

    public class CommonSettingsPage : Page
    {
    }

    public class KeyMouseRecordPage : Page
    {
    }
}

namespace BetterGenshinImpact.View.Pages.View
{
    public class HardwareAccelerationView : UserControl
    {
        public HardwareAccelerationView(object viewModel)
        {
            DataContext = viewModel;
        }
    }

    public class PathingConfigView : BetterGenshinImpact.View.Windows.FeatureWindow
    {
        public PathingConfigView(object? viewModel)
        {
            DataContext = viewModel;
            Title = "地图追踪设置";
        }
    }

    public class ScriptGroupConfigView : UserControl
    {
        public ScriptGroupConfigView(object viewModel)
        {
            DataContext = viewModel;
        }
    }
}

namespace BetterGenshinImpact.View.Controls
{
    public class CascadeSelector : UserControl
    {
        public static readonly DependencyProperty SelectedValueProperty =
            DependencyProperty.Register(nameof(SelectedValue), typeof(string), typeof(CascadeSelector));

        public Dictionary<string, List<string>>? CascadeOptions { get; set; }

        public string? SelectedValue { get; set; }

        public string? DefaultValue { get; set; }
    }
}

namespace BetterGenshinImpact.View.Controls.Drawer
{
    public enum DrawerPosition
    {
        Left,
        Right,
        Top,
        Bottom,
    }
}

namespace BetterGenshinImpact.View.Controls.Markdown
{
    /// <summary>
    /// Markdown 显示控件的模型：宿主渲染时读取 Markdown / FilePath
    /// </summary>
    public class MarkdownView : UserControl
    {
        public string? Markdown { get; set; }

        public string? FilePath { get; set; }

        public string? BasePath { get; set; }

        public MarkdownLinkNavigationMode LinkNavigationMode { get; set; }

        public TimeSpan PresentationDelay { get; set; }

        public bool WatchFileChanges { get; set; }
    }
}

namespace BetterGenshinImpact.View.Controls.Webview
{
    /// <summary>
    /// 网页面板：macOS 版没有内嵌浏览器，导航时改用系统浏览器打开
    /// </summary>
    public class WebpagePanel : UserControl, IDisposable
    {
        public string? DownloadFolderPath { get; set; }

        public Action? OnWebViewInitializedAction { get; set; }

        public Microsoft.Web.WebView2.Wpf.WebView2 WebView { get; } = new();

        public static bool IsWebView2Available() => false;

        public static Uri FilePathToFileUrl(string filePath) => new(Path.GetFullPath(filePath));

        public void NavigateToFile(string path) => UiPlatform.Dialogs.OpenUrl(FilePathToFileUrl(path).AbsoluteUri);

        public void NavigateToUri(Uri uri) => UiPlatform.Dialogs.OpenUrl(uri.AbsoluteUri);

        public void NavigateToHtml(string htmlContentOrUrl)
        {
            if (Uri.TryCreate(htmlContentOrUrl, UriKind.Absolute, out var uri) && uri.Scheme.StartsWith("http"))
            {
                NavigateToUri(uri);
                return;
            }

            var file = Path.Combine(Path.GetTempPath(), $"bgi_{Guid.NewGuid():N}.html");
            File.WriteAllText(file, htmlContentOrUrl);
            NavigateToFile(file);
        }

        public void NavigateToMd(string md, string backgroundColor = "#2b2b2b")
        {
            var html = Markdig.Markdown.ToHtml(md);
            NavigateToHtml($"<html><head><meta charset=\"utf-8\"></head><body style=\"background:{backgroundColor};color:#ddd;font-family:-apple-system\">{html}</body></html>");
        }

        public void Dispose()
        {
        }
    }

    public class WebpageWindow : BetterGenshinImpact.View.Windows.FeatureWindow
    {
        public WebpageWindow()
        {
            Panel = new WebpagePanel();
        }

        public WebpagePanel? Panel { get; }

        public Microsoft.Web.WebView2.Wpf.WebView2 WebView => Panel!.WebView;

        public override void Show()
        {
            // 网页内容已经在 Navigate* 时交给系统浏览器
        }

        public void NavigateToUri(Uri uri) => Panel?.NavigateToUri(uri);

        public void NavigateToHtml(string html) => Panel?.NavigateToHtml(html);

        public void NavigateToFile(string path) => Panel?.NavigateToFile(path);
    }
}

namespace BetterGenshinImpact.View.Behavior
{
    public enum TemplateImageSelectionTarget
    {
        Template,
        SearchBox,
    }
}

namespace BetterGenshinImpact.View.Drawable
{
}

namespace BetterGenshinImpact.Platform.Wine
{
    public class WinePlatformAddon : IDisposable
    {
        public static bool IsRunningOnWine => false;

        public static void ApplyApplicationConfig()
        {
        }

        public static bool IsKeyDown(System.Windows.Forms.Keys key) => false;

        public void Dispose()
        {
        }
    }
}
