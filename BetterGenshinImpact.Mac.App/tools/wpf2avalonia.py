#!/usr/bin/env python3
"""
把原版 BetterGI 的 WPF 页面 XAML 转换为 Avalonia XAML。

原版页面绑定的都是 ViewModel 的属性和命令，macOS 版直接复用这些 ViewModel，
所以只需要把界面描述从 WPF / WPF-UI 语法转换到 Avalonia：
- WPF-UI 控件映射到 Avalonia 自带控件或 BetterGenshinImpact.MacApp.Controls 中的同名替代控件；
- {Binding ViewModel.X} 改为 {Binding X}（Avalonia 页面的 DataContext 直接是 ViewModel）；
- Visibility + BooleanToVisibilityConverter 改为 IsVisible；
- Microsoft.Xaml.Behaviors 的事件触发器改为 Avalonia.Xaml.Behaviors；
- 删除 WPF 专有的样式、模板触发器、设计时属性和代码隐藏事件。

用法：python3 wpf2avalonia.py <输出目录> <页面名=WPF文件路径>...
"""

import os
import re
import sys
import xml.etree.ElementTree as ET

WPF = "http://schemas.microsoft.com/winfx/2006/xaml/presentation"
X = "http://schemas.microsoft.com/winfx/2006/xaml"
UI = "http://schemas.lepo.co/wpfui/2022/xaml"
VIO = "http://schemas.lepo.co/wpfui/2022/xaml/violeta"
B = "http://schemas.microsoft.com/xaml/behaviors"
D = "http://schemas.microsoft.com/expression/blend/2008"
MC = "http://schemas.openxmlformats.org/markup-compatibility/2006"

# 不需要输出的命名空间（设计时、拖放、行为、WPF-UI 等）
DROP_NS_PREFIX_URIS = {D, MC}

# 全局资源中已经定义的键（App.axaml）
KNOWN_RESOURCES = {
    "BooleanToVisibilityConverter", "BooleanToVisibilityRevertConverter", "InverseBooleanConverter", "NotNullConverter",
    "BooleanToEnableTextConverter", "EnumToKVPConverter", "CultureInfoNameToKVPConverter", "StringToColorConverter",
    "AdaptiveUniformGridColumnsConverter", "DomainNameConverter", "AutoBossNameConverter", "MillisecondsToTimeConverter",
    "TextFillColorPrimaryBrush", "TextFillColorSecondaryBrush", "TextFillColorTertiaryBrush", "SystemAccentColorPrimaryBrush",
    "ApplicationBackgroundBrush", "TextThemeFontFamily", "PagePadding", "ControlStrokeColorDefaultBrush",
    "ControlFillColorSecondaryBrush", "AccentFillColorDefaultBrush", "CardBackgroundFillColorDefaultBrush",
    "ControlFillColorDefaultBrush", "SystemFillColorSuccessBrush", "ControlCornerRadius", "CardBorderBrush", "CardBackground",
    "SystemFillColorCautionBrush", "ControlStrongFillColorDisabledBrush", "CardStrokeColorDefaultBrush",
    "CardBackgroundFillColorSecondaryBrush", "TextOnAccentFillColorPrimaryBrush", "SystemFillColorCriticalBrush",
    "SystemAccentColorSecondary", "SystemAccentColorPrimary", "ControlStrongFillColorDefaultBrush", "ControlFillColorTertiaryBrush",
    "AccentFillColorDefault", "ImageSourceToBitmapConverter", "WpfBrushConverter",
}

# 直接删除的元素（WPF 专有或 macOS 不显示的图标类元素）
DROP_ELEMENTS = {
    (UI, "FontIcon"), (UI, "SymbolIcon"), (UI, "ImageIcon"), (UI, "InfoBadge"), (UI, "TitleBar"),
    (WPF, "Style"), (WPF, "ControlTemplate"), (WPF, "Trigger"), (WPF, "DataTrigger"), (WPF, "MultiDataTrigger"),
    (WPF, "EventSetter"), (WPF, "Setter"), (WPF, "Storyboard"), (WPF, "BeginStoryboard"), (WPF, "VisualStateManager.VisualStateGroups"),
    (WPF, "CollectionViewSource"),
}

# 删除的属性元素后缀（*.Style、*.Triggers 等）
DROP_PROPERTY_SUFFIXES = (".Style", ".Triggers", ".ItemContainerStyle", ".FocusVisualStyle", ".Template", ".Effect",
                          ".ContextMenu.Style", ".CacheMode", ".Icon", ".ToolTip", ".InputBindings", ".CommandBindings",
                          ".BitmapEffect", ".ItemContainerStyleSelector", ".ColumnHeaderContainerStyle")

