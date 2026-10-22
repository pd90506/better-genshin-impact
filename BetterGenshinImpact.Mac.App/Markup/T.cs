using System;
using Avalonia.Markup.Xaml;
using BetterGenshinImpact.Service.I18n;

namespace BetterGenshinImpact.MacApp.Markup;

/// <summary>
/// 界面文字翻译，对应原版的 {i18n:T 文本}
/// </summary>
public sealed class T : MarkupExtension
{
    public T()
    {
    }

    public T(string text)
    {
        Text = text;
    }

    public string Text { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        try
        {
            return I18nService.Instance.Translate(Text);
        }
        catch
        {
            return Text;
        }
    }
}

/// <summary>
/// WPF-UI 的图标标记扩展（{ui:SymbolIcon Name}），macOS 版不显示 Fluent 图标
/// </summary>
public sealed class SymbolIcon : MarkupExtension
{
    public SymbolIcon()
    {
    }

    public SymbolIcon(string symbol)
    {
    }

    public override object? ProvideValue(IServiceProvider serviceProvider) => null;
}
