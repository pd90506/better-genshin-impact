using Avalonia.Controls;

namespace BetterGenshinImpact.MacApp.Views.Generated;

/// <summary>
/// 由原版 MailPage.xaml 转换生成，DataContext 为原版 ViewModel
/// </summary>
public partial class OneDragonMailPage : UserControl
{
    public OneDragonMailPage()
    {
        InitializeComponent();
        OnPageCreated();
    }

    /// <summary>
    /// macOS 版补充的初始化逻辑（例如原版代码隐藏中的事件处理），在 OneDragonMailPage.Mac.cs 中实现
    /// </summary>
    partial void OnPageCreated();
}
