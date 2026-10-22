using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

// 原版部分 ViewModel 会在代码里临时拼装对话框内容（StackPanel + TextBox + CheckBox ...），
// 再交给 PromptDialog / MessageBox 显示；JS 脚本的设置界面（SettingItem）也是这样生成并通过 Binding 写回配置。
// macOS 版把这些控件实现为一个轻量的控件模型（属性变更会发出通知），
// 由 Avalonia 宿主在显示时转换为真实控件，用户输入写回模型，原版读取 Text / IsChecked / SelectedItem 的逻辑不变。
namespace System.Windows.Controls
{
    public enum Orientation
    {
        Horizontal = 0,
        Vertical = 1,
    }

    public enum ScrollBarVisibility
    {
        Disabled = 0,
        Auto = 1,
        Hidden = 2,
        Visible = 3,
    }

    public enum SelectionMode
    {
        Single = 0,
        Multiple = 1,
        Extended = 2,
    }

    public enum Dock
    {
        Left = 0,
        Top = 1,
        Right = 2,
        Bottom = 3,
    }

    /// <summary>
    /// 控件模型基类：属性变更会通知渲染层与绑定
    /// </summary>
    public class Control : FrameworkElement, INotifyPropertyChanged
    {
        private object? _resourceBackgroundKey;

        public event PropertyChangedEventHandler? PropertyChanged;

        public double FontSize { get; set; } = double.NaN;

        public FontWeight FontWeight { get; set; }

        public Brush? Foreground { get; set; }

        public Brush? Background { get; set; }

        public Brush? BorderBrush { get; set; }

        public Thickness BorderThickness { get; set; }

        public Thickness Padding { get; set; }

        public bool IsTabStop { get; set; } = true;

        public VerticalAlignment VerticalContentAlignment { get; set; }

        public HorizontalAlignment HorizontalContentAlignment { get; set; }

        public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(nameof(Background), typeof(Brush), typeof(Control));

        /// <summary>
        /// 引用主题资源（例如卡片背景色）。渲染层按资源键自行取主题色
        /// </summary>
        public void SetResourceReference(DependencyProperty dp, object name)
        {
            if (dp.Name == nameof(Background))
            {
                _resourceBackgroundKey = name;
            }
        }

        public object? BackgroundResourceKey => _resourceBackgroundKey;

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(name);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public event KeyEventHandler? KeyDown;

        public event KeyEventHandler? PreviewKeyDown;

        public void RaiseKeyDown(KeyEventArgs e)
        {
            PreviewKeyDown?.Invoke(this, e);
            KeyDown?.Invoke(this, e);
        }

        public event MouseButtonEventHandler? MouseDoubleClick;

        public void RaiseMouseDoubleClick() => MouseDoubleClick?.Invoke(this, new MouseButtonEventArgs());

        public void RaiseLoadedEvent() => RaiseLoaded();
    }

    public readonly struct FontWeight
    {
        public FontWeight(int weight)
        {
            Weight = weight;
        }

        public int Weight { get; }
    }

    /// <summary>
    /// 子元素集合，维护子元素的 Parent
    /// </summary>
    public sealed class UIElementCollection : IList<UIElement>
    {
        private readonly List<UIElement> _items = new();
        private readonly DependencyObject _owner;

        public UIElementCollection(DependencyObject owner)
        {
            _owner = owner;
        }

        public event EventHandler? Changed;