DROP_ATTRS = {
    "WindowCornerPreference", "WindowBackdropType", "ExtendsContentIntoTitleBar", "WindowStartupLocation", "ResizeMode",
    "SizeToContent", "ShowInTaskbar", "Topmost", "AllowsTransparency", "WindowStyle", "PopupAnimation",
    "SnapsToDevicePixels", "UseLayoutRounding", "FocusVisualStyle", "OverridesDefaultStyle", "Style", "ItemContainerStyle",
    "Title", "ContentPadding", "Icon", "SpinButtonPlacementMode", "ValidationMode", "ClearButtonEnabled", "AcceptsExpression",
    "LargeChange", "Appearance", "TextTrimming", "IsMoveToPointEnabled", "IsSnapToTickEnabled", "ShowsPreview",
    "ScrollViewer.CanContentScroll", "ScrollViewer.PanningMode", "Grid.IsSharedSizeScope", "SharedSizeGroup",
    "VirtualizingPanel.IsVirtualizing", "VirtualizingPanel.VirtualizationMode", "VirtualizingStackPanel.IsVirtualizing",
    "VirtualizingStackPanel.VirtualizationMode", "VirtualizingPanel.ScrollUnit", "KeyboardNavigation.TabNavigation",
    "KeyboardNavigation.DirectionalNavigation", "KeyboardNavigation.IsTabStop", "TextOptions.TextFormattingMode",
    "TextOptions.TextRenderingMode", "RenderOptions.BitmapScalingMode", "ToolTipService.InitialShowDelay",
    "ToolTipService.ShowDuration", "ToolTipService.ShowOnDisabled", "ToolTipService.BetweenShowDelay", "ToolTipService.Placement",
    "InputMethod.IsInputMethodEnabled", "Validation.ErrorTemplate", "IsDeferredScrollingEnabled", "AlternationCount",
    "MaxDropDownHeight", "DisplayMemberPath", "SelectedValuePath", "IsTextSearchEnabled", "StaysOpenOnEdit",
    "HorizontalScrollBarVisibility", "VerticalScrollBarVisibility", "AllowDrop", "ContextMenuService.IsEnabled",
    "ContextMenuService.Placement", "SelectionUnit", "PasswordChar", "ui:Design.Background", "ui:Design.Foreground",
    "Panel.ZIndex", "Focusable", "IsHitTestVisible", "BorderBrush", "SmallChange", "MaxDecimalPlaces", "FontTypography",
    "WindowBackdropType", "ExtendsContentIntoTitleBar", "WindowStartupLocation", "ResizeMode", "SizeToContent",
    "ShowInTaskbar", "Topmost", "AllowsTransparency", "WindowStyle", "TabStripPlacement", "DisableIcon", "EnableIcon",
    "Severity", "Language", "xml:space", "HorizontalScrollBar", "IsSynchronizedWithCurrentItem", "OpenWidth", "OpenHeight",
    "DrawerBackground", "DrawerPosition", "CornerRadius", "ColumnDefinitions", "RowDefinitions", "TickPlacement",
}

EVENT_MAP = {
    "Loaded": "Loaded", "Unloaded": "Unloaded", "Click": "Click", "SelectionChanged": "SelectionChanged",
    "MouseDoubleClick": "DoubleTapped", "PreviewMouseDoubleClick": "DoubleTapped", "PreviewMouseLeftButtonDown": "PointerPressed",
    "MouseLeftButtonDown": "PointerPressed", "MouseLeftButtonUp": "PointerReleased", "PreviewMouseLeftButtonUp": "PointerReleased",
    "TextChanged": "TextChanged", "Checked": "IsCheckedChanged", "Unchecked": "IsCheckedChanged", "ValueChanged": "ValueChanged",
    "DropDownClosed": "DropDownClosed", "DropDownOpened": "DropDownOpened", "LostFocus": "LostFocus", "GotFocus": "GotFocus",
    "KeyDown": "KeyDown", "PreviewKeyDown": "KeyDown", "Expanded": "Expanded", "Collapsed": "Collapsed",
    "SelectedItemChanged": "SelectionChanged", "MouseEnter": "PointerEntered", "MouseLeave": "PointerExited",
}

warnings = []

# WPF-UI SymbolIcon → 文字图标（只用于没有文字的图标按钮）
SYMBOL_GLYPHS = {
    "Play": "▶", "Stop": "■", "Add": "＋", "Delete": "🗑", "Dismiss": "✕", "DismissCircle": "✕", "Settings": "⚙",
    "FolderOpen": "📂", "Folder": "📁", "FolderZip": "🗜", "Open": "↗", "LinkSquare": "↗", "Archive": "🗄",
    "ArrowDownload": "⤓", "ArrowSync": "⟳", "ArrowClockwise": "⟳", "ArrowCounterclockwise": "⟲", "ArrowReset": "⟲",
    "ArrowSwap": "⇄", "ArrowMinimize": "⤡", "ArrowBounce": "↩", "Edit": "✎", "Save": "💾", "Image": "🖼", "Camera": "📷",
    "Globe": "🌐", "Map": "🗺", "Keyboard": "⌨", "KeyboardMouse": "⌨", "Timer": "⏱", "Previous": "⏮", "Next": "⏭",
    "Pin": "📌", "PinOff": "📍", "Info": "ℹ", "Alert": "🔔", "Chat": "💬", "ChatMultiple": "💬", "Send": "➤",
    "MoreHorizontal": "⋯", "EyeOff": "🙈", "Cloud": "☁", "CloudSync": "☁", "CloudLink": "☁", "CloudArrowUp": "☁",
    "Record": "⏺", "Cursor": "🖱", "DesktopCursor": "🖱", "Bot": "🤖", "Script": "📜", "Notepad": "📝",
    "MusicNote2": "♪", "Search": "🔍", "Copy": "⧉", "Filter": "⏷",
}


def symbol_glyph(value):
    m = re.search(r"SymbolIcon\s+(?:Symbol=)?(\w+?)(\d+)?\}", value or "")
    if not m:
        return None
    return SYMBOL_GLYPHS.get(m.group(1), "•")

# 需要保留的代码隐藏事件（macOS 版在 Views/Generated/*.Mac.cs 中重新实现）
KEEP_HANDLERS = {
    "OneDragonFlowPage": {"AddTaskGroupButton_Click", "ConfirmButton_Click", "CancelButton_Click", "SereniteaPotTpType_Clicked"},
    "SkillCdConfigWindow": {"OnAddClick", "OnDeleteClick"},
}


def warn(msg):
    warnings.append(msg)


def qn(el):
    """返回 (命名空间, 本地名)"""
    m = re.match(r"\{(.*)\}(.*)", el.tag)
    return (m.group(1), m.group(2)) if m else ("", el.tag)


