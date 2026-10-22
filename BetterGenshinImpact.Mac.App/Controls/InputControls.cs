using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.MacApp.Infrastructure;
using BetterGenshinImpact.Model;
using WKey = System.Windows.Input.Key;
using WModifierKeys = System.Windows.Input.ModifierKeys;
using WMouseButton = System.Windows.Input.MouseButton;

namespace BetterGenshinImpact.MacApp.Controls;

/// <summary>
/// 快捷键录入框（对应原版 HotKeyTextBox）：点击后按下组合键即可设置，Delete/Backspace/Esc 清空
/// </summary>
public class HotKeyTextBox : TextBox
{
    public static readonly StyledProperty<string?> HotKeyTypeNameProperty =
        AvaloniaProperty.Register<HotKeyTextBox, string?>(nameof(HotKeyTypeName));

    public static readonly StyledProperty<HotKey> HotkeyProperty =
        AvaloniaProperty.Register<HotKeyTextBox, HotKey>(nameof(Hotkey), defaultBindingMode: BindingMode.TwoWay);

    static HotKeyTextBox()
    {
        HotkeyProperty.Changed.AddClassHandler<HotKeyTextBox>((box, _) => box.Text = box.Hotkey.ToString());
    }

    public HotKeyTextBox()
    {
        IsReadOnly = true;
        Text = Hotkey.ToString();
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnPointerPressedTunnel, RoutingStrategies.Tunnel);
    }

    protected override Type StyleKeyOverride => typeof(TextBox);

    public string? HotKeyTypeName
    {
        get => GetValue(HotKeyTypeNameProperty);
        set => SetValue(HotKeyTypeNameProperty, value);
    }

    public HotKey Hotkey
    {
        get => GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    private bool IsGlobalRegister => HotKeyTypeName == HotKeyTypeEnum.GlobalRegister.ToChineseName();

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = KeyMapper.ToWpf(e.Key);
        var modifiers = KeyMapper.ToWpf(e.KeyModifiers);
        if (key == WKey.None)
        {
            return;
        }

        if (key is WKey.Delete or WKey.Back or WKey.Escape && modifiers == WModifierKeys.None)
        {
            Hotkey = HotKey.None;
            return;
        }

        if (key is WKey.LeftCtrl or WKey.RightCtrl or WKey.LeftAlt or WKey.RightAlt or WKey.LeftShift or WKey.RightShift
            or WKey.LWin or WKey.RWin or WKey.Clear or WKey.OemClear or WKey.Apps)
        {
            return;
        }

        if (key is WKey.Enter or WKey.Tab && modifiers == WModifierKeys.None)
        {
            return;
        }

        if (IsGlobalRegister && key is WKey.Space && modifiers == WModifierKeys.None)
        {
            return;
        }

        // 全局热键不允许单个字符键（会影响正常打字）
        if (IsGlobalRegister && HasKeyChar(key) && modifiers is WModifierKeys.None or WModifierKeys.Shift)
        {
            return;
        }

        Hotkey = new HotKey(key, modifiers);
    }

    private void OnPointerPressedTunnel(object? sender, PointerPressedEventArgs e)
    {
        var kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
        var button = kind switch
        {
            PointerUpdateKind.XButton1Pressed => WMouseButton.XButton1,
            PointerUpdateKind.XButton2Pressed => WMouseButton.XButton2,
            _ => (WMouseButton?)null,
        };
        if (button == null)
        {
            return;
        }

        e.Handled = true;
        Hotkey = IsGlobalRegister ? new HotKey(WKey.None) : new HotKey(WKey.None, WModifierKeys.None, button.Value);
    }

    private static bool HasKeyChar(WKey key) =>
        key is >= WKey.A and <= WKey.Z
            or >= WKey.D0 and <= WKey.D9
            or >= WKey.NumPad0 and <= WKey.NumPad9
            or WKey.OemQuestion or WKey.OemQuotes or WKey.OemPlus or WKey.OemOpenBrackets or WKey.OemCloseBrackets
            or WKey.OemMinus or WKey.Oem1 or WKey.Oem5 or WKey.Oem7 or WKey.OemPeriod or WKey.OemComma
            or WKey.Add or WKey.Divide or WKey.Multiply or WKey.Subtract or WKey.Oem102 or WKey.Decimal;
}

/// <summary>
/// 游戏按键绑定录入框（对应原版 KeyBindingTextBox）：获得焦点后按任意键或鼠标键
/// </summary>
public class KeyBindingTextBox : TextBox
{
    public static readonly StyledProperty<KeyId> KeyBindingProperty =
        AvaloniaProperty.Register<KeyBindingTextBox, KeyId>(nameof(KeyBinding), KeyId.None, defaultBindingMode: BindingMode.TwoWay);

    static KeyBindingTextBox()
    {
        KeyBindingProperty.Changed.AddClassHandler<KeyBindingTextBox>((box, _) => box.Text = box.IsFocused ? "等待按键..." : box.KeyBinding.ToName());
    }

