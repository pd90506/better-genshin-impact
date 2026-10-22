using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace BetterGenshinImpact.MacApp.Infrastructure;

/// <summary>
/// 简单的 Markdown 显示：标题、段落、列表、代码块。用于脚本说明等场景
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    public static Control Render(string markdown)
    {
        var doc = Markdown.Parse(markdown ?? string.Empty, Pipeline);
        var panel = new StackPanel { Spacing = 6 };
        foreach (var block in doc)
        {
            AddBlock(panel, block, 0);
        }

        return new ScrollViewer { Content = panel, MaxHeight = 600 };
    }

    private static void AddBlock(StackPanel panel, Block block, int indent)
    {
        switch (block)
        {
            case HeadingBlock h:
                panel.Children.Add(new SelectableTextBlock
                {
                    Text = InlineText(h.Inline),
                    FontSize = h.Level switch { 1 => 22, 2 => 18, 3 => 16, _ => 14 },
                    FontWeight = FontWeight.SemiBold,
                    Margin = new Avalonia.Thickness(0, 6, 0, 2),
                });
                break;
            case ParagraphBlock p:
                panel.Children.Add(new SelectableTextBlock
                {
                    Text = InlineText(p.Inline),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Avalonia.Thickness(indent * 16, 0, 0, 0),
                });
                break;
            case ListBlock list:
                var index = 1;
                foreach (var item in list.OfType<ListItemBlock>())
                {
                    var itemPanel = new StackPanel();
                    foreach (var child in item)
                    {
                        AddBlock(itemPanel, child, 0);
                    }

                    var bullet = list.IsOrdered ? $"{index++}." : "•";
                    var row = new DockPanel { Margin = new Avalonia.Thickness(indent * 16 + 8, 0, 0, 0) };
                    var bulletText = new TextBlock { Text = bullet, Width = 20, VerticalAlignment = VerticalAlignment.Top };
                    DockPanel.SetDock(bulletText, Dock.Left);
                    row.Children.Add(bulletText);
                    row.Children.Add(itemPanel);
                    panel.Children.Add(row);
                }

                break;
            case CodeBlock code:
                panel.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
                    CornerRadius = new Avalonia.CornerRadius(4),
                    Padding = new Avalonia.Thickness(8),
                    Child = new SelectableTextBlock
                    {
                        Text = string.Join("\n", code.Lines.Lines.Take(code.Lines.Count).Select(l => l.ToString())),
                        FontFamily = new FontFamily("Menlo, monospace"),
                    },
                });
                break;
            case QuoteBlock quote:
                foreach (var child in quote)
                {
                    AddBlock(panel, child, indent + 1);
                }

                break;
            case ThematicBreakBlock:
                panel.Children.Add(new Separator());
                break;
            case ContainerBlock container:
                foreach (var child in container)
                {
                    AddBlock(panel, child, indent);
                }

                break;
        }
    }

    private static string InlineText(ContainerInline? inline)
    {
        if (inline == null)
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var i in inline.Descendants<Inline>())
        {
            switch (i)
            {
                case LiteralInline lit:
                    sb.Append(lit.Content.ToString());
                    break;
                case CodeInline code:
                    sb.Append(code.Content);
                    break;
                case LineBreakInline:
                    sb.Append('\n');
                    break;
            }
        }

        return sb.ToString();
    }
}
