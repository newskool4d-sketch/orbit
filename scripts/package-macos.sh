#!/bin/bash
set -euo pipefail

if [[ "$(uname -s)" != Darwin || "$(uname -m)" != arm64 ]]; then
  echo 'macOS packaging requires an Apple Silicon Mac.' >&2
  exit 1
fi

ORBIT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ORBIT_OUTPUT="$ORBIT_ROOT/build/releases"
ORBIT_TEMP="${RUNNER_TEMP:-${TMPDIR:-/tmp}}"
ORBIT_TEMP="$(cd "$ORBIT_TEMP" && pwd)"
ORBIT_STAGE="$(mktemp -d "$ORBIT_TEMP/Orbit-package.XXXXXX")"
ORBIT_MOUNT="$(mktemp -d "$ORBIT_TEMP/Orbit-mount.XXXXXX")"
ORBIT_MOUNTED=false
cleanup() {
  if $ORBIT_MOUNTED; then hdiutil detach "$ORBIT_MOUNT" >/dev/null || return 1; fi
  case "$ORBIT_STAGE" in "$ORBIT_TEMP"/Orbit-package.*) rm -rf "$ORBIT_STAGE";; esac
  case "$ORBIT_MOUNT" in "$ORBIT_TEMP"/Orbit-mount.*) rmdir "$ORBIT_MOUNT";; esac
}
trap cleanup EXIT

cd "$ORBIT_ROOT"
if [[ -n "$(git status --porcelain)" ]]; then
  echo 'Refusing to package an uncommitted working tree.' >&2
  exit 1
fi
export TZ=UTC NO_COLOR=1
mkdir -p "$ORBIT_OUTPUT"
ORBIT_COMMIT="$(git rev-parse HEAD)"
ORBIT_ID="Orbit.macOS-$(date -u +%Y%m%d-%H%M%S)"
ORBIT_DMG="$ORBIT_OUTPUT/$ORBIT_ID.dmg"
ORBIT_VERSION="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' Info.plist)"
ORBIT_BUILD="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleVersion' Info.plist)"
ORBIT_MINIMUM="$(/usr/libexec/PlistBuddy -c 'Print :LSMinimumSystemVersion' Info.plist)"
[[ "$ORBIT_MINIMUM" == 14.0 && ! -e "$ORBIT_DMG" ]]

bash scripts/build.sh 2>&1 | tee "$ORBIT_OUTPUT/build.log"
build/Orbit.app/Contents/MacOS/Orbit --self-test | tee "$ORBIT_OUTPUT/self-test.log"
node --test tests/web.test.cjs tests/platform.test.cjs tests/windows-ui.test.cjs tests/dday-integration.test.cjs | tee "$ORBIT_OUTPUT/web-tests.log"

cp -R build/Orbit.app "$ORBIT_STAGE/Orbit.app"
ln -s /Applications "$ORBIT_STAGE/Applications"
ORBIT_BINARY="$ORBIT_STAGE/Orbit.app/Contents/MacOS/Orbit"
[[ "$(lipo -archs "$ORBIT_BINARY")" == arm64 ]]
ORBIT_BINARY_MINIMUM="$(otool -l "$ORBIT_BINARY" | awk '$1 == "cmd" { section=$2 } section == "LC_BUILD_VERSION" && $1 == "minos" { print $2 }')"
[[ "$ORBIT_BINARY_MINIMUM" == "$ORBIT_MINIMUM" ]]

export ORBIT_ROOT ORBIT_OUTPUT ORBIT_COMMIT ORBIT_ID ORBIT_VERSION ORBIT_BUILD ORBIT_MINIMUM ORBIT_STAGE
python3 - <<'PY'
import datetime, hashlib, json, os, pathlib, re, subprocess
root = pathlib.Path(os.environ['ORBIT_ROOT'])
output = pathlib.Path(os.environ['ORBIT_OUTPUT'])
stage = pathlib.Path(os.environ['ORBIT_STAGE'])
native = (output / 'self-test.log').read_text()
web = (output / 'web-tests.log').read_text()
native_match = re.search(r'Orbit tests: (\d+) passed, 0 failed', native)
assert native_match and int(native_match[1]) >= 52, 'Mac regressions were not all executed'
web_count = re.search(r'(?:# |ℹ )?tests (\d+)', web)
assert web_count and int(web_count[1]) == 35, 'Shared web tests were not all executed'
tracked = subprocess.check_output(['git', 'ls-files', '-z'], cwd=root).decode().split('\0')
manifest = '\n'.join(f'{p} {hashlib.sha256((root / p).read_bytes()).hexdigest()}' for p in sorted(filter(None, tracked)))
info = {
    'packageId': os.environ['ORBIT_ID'], 'builtAtUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'baseCommit': os.environ['ORBIT_COMMIT'], 'workingTreeDirty': False,
    'sourceManifestSha256': hashlib.sha256(manifest.encode()).hexdigest(),
    'version': os.environ['ORBIT_VERSION'], 'bundleVersion': os.environ['ORBIT_BUILD'],
    'architecture': 'arm64', 'minimumMacOS': os.environ['ORBIT_MINIMUM'],
    'runnerMacOS': subprocess.check_output(['sw_vers', '-productVersion']).decode().strip(),
    'swiftVersion': subprocess.check_output(['xcrun', 'swiftc', '--version']).decode().strip(),
    'nativeSelfTestsPassed': int(native_match[1]), 'sharedWebTestsPassed': int(web_count[1]),
    'signing': 'ad-hoc', 'notarized': False, 'actualGuiRun': False
}
(stage / 'Orbit.app/Contents/Resources/BUILD-INFO.json').write_text(json.dumps(info, ensure_ascii=False, indent=2) + '\n')
(output / 'build-info.json').write_text(json.dumps(info, ensure_ascii=False, indent=2) + '\n')
(stage / 'README.txt').write_text(
    f'Orbit {info["version"]} · macOS preview\nCommit: {info["baseCommit"]}\n\n'
    'Apple Silicon Mac, macOS 14.0 or later. Intel Macs are not supported.\n'
    'Drag Orbit.app to Applications, then open it. This package does not install automatically.\n'
    'The app is ad-hoc signed and has not been notarized by Apple. Gatekeeper may block first launch.\n'
    'If blocked, review it in System Settings > Privacy & Security.\n'
    'Settings, D-day entries, and Keychain credentials are not included in this image.\n'
    'Native fixture tests passed; real Google accounts and the GUI were not exercised by this build.\n')
