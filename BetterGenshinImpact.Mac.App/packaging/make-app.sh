#!/usr/bin/env bash
# 把 dotnet publish 的输出打包成 macOS 应用 BetterGI.app（在 Mac 上运行）。
#
# 用法：make-app.sh <publish 输出目录> <输出目录> [版本号]
# 输出：<输出目录>/BetterGI.app 和 <输出目录>/BetterGI-macOS.zip
set -euo pipefail

PUBLISH_DIR="$(cd "$1" && pwd)"
OUT_DIR="$(mkdir -p "$2" && cd "$2" && pwd)"
VERSION="${3:-0.0.0}"
HERE="$(cd "$(dirname "$0")" && pwd)"

APP="$OUT_DIR/BetterGI.app"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

# 程序文件（.NET 应用的所有文件都放在 MacOS 目录下）
cp -R "$PUBLISH_DIR/." "$APP/Contents/MacOS/"
chmod +x "$APP/Contents/MacOS/BetterGI"

# Info.plist
sed "s/__VERSION__/$VERSION/g" "$HERE/Info.plist" > "$APP/Contents/Info.plist"

# 图标：logo.png → BetterGI.icns
ICONSET="$OUT_DIR/BetterGI.iconset"
rm -rf "$ICONSET"
mkdir -p "$ICONSET"
LOGO="$HERE/../Assets/logo.png"
for size in 16 32 128 256 512; do
  sips -z $size $size "$LOGO" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
  double=$((size * 2))
  sips -z $double $double "$LOGO" --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/BetterGI.icns"
rm -rf "$ICONSET"

# 临时签名（ad-hoc）：Apple Silicon 上未签名的程序无法运行
xattr -cr "$APP"
codesign --force --deep --sign - --entitlements "$HERE/BetterGI.entitlements" "$APP"
codesign --verify --deep --strict "$APP"

# 压缩（ditto 会保留符号链接和扩展属性）
rm -f "$OUT_DIR/BetterGI-macOS.zip"
(cd "$OUT_DIR" && ditto -c -k --keepParent "BetterGI.app" "BetterGI-macOS.zip")
echo "已生成：$APP"