        public UIElement this[int index]
        {
            get => _items[index];
            set
            {
                _items[index].Parent = null;
                _items[index] = value;
                value.Parent = _owner;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public int Count => _items.Count;

        public bool IsReadOnly => false;

        public int Add(UIElement element)
        {
            element.Parent = _owner;
            _items.Add(element);
            Changed?.Invoke(this, EventArgs.Empty);
            return _items.Count - 1;
        }

        void ICollection<UIElement>.Add(UIElement item) => Add(item);

        public void Insert(int index, UIElement element)
        {
            element.Parent = _owner;
            _items.Insert(index, element);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Remove(UIElement element)
        {
            if (_items.Remove(element))
            {
                element.Parent = null;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        bool ICollection<UIElement>.Remove(UIElement item)
        {
            var contains = _items.Contains(item);
            Remove(item);
            return contains;
        }

        public void RemoveAt(int index) => Remove(_items[index]);

        public void RemoveRange(int index, int count)
        {
            foreach (var e in _items.GetRange(index, count))
            {
                Remove(e);
            }
        }

        public void Clear()
        {
            foreach (var e in _items)
            {
                e.Parent = null;
            }

            _items.Clear();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public bool Contains(UIElement item) => _items.Contains(item);

        public void CopyTo(UIElement[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

        public int IndexOf(UIElement item) => _items.IndexOf(item);

        public IEnumerator<UIElement> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class Panel : Control
    {
        public Panel()
        {
            Children = new UIElementCollection(this);
        }

        public UIElementCollection Children { get; }
    }

    public class StackPanel : Panel
    {
        public Orientation Orientation { get; set; } = Orientation.Vertical;
    }

    public class WrapPanel : Panel
    {
        public Orientation Orientation { get; set; } = Orientation.Horizontal;

        public double ItemWidth { get; set; } = double.NaN;
    }

    public class DockPanel : Panel
    {
        private static readonly ConditionalWeakTable<UIElement, object> Docks = new();

        public bool LastChildFill { get; set; } = true;

        public static void SetDock(UIElement element, Dock dock) => Docks.AddOrUpdate(element, dock);

        public static Dock GetDock(UIElement element) => Docks.TryGetValue(element, out var d) ? (Dock)d : Dock.Left;
    }

    public class ColumnDefinition
    {
        public GridLength Width { get; set; } = GridLength.Star;
    }

    public class RowDefinition
    {
        public GridLength Height { get; set; } = GridLength.Auto;
    }

    public readonly struct GridLength
    {
        public GridLength(double value, GridUnitType type = GridUnitType.Pixel)
        {
            Value = value;
            GridUnitType = type;
        }

        public double Value { get; }

        public GridUnitType GridUnitType { get; }

        public bool IsAuto => GridUnitType == GridUnitType.Auto;

        public bool IsStar => GridUnitType == GridUnitType.Star;

        public static GridLength Auto => new(1, GridUnitType.Auto);

        public static GridLength Star => new(1, GridUnitType.Star);
    }

    public enum GridUnitType
    {
        Auto = 0,
        Pixel = 1,
        Star = 2,
    }

    public class Grid : Panel
    {
        private static readonly ConditionalWeakTable<UIElement, int[]> Positions = new();

        public Collection<ColumnDefinition> ColumnDefinitions { get; } = new();

        public Collection<RowDefinition> RowDefinitions { get; } = new();

        private static int[] Pos(UIElement element) => Positions.GetValue(element, _ => new int[4] { 0, 0, 1, 1 });

        public static void SetRow(UIElement element, int value) => Pos(element)[0] = value;

        public static void SetColumn(UIElement element, int value) => Pos(element)[1] = value;

        public static void SetRowSpan(UIElement element, int value) => Pos(element)[2] = value;

        public static void SetColumnSpan(UIElement element, int value) => Pos(element)[3] = value;

        public static int GetRow(UIElement element) => Pos(element)[0];

        public static int GetColumn(UIElement element) => Pos(element)[1];

        public static int GetRowSpan(UIElement element) => Pos(element)[2];

        public static int GetColumnSpan(UIElement element) => Pos(element)[3];
    }

    public class Border : Control
    {
        private UIElement? _child;

        public UIElement? Child
        {
            get => _child;
            set
            {
                if (_child != null)
                {
                    _child.Parent = null;
                }

                _child = value;
                if (value != null)
                {
                    value.Parent = this;
                }
            }
        }

        public CornerRadius CornerRadius { get; set; }
    }

    public class ContentControl : Control
    {
        private object? _content;

        public object? Content
        {
            get => _content;
            set
            {
                if (_content is UIElement old)
                {
                    old.Parent = null;
                }

                if (Set(ref _content, value) && value is UIElement element)
                {
                    element.Parent = this;
                }
            }
        }
    }

    public class Label : ContentControl
    {
    }

    public class ScrollViewer : ContentControl
    {
        public ScrollBarVisibility VerticalScrollBarVisibility { get; set; } = ScrollBarVisibility.Auto;

        public ScrollBarVisibility HorizontalScrollBarVisibility { get; set; } = ScrollBarVisibility.Disabled;

        public void ScrollToEnd()
        {
        }

        public void ScrollToTop()
        {
        }

        public void ScrollToHome()
        {
        }
    }

    public class TextBlock : Control
    {
        private string _text = string.Empty;

        public TextBlock()
        {
        }

        public TextBlock(Documents.Inline inline)
        {
            Inlines.Add(inline);
            _text = inline.Text;
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(TextBlock));

        public string Text
        {
            get => _text;
            set => Set(ref _text, value ?? string.Empty);
        }

        public TextWrapping TextWrapping { get; set; }

        public TextAlignment TextAlignment { get; set; }

        public Documents.InlineCollection Inlines { get; } = new();

        /// <summary>
        /// 渲染用的完整文本：Text 为空时拼接 Inlines
        /// </summary>
        public string DisplayText => !string.IsNullOrEmpty(Text) ? Text : string.Concat(Inlines.Select(i => i.Text));
    }

    public class TextChangedEventArgs : RoutedEventArgs
    {
    }

    public delegate void TextChangedEventHandler(object sender, TextChangedEventArgs e);

    public class TextBox : Control
    {
        private string _text = string.Empty;

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(TextBox));

        public string Text
        {
            get => _text;
            set
            {
                if (Set(ref _text, value ?? string.Empty))
                {
                    TextChanged?.Invoke(this, new TextChangedEventArgs());
                }
            }
        }

        public bool AcceptsReturn { get; set; }

        public bool AcceptsTab { get; set; }

        public bool IsReadOnly { get; set; }

        public TextWrapping TextWrapping { get; set; }

        public int MaxLength { get; set; }

        public ScrollBarVisibility VerticalScrollBarVisibility { get; set; }

        public ScrollBarVisibility HorizontalScrollBarVisibility { get; set; }

        public int CaretIndex { get; set; }

        public event TextChangedEventHandler? TextChanged;

        public void SelectAll()
        {
        }

        public void ScrollToEnd()
        {
        }

        public void AppendText(string textData) => Text += textData;

        public void Clear() => Text = string.Empty;
    }

    public class PasswordBox : Control
    {
        public string Password { get; set; } = string.Empty;
    }

    public class ButtonBase : ContentControl
    {
        public ICommand? Command { get; set; }

        public object? CommandParameter { get; set; }

        public event RoutedEventHandler? Click;

        /// <summary>
        /// 渲染层在用户点击时调用
        /// </summary>
        public virtual void PerformClick()
        {
            Click?.Invoke(this, new RoutedEventArgs { Source = this, OriginalSource = this });
            if (Command?.CanExecute(CommandParameter) == true)
            {
                Command.Execute(CommandParameter);
            }
        }
    }

    public class Button : ButtonBase
    {
        public bool IsDefault { get; set; }

        public bool IsCancel { get; set; }
    }

    public class CheckBox : ToggleButton
    {
    }

    public class RadioButton : ToggleButton
    {
        public string GroupName { get; set; } = string.Empty;
    }

    public class SelectionChangedEventArgs : RoutedEventArgs
    {
        public SelectionChangedEventArgs(IList removedItems, IList addedItems)
        {
            RemovedItems = removedItems;
            AddedItems = addedItems;
        }

        public IList RemovedItems { get; }

        public IList AddedItems { get; }
    }

    public delegate void SelectionChangedEventHandler(object sender, SelectionChangedEventArgs e);

    public class ItemCollection : ObservableCollection<object>
    {
    }

    public class ItemsControl : Control
    {
        private IEnumerable? _itemsSource;

        public ItemCollection Items { get; } = new();

        public IEnumerable? ItemsSource
        {
            get => _itemsSource;
            set => Set(ref _itemsSource, value);
        }

        public string DisplayMemberPath { get; set; } = string.Empty;

        public string SelectedValuePath { get; set; } = string.Empty;

        public object? ItemTemplate { get; set; }

        /// <summary>
        /// 实际展示的条目：优先 ItemsSource
        /// </summary>
        public IList<object?> EffectiveItems => ItemsSource != null ? ItemsSource.Cast<object?>().ToList() : Items.Cast<object?>().ToList();

        /// <summary>
        /// 条目的显示文本
        /// </summary>
        public string GetDisplayText(object? item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(DisplayMemberPath))
            {
                return item.GetType().GetProperty(DisplayMemberPath)?.GetValue(item)?.ToString() ?? string.Empty;
            }

            return item is ContentControl cc ? cc.Content?.ToString() ?? string.Empty : item.ToString() ?? string.Empty;
        }
    }

    public class Selector : ItemsControl
    {
        private object? _selectedItem;

        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(Selector));

        public static readonly DependencyProperty SelectedValueProperty = DependencyProperty.Register(nameof(SelectedValue), typeof(object), typeof(Selector));

        public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(Selector));

        public object? SelectedItem
        {
            get => _selectedItem;
            set
            {
                var old = _selectedItem;
                if (Set(ref _selectedItem, value))
                {
                    OnPropertyChanged(nameof(SelectedIndex));
                    OnPropertyChanged(nameof(SelectedValue));
                    SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(old == null ? Array.Empty<object>() : new[] { old },
                        value == null ? Array.Empty<object>() : new[] { value }));
                }
            }
        }

        public int SelectedIndex
        {
            get => SelectedItem == null ? -1 : EffectiveItems.IndexOf(SelectedItem);
            set
            {
                var items = EffectiveItems;
                SelectedItem = value >= 0 && value < items.Count ? items[value] : null;
            }
        }

        public object? SelectedValue
        {
            get
            {
                if (SelectedItem == null || string.IsNullOrEmpty(SelectedValuePath))
                {
                    return SelectedItem;
                }

                return SelectedItem.GetType().GetProperty(SelectedValuePath)?.GetValue(SelectedItem);
            }
            set
            {
                if (string.IsNullOrEmpty(SelectedValuePath))
                {
                    SelectedItem = EffectiveItems.FirstOrDefault(i => Equals(i, value) || Equals(i?.ToString(), value?.ToString()));
                    return;
                }

                SelectedItem = EffectiveItems.FirstOrDefault(i =>
                    Equals(i?.GetType().GetProperty(SelectedValuePath)?.GetValue(i), value));
            }
        }

        public event SelectionChangedEventHandler? SelectionChanged;
    }

    public class ComboBox : Selector
    {
        public bool IsEditable { get; set; }

        public string Text { get; set; } = string.Empty;

        public bool IsDropDownOpen { get; set; }
    }

    public class ComboBoxItem : ContentControl
    {
        public override string ToString() => Content?.ToString() ?? string.Empty;
    }

    public class ListBox : Selector
    {
        public SelectionMode SelectionMode { get; set; }

        public IList SelectedItems { get; } = new List<object>();
    }

    public class ListBoxItem : ContentControl
    {
    }

    public class ListView : ListBox
    {
    }

    public class Separator : Control
    {
    }

    public class Expander : ContentControl
    {
        private object? _header;

        public object? Header
        {
            get => _header;
            set
            {
                if (_header is UIElement old)
                {
                    old.Parent = null;
                }

                _header = value;
                if (value is UIElement element)
                {
                    element.Parent = this;
                }
            }
        }

        public bool IsExpanded { get; set; }
    }

    public class GroupBox : ContentControl
    {
        public object? Header { get; set; }
    }

    public class Image : Control
    {
        public ImageSource? Source { get; set; }

        public Stretch Stretch { get; set; } = Stretch.Uniform;
    }

    public class ProgressBar : Control
    {
        public double Value { get; set; }

        public double Minimum { get; set; }

        public double Maximum { get; set; } = 100;

        public bool IsIndeterminate { get; set; }
    }

    public class Slider : Control
    {
        public double Value { get; set; }

        public double Minimum { get; set; }

        public double Maximum { get; set; } = 10;
    }

    public class UserControl : ContentControl
    {
    }

    public class Page : UserControl
    {
        public string Title { get; set; } = string.Empty;
    }

    public class ToolTip : ContentControl
    {
    }

    public class MouseButtonEventArgs : RoutedEventArgs
    {
        public MouseButton ChangedButton { get; init; }

        public int ClickCount { get; init; } = 1;
    }

    public delegate void MouseButtonEventHandler(object sender, MouseButtonEventArgs e);
}

namespace System.Windows.Controls.Primitives
{
    public class ToggleButton : ButtonBase
    {
        private bool? _isChecked = false;

        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(nameof(IsChecked), typeof(bool?), typeof(ToggleButton));

        public bool? IsChecked
        {
            get => _isChecked;
            set
            {
                if (Set(ref _isChecked, value))
                {
                    var args = new RoutedEventArgs { Source = this };
                    switch (value)
                    {
                        case true:
                            Checked?.Invoke(this, args);
                            break;
                        case false:
                            Unchecked?.Invoke(this, args);
                            break;
                        default:
                            Indeterminate?.Invoke(this, args);
                            break;
                    }
                }
            }
        }

        public bool IsThreeState { get; set; }

        public event RoutedEventHandler? Checked;

        public event RoutedEventHandler? Unchecked;

        public event RoutedEventHandler? Indeterminate;

        public override void PerformClick()
        {
            IsChecked = IsChecked != true;
            base.PerformClick();
        }
    }

    public enum PlacementMode
    {
        Bottom,
        Top,
        Left,
        Right,
        Mouse,
        Center,
    }

    public class Popup : Control
    {
        public bool IsOpen { get; set; }

        public UIElement? Child { get; set; }

        public PlacementMode Placement { get; set; }
    }
}

namespace System.Windows.Documents
{
    public class Inline
    {
        public string Text { get; set; } = string.Empty;

        public override string ToString() => Text;
    }

    public class Run : Inline
    {
        public Run()
        {
        }

        public Run(string text)
        {
            Text = text;
        }

        public Media.Brush? Foreground { get; set; }

        public Controls.FontWeight FontWeight { get; set; }
    }

    public class LineBreak : Inline
    {
        public LineBreak()
        {
            Text = "\n";
        }
    }

    public class Hyperlink : Inline
    {
        public Hyperlink()
        {
        }

        public Hyperlink(Inline childInline)
        {
            Text = childInline.Text;
            Inlines.Add(childInline);
        }

        public Uri? NavigateUri { get; set; }

        public InlineCollection Inlines { get; } = new();

        public event RoutedEventHandler? Click;

        public event Navigation.RequestNavigateEventHandler? RequestNavigate;
    }

    public class InlineCollection : Collection<Inline>
    {
        public void Add(string text) => Add(new Run(text));
    }

    public class Block
    {
    }

    public class Paragraph : Block
    {
        public Paragraph()
        {
        }

        public Paragraph(Inline inline)
        {
            Inlines.Add(inline);
        }

        public InlineCollection Inlines { get; } = new();
    }

    public class BlockCollection : Collection<Block>
    {
    }

    public class FlowDocument
    {
        public BlockCollection Blocks { get; } = new();
    }
}

namespace System.Windows.Navigation
{
    public class RequestNavigateEventArgs : RoutedEventArgs
    {
        public RequestNavigateEventArgs(Uri uri, string target)
        {
            Uri = uri;
            Target = target;
        }

        public Uri Uri { get; }

        public string Target { get; }
    }

    public delegate void RequestNavigateEventHandler(object sender, RequestNavigateEventArgs e);
}

namespace System.Windows.Data
{
    public interface IValueConverter
    {
        object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture);

        object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture);
    }

    public interface IMultiValueConverter
    {
        object? Convert(object?[] values, Type targetType, object? parameter, System.Globalization.CultureInfo culture);

        object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, System.Globalization.CultureInfo culture);
    }