PY

codesign --force --sign - --identifier local.orbit.dashboard "$ORBIT_STAGE/Orbit.app"
codesign --verify --deep --strict "$ORBIT_STAGE/Orbit.app"
codesign --display --verbose=4 "$ORBIT_STAGE/Orbit.app" 2>"$ORBIT_OUTPUT/signature.log"
grep -q '^Signature=adhoc$' "$ORBIT_OUTPUT/signature.log"
if [[ -n "$(find "$ORBIT_STAGE" -type f \( -iname 'settings.json' -o -iname 'ddays.json' -o -iname '*.sqlite*' -o -iname '*.db' -o -iname '*.jsonl' -o -iname '*.log' -o -iname '.env*' -o -iname '*oauth*.json' -o -iname '*token*.json' -o -iname 'credentials*.json' -o -iname 'client_secret*.json' -o -iname '*.pem' -o -iname '*.key' -o -iname '*.p12' \) -print)" ]]; then
  echo 'Private or runtime data detected in package staging.' >&2
  exit 1
fi
hdiutil create -volname "Orbit $ORBIT_VERSION" -srcfolder "$ORBIT_STAGE" -fs HFS+ -format UDZO "$ORBIT_DMG"
hdiutil verify "$ORBIT_DMG"
hdiutil attach "$ORBIT_DMG" -readonly -nobrowse -mountpoint "$ORBIT_MOUNT" >"$ORBIT_OUTPUT/dmg-attach.log"
ORBIT_MOUNTED=true
[[ -L "$ORBIT_MOUNT/Applications" && "$(readlink "$ORBIT_MOUNT/Applications")" == /Applications ]]
[[ "$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$ORBIT_MOUNT/Orbit.app/Contents/Info.plist")" == local.orbit.dashboard ]]
[[ "$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$ORBIT_MOUNT/Orbit.app/Contents/Info.plist")" == "$ORBIT_VERSION" ]]
[[ "$(/usr/libexec/PlistBuddy -c 'Print :CFBundleVersion' "$ORBIT_MOUNT/Orbit.app/Contents/Info.plist")" == "$ORBIT_BUILD" ]]
[[ "$(/usr/libexec/PlistBuddy -c 'Print :LSMinimumSystemVersion' "$ORBIT_MOUNT/Orbit.app/Contents/Info.plist")" == "$ORBIT_MINIMUM" ]]
[[ "$(lipo -archs "$ORBIT_MOUNT/Orbit.app/Contents/MacOS/Orbit")" == arm64 ]]
[[ "$(shasum -a 256 "$ORBIT_BINARY" | cut -d ' ' -f 1)" == "$(shasum -a 256 "$ORBIT_MOUNT/Orbit.app/Contents/MacOS/Orbit" | cut -d ' ' -f 1)" ]]
codesign --verify --deep --strict "$ORBIT_MOUNT/Orbit.app"
"$ORBIT_MOUNT/Orbit.app/Contents/MacOS/Orbit" --self-test | tee "$ORBIT_OUTPUT/mounted-self-test.log"
export ORBIT_DMG ORBIT_BINARY
python3 - <<'PY'
import hashlib, json, os, pathlib, re
output = pathlib.Path(os.environ['ORBIT_OUTPUT'])
info = json.loads((output / 'build-info.json').read_text())
mounted = re.search(r'Orbit tests: (\d+) passed, 0 failed', (output / 'mounted-self-test.log').read_text())
assert mounted and int(mounted[1]) == info['nativeSelfTestsPassed']
info.update({
    'dmgIntegrityVerified': True, 'readOnlyMountVerified': True, 'appSignatureVerified': True,
    'mountedSelfTestsPassed': int(mounted[1]), 'privateFiles': 0,
    'binarySha256': hashlib.sha256(pathlib.Path(os.environ['ORBIT_BINARY']).read_bytes()).hexdigest(),
    'dmgSha256': hashlib.sha256(pathlib.Path(os.environ['ORBIT_DMG']).read_bytes()).hexdigest()
})
(output / (os.environ['ORBIT_ID'] + '.verification.json')).write_text(json.dumps(info, ensure_ascii=False, indent=2) + '\n')
PY
(cd "$ORBIT_OUTPUT" && shasum -a 256 "$ORBIT_ID.dmg" >"$ORBIT_ID.dmg.sha256" && shasum -a 256 -c "$ORBIT_ID.dmg.sha256")
printf 'DMG ready: %s\n' "$ORBIT_DMG"
if [[ -n "${GITHUB_OUTPUT:-}" ]]; then printf 'package_id=%s\n' "$ORBIT_ID" >>"$GITHUB_OUTPUT"; fi
