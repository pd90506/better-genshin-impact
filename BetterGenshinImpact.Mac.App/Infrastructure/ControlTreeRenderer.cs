using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using W = System.Windows;
using WC = System.Windows.Controls;

namespace BetterGenshinImpact.MacApp.Infrastructure;

/// <summary>
/// 把原版 ViewModel 在代码中拼装的控件模型（System.Windows.Controls 的替身）转换为 Avalonia 控件，并双向同步：
/// 用户的输入写回模型，模型的变化刷新到界面。原版读取 Text / IsChecked / SelectedItem 的逻辑因此不需要改动。
/// </summary>
public static class ControlTreeRenderer
{
    public static Control Render(object? model)
    {
        var control = RenderCore(model);
        if (model is W.FrameworkElement fe)
        {
            ApplyLayout(fe, control);
        }

        return control;
    }

    private static Control RenderCore(object? model)
    {
        switch (model)
        {
            case null:
                return new Panel();
            case Control avaloniaControl:
                return avaloniaControl;
            case View.Pages.View.ScriptGroupConfigView view:
                // 调度器配置组设置：由原版 XAML 转换生成的界面
                return new Views.Generated.ScriptGroupConfigView { DataContext = view.DataContext };
            case string text:
                return new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
            case WC.StackPanel sp:
            {
                var panel = new StackPanel
                {
                    Orientation = sp.Orientation == WC.Orientation.Horizontal ? Orientation.Horizontal : Orientation.Vertical,
                    Spacing = 4,
                };
                FillChildren(sp, panel);
                return panel;
            }
            case WC.WrapPanel wp:
            {
                var panel = new WrapPanel
                {
                    Orientation = wp.Orientation == WC.Orientation.Horizontal ? Orientation.Horizontal : Orientation.Vertical,
                };
                FillChildren(wp, panel);
                return panel;
            }
            case WC.Grid g:
                return RenderGrid(g);
            case WC.DockPanel dp:
            {
                var panel = new DockPanel { LastChildFill = dp.LastChildFill };
                FillChildren(dp, panel);
                return panel;
            }
            case WC.Panel p:
            {
                var panel = new StackPanel();
                FillChildren(p, panel);
                return panel;
            }
            case WC.Border b:
                return new Border { Child = Render(b.Child), Padding = Thick(b.Padding), CornerRadius = new CornerRadius(6) };
            case WC.ScrollViewer sv:
                return new ScrollViewer
                {
                    Content = Render(sv.Content),
                    VerticalScrollBarVisibility = sv.VerticalScrollBarVisibility == WC.ScrollBarVisibility.Disabled
                        ? Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
                        : Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = sv.HorizontalScrollBarVisibility == WC.ScrollBarVisibility.Disabled
                        ? Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
                        : Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                };
            case WC.Expander ex:
                return new Expander { Header = RenderHeader(ex.Header), Content = Render(ex.Content), IsExpanded = ex.IsExpanded };
            case WC.GroupBox gb:
                return new Avalonia.Controls.Primitives.HeaderedContentControl { Header = RenderHeader(gb.Header), Content = Render(gb.Content) };
            case WC.TextBox tb:
                return RenderTextBox(tb);
            case WC.TextBlock tb:
                return RenderTextBlock(tb);
            case WC.PasswordBox pb:
            {
                var box = new TextBox { PasswordChar = '•', Text = pb.Password };
                box.TextChanged += (_, _) => pb.Password = box.Text ?? string.Empty;
                return box;
            }
            case WC.RadioButton rb:
                return RenderToggle(rb, new RadioButton { GroupName = string.IsNullOrEmpty(rb.GroupName) ? null : rb.GroupName });
            case WC.CheckBox cb:
                return RenderToggle(cb, new CheckBox { IsThreeState = cb.IsThreeState });
            case WC.Primitives.ToggleButton tg:
                return RenderToggle(tg, new ToggleSwitch());
            case WC.Button btn:
                return RenderButton(btn);
            case WC.ComboBox combo:
                return RenderComboBox(combo);
            case WC.ListBox list:
                return RenderListBox(list);
            case WC.Label label:
                return new TextBlock { Text = label.Content?.ToString() ?? string.Empty };
            case WC.Separator:
                return new Separator();
            case WC.ProgressBar pbar:
                return new ProgressBar { Minimum = pbar.Minimum, Maximum = pbar.Maximum, Value = pbar.Value, IsIndeterminate = pbar.IsIndeterminate };
            case WC.Image img:
                return new Image { Source = ImageSourceConverter.ToBitmap(img.Source), Stretch = Stretch.Uniform };
            case View.Controls.Markdown.MarkdownView md:
                return MarkdownRenderer.Render(md.Markdown ?? (md.FilePath != null && System.IO.File.Exists(md.FilePath)
                    ? System.IO.File.ReadAllText(md.FilePath)
                    : string.Empty));
            case WC.ContentControl cc:
                return Render(cc.Content);
            case Wpf.Ui.Controls.TitleBar:
                return new Panel();
            default:
                return new TextBlock { Text = model.ToString() ?? string.Empty, TextWrapping = TextWrapping.Wrap };
        }
    }

