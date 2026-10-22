using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using BetterGenshinImpact.Core.Script.Group;
using BetterGenshinImpact.ViewModel.Pages;
using Wpf.Ui.Violeta.Controls;

namespace BetterGenshinImpact.MacApp.Views.Generated;

/// <summary>
/// 原版 OneDragonFlowPage.xaml.cs 中的事件处理
/// </summary>
public partial class OneDragonFlowPage
{
    private Dictionary<CheckBox, string> _checkBoxMappings = new();

    private OneDragonFlowViewModel? ViewModel => DataContext as OneDragonFlowViewModel;

    partial void OnPageCreated()
    {
        _checkBoxMappings = new Dictionary<CheckBox, string>
        {
            { ClothCheckBox, "布匹" },
            { MomentResinCheckBox, "须臾树脂" },
            { SereniteapotExpBookCheckBox, "大英雄的经验" },
            { SereniteapotExpBookSmallCheckBox, "流浪者的经验" },
            { MagicmineralprecisionCheckBox, "精锻用魔矿" },
            { MOlaCheckBox, "摩拉" },
            { ExpBottleBigCheckBox, "祝圣精华" },
            { ExpBottleSmallCheckBox, "祝圣油膏" },
        };

        OneDragonFlowViewModel? subscribed = null;
        DataContextChanged += (_, _) =>
        {
            if (subscribed != null)
            {
                subscribed.PropertyChanged -= ViewModel_PropertyChanged;
            }

            subscribed = ViewModel;
            if (subscribed != null)
            {
                subscribed.PropertyChanged += ViewModel_PropertyChanged;
            }
        };
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel is { } vm && e.PropertyName == nameof(vm.ShouldShowAddTaskGroupPopup) && vm.ShouldShowAddTaskGroupPopup)
        {
            vm.ShouldShowAddTaskGroupPopup = false;
            ShowAddTaskGroupPopup();
        }
    }

    private void ShowAddTaskGroupPopup()
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        try
        {
            vm.ReadScriptGroup();
            if (vm.ScriptGroups == null || !vm.ScriptGroups.Any())
            {
                Toast.Warning("没有找到配置组");
                return;
            }

            ScriptGroupItemsControl.ItemsSource = vm.ScriptGroups;
            AddConfigGroupPopup.PlacementTarget = this;
            AddConfigGroupPopup.Placement = Avalonia.Controls.PlacementMode.Center;
            AddConfigGroupPopup.IsOpen = true;
        }
        catch (Exception ex)
        {
            Toast.Error($"添加任务组时出错: {ex.Message}");
        }
    }

    private void AddTaskGroupButton_Click(object? sender, RoutedEventArgs e) => ShowAddTaskGroupPopup();

    private void ConfirmButton_Click(object? sender, RoutedEventArgs e)
    {
        var selectedGroups = ScriptGroupItemsControl.GetVisualDescendants()
            .OfType<CheckBox>()
            .Where(c => c.IsChecked == true && c.Tag is ScriptGroup)
            .Select(c => ((ScriptGroup)c.Tag!).Name)
            .ToList();

        if (selectedGroups.Count > 0 && ViewModel is { } vm)
        {
            vm.ProcessSelectedGroups(selectedGroups);
            vm.SaveConfig();
            vm.SelectedTask = null;
        }

        AddConfigGroupPopup.IsOpen = false;
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e) => AddConfigGroupPopup.IsOpen = false;

    private void SereniteaPotTpType_Clicked(object? sender, RoutedEventArgs e)
    {
        var config = ViewModel?.SelectedConfig;
        if (config == null)
        {
            return;
        }

        if (ReferenceEquals(sender, PopupCloseButton))
        {
            PotButton.IsChecked = false;
        }
        else if (ReferenceEquals(sender, PopupConfirmButton))
        {
            var selectedObjects = (SereniteaPotComboBox.SelectedItem?.ToString() ?? "每天重复").Split('/')
                .Concat(_checkBoxMappings.Where(pair => pair.Key.IsChecked == true).Select(pair => pair.Value))
                .ToList();
            config.SecretTreasureObjects = selectedObjects;
            PotButton.IsChecked = false;
        }
        else if (ReferenceEquals(sender, PotButton))
        {
            if (!config.SecretTreasureObjects.Any())
            {
                config.SecretTreasureObjects.Add("每天重复");
            }

            SereniteaPotComboBox.SelectedItem = config.SecretTreasureObjects[0];
            foreach (var pair in _checkBoxMappings)
            {
                pair.Key.IsChecked = config.SecretTreasureObjects.Contains(pair.Value);
            }
        }
    }
}
