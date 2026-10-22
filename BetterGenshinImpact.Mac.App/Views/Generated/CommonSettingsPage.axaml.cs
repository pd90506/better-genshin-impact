using Avalonia.Controls;

namespace BetterGenshinImpact.MacApp.Views.Generated;

/// <summary>
/// 由原版 CommonSettingsPage.xaml 转换生成，DataContext 为原版 ViewModel
/// </summary>
public partial class CommonSettingsPage : UserControl
{
    public CommonSettingsPage()
    {
        InitializeComponent();
        OnPageCreated();
    }

    /// <summary>
    /// macOS 版补充的初始化逻辑（例如原版代码隐藏中的事件处理），在 CommonSettingsPage.Mac.cs 中实现
    /// </summary>
    partial void OnPageCreated();
}
