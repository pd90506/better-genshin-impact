using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using BetterGenshinImpact.Platform;
using BetterGenshinImpact.View.Windows;
using W = System.Windows;

namespace BetterGenshinImpact.MacApp.Infrastructure;

/// <summary>
/// 原版的消息框、输入框、文件选择、功能窗口等请求在 macOS 上的实现
/// </summary>
public sealed class AvaloniaDialogHost : IDialogHost
{
    private static Window? Owner =>
        (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Windows
        .LastOrDefault(w => w.IsActive) ??
        (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    public async Task<W.MessageBoxResult> ShowMessageAsync(string content, string title, W.MessageBoxButton button,
        Wpf.Ui.Violeta.Controls.MessageBoxIcon icon, W.MessageBoxResult defaultResult)
    {
        var buttons = button switch
        {
            W.MessageBoxButton.OKCancel => new[] { ("确定", W.MessageBoxResult.OK), ("取消", W.MessageBoxResult.Cancel) },
            W.MessageBoxButton.YesNo => new[] { ("是", W.MessageBoxResult.Yes), ("否", W.MessageBoxResult.No) },
            W.MessageBoxButton.YesNoCancel => new[] { ("是", W.MessageBoxResult.Yes), ("否", W.MessageBoxResult.No), ("取消", W.MessageBoxResult.Cancel) },
            _ => new[] { ("确定", W.MessageBoxResult.OK) },
        };
        var glyph = icon switch
        {
            Wpf.Ui.Violeta.Controls.MessageBoxIcon.Error => "⛔",
            Wpf.Ui.Violeta.Controls.MessageBoxIcon.Warning => "⚠️",
            Wpf.Ui.Violeta.Controls.MessageBoxIcon.Question => "❓",
            Wpf.Ui.Violeta.Controls.MessageBoxIcon.Success => "✅",
            Wpf.Ui.Violeta.Controls.MessageBoxIcon.None => null,
            _ => "ℹ️",
        };

        var body = new DockPanel { Margin = new Thickness(20) };
        if (glyph != null)
        {
            var iconText = new TextBlock { Text = glyph, FontSize = 28, Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Top };
            DockPanel.SetDock(iconText, Dock.Left);
            body.Children.Add(iconText);
        }

        body.Children.Add(new ScrollViewer
        {
            MaxHeight = 480,
            Content = new SelectableTextBlock { Text = content, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 },
        });

        var result = await ShowDialogAsync(title, body, buttons.Select(b => (b.Item1, (object)b.Item2)).ToArray(), defaultResult);
        return result is W.MessageBoxResult r ? r : defaultResult == W.MessageBoxResult.None
            ? button == W.MessageBoxButton.OK ? W.MessageBoxResult.OK : W.MessageBoxResult.Cancel
            : defaultResult;
    }

    public async Task<string?> PromptAsync(string question, string title, string defaultValue)
    {
        var box = new W.Controls.TextBox { Text = defaultValue };
        return await ShowContentAsync(question, title, box, new W.Size(400, 0), null) ? box.Text : null;
    }

    public async Task<bool> ShowContentAsync(string question, string title, W.UIElement? content, W.Size size, PromptDialogConfig? config)
    {
        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(20) };
        if (!string.IsNullOrWhiteSpace(question))
        {
            panel.Children.Add(new SelectableTextBlock { Text = question, TextWrapping = TextWrapping.Wrap });
        }

        if (content != null)
        {
            panel.Children.Add(ControlTreeRenderer.Render(content));
        }

        var buttons = new List<(string, object)>();
        if (config is { ShowLeftButton: true })
        {
            buttons.Add((config.LeftButtonText, "left"));
        }

        buttons.Add(("确定", true));
        buttons.Add(("取消", false));
        var width = size.Width > 0 ? size.Width : 460;
        var height = size.Height > 0 ? size.Height : double.NaN;
        var result = await ShowDialogAsync(title, new ScrollViewer { Content = panel }, buttons.ToArray(), null, width, height,
            onButton: tag =>
            {
                if (tag is "left")
                {
                    config!.LeftButtonClick?.Invoke(this, new W.RoutedEventArgs());
                    return false;
                }

                return true;
            });
        return result is true;
    }

    public async Task<Wpf.Ui.Controls.MessageBoxResult> ShowContentDialogAsync(Wpf.Ui.Controls.MessageBox messageBox)
    {
        Control body = messageBox.Content is W.UIElement element
            ? ControlTreeRenderer.Render(element)
            : new SelectableTextBlock { Text = messageBox.Content?.ToString() ?? string.Empty, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 };
        var buttons = new List<(string, object)>();
        if (!string.IsNullOrEmpty(messageBox.PrimaryButtonText) && messageBox.IsPrimaryButtonEnabled)
        {
            buttons.Add((messageBox.PrimaryButtonText, Wpf.Ui.Controls.MessageBoxResult.Primary));
        }

        if (!string.IsNullOrEmpty(messageBox.SecondaryButtonText) && messageBox.IsSecondaryButtonEnabled)
        {
            buttons.Add((messageBox.SecondaryButtonText, Wpf.Ui.Controls.MessageBoxResult.Secondary));
        }

        buttons.Add((string.IsNullOrEmpty(messageBox.CloseButtonText) ? "关闭" : messageBox.CloseButtonText, Wpf.Ui.Controls.MessageBoxResult.None));
        var width = double.IsNaN(messageBox.Width) || messageBox.Width <= 0 ? 520 : messageBox.Width;
        var height = double.IsNaN(messageBox.Height) || messageBox.Height <= 0 ? double.NaN : messageBox.Height;
        var result = await ShowDialogAsync(messageBox.Title, new ScrollViewer { Content = new Border { Padding = new Thickness(20), Child = body } },
            buttons.ToArray(), null, width, height);
        return result is Wpf.Ui.Controls.MessageBoxResult r ? r : Wpf.Ui.Controls.MessageBoxResult.None;
    }

    /// <summary>
    /// 通用的对话框窗口：内容 + 底部按钮
    /// </summary>
    private static async Task<object?> ShowDialogAsync(string title, Control body, (string Text, object Tag)[] buttons, object? defaultResult,
        double width = 480, double height = double.NaN, Func<object, bool>? onButton = null)
    {
        var window = new Window
        {
            Title = string.IsNullOrEmpty(title) ? "BetterGI" : title,
            Width = width,
            SizeToContent = double.IsNaN(height) ? SizeToContent.Height : SizeToContent.Manual,
            MaxHeight = 760,
            CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
        };
        if (!double.IsNaN(height))
        {
            window.Height = height;
        }

        object? result = defaultResult;
        var buttonBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(20, 0, 20, 16),
        };
        for (var i = 0; i < buttons.Length; i++)
        {
            var (text, tag) = buttons[i];
            var btn = new Button { Content = text, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
            if (i == 0)
            {
                btn.Classes.Add("accent");
                btn.IsDefault = true;
            }

            if (tag is false or W.MessageBoxResult.Cancel or Wpf.Ui.Controls.MessageBoxResult.None)
            {
                btn.IsCancel = true;
            }

            btn.Click += (_, _) =>
            {
                if (onButton != null && !onButton(tag))
                {
                    return;
                }

                result = tag;
                window.Close();
            };
            buttonBar.Children.Add(btn);
        }

        var root = new DockPanel();
        DockPanel.SetDock(buttonBar, Dock.Bottom);
        root.Children.Add(buttonBar);
        root.Children.Add(body);
        window.Content = root;

        var owner = Owner;
        if (owner != null && owner.IsVisible)
        {
            await window.ShowDialog(owner);
        }
        else
        {
            var tcs = new TaskCompletionSource();
            window.Closed += (_, _) => tcs.TrySetResult();
            window.Show();
            await tcs.Task;
        }

        return result;
    }

    public async Task<string?> PickFileAsync(string title, string? filter, bool save, string? defaultFileName)
    {
        var top = Owner;
        if (top == null)
        {
            return null;
        }

        var fileTypes = ParseFilter(filter);
        if (save)
        {
            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = string.IsNullOrEmpty(defaultFileName) ? null : System.IO.Path.GetFileName(defaultFileName),
                FileTypeChoices = fileTypes,
            });
            return file?.TryGetLocalPath();
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypes,
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    /// <summary>
    /// 把 WinForms 风格的过滤器（"图片|*.png;*.jpg|所有文件|*.*"）转换为 Avalonia 的文件类型
    /// </summary>
    private static List<FilePickerFileType>? ParseFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return null;
        }

        var parts = filter.Split('|');
        var list = new List<FilePickerFileType>();
        for (var i = 0; i + 1 < parts.Length; i += 2)
        {
            list.Add(new FilePickerFileType(parts[i]) { Patterns = parts[i + 1].Split(';', StringSplitOptions.RemoveEmptyEntries) });
        }

        return list.Count > 0 ? list : null;
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        var top = Owner;
        if (top == null)
        {
            return null;
        }

        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public bool OpenFeatureWindow(W.Window window, bool modal) => FeatureWindows.Open(window, modal);

    public void OpenUrl(string url)
    {
        try
        {
            var psi = new ProcessStartInfo(OperatingSystem.IsMacOS() ? "/usr/bin/open" : "xdg-open") { UseShellExecute = false };
            psi.ArgumentList.Add(url);
            using var _ = Process.Start(psi);
        }
        catch
        {
            // ignored
        }
    }
}