class Converter:
    def __init__(self, page_name, ns_map):
        self.page_name = page_name
        self.ns_map = ns_map  # uri -> prefix（原文件）
        self.used_clr = {}  # prefix -> using:命名空间

    # ---------------- 标记扩展与绑定 ----------------

    def convert_markup(self, value, attr_name=""):
        """转换属性值中的标记扩展，返回 None 表示删除该属性"""
        if not value.startswith("{") or value.startswith("{}"):
            return value
        inner = value[1:-1].strip()
        head = re.match(r"([\w:]+)", inner)
        if not head:
            return value
        kind = head.group(1)

        if kind in ("StaticResource", "DynamicResource"):
            key = inner[len(kind):].strip()
            if key in KNOWN_RESOURCES:
                return value
            warn(f"{self.page_name}: 删除未知资源 {key}（{attr_name}）")
            return None

        if kind.endswith(":SymbolIcon") or kind.endswith(":FontIcon"):
            return None

        if kind.endswith(":ThemeResource"):
            key = inner[len(kind):].strip()
            if key in KNOWN_RESOURCES:
                return "{DynamicResource " + key + "}"
            warn(f"{self.page_name}: 删除未知主题资源 {key}（{attr_name}）")
            return None

        if kind.endswith(":T"):
            # i18n:T 文本
            prefix = kind.split(":")[0]
            self.used_clr[prefix] = "using:BetterGenshinImpact.MacApp.Markup"
            return value

        if kind == "Binding" or kind == "TemplateBinding" or kind == "MultiBinding":
            if kind == "TemplateBinding":
                return None
            return self.convert_binding(inner[len(kind):].strip())

        if kind.startswith("x:"):
            # x:Static / x:Type / x:Null 中的命名空间前缀需要声明
            for p in re.findall(r"(\w+):", inner[2:]):
                if not self.note_prefix(p):
                    warn(f"{self.page_name}: 删除引用非 CLR 命名空间的 {kind}（{attr_name}）")
                    return None
            return value

        if ":" in kind:
            warn(f"{self.page_name}: 删除未知标记扩展 {kind}（{attr_name}）")
            return None
        return value

    def note_prefix(self, prefix):
        """声明前缀对应的 CLR 命名空间，返回 False 表示前缀不是 CLR 命名空间"""
        for uri, p in self.ns_map.items():
            if p == prefix and uri.startswith("clr-namespace:"):
                ns = uri[len("clr-namespace:"):].split(";")[0]
                self.used_clr[prefix] = "using:" + ns
                return True
            elif p == prefix and uri.startswith("using:"):
                self.used_clr[prefix] = uri
                return True
        return prefix in ("x", "sys")

    def convert_binding(self, args):
        parts = split_args(args)
        out = []
        path = None
        relative = None
        for i, part in enumerate(parts):
            if "=" not in part.split("{")[0]:
                if i == 0:
                    path = part.strip()
                    continue
            key, _, val = part.partition("=")
            key = key.strip()
            val = val.strip()
            if key == "Path":
                path = val
                continue
            if key == "RelativeSource":
                relative = val
                continue
            if key in ("NotifyOnSourceUpdated", "NotifyOnTargetUpdated", "ValidatesOnDataErrors", "ValidatesOnExceptions",
                       "IsAsync", "NotifyOnValidationError", "ValidatesOnNotifyDataErrors"):
                continue
            if key == "Converter":
                conv = self.convert_markup(val, "Converter")
                if conv is None:
                    continue
                val = conv
            if key == "Source":
                conv = self.convert_markup(val, "Source")
                if conv is None:
                    continue
                val = conv
            if key == "ElementName":
                out.append(f"ElementName={val}")
                continue
            out.append(f"{key}={val}")

        if path is not None:
            path = re.sub(r"^ViewModel\.", "", path)
            path = path.replace("DataContext.ViewModel.", "DataContext.")
            if path == "ViewModel":
                path = "."

        if relative is not None:
            m = re.search(r"AncestorType=\{x:Type\s+([\w:]+)\}|AncestorType=([\w:]+)", relative)
            if "Self" in relative:
                path = "$self." + (path or "")
                path = path.rstrip(".")
            elif m:
                ancestor = m.group(1) or m.group(2)
                local = ancestor.split(":")[-1]
                is_page = local in ("Page", "UserControl", "Window", "FluentWindow") or local.endswith("Page") or local.endswith("View")
                if is_page and local not in ("ListView", "TreeListView", "TreeView", "DataGridView"):
                    p = path or "DataContext"
                    if p.startswith("ViewModel."):
                        p = "DataContext." + p[len("ViewModel."):]
                    path = "#PageRoot." + p
                else:
                    local = {"ListView": "ListBox", "ListViewItem": "ListBoxItem", "TreeListView": "TreeView",
                             "CardExpander": "Expander"}.get(local, local)
                    path = f"$parent[{local}]." + (path or "DataContext")
            else:
                warn(f"{self.page_name}: 无法转换 RelativeSource {relative}")

        result = "{Binding"
        if path:
            result += " " + path
            if out:
                result += ","
        if out:
            result += " " + ", ".join(out)
        return result + "}"

    # ---------------- 元素 ----------------

    def convert_element(self, el, parent_out=None, root=False):
        ns, name = qn(el)
        full = name

        if ns == B:
            return self.convert_behavior(el)
        if (ns, name) in DROP_ELEMENTS:
            return None
        if "." in name and name.endswith(DROP_PROPERTY_SUFFIXES):
            return None
        if ns == VIO:
            if name == "CascadingComboBox":
                out = ET.Element("c:CascadingComboBox")
                for k, v in el.attrib.items():
                    self.convert_attr(out, "c:CascadingComboBox", k, v, ns, name)
                return out
            warn(f"{self.page_name}: 删除 Violeta 控件 {name}")
            return None
        if ns not in (WPF, UI, X) and not ns.startswith("clr-namespace") and not ns.startswith("using:"):
            if ns not in (D, MC):
                warn(f"{self.page_name}: 删除未知命名空间元素 {name}")
            return None

        if name in ("ListView", "TreeListView") and ns in (WPF, UI):
            table = self.convert_table(el, name)
            if table is not None:
                return table

        prefix = self.ns_map.get(ns, "")
        tag, attrs, classes = self.map_tag(ns, name, el)
        if tag is None:
            return None

        out = ET.Element(tag)
        for k, v in attrs.items():
            out.set(k, v)
        if classes:
            out.set("Classes", " ".join(classes))

        if root:
            out.set("x:Name", "PageRoot")

        # 属性
        for k, v in el.attrib.items():
            self.convert_attr(out, tag, k, v, ns, name)

        # Grid 简写
        if tag in ("Grid",) and out.get("Visibility"):
            pass

        # 子元素
        text = (el.text or "").strip()
        if text:
            out.text = el.text
        for child in el:
            converted = self.convert_child(child, out, tag)
            if converted is not None:
                if isinstance(converted, list):
                    for c in converted:
                        out.append(c)
                else:
                    out.append(converted)
        # 只有图标的按钮：用文字图标代替 SymbolIcon
        if tag in ("Button", "DropDownButton", "ToggleButton") and not out.get("Content") and len(out) == 0 and not (out.text or "").strip():
            glyph = symbol_glyph(el.get("Icon"))
            if glyph is None:
                for child in el:
                    if qn(child)[1].endswith(".Icon"):
                        for icon in child:
                            if qn(icon)[1] == "SymbolIcon":
                                glyph = SYMBOL_GLYPHS.get(re.sub(r"\d+$", "", icon.get("Symbol", "")), "•")
            if glyph is not None:
                out.set("Content", glyph)
                out.set("MinWidth", out.get("MinWidth") or "36")
        self.post_process(out, tag)
        return out

    def convert_table(self, el, name):
        """ListView + GridView / TreeListView：转换为带列模板的 ListBox / TreeView"""
        columns = [c for c in el.iter() if qn(c)[1] == "GridViewColumn"]
        if not columns:
            return None
        is_tree = name == "TreeListView"
        widths, headers, cells = [], [], []
        for col in columns:
            w = col.get("Width", "*")
            widths.append("*" if w.startswith("{") else w)
            header = col.get("Header", "")
            headers.append(header)
            cell = None
            dmb = col.get("DisplayMemberBinding")
            if dmb:
                cell = ET.Element("TextBlock")
                cv = self.convert_markup(dmb, "Text")
                if cv:
                    cell.set("Text", cv)
                cell.set("VerticalAlignment", "Center")
            else:
                for t in col.iter():
                    if qn(t)[1] == "DataTemplate":
                        inner = [c for c in t]
                        if inner:
                            cell = self.convert_element(inner[0])
                        break
            cells.append(cell)

        grid = ET.Element("Grid")
        grid.set("ColumnDefinitions", ",".join(widths))
        for i, cell in enumerate(cells):
            if cell is None:
                continue
            cell.set("Grid.Column", str(i))
            grid.append(cell)

        tag = "TreeView" if is_tree else "ListBox"
        out = ET.Element(tag)
        for k, v in el.attrib.items():
            local = re.sub(r"^\{.*\}", "", k)
            if local in ("View",):
                continue
            self.convert_attr(out, tag, k, v, qn(el)[0], name)

        if is_tree:
            items_source = "{Binding Children}"
            for t in el.iter():
                if qn(t)[1] == "HierarchicalDataTemplate" and t.get("ItemsSource"):
                    items_source = self.convert_markup(t.get("ItemsSource")) or items_source
            it = ET.Element("TreeView.ItemTemplate")
            tdt = ET.Element("TreeDataTemplate")
            tdt.set("ItemsSource", items_source)
            tdt.append(grid)
            it.append(tdt)
            out.append(it)
        else:
            it = ET.Element("ListBox.ItemTemplate")
            dt = ET.Element("DataTemplate")
            dt.append(grid)
            it.append(dt)
            out.append(it)

        for child in el:
            cns, cname = qn(child)
            if cname.endswith((".View", ".Columns", ".ItemTemplate")):
                continue
            converted = self.convert_child(child, out, tag)
            if converted is not None:
                out.append(converted)

        # 表头
        header_grid = ET.Element("Grid")
        header_grid.set("ColumnDefinitions", ",".join(widths))
        header_grid.set("Margin", "12,4")
        header_grid.set("DockPanel.Dock", "Top")
        for i, h in enumerate(headers):
            tb = ET.Element("TextBlock")
            cv = self.convert_markup(h, "Header") if h else ""
            tb.set("Text", cv or "")
            tb.set("Grid.Column", str(i))
            tb.set("Classes", "caption")
            header_grid.append(tb)
        dock = ET.Element("DockPanel")
        for attached in ("Grid.Row", "Grid.Column", "Grid.RowSpan", "Grid.ColumnSpan", "Margin"):
            if out.get(attached) is not None:
                dock.set(attached, out.get(attached))
                del out.attrib[attached]
        dock.append(header_grid)
        dock.append(out)
        return dock

    def convert_child(self, child, out, parent_tag):
        cns, cname = qn(child)
        # 属性元素：Owner.Property
        if "." in cname and cns in (WPF, UI, X) or ("." in cname and (cns.startswith("clr-namespace") or cns.startswith("using:"))):
            owner, prop = cname.split(".", 1)
            if prop == "Style" and owner != "ContentControl":
                self.convert_style_triggers(child, out)
                return None
            if prop == "Style" and owner == "ContentControl":
                # 原版有时把内容放在 Style 的 Content Setter 中（配合 DataTrigger 显示空状态），取出默认内容
                for setter in child.iter():
                    if qn(setter)[1] == "Setter" and setter.get("Property") == "Content":
                        for value in setter:
                            if qn(value)[1] == "Setter.Value":
                                for content in value:
                                    return self.convert_element(content)
                return None
            if cname.endswith(DROP_PROPERTY_SUFFIXES) or prop in ("Resources",) and owner in ("Page", "UserControl", "Window", "FluentWindow"):
                if prop == "Resources":
                    return self.convert_resources(child, parent_tag)
                return None
            if prop == "Resources":
                return self.convert_resources(child, parent_tag)
            if prop in ("Icon", "ToolTip") and owner.startswith(("Card", "Button", "MenuItem", "TextBox")):
                return None
            new_owner = parent_tag.split(":")[-1]
            pe = ET.Element(f"{parent_tag}.{prop}")
            if prop == "Flyout":
                # WPF-UI DropDownButton.Flyout 内是 ContextMenu
                for g in child:
                    gns, gname = qn(g)
                    if gname == "ContextMenu":
                        mf = ET.Element("MenuFlyout")
                        for item in g:
                            c = self.convert_element(item)
                            if c is not None:
                                mf.append(c)
                        pe.append(mf)
                    else:
                        c = self.convert_element(g)
                        if c is not None:
                            pe.append(c)
                return pe
            for g in child:
                c = self.convert_element(g)
                if c is not None:
                    pe.append(c)
            if len(pe) == 0 and not (child.text or "").strip():
                return None
            if (child.text or "").strip():
                pe.text = child.text
            return pe
        return self.convert_element(child)

    def convert_style_triggers(self, style_prop, out):
        """把局部 Style 中的 DataTrigger 转换为带 c:TriggerConverter 的绑定（只处理同一属性只依赖一个绑定的情况）"""
        style = next((e for e in style_prop if qn(e)[1] == "Style"), None)
        if style is None:
            return
        defaults = {e.get("Property"): e.get("Value") for e in style if qn(e)[1] == "Setter"}
        groups = {}
        for trig in style.iter():
            if qn(trig)[1] != "DataTrigger":
                continue
            for setter in trig:
                if qn(setter)[1] == "Setter" and setter.get("Value") is not None:
                    groups.setdefault(setter.get("Property"), []).append((trig.get("Binding"), trig.get("Value"), setter.get("Value")))
        def apply_default(prop):
            # 无法转换的触发器：至少保留 Style 中的默认值
            default = defaults.get(prop)
            if default is not None and prop not in out.attrib and prop not in ("Template", "Visibility"):
                cv = self.convert_markup(default, prop)
                if cv is not None:
                    out.set(prop, cv)

        for prop, items in groups.items():
            target = {"Visibility": "IsVisible", "Foreground": "Foreground", "Content": "Content", "Fill": "Fill"}.get(prop)
            bindings = {b for b, _, _ in items}
            results = {r for _, _, r in items}
            markup_values = any((v or "").startswith("{") for v in list(results) + [defaults.get(prop)]) and prop not in ("Foreground", "Fill", "Visibility")
            if target is None or len(bindings) != 1 or len(results) != 1 or target in out.attrib or markup_values:
                warn(f"{self.page_name}: 无法转换 DataTrigger（{prop}），使用默认值")
                apply_default(prop)
                continue
            binding = bindings.pop()
            result = results.pop()
            matches = []
            negate = False
            if "NotNullConverter" in binding:
                binding = re.sub(r",?\s*Converter=\{StaticResource NotNullConverter\}", "", binding)
                negate = None
            for _, value, _ in items:
                if negate is None:
                    matches.append("@notnull" if value == "True" else "@null")
                elif value == "{x:Null}":
                    matches.append("@null")
                else:
                    m = re.match(r"\{x:Static\s+[\w:]+\.(\w+)\}", value or "")
                    matches.append(m.group(1) if m else (value or ""))
            if "Converter=" in binding:
                warn(f"{self.page_name}: DataTrigger 绑定带转换器，无法转换（{prop}）")
                continue
            cv = self.convert_markup(binding, prop)
            if cv is None or not cv.startswith("{Binding"):
                warn(f"{self.page_name}: 无法转换 DataTrigger 绑定（{prop}）")
                continue

            def norm(v):
                if v is None:
                    return "@unset"
                if prop == "Visibility":
                    return "True" if v == "Visible" else "False"
                if v.startswith("{"):
                    return "@unset"
                return v
            param = ";".join(matches) + "|" + norm(result) + "|" + norm(defaults.get(prop))
            self.used_clr["c"] = "using:BetterGenshinImpact.MacApp.Controls"
            body = cv[len("{Binding"):-1].strip()
            sep = ", " if body else " "
            out.set(target, "{Binding" + (" " + body if body else "") + sep.rstrip() + " Converter={x:Static c:TriggerConverter.Instance}, ConverterParameter='" + param.replace("'", "") + "'}")

    def convert_resources(self, res_el, parent_tag):
        kept = []
        for r in res_el:
            rns, rname = qn(r)
            key = r.get(f"{{{X}}}Key")
            if rname in ("Style", "ControlTemplate") or rname.endswith("Converter") or rname == "CollectionViewSource":
                continue
            if rname == "ResourceDictionary":
                continue
            if rname in ("DataTemplate", "HierarchicalDataTemplate"):
                c = self.convert_element(r)
                if c is not None:
                    kept.append(c)
                continue
            if rns == WPF and rname in ("SolidColorBrush", "Thickness", "CornerRadius", "GridLength"):
                c = self.convert_element(r)
                if c is not None:
                    kept.append(c)
                    if key:
                        KNOWN_RESOURCES.add(key)
                continue
            if rns.startswith("clr-namespace:System") or rname in ("Double", "String", "Int32", "Boolean"):
                c = self.convert_element(r)
                if c is not None:
                    kept.append(c)
                    if key:
                        KNOWN_RESOURCES.add(key)
                continue
        if not kept:
            return None
        pe = ET.Element(f"{parent_tag}.Resources")
        for k in kept:
            pe.append(k)
            key = k.get("x:Key")
            if key:
                KNOWN_RESOURCES.add(key)
        return pe

    def convert_behavior(self, el):
        ns, name = qn(el)
        if name == "Interaction.Triggers":
            out = ET.Element("i:Interaction.Behaviors")
            for trig in el:
                tns, tname = qn(trig)
                if tname != "EventTrigger":
                    continue
                event = trig.get("EventName", "Loaded")
                if event in ("Checked", "Unchecked"):
                    # Avalonia 的 IsCheckedChanged 不区分勾选/取消，且原版常把触发器放在父面板上
                    for action in trig:
                        b = ET.Element("c:CheckedCommandBehavior")
                        b.set("When", "True" if event == "Checked" else "False")
                        for k in ("Command", "CommandParameter"):
                            if action.get(k) is not None:
                                cv = self.convert_markup(action.get(k), k)
                                if cv is not None:
                                    b.set(k, cv)
                        out.append(b)
                    continue
                if event in ("PreviewMouseRightButtonDown", "ContextMenuClosing"):
                    # 没有对应事件，使用 macOS 版自定义行为
                    for action in trig:
                        b = ET.Element("c:RightPressCommandBehavior" if event == "PreviewMouseRightButtonDown" else "c:ContextMenuClosedCommandBehavior")
                        for k in ("Command", "CommandParameter"):
                            if action.get(k) is not None:
                                cv = self.convert_markup(action.get(k), k)
                                if cv is not None:
                                    b.set(k, cv)
                        out.append(b)
                    continue
                if event in ("Opened", "Closing"):
                    # 抽屉的打开/关闭命令由 c:DrawerHost 调用，窗口的 ClosingCommand 由 FeatureWindows 调用
                    continue
                mapped = EVENT_MAP.get(event)
                if mapped is None:
                    warn(f"{self.page_name}: 未映射事件 {event}")
                    continue
                t = ET.Element("ia:EventTriggerBehavior")
                t.set("EventName", mapped)
                for action in trig:
                    ans, aname = qn(action)
                    if aname != "InvokeCommandAction":
                        continue
                    a = ET.Element("ia:InvokeCommandAction")
                    for k, v in action.attrib.items():
                        if k in ("Command", "CommandParameter"):
                            cv = self.convert_markup(v, k)
                            if cv is not None:
                                a.set(k, cv)
                        elif k == "PassEventArgsToCommand":
                            a.set("PassEventArgsToCommand", v)
                    t.append(a)
                if len(t):
                    out.append(t)
            return out if len(out) else None
        return None

    def map_tag(self, ns, name, el):
        attrs = {}
        classes = []
        if ns == UI:
            mapping = {
                "TextBlock": "TextBlock", "TextBox": "TextBox", "ToggleSwitch": "ToggleSwitch", "Button": "Button",
                "NumberBox": "NumericUpDown", "CardExpander": "Expander", "CardControl": "c:CardControl",
                "ProgressRing": "ProgressBar", "HyperlinkButton": "Button", "DropDownButton": "DropDownButton",
                "MenuItem": "MenuItem", "PasswordBox": "TextBox", "ListView": "ListBox", "Card": "Border", "Border": "Border",
                "Grid": "Grid", "StackPanel": "StackPanel", "TreeListView": "TreeView", "FluentWindow": "UserControl",
                "ListViewItem": "ListBoxItem", "Flyout": "Flyout", "ToggleButton": "ToggleButton", "Anchor": "Button",
                "AutoSuggestBox": "AutoCompleteBox", "Image": "Image", "Badge": "Border", "InfoBar": "Border",
                "TreeRowExpander": "ContentControl", "Hyperlink": "Button", "DynamicScrollViewer": "ScrollViewer",
                "PassiveScrollViewer": "ScrollViewer", "VirtualizingItemsControl": "ItemsControl", "ContentDialog": "Border",
                "Arc": None, "LoadingScreen": None, "Snackbar": None, "SnackbarPresenter": None, "ContentDialogHost": None,
            }
            if name not in mapping:
                warn(f"{self.page_name}: 未知 WPF-UI 控件 {name}")
                return None, {}, []
            tag = mapping[name]
            if tag is None:
                return None, {}, []
            if name == "CardExpander":
                classes.append("card")
            if name in ("Card",):
                classes.append("card")
            if name == "HyperlinkButton" or name == "Hyperlink":
                classes.append("link")
            if name == "ProgressRing":
                attrs["IsIndeterminate"] = "True"
            if name == "PasswordBox":
                attrs["PasswordChar"] = "•"
            if name == "Button" and el.get("Appearance") == "Primary":
                classes.append("accent")
            if name == "TextBlock":
                typo = el.get("FontTypography")
                if typo == "Caption":
                    classes.append("caption")
                elif typo == "BodyStrong":
                    attrs["FontWeight"] = "SemiBold"
                elif typo in ("Subtitle",):
                    classes.append("subtitle")
                elif typo in ("Title", "TitleLarge", "Display"):
                    classes.append("title")
            return tag, attrs, classes
        if ns == WPF or ns == "":
            mapping = {
                "Page": "UserControl", "Window": "UserControl", "ListView": "ListBox", "ListViewItem": "ListBoxItem",
                "GroupBox": "Expander", "Hyperlink": "Span", "HierarchicalDataTemplate": "TreeDataTemplate",
                "WrapPanel": "WrapPanel", "TextBlock": "TextBlock", "RichTextBox": "TextBox", "DocumentViewer": None,
                "FlowDocumentScrollViewer": None, "WebBrowser": None, "Frame": None, "StatusBar": "StackPanel",
                "StatusBarItem": "ContentControl", "ToolBar": "StackPanel", "ToolBarTray": "StackPanel",
                "Polyline": "Polyline",
            }
            tag = mapping.get(name, name)
            if tag is None:
                return None, {}, []
            if name == "GroupBox":
                attrs["IsExpanded"] = "True"
                classes.append("card")
            if name == "ToolBar":
                attrs["Orientation"] = "Horizontal"
            return tag, attrs, classes
        if ns == X:
            if name in ("Array", "String", "Double", "Int32", "Boolean", "Null", "Static", "Type"):
                return f"x:{name}", {}, []
            return None, {}, []
        # clr-namespace 控件
        prefix = self.ns_map.get(ns, "")
        clr = ns[len("clr-namespace:"):].split(";")[0] if ns.startswith("clr-namespace:") else ns[len("using:"):]
        local_controls = {"TwoStateButton": "c:TwoStateButton", "MultiSelectComboBox": "c:MultiSelectComboBox",
                          "CascadingComboBox": "c:CascadingComboBox", "HotKeyTextBox": "c:HotKeyTextBox",
                          "KeyBindingTextBox": "c:KeyBindingTextBox"}
        if name in local_controls:
            return local_controls[name], {}, []
        if name == "TextBlock" and "Emoji" in clr:
            return "TextBlock", {}, []
        if name == "CustomDrawer":
            return "c:DrawerHost", {}, []
        if name == "MarkdownView":
            return "c:MarkdownPresenter", {}, []
        if name == "JsonCodeBox":
            return "c:CodeTextBox", {}, []
        if clr == "BetterGenshinImpact.View.Pages.OneDragon":
            self.used_clr["gen"] = "using:BetterGenshinImpact.MacApp.Views.Generated"
            return f"gen:OneDragon{name}", {}, []
        if clr.startswith("System") and not clr.startswith("System.Windows"):
            self.used_clr[prefix] = "using:" + clr
            return f"{prefix}:{name}", {}, []
        if clr.startswith("BetterGenshinImpact.View") or clr.startswith("Wpf.Ui") or clr.startswith("GongSolutions"):
            warn(f"{self.page_name}: 删除原版视图控件 {prefix}:{name}")
            return None, {}, []
        # 数据类型（例如 DataTemplate 中引用的模型），保留并声明命名空间
        self.used_clr[prefix] = "using:" + clr
        return f"{prefix}:{name}", {}, []

    def convert_attr(self, out, tag, k, v, ns, name):
        local = re.sub(r"^\{.*\}", "", k)
        attr_ns = re.match(r"\{(.*)\}", k)
        attr_ns = attr_ns.group(1) if attr_ns else None

        if attr_ns == X:
            if local in ("Name", "Key"):
                out.set(f"x:{local}", v)
            elif local == "Class":
                pass
            elif local in ("Static", "Type"):
                pass
            return
        if attr_ns in (D, MC) or attr_ns == B:
            return
        if attr_ns is not None and attr_ns != WPF:
            p = self.ns_map.get(attr_ns, "")
            if attr_ns == UI or p in ("dd", "behavior", "behaviors", "b", "local", "controls", "vio", "drawer") or "Behavior" in local:
                return
            # 其他附加属性（例如 i18n）忽略
            return

        if local in DROP_ATTRS:
            if local == "Title" and tag not in ("UserControl",):
                pass
            if local == "DisplayMemberPath":
                out.set("DisplayMemberBinding", "{Binding " + v + "}")
            if local == "SelectedValuePath":
                out.set("SelectedValueBinding", "{Binding " + v + "}")
            if local in ("HorizontalScrollBarVisibility", "VerticalScrollBarVisibility") and tag in ("TextBox", "ListBox", "ScrollViewer", "TreeView", "DataGrid"):
                key = local if tag == "ScrollViewer" else f"ScrollViewer.{local}"
                out.set(key, v)
            if local == "CornerRadius" and tag in ("Border", "Button", "TextBox", "ToggleButton"):
                cv = self.convert_markup(v, local)
                if cv is not None:
                    out.set(local, cv)
            if local in ("ColumnDefinitions", "RowDefinitions") and tag == "Grid":
                out.set(local, v)
            if local == "BorderBrush" and tag in ("Border",):
                cv = self.convert_markup(v, local)
                if cv is not None:
                    out.set(local, cv)
            if local == "SmallChange" and tag == "NumericUpDown":
                out.set("Increment", v)
            if local == "MaxDecimalPlaces" and tag == "NumericUpDown":
                try:
                    places = int(v)
                    out.set("FormatString", "0" if places == 0 else "0." + "#" * places)
                except ValueError:
                    pass
            if local == "Panel.ZIndex":
                out.set("ZIndex", v)
            if local == "TickPlacement" and tag == "Slider":
                pass
            return

        if re.match(r"^[A-Z]\w*$", local) and v.startswith("{") is False and local in ("Click", "Loaded", "Unloaded", "Checked",
                                                                                       "Unchecked", "TextChanged", "SelectionChanged",
                                                                                       "MouseDoubleClick", "PreviewMouseDown",
                                                                                       "KeyDown", "PreviewKeyDown", "ValueChanged",
                                                                                       "MouseLeftButtonDown", "PreviewMouseLeftButtonDown",
                                                                                       "LostFocus", "GotFocus", "DragEnter", "Drop",
                                                                                       "SizeChanged", "Closing", "Closed", "MouseEnter",
                                                                                       "MouseLeave", "PreviewTextInput", "DropDownClosed",
                                                                                       "IsVisibleChanged", "ScrollChanged", "Expanded",
                                                                                       "Collapsed", "MouseRightButtonUp", "PreviewMouseWheel",
                                                                                       "RequestNavigate", "Navigated", "SelectedItemChanged",
                                                                                       "ContextMenuOpening"):
            if v in KEEP_HANDLERS.get(self.page_name, set()):
                out.set(local, v)
                return
            warn(f"{self.page_name}: 删除代码隐藏事件 {local}={v}")
            return

        if local == "Visibility":
            self.convert_visibility(out, v)
            return
        if local == "ToolTip":
            cv = self.convert_markup(v, local)
            if cv is not None:
                out.set("ToolTip.Tip", cv)
            return
        if local == "PlaceholderText":
            cv = self.convert_markup(v, local)
            if cv is not None:
                out.set("Watermark", cv)
            return
        if local == "Password" and tag == "TextBox":
            cv = self.convert_markup(v, local)
            if cv is not None:
                out.set("Text", cv)
            return
        if local == "Text" and tag == "NumericUpDown":
            cv = self.convert_markup(v, local)
            if cv is not None and not out.get("Value"):
                out.set("Value", cv)
            return
        if local == "TextWrapping" and v == "WrapWithOverflow":
            v = "Wrap"
        if local in ("Width", "Height", "MinWidth", "MaxWidth", "MinHeight", "MaxHeight") and v == "Auto":
            return
        if local == "IsExpanded" and tag == "Expander":
            pass
        if local == "Header" and tag == "Expander" and not v.startswith("{"):
            pass
        if local == "Source" and tag == "Image" and v.startswith("pack://"):
            fname = v.split("/")[-1]
            out.set("Source", f"avares://BetterGI/Assets/{fname}")
            return
        if local == "Cursor" and v not in ("Hand", "Arrow", "IBeam", "Wait", "Cross", "SizeAll", "No", "Help"):
            return
        if local == "Content" and tag in ("ToggleSwitch",) and v in ("",):
            return
        if local in ("OnContent", "OffContent") and tag != "ToggleSwitch":
            return
        if local == "Severity":
            return
        if local == "IsEditable" and tag == "ComboBox":
            return
        if local == "Spacing" and tag not in ("StackPanel",):
            return
        if tag == "Popup" and local == "StaysOpen":
            out.set("IsLightDismissEnabled", "True" if v == "False" else "False")
            return
        if tag == "Popup" and local == "Placement":
            v = {"Absolute": "Center", "AbsolutePoint": "Center", "Relative": "Bottom", "RelativePoint": "Bottom",
                 "Mouse": "Pointer", "MousePoint": "Pointer", "Custom": "Bottom"}.get(v, v)
        cv = self.convert_markup(v, local)
        if cv is None:
            return
        out.set(local, cv)

    def convert_visibility(self, out, v):
        if not v.startswith("{"):
            if v in ("Collapsed", "Hidden"):
                out.set("IsVisible", "False")
            return
        cv = self.convert_markup(v, "Visibility")
        if cv is None:
            return
        if "BooleanToVisibilityRevertConverter" in cv:
            cv = re.sub(r",?\s*Converter=\{StaticResource BooleanToVisibilityRevertConverter\}", "", cv)
            cv = cv.replace("{Binding ", "{Binding !", 1) if cv.startswith("{Binding ") and not cv.startswith("{Binding ,") else cv
        else:
            cv = re.sub(r",?\s*Converter=\{StaticResource BooleanToVisibilityConverter\}", "", cv)
        cv = cv.replace("{Binding ,", "{Binding")
        out.set("IsVisible", cv)

    def post_process(self, out, tag):
        # TextBlock 中的 Hyperlink（Command）→ 内嵌链接按钮
        if tag == "Span" and out.get("Command"):
            text = ET.Element("TextBlock")
            for child in list(out):
                out.remove(child)
                text.append(child)
            if out.text:
                text.text, out.text = out.text, None
            button = ET.Element("Button")
            button.set("Classes", "link")
            for k in ("Command", "CommandParameter", "Foreground", "ToolTip.Tip"):
                if out.get(k) is not None:
                    button.set(k, out.get(k))
            for k in list(out.attrib):
                del out.attrib[k]
            button.append(text)
            out.tag = "InlineUIContainer"
            out.append(button)
        # TextBox 的 AcceptsReturn 需要换行
        if tag == "Expander" and "card" in (out.get("Classes") or ""):
            out.set("HorizontalAlignment", "Stretch")
        if tag == "ListBox" and out.find("ListBox.View") is not None:
            pass
        if tag == "TreeDataTemplate":
            pass


