using Avalonia.Controls;

namespace BetterGenshinImpact.MacApp.Views.Generated;

/// <summary>
/// 由原版 HotkeyPage.xaml 转换生成，DataContext 为原版 ViewModel
/// </summary>
public partial class HotKeyPage : UserControl
{
    public HotKeyPage()
    {
        InitializeComponent();
        OnPageCreated();
    }

    /// <summary>
    /// macOS 版补充的初始化逻辑（例如原版代码隐藏中的事件处理），在 HotKeyPage.Mac.cs 中实现
    /// </summary>
    partial void OnPageCreated();
}