    public sealed class ValueConversionAttribute : Attribute
    {
        public ValueConversionAttribute(Type sourceType, Type targetType)
        {
        }
    }

    public enum BindingMode
    {
        TwoWay,
        OneWay,
        OneTime,
        OneWayToSource,
        Default,
    }

    public enum UpdateSourceTrigger
    {
        Default,
        PropertyChanged,
        LostFocus,
        Explicit,
    }

    public abstract class BindingBase
    {
    }

    public class Binding : BindingBase
    {
        public Binding()
        {
        }

        public Binding(string path)
        {
            Path = new PropertyPath(path);
        }

        public PropertyPath? Path { get; set; }

        public object? Source { get; set; }

        public BindingMode Mode { get; set; }

        public UpdateSourceTrigger UpdateSourceTrigger { get; set; }

        public IValueConverter? Converter { get; set; }

        public object? ConverterParameter { get; set; }
    }

    /// <summary>
    /// 简化的数据绑定：控件属性与数据源（字典或普通对象）之间双向同步
    /// </summary>
    public static class BindingOperations
    {
        public static void EnableCollectionSynchronization(IEnumerable collection, object lockObject)
        {
        }

        public static void SetBinding(DependencyObject target, DependencyProperty dp, BindingBase bindingBase)
        {
            if (bindingBase is not Binding binding)
            {
                return;
            }

            var source = binding.Source ?? (target as FrameworkElement)?.DataContext;
            var path = binding.Path?.Path;
            if (source == null || string.IsNullOrEmpty(path))
            {
                return;
            }

            var targetProperty = target.GetType().GetProperty(dp.Name, BindingFlags.Public | BindingFlags.Instance);
            if (targetProperty == null)
            {
                return;
            }

            var culture = System.Globalization.CultureInfo.CurrentCulture;
            var updating = false;

            void SourceToTarget()
            {
                if (!TryGetSourceValue(source, path, out var value))
                {
                    return;
                }

                if (binding.Converter != null)
                {
                    value = binding.Converter.Convert(value, targetProperty.PropertyType, binding.ConverterParameter, culture);
                }

                updating = true;
                try
                {
                    targetProperty.SetValue(target, CoerceValue(value, targetProperty.PropertyType));
                }
                catch
                {
                    // 类型不匹配时保持控件默认值
                }
                finally
                {
                    updating = false;
                }
            }

            if (binding.Mode != BindingMode.OneWayToSource)
            {
                SourceToTarget();
            }

            if (binding.Mode is BindingMode.TwoWay or BindingMode.Default or BindingMode.OneWayToSource && target is INotifyPropertyChanged targetNotify)
            {
                targetNotify.PropertyChanged += (_, e) =>
                {
                    if (updating || e.PropertyName != dp.Name)
                    {
                        return;
                    }

                    var value = targetProperty.GetValue(target);
                    if (binding.Converter != null)
                    {
                        value = binding.Converter.ConvertBack(value, typeof(object), binding.ConverterParameter, culture);
                    }

                    SetSourceValue(source, path, value);
                };
            }

            if (binding.Mode is BindingMode.TwoWay or BindingMode.Default or BindingMode.OneWay && source is INotifyPropertyChanged sourceNotify)
            {
                sourceNotify.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == path)
                    {
                        SourceToTarget();
                    }
                };
            }
        }

