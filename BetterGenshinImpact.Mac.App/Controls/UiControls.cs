using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Wpf.Ui.Violeta.Controls;

namespace BetterGenshinImpact.MacApp.Controls;

/// <summary>
/// 设置卡片：左边标题说明，右边控件（对应 WPF-UI 的 CardControl）。外观在 App.axaml 的 ControlTheme 中定义
/// </summary>
public class CardControl : HeaderedContentControl
{
    public static readonly StyledProperty<object?> IconProperty = AvaloniaProperty.Register<CardControl, object?>(nameof(Icon));

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
}

/// <summary>
/// 启动/停止两态按钮（对应原版 controls:TwoStateButton）
/// </summary>
public class TwoStateButton : Button
{
    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<TwoStateButton, bool>(nameof(IsChecked), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<object?> EnableContentProperty = AvaloniaProperty.Register<TwoStateButton, object?>(nameof(EnableContent), "启动");

    public static readonly StyledProperty<object?> DisableContentProperty = AvaloniaProperty.Register<TwoStateButton, object?>(nameof(DisableContent), "停止");

    public static readonly StyledProperty<ICommand?> EnableCommandProperty = AvaloniaProperty.Register<TwoStateButton, ICommand?>(nameof(EnableCommand));

    public static readonly StyledProperty<ICommand?> DisableCommandProperty = AvaloniaProperty.Register<TwoStateButton, ICommand?>(nameof(DisableCommand));

    public static readonly StyledProperty<object?> EnableIconProperty = AvaloniaProperty.Register<TwoStateButton, object?>(nameof(EnableIcon));

    public static readonly StyledProperty<object?> DisableIconProperty = AvaloniaProperty.Register<TwoStateButton, object?>(nameof(DisableIcon));

    protected override Type StyleKeyOverride => typeof(Button);

    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public object? EnableContent
    {
        get => GetValue(EnableContentProperty);
        set => SetValue(EnableContentProperty, value);
    }

    public object? DisableContent
    {
        get => GetValue(DisableContentProperty);
        set => SetValue(DisableContentProperty, value);
    }

    public ICommand? EnableCommand
    {
        get => GetValue(EnableCommandProperty);
        set => SetValue(EnableCommandProperty, value);
    }

    public ICommand? DisableCommand
    {
        get => GetValue(DisableCommandProperty);
        set => SetValue(DisableCommandProperty, value);
    }

    public object? EnableIcon
    {
        get => GetValue(EnableIconProperty);
        set => SetValue(EnableIconProperty, value);
    }

    public object? DisableIcon
    {
        get => GetValue(DisableIconProperty);
        set => SetValue(DisableIconProperty, value);
    }

    public TwoStateButton()
    {
        MinWidth = 96;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        UpdateContent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty || change.Property == EnableContentProperty || change.Property == DisableContentProperty)
        {
            UpdateContent();
        }
    }

    private void UpdateContent()
    {
        Content = IsChecked ? DisableContent : EnableContent;
        Classes.Set("accent", !IsChecked);
    }

    protected override void OnClick()
    {
        base.OnClick();
        var command = IsChecked ? DisableCommand : EnableCommand;
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
        }
    }
}

/// <summary>
/// 级联选择（国家 → 秘境 / 首领），对应 Wpf.Ui.Violeta 的 CascadingComboBox
/// </summary>
public class CascadingComboBox : Button
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<CascadingComboBox, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<ICascadingItem?> SelectedCascadingItemProperty =
        AvaloniaProperty.Register<CascadingComboBox, ICascadingItem?>(nameof(SelectedCascadingItem), defaultBindingMode: BindingMode.TwoWay);

    protected override Type StyleKeyOverride => typeof(Button);

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ICascadingItem? SelectedCascadingItem
    {
        get => GetValue(SelectedCascadingItemProperty);
        set => SetValue(SelectedCascadingItemProperty, value);
    }

    public CascadingComboBox()
    {
        MinWidth = 180;
        HorizontalContentAlignment = HorizontalAlignment.Left;
        UpdateText();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            BuildMenu();
        }
        else if (change.Property == SelectedCascadingItemProperty)
        {
            UpdateText();
        }
    }

    private void UpdateText() => Content = SelectedCascadingItem?.Header ?? "（未选择）";

    private void BuildMenu()
    {
        var flyout = new MenuFlyout();
        foreach (var item in ItemsSource?.OfType<ICascadingItem>() ?? [])
        {
            flyout.Items.Add(BuildItem(item));
        }

        Flyout = flyout;
    }

    private MenuItem BuildItem(ICascadingItem item)
    {
        var menuItem = new MenuItem { Header = item.Header };
        if (item.Children.Count > 0)
        {
            foreach (var child in item.Children)
            {
                menuItem.Items.Add(BuildItem(child));
            }
        }
        else
        {
            menuItem.Click += (_, _) => SelectedCascadingItem = item;
        }

        return menuItem;
    }
}

/// <summary>
/// 多选下拉框：下拉面板中为每个选项显示复选框，选中结果写回 SelectedItems 集合
/// </summary>
public class MultiSelectComboBox : Button
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<MultiSelectComboBox, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<IList?> SelectedItemsProperty =
        AvaloniaProperty.Register<MultiSelectComboBox, IList?>(nameof(SelectedItems), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> MaxDropDownHeightProperty = AvaloniaProperty.Register<MultiSelectComboBox, double>(nameof(MaxDropDownHeight), 400);

    protected override Type StyleKeyOverride => typeof(Button);

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public IList? SelectedItems
    {
        get => GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }

    public double MaxDropDownHeight
    {
        get => GetValue(MaxDropDownHeightProperty);
        set => SetValue(MaxDropDownHeightProperty, value);
    }

    public MultiSelectComboBox()
    {
        MinWidth = 180;
        HorizontalContentAlignment = HorizontalAlignment.Left;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty || change.Property == SelectedItemsProperty)
        {
            if (change.OldValue is INotifyCollectionChanged oldNcc)
            {
                oldNcc.CollectionChanged -= OnSelectionCollectionChanged;
            }

            if (change.Property == SelectedItemsProperty && change.NewValue is INotifyCollectionChanged ncc)
            {
                ncc.CollectionChanged += OnSelectionCollectionChanged;
            }

            Rebuild();
        }
    }

    private void OnSelectionCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateText();

    private void Rebuild()
    {
        var panel = new StackPanel { Spacing = 2, Margin = new Thickness(8) };
        foreach (var item in ItemsSource?.Cast<object>() ?? [])
        {
            var check = new CheckBox { Content = item.ToString(), IsChecked = SelectedItems?.Contains(item) == true };
            check.IsCheckedChanged += (_, _) =>
            {
                if (SelectedItems == null)
                {
                    return;
                }

                if (check.IsChecked == true && !SelectedItems.Contains(item))
                {
                    SelectedItems.Add(item);
                }
                else if (check.IsChecked != true && SelectedItems.Contains(item))
                {
                    SelectedItems.Remove(item);
                }

                UpdateText();
            };
            panel.Children.Add(check);
        }

        Flyout = new Flyout { Content = new ScrollViewer { Content = panel, MaxHeight = MaxDropDownHeight } };
        UpdateText();
    }

    private void UpdateText()
    {
        var selected = SelectedItems?.Cast<object>().Select(o => o.ToString()).ToList() ?? new List<string?>();
        Content = selected.Count == 0 ? "（未选择）" : string.Join("、", selected);
    }
}
