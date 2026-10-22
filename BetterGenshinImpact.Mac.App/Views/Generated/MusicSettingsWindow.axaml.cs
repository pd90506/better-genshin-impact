using Avalonia.Controls;

namespace BetterGenshinImpact.MacApp.Views.Generated;

/// <summary>
/// 由原版 MusicSettingsWindow.xaml 转换生成，DataContext 为原版 ViewModel
/// </summary>
public partial class MusicSettingsWindow : UserControl
{
    public MusicSettingsWindow()
    {
        InitializeComponent();
        OnPageCreated();
    }

    /// <summary>
    /// macOS 版补充的初始化逻辑（例如原版代码隐藏中的事件处理），在 MusicSettingsWindow.Mac.cs 中实现
    /// </summary>
    partial void OnPageCreated();
}