        private static object? CoerceValue(object? value, Type targetType)
        {
            if (value == null)
            {
                return null;
            }

            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (underlying.IsInstanceOfType(value))
            {
                return value;
            }

            if (underlying == typeof(string))
            {
                return value.ToString();
            }

            if (underlying == typeof(bool) && bool.TryParse(value.ToString(), out var b))
            {
                return b;
            }

            if (underlying == typeof(object))
            {
                return value;
            }

            return System.Convert.ChangeType(value, underlying, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool TryGetSourceValue(object source, string path, out object? value)
        {
            switch (source)
            {
                case IDictionary<string, object?> dict:
                    return dict.TryGetValue(path, out value);
                case IDictionary dict2:
                    value = dict2.Contains(path) ? dict2[path] : null;
                    return dict2.Contains(path);
            }

            var property = source.GetType().GetProperty(path);
            value = property?.GetValue(source);
            return property != null;
        }

        private static void SetSourceValue(object source, string path, object? value)
        {
            switch (source)
            {
                case IDictionary<string, object?> dict:
                    dict[path] = value;
                    return;
                case IDictionary dict2:
                    dict2[path] = value;
                    return;
            }

            var property = source.GetType().GetProperty(path);
            if (property?.CanWrite == true)
            {
                property.SetValue(source, CoerceValue(value, property.PropertyType));
            }
        }
    }

