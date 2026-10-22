#!/usr/bin/env bash
# 从原版 WPF 页面重新生成 Avalonia 页面（原版页面更新后运行一次）
set -euo pipefail
cd "$(dirname "$0")/.."
SRC=../BetterGenshinImpact/View/Pages
python3 tools/wpf2avalonia.py Views/Generated \
  TriggerSettingsPage=$SRC/TriggerSettingsPage.xaml \
  TaskSettingsPage=$SRC/TaskSettingsPage.xaml \
  OneDragonFlowPage=$SRC/OneDragonFlowPage.xaml \
  ScriptControlPage=$SRC/ScriptControlPage.xaml \
  MapPathingPage=$SRC/MapPathingPage.xaml \
  JsListPage=$SRC/JsListPage.xaml \
  CommonSettingsPage=$SRC/CommonSettingsPage.xaml \
  HotKeyPage=$SRC/HotkeyPage.xaml \
  KeyBindingsSettingsPage=$SRC/KeyBindingsSettingsPage.xaml \
  NotificationSettingsPage=$SRC/NotificationSettingsPage.xaml \
  MacroSettingsPage=$SRC/MacroSettingsPage.xaml \
  KeyMouseRecordPage=$SRC/KeyMouseRecordPage.xaml \
  MusicPage=$SRC/MusicPage.xaml \
  ScriptGroupConfigView=$SRC/View/ScriptGroupConfigView.xaml \
  PathingConfigView=$SRC/View/PathingConfigView.xaml \
  $(for f in $SRC/OneDragon/*.xaml; do n=$(basename "$f" .xaml); echo "OneDragon$n=$f"; done)
