#!/bin/bash
set -euo pipefail
ORBIT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ORBIT_FIXTURE="$(mktemp -d "${TMPDIR:-/tmp}/orbit-auth-bridge.XXXXXX")"
trap 'rm -rf "$ORBIT_FIXTURE"' EXIT
ORBIT_TEST_APP="$ORBIT_FIXTURE/OrbitTests.app"
mkdir -p "$ORBIT_TEST_APP/Contents/MacOS" "$ORBIT_TEST_APP/Contents/Resources"
cat > "$ORBIT_TEST_APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>local.orbit.auth-bridge-tests</string>
<key>CFBundleExecutable</key><string>OrbitTests</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>LSUIElement</key><true/>
</dict></plist>
PLIST
cp "$ORBIT_ROOT"/Resources/* "$ORBIT_TEST_APP/Contents/Resources/"
xcrun swiftc -swift-version 5 -parse-as-library -module-cache-path "$ORBIT_FIXTURE/ModuleCache" \
  "$ORBIT_ROOT/Sources/Models.swift" "$ORBIT_ROOT/Sources/GoogleClient.swift" \
  "$ORBIT_ROOT/Sources/LocalProviders.swift" "$ORBIT_ROOT/Sources/Bridge.swift" \
  "$ORBIT_ROOT/tests/MacOSAuthBridgeTests.swift" \
  -o "$ORBIT_TEST_APP/Contents/MacOS/OrbitTests" \
  -framework AppKit -framework WebKit -framework ServiceManagement -lsqlite3
codesign --force --sign - --identifier local.orbit.auth-bridge-tests "$ORBIT_TEST_APP"
"$ORBIT_TEST_APP/Contents/MacOS/OrbitTests" "$ORBIT_ROOT/tests/ui-focus-fixture.js"
