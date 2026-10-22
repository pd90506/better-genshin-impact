using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using BetterGenshinImpact.Helpers;
using Wpf.Ui.Violeta.Controls;

namespace BetterGenshinImpact.MacApp.Controls;

// 原版 App.xaml 中注册的转换器，在 Avalonia 中以同样的资源键提供。
// WPF 里返回 Visibility 的转换器在这里返回 bool（页面转换时 Visibility 已改为 IsVisible）。

public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is true;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is true;
}

public sealed class BooleanToVisibilityRevertConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

public sealed class InverseBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

public sealed class NotNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s ? !string.IsNullOrWhiteSpace(s) : value != null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

public sealed class BooleanToEnableTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is "Enabled";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is false ? "Disabled" : "Enabled";
}

public sealed class MillisecondsToTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double milliseconds || double.IsNaN(milliseconds) || double.IsInfinity(milliseconds))
        {
            return "00:00";
        }

        var time = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes:00}:{time.Seconds:00}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

public sealed class StringToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && System.Windows.Media.ColorConverter.TryParse(s.StartsWith('#') ? s : "#" + s, out var c))
        {
            var color = Color.FromArgb(c.A, c.R, c.G, c.B);
            return targetType == typeof(IBrush) || targetType == typeof(Brush) ? new SolidColorBrush(color) : color;
        }

        return targetType == typeof(IBrush) || targetType == typeof(Brush) ? Brushes.White : Colors.White;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

/// <summary>
/// 枚举 ↔ (枚举, 描述) 键值对，用于带中文描述的下拉框
/// </summary>
public sealed class EnumToKVPConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || !value.GetType().IsEnum)
        {
            return null;
        }

        return new KeyValuePair<Enum, string>((Enum)value, Describe((Enum)value));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is KeyValuePair<Enum, string> kvp ? kvp.Key : null;

    public static string Describe(Enum value)
    {
        var name = Enum.GetName(value.GetType(), value) ?? value.ToString();
        var field = value.GetType().GetField(name);
        return field?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? name;
    }
}

public sealed class CultureInfoNameToKVPConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string name ? new KeyValuePair<string, string>(name, View.Converters.CultureInfoNameToKVPConverter.GetDisplayName(name)) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is KeyValuePair<string, string> kvp ? kvp.Key : null;
}

/// <summary>
/// 秘境名 ↔ 级联选项
/// </summary>
public sealed class DomainNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string name && !string.IsNullOrEmpty(name))
        {
            foreach (var country in DomainCascadingItems.Items)
            {
                var found = country.Children.FirstOrDefault(d => d.Tag as string == name);
                if (found != null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ICascadingItem { Tag: string name } && !string.IsNullOrWhiteSpace(name) ? name : BindingOperations.DoNothing;
}

/// <summary>
/// 首领名 ↔ 级联选项
/// </summary>
public sealed class AutoBossNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string name && !string.IsNullOrEmpty(name))
        {
            foreach (var country in AutoBossCascadingItems.Items)
            {
                var found = country.Children.FirstOrDefault(d => d.Header == name || d.Tag as string == name);
                if (found != null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ICascadingItem item ? item.Tag as string ?? item.Header : BindingOperations.DoNothing;
}

public sealed class AdaptiveUniformGridColumnsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var minWidth = parameter is string p && double.TryParse(p, NumberStyles.Any, CultureInfo.InvariantCulture, out var w) ? w : 300;
        return value is double width && width > 0 ? Math.Max(1, (int)(width / minWidth)) : 1;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

/// <summary>
/// 原版 ImageSource 替身 → Avalonia 位图
/// </summary>
public sealed class ImageSourceToBitmapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Infrastructure.ImageSourceConverter.ToBitmap(value as System.Windows.Media.ImageSource);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}

/// <summary>
/// 原版 Brush 替身 → Avalonia 画刷
/// </summary>
public sealed class WpfBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        System.Windows.Media.SolidColorBrush b => new SolidColorBrush(Color.FromArgb(b.Color.A, b.Color.R, b.Color.G, b.Color.B)),
        System.Windows.Media.Color c => new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B)),
        _ => null,
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
}