def split_args(s):
    """按顶层逗号分割绑定参数"""
    parts, depth, cur = [], 0, ""
    for ch in s:
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
        if ch == "," and depth == 0:
            parts.append(cur)
            cur = ""
        else:
            cur += ch
    if cur.strip():
        parts.append(cur)
    return [p.strip() for p in parts]


def read_ns_map(path):
    ns = {}
    for event, (prefix, uri) in ET.iterparse(path, events=["start-ns"]):
        ns[uri] = prefix
    return ns


def serialize(el, indent=0):
    pad = "    " * indent
    attrs = ""
    for k, v in el.attrib.items():
        v = v.replace("&", "&amp;").replace('"', "&quot;").replace("<", "&lt;").replace(">", "&gt;")
        attrs += f'\n{pad}        {k}="{v}"' if len(el.attrib) > 3 else f' {k}="{v}"'
    children = list(el)
    text = (el.text or "").strip()
    if not children and not text:
        return f"{pad}<{el.tag}{attrs} />\n"
    s = f"{pad}<{el.tag}{attrs}>"
    if text and not children:
        t = text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
        return s + t + f"</{el.tag}>\n"
    s += "\n"
    if text:
        s += pad + "    " + text.replace("&", "&amp;").replace("<", "&lt;") + "\n"
    for c in children:
        s += serialize(c, indent + 1)
        tail = (c.tail or "").strip()
        if tail:
            s += pad + "    " + tail.replace("&", "&amp;").replace("<", "&lt;") + "\n"
    return s + f"{pad}</{el.tag}>\n"


