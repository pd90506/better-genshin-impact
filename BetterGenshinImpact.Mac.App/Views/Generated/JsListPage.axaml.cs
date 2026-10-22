using Avalonia.Controls;

namespace BetterGenshinImpact.MacApp.Views.Generated;

/// <summary>
/// 由原版 JsListPage.xaml 转换生成，DataContext 为原版 ViewModel
/// </summary>
public partial class JsListPage : UserControl
{
    public JsListPage()
    {
        InitializeComponent();
        OnPageCreated();
    }

    /// <summary>
    /// macOS 版补充的初始化逻辑（例如原版代码隐藏中的事件处理），在 JsListPage.Mac.cs 中实现
    /// </summary>
    partial void OnPageCreated();
}
