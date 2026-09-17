#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
BUILD="$PWD/build"
APP="$BUILD/Prozesshandbuch.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources" "$BUILD/AppIcon.iconset"
SDK="$(xcrun --sdk macosx --show-sdk-path)"
for arch in arm64 x86_64; do
  xcrun swiftc -swift-version 5 -O -parse-as-library -sdk "$SDK" -target "$arch-apple-macosx13.0" \
    Sources/Core.swift Sources/App.swift Sources/Editor.swift \
    -o "$BUILD/Prozesshandbuch-$arch"
done
lipo -create "$BUILD/Prozesshandbuch-arm64" "$BUILD/Prozesshandbuch-x86_64" -output "$APP/Contents/MacOS/Prozesshandbuch"
xcrun swift MakeIcon.swift "$BUILD/AppIcon.iconset"
iconutil -c icns "$BUILD/AppIcon.iconset" -o "$APP/Contents/Resources/AppIcon.icns"
ditto "../Resources/Mieterwechsel" "$APP/Contents/Resources/Mieterwechsel"
cp Info.plist "$APP/Contents/Info.plist"
codesign --force --sign - "$APP"
codesign --verify --deep --strict "$APP"
xcrun swiftc -swift-version 5 -parse-as-library Sources/Core.swift Tests/StorageTests.swift -o "$BUILD/StorageTests"
"$BUILD/StorageTests"
mkdir -p "$BUILD/Paket"
ditto "$APP" "$BUILD/Paket/Prozesshandbuch.app"
cp LIESMICH-Mac.txt "$BUILD/Paket/"
ditto -c -k --sequesterRsrc --keepParent "$BUILD/Paket" "$BUILD/Prozesshandbuch-Mac.zip"
mkdir -p "$BUILD/Disk"
ditto "$APP" "$BUILD/Disk/Prozesshandbuch.app"
cp LIESMICH-Mac.txt "$BUILD/Disk/"
ln -sfn /Applications "$BUILD/Disk/Programme"
hdiutil create -volname Prozesshandbuch -srcfolder "$BUILD/Disk" -ov -format UDZO "$BUILD/Prozesshandbuch-Mac.dmg"
lipo -info "$APP/Contents/MacOS/Prozesshandbuch"