def convert_file(page_name, wpf_path, out_dir, namespace):
    ns_map = read_ns_map(wpf_path)
    tree = ET.parse(wpf_path)
    conv = Converter(page_name, ns_map)
    root = conv.convert_element(tree.getroot(), root=True)
    root.tag = "UserControl"

    header = [
        'xmlns="https://github.com/avaloniaui"',
        'xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"',
        'xmlns:c="using:BetterGenshinImpact.MacApp.Controls"',
        'xmlns:i="using:Avalonia.Xaml.Interactivity"',
        'xmlns:ia="using:Avalonia.Xaml.Interactions.Core"',
    ]
    for prefix, uri in sorted(conv.used_clr.items()):
        if prefix in ("x", "c", "i", "ia", ""):
            continue
        header.append(f'xmlns:{prefix}="{uri}"')
    header.append(f'x:Class="{namespace}.{page_name}"')

    body = serialize(root)
    first_line_end = body.index(">")
    open_tag = body[:first_line_end]
    rest = body[first_line_end:]
    # 把命名空间声明插入根元素
    open_tag = open_tag.replace("<UserControl", "<UserControl\n    " + "\n    ".join(header), 1)
    content = "<!-- 由 tools/wpf2avalonia.py 从原版 WPF 页面生成，请勿手动修改；需要调整时修改转换脚本或在 Views/Pages 中另写页面 -->\n" + open_tag + rest

    with open(os.path.join(out_dir, f"{page_name}.axaml"), "w", encoding="utf-8") as f:
        f.write(content)
    with open(os.path.join(out_dir, f"{page_name}.axaml.cs"), "w", encoding="utf-8") as f:
        f.write(f"""using Avalonia.Controls;

namespace {namespace};

/// <summary>
/// 由原版 {os.path.basename(wpf_path)} 转换生成，DataContext 为原版 ViewModel
/// </summary>
public partial class {page_name} : UserControl
{{
    public {page_name}()
    {{
        InitializeComponent();
        OnPageCreated();
    }}

    /// <summary>
    /// macOS 版补充的初始化逻辑（例如原版代码隐藏中的事件处理），在 {page_name}.Mac.cs 中实现
    /// </summary>
    partial void OnPageCreated();
}}
""")


def main():
    out_dir = sys.argv[1]
    namespace = "BetterGenshinImpact.MacApp.Views.Generated"
    os.makedirs(out_dir, exist_ok=True)
    for arg in sys.argv[2:]:
        name, path = arg.split("=", 1)
        convert_file(name, path, out_dir, namespace)
    with open(os.path.join(out_dir, "conversion-warnings.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(sorted(set(warnings))) + "\n")
    print(f"converted {len(sys.argv) - 2} pages, {len(set(warnings))} warnings")


if __name__ == "__main__":
    main()
