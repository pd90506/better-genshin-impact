using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BetterGenshinImpact.GameTask.SkillCd;
using BetterGenshinImpact.ViewModel.Windows;

namespace BetterGenshinImpact.MacApp.Views.Generated;

/// <summary>
/// 原版 SkillCdConfigWindow.xaml.cs 中的增删行事件。DataContext 为 SkillCdConfigWindow（兼容层）
/// </summary>
public partial class SkillCdConfigWindow
{
    private View.Windows.SkillCdConfigWindow? Owner => DataContext as View.Windows.SkillCdConfigWindow;

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: SkillCdRule rule })
        {
            Owner?.Rules.Remove(rule);
        }
    }

    private void OnAddClick(object? sender, RoutedEventArgs e) => Owner?.Rules.Add(new SkillCdRule());
}

/// <summary>
/// 原版 JsonMonoDialog 通过 TextChanged 手动同步编辑内容
/// </summary>
public partial class JsonMonoDialog
{
    partial void OnPageCreated()
    {
        JsonCodeBox.TextChanged += (_, _) =>
        {
            if (DataContext is JsonMonoViewModel vm)
            {
                vm.JsonText = JsonCodeBox.Text ?? string.Empty;
            }
        };
    }
}
