#!/bin/bash
# Builds "Usage Widget.app" for one Mac architecture, signs it ad hoc and zips it into artifacts/release:
#   UsageWidget-<version>-macos-arm64.zip or UsageWidget-<version>-macos-x64.zip
# Runs on macOS only (codesign, sips, iconutil, ditto). Usage: tools/package-macos.sh arm64|x64
set -euo pipefail
arch="$1"
root="$(cd "$(dirname "$0")/.." && pwd)"
project="$root/src/UsageWidget/UsageWidget.csproj"
version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$project" | head -1)"
out="$root/artifacts/release"
work="$root/artifacts/macos-$arch"
app="$work/Usage Widget.app"

rm -rf "$work"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources" "$out"
dotnet publish "$project" -c Release -r "osx-$arch" --self-contained true -p:DebugType=none -o "$app/Contents/MacOS"

sed "s/@VERSION@/$version/g" "$root/packaging/macos/Info.plist" > "$app/Contents/Info.plist"

iconset="$work/AppIcon.iconset"
mkdir -p "$iconset"
for size in 16 32 128 256; do
    sips -z $size $size "$root/packaging/macos/icon-256.png" --out "$iconset/icon_${size}x${size}.png" >/dev/null
done
for size in 16 32 128; do
    sips -z $((size * 2)) $((size * 2)) "$root/packaging/macos/icon-256.png" --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$iconset" -o "$app/Contents/Resources/AppIcon.icns"
rm -rf "$iconset"

# Apple Silicon refuses unsigned code; an ad-hoc signature is enough to run once the user allows the app.
codesign --force --deep --sign - "$app"
codesign --verify --deep --strict "$app"

zip="$out/UsageWidget-$version-macos-$arch.zip"
rm -f "$zip"
ditto -c -k --keepParent "$app" "$zip"
echo "$zip"