    /// <summary>
    /// 集合视图：保留过滤与排序能力，供 ViewModel 使用
    /// </summary>
    public class ListCollectionView : System.ComponentModel.ICollectionView
    {
        private readonly IEnumerable _source;

        public ListCollectionView(IList source)
        {
            _source = source;
            if (source is System.Collections.Specialized.INotifyCollectionChanged ncc)
            {
                ncc.CollectionChanged += (_, e) => CollectionChanged?.Invoke(this, e);
            }
        }

        public Predicate<object>? Filter { get; set; }

        public System.ComponentModel.SortDescriptionCollection SortDescriptions { get; } = new();

        public IEnumerable SourceCollection => _source;

        public bool CanFilter => true;

        public bool CanSort => true;

        public object? CurrentItem { get; private set; }

        public void Refresh() => CollectionChanged?.Invoke(this,
            new System.Collections.Specialized.NotifyCollectionChangedEventArgs(System.Collections.Specialized.NotifyCollectionChangedAction.Reset));

        public IDisposable DeferRefresh() => new Deferral(this);

        public bool MoveCurrentTo(object? item)
        {
            CurrentItem = item;
            return true;
        }

        public IEnumerator GetEnumerator()
        {
            IEnumerable<object> items = _source.Cast<object>();
            if (Filter != null)
            {
                items = items.Where(i => Filter(i));
            }

            foreach (var sort in SortDescriptions)
            {
                var name = sort.PropertyName;
                Func<object, object?> key = o => o.GetType().GetProperty(name)?.GetValue(o);
                items = sort.Direction == System.ComponentModel.ListSortDirection.Ascending ? items.OrderBy(key) : items.OrderByDescending(key);
            }

            return items.ToList().GetEnumerator();
        }

