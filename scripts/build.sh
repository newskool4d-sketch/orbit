#!/bin/bash
set -euo pipefail
ORBIT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ORBIT_APP="$ORBIT_ROOT/build/Orbit.app"
mkdir -p "$ORBIT_APP/Contents/MacOS" "$ORBIT_APP/Contents/Resources"
xcrun swiftc -swift-version 5 -parse-as-library -O -target arm64-apple-macosx14.0 "$ORBIT_ROOT"/Sources/*.swift -o "$ORBIT_APP/Contents/MacOS/Orbit" -framework AppKit -framework WebKit -framework Network -framework Security -framework CryptoKit -framework ServiceManagement -lsqlite3
cp "$ORBIT_ROOT/Info.plist" "$ORBIT_APP/Contents/Info.plist"
cp "$ORBIT_ROOT"/Resources/* "$ORBIT_APP/Contents/Resources/"
codesign --force --sign - --identifier local.orbit.dashboard "$ORBIT_APP"
codesign --verify --strict "$ORBIT_APP"
printf 'Built: %s\n' "$ORBIT_APP"