    private static object? RenderHeader(object? header) => header is W.UIElement ? Render(header) : header;

    private static void FillChildren(WC.Panel source, Panel target)
    {
        foreach (var child in source.Children)
        {
            target.Children.Add(Render(child));
        }

        source.Children.Changed += (_, _) =>
        {
            target.Children.Clear();
            foreach (var child in source.Children)
            {
                target.Children.Add(Render(child));
            }
        };
    }

    private static Control RenderGrid(WC.Grid g)
    {
        var grid = new Grid();
        foreach (var c in g.ColumnDefinitions)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(ToGridLength(c.Width)));
        }

        foreach (var r in g.RowDefinitions)
        {
            grid.RowDefinitions.Add(new RowDefinition(ToGridLength(r.Height)));
        }

        foreach (var child in g.Children)
        {
            var rendered = Render(child);
            Grid.SetRow(rendered, WC.Grid.GetRow(child));
            Grid.SetColumn(rendered, WC.Grid.GetColumn(child));
            Grid.SetRowSpan(rendered, Math.Max(1, WC.Grid.GetRowSpan(child)));
            Grid.SetColumnSpan(rendered, Math.Max(1, WC.Grid.GetColumnSpan(child)));
            grid.Children.Add(rendered);
        }

        return grid;
    }

    private static GridLength ToGridLength(WC.GridLength length) => length.GridUnitType switch
    {
        WC.GridUnitType.Auto => GridLength.Auto,
        WC.GridUnitType.Star => new GridLength(length.Value, GridUnitType.Star),
        _ => new GridLength(length.Value, GridUnitType.Pixel),
    };

    private static Control RenderTextBox(WC.TextBox model)
    {
        var box = new TextBox
        {
            Text = model.Text,
            AcceptsReturn = model.AcceptsReturn,
            AcceptsTab = model.AcceptsTab,
            IsReadOnly = model.IsReadOnly,
            TextWrapping = model.TextWrapping == W.TextWrapping.NoWrap ? TextWrapping.NoWrap : TextWrapping.Wrap,
            Watermark = model is Wpf.Ui.Controls.TextBox uiBox ? uiBox.PlaceholderText : null,
            MinWidth = 200,
        };
        if (model.AcceptsReturn)
        {
            box.MinHeight = 120;
        }

        var updating = false;
        box.TextChanged += (_, _) =>
        {
            if (!updating)
            {
                updating = true;
                model.Text = box.Text ?? string.Empty;
                updating = false;
            }
        };
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WC.TextBox.Text) && !updating)
            {
                updating = true;
                box.Text = model.Text;
                updating = false;
            }
        };
        box.KeyDown += (_, e) => model.RaiseKeyDown(new System.Windows.Input.KeyEventArgs(KeyMapper.ToWpf(e.Key)));
        return box;
    }

    private static Control RenderTextBlock(WC.TextBlock model)
    {
        var block = new TextBlock
        {
            Text = model.DisplayText,
            TextWrapping = model.TextWrapping == W.TextWrapping.NoWrap ? TextWrapping.NoWrap : TextWrapping.Wrap,
        };
        if (!double.IsNaN(model.FontSize))
        {
            block.FontSize = model.FontSize;
        }

        if (model.FontWeight.Weight >= 600)
        {
            block.FontWeight = FontWeight.SemiBold;
        }

        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WC.TextBlock.Text))
            {
                block.Text = model.DisplayText;
            }
        };
        return block;
    }

    private static Control RenderToggle(WC.Primitives.ToggleButton model, Avalonia.Controls.Primitives.ToggleButton toggle)
    {
        toggle.Content = model.Content is W.UIElement ? Render(model.Content) : model.Content;
        toggle.IsChecked = model.IsChecked;
        var updating = false;
        toggle.IsCheckedChanged += (_, _) =>
        {
            if (!updating)
            {
                updating = true;
                model.IsChecked = toggle.IsChecked;
                updating = false;
            }
        };
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WC.Primitives.ToggleButton.IsChecked) && !updating)
            {
                updating = true;
                toggle.IsChecked = model.IsChecked;
                updating = false;
            }
        };
        return toggle;
    }

    private static Control RenderButton(WC.Button model)
    {
        var button = new Button { Content = model.Content is W.UIElement ? Render(model.Content) : model.Content };
        button.Click += (_, _) => model.PerformClick();
        return button;
    }

    private static Control RenderComboBox(WC.ComboBox model)
    {
        var combo = new ComboBox { MinWidth = 200 };
        void Refresh()
        {
            var items = model.EffectiveItems;
            combo.ItemsSource = items.Select(model.GetDisplayText).ToList();
            combo.SelectedIndex = model.SelectedIndex;
        }

        Refresh();
        var updating = false;
        combo.SelectionChanged += (_, _) =>
        {
            if (!updating)
            {
                updating = true;
                model.SelectedIndex = combo.SelectedIndex;
                updating = false;
            }
        };
        model.PropertyChanged += (_, e) =>
        {
            if (updating)
            {
                return;
            }

            updating = true;
            if (e.PropertyName == nameof(WC.ItemsControl.ItemsSource))
            {
                Refresh();
            }
            else if (e.PropertyName is nameof(WC.Selector.SelectedItem) or nameof(WC.Selector.SelectedIndex))
            {
                combo.SelectedIndex = model.SelectedIndex;
            }

            updating = false;
        };
        model.Items.CollectionChanged += (_, _) => Refresh();
        return combo;
    }

    private static Control RenderListBox(WC.ListBox model)
    {
        var list = new ListBox
        {
            ItemsSource = model.EffectiveItems.Select(model.GetDisplayText).ToList(),
            SelectionMode = model.SelectionMode == WC.SelectionMode.Single ? SelectionMode.Single : SelectionMode.Multiple,
            MaxHeight = 400,
        };
        list.SelectionChanged += (_, _) => model.SelectedIndex = list.SelectedIndex;
        return list;
    }

    private static void ApplyLayout(W.FrameworkElement model, Control control)
    {
        control.Margin = Thick(model.Margin);
        if (!double.IsNaN(model.Width) && model.Width > 0)
        {
            control.Width = model.Width;
        }

        if (!double.IsNaN(model.Height) && model.Height > 0)
        {
            control.Height = model.Height;
        }

        if (model.MinWidth > 0)
        {
            control.MinWidth = model.MinWidth;
        }

        if (model.MaxHeight is > 0 and < double.PositiveInfinity)
        {
            control.MaxHeight = model.MaxHeight;
        }

        control.IsEnabled = model.IsEnabled;
        control.IsVisible = model.Visibility == W.Visibility.Visible;
        if (model.ToolTip != null)
        {
            ToolTip.SetTip(control, model.ToolTip.ToString());
        }

        if (model is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(W.UIElement.Visibility))
                {
                    control.IsVisible = model.Visibility == W.Visibility.Visible;
                }
                else if (e.PropertyName == nameof(W.UIElement.IsEnabled))
                {
                    control.IsEnabled = model.IsEnabled;
                }
            };
        }
    }

    private static Thickness Thick(W.Thickness t) => new(t.Left, t.Top, t.Right, t.Bottom);
}