        public event System.Collections.Specialized.NotifyCollectionChangedEventHandler? CollectionChanged;

        private sealed class Deferral(ListCollectionView view) : IDisposable
        {
            public void Dispose() => view.Refresh();
        }
    }

    public static class CollectionViewSource
    {
        public static System.ComponentModel.ICollectionView GetDefaultView(object source) =>
            source is IList list ? new ListCollectionView(list) : new ListCollectionView(((IEnumerable)source).Cast<object>().ToList());
    }
}

namespace System.ComponentModel
{
    public interface ICollectionView : IEnumerable, System.Collections.Specialized.INotifyCollectionChanged
    {
        Predicate<object>? Filter { get; set; }

        SortDescriptionCollection SortDescriptions { get; }

        IEnumerable SourceCollection { get; }

        bool CanFilter { get; }

        bool CanSort { get; }

        object? CurrentItem { get; }

        void Refresh();

        IDisposable DeferRefresh();

        bool MoveCurrentTo(object? item);
    }

    public readonly struct SortDescription
    {
        public SortDescription(string propertyName, ListSortDirection direction)
        {
            PropertyName = propertyName;
            Direction = direction;
        }

        public string PropertyName { get; }

        public ListSortDirection Direction { get; }
    }

    public class SortDescriptionCollection : System.Collections.ObjectModel.Collection<SortDescription>
    {
    }