    public KeyBindingTextBox()
    {
        IsReadOnly = true;
        Text = KeyBinding.ToName();
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnPointerPressedTunnel, RoutingStrategies.Tunnel);
    }

    protected override Type StyleKeyOverride => typeof(TextBox);

    public KeyId KeyBinding
    {
        get => GetValue(KeyBindingProperty);
        set => SetValue(KeyBindingProperty, value);
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = KeyMapper.ToWpf(e.Key);
        if (key != WKey.None)
        {
            KeyBinding = KeyIdConverter.FromInputKey(key);
            Text = KeyBinding.ToName();
        }
    }

    private void OnPointerPressedTunnel(object? sender, PointerPressedEventArgs e)
    {
        var kind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
        // 首次点击（未获得焦点）时，忽略一次鼠标左键的输入
        if (kind == PointerUpdateKind.LeftButtonPressed && !IsFocused)
        {
            return;
        }

        WMouseButton? button = kind switch
        {
            PointerUpdateKind.LeftButtonPressed => WMouseButton.Left,
            PointerUpdateKind.RightButtonPressed => WMouseButton.Right,
            PointerUpdateKind.MiddleButtonPressed => WMouseButton.Middle,
            PointerUpdateKind.XButton1Pressed => WMouseButton.XButton1,
            PointerUpdateKind.XButton2Pressed => WMouseButton.XButton2,
            _ => null,
        };
        if (button != null)
        {
            e.Handled = true;
            KeyBinding = KeyIdConverter.FromMouseButton(button.Value);
            Text = KeyBinding.ToName();
        }
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        Text = "等待按键...";
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        Text = KeyBinding.ToName();
        base.OnLostFocus(e);
    }
}

/// <summary>
/// 右侧抽屉（对应原版 DrawerHost）：Content 为原版 ViewModel 生成的 WPF 控件模型，经 ControlTreeRenderer 转为 Avalonia 控件
/// </summary>
public class DrawerHost : Grid
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<DrawerHost, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<DrawerHost, object?>(nameof(Content));

    public static readonly StyledProperty<double> DrawerWidthProperty =
        AvaloniaProperty.Register<DrawerHost, double>(nameof(DrawerWidth), 400);

    private readonly Border _mask;
    private readonly Border _panel;
    private readonly ScrollViewer _scroll;

    static DrawerHost()
    {
        IsOpenProperty.Changed.AddClassHandler<DrawerHost>((d, _) => d.UpdateState());
        ContentProperty.Changed.AddClassHandler<DrawerHost>((d, _) => d.UpdateContent());
        DrawerWidthProperty.Changed.AddClassHandler<DrawerHost>((d, _) => d._panel.Width = d.DrawerWidth);
    }

    public DrawerHost()
    {
        _mask = new Border { Background = new SolidColorBrush(Colors.Black, 0.25) };
        _mask.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            IsOpen = false;
        };

        var close = new Button { Content = "✕", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 8, 0), Padding = new Thickness(10, 4) };
        close.Click += (_, _) => IsOpen = false;
        _scroll = new ScrollViewer { Margin = new Thickness(16, 0, 16, 16), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var dock = new DockPanel();
        DockPanel.SetDock(close, Dock.Top);
        dock.Children.Add(close);
        dock.Children.Add(_scroll);

        _panel = new Border
        {
            Width = DrawerWidth,
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = dock,
            BoxShadow = BoxShadows.Parse("-4 0 16 0 #40000000"),
        };
        _panel.Bind(Border.BackgroundProperty, _panel.GetResourceObservable("ApplicationBackgroundBrush"));

        Children.Add(_mask);
        Children.Add(_panel);
        UpdateState();
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public double DrawerWidth
    {
        get => GetValue(DrawerWidthProperty);
        set => SetValue(DrawerWidthProperty, value);
    }

    private void UpdateState()
    {
        IsVisible = IsOpen;
        IsHitTestVisible = IsOpen;
        if (IsOpen && DataContext is { } dc)
        {
            // 原版在抽屉打开后执行 OnDrawerOpenedCommand
            if (dc.GetType().GetProperty("DrawerVm")?.GetValue(dc) is View.Controls.Drawer.DrawerViewModel vm)
            {
                _panel.Width = vm.DrawerWidth;
                vm.OnDrawerOpenedCommand?.Execute(null);
            }
        }
    }

    private void UpdateContent()
    {
        try
        {
            _scroll.Content = Content == null ? null : ControlTreeRenderer.Render(Content);
        }
        catch (Exception e)
        {
            _scroll.Content = new TextBlock { Text = "内容加载失败：" + e.Message, TextWrapping = TextWrapping.Wrap };
        }
    }
}

/// <summary>
/// 代码编辑框（对应原版 JsonCodeBox）：等宽字体的多行文本框
/// </summary>
public class CodeTextBox : TextBox
{
    public static readonly StyledProperty<string?> CodeProperty =
        AvaloniaProperty.Register<CodeTextBox, string?>(nameof(Code));

    static CodeTextBox()
    {
        CodeProperty.Changed.AddClassHandler<CodeTextBox>((box, _) =>
        {
            if (box.Text != box.Code)
            {
                box.Text = box.Code;
            }
        });
    }

    public CodeTextBox()
    {
        AcceptsReturn = true;
        AcceptsTab = true;
        TextWrapping = TextWrapping.NoWrap;
        FontFamily = new FontFamily("Menlo, Consolas, monospace");
        Foreground = Brushes.Gainsboro;
    }

    protected override Type StyleKeyOverride => typeof(TextBox);

    public string? Code
    {
        get => GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }
}
