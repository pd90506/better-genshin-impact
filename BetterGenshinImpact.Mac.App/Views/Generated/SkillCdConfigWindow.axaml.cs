using Avalonia.Controls;

namespace BetterGenshinImpact.MacApp.Views.Generated;

/// <summary>
/// 由原版 SkillCdConfigWindow.xaml 转换生成，DataContext 为原版 ViewModel
/// </summary>
public partial class SkillCdConfigWindow : UserControl
{
    public SkillCdConfigWindow()
    {
        InitializeComponent();
        OnPageCreated();
    }

    /// <summary>
    /// macOS 版补充的初始化逻辑（例如原版代码隐藏中的事件处理），在 SkillCdConfigWindow.Mac.cs 中实现
    /// </summary>
    partial void OnPageCreated();
}