    /// <summary>
    /// WPF 的弱事件管理器，这里直接订阅（对象生命周期与应用一致）
    /// </summary>
    public static class PropertyChangedEventManager
    {
        public static void AddHandler(INotifyPropertyChanged source, EventHandler<PropertyChangedEventArgs> handler, string propertyName)
        {
            source.PropertyChanged += (s, e) =>
            {
                if (string.IsNullOrEmpty(propertyName) || e.PropertyName == propertyName)
                {
                    handler(s, e);
                }
            };
        }

        public static void RemoveHandler(INotifyPropertyChanged source, EventHandler<PropertyChangedEventArgs> handler, string propertyName)
        {
        }
    }
}

namespace System.Windows
{
    public class PropertyPath
    {
        public PropertyPath(string path)
        {
            Path = path;
        }

        public string Path { get; }
    }

    public static class FontWeights
    {
        public static Controls.FontWeight Normal => new(400);

        public static Controls.FontWeight Bold => new(700);

        public static Controls.FontWeight SemiBold => new(600);

        public static Controls.FontWeight Light => new(300);
    }

    public static class LogicalTreeHelper
    {
        public static DependencyObject? GetParent(DependencyObject current) => (current as UIElement)?.Parent;

        public static IEnumerable GetChildren(DependencyObject current) => current switch
        {
            Controls.Panel p => p.Children,
            Controls.ContentControl { Content: UIElement c } => new[] { c },
            Controls.Border { Child: { } c } => new[] { c },
            _ => Array.Empty<object>(),
        };
    }

    public static class VisualTreeHelper
    {
        public static DependencyObject? GetParent(DependencyObject reference) => LogicalTreeHelper.GetParent(reference);

        public static int GetChildrenCount(DependencyObject reference) => LogicalTreeHelper.GetChildren(reference).Cast<object>().Count();

        public static DependencyObject? GetChild(DependencyObject reference, int childIndex) =>
            LogicalTreeHelper.GetChildren(reference).Cast<object>().ElementAtOrDefault(childIndex) as DependencyObject;
    }
}

namespace System.Windows.Input
{
    public class KeyEventArgs : RoutedEventArgs
    {
        public KeyEventArgs(Key key)
        {
            Key = key;
        }

        public Key Key { get; }

        public bool IsDown => true;

        public bool IsRepeat { get; init; }
    }

    public delegate void KeyEventHandler(object sender, KeyEventArgs e);
}
