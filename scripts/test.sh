#!/bin/bash
set -euo pipefail
ORBIT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ORBIT_ROOT"
export TZ=UTC NO_COLOR=1

bash scripts/build.sh
build/Orbit.app/Contents/MacOS/Orbit --self-test
node --test tests/*.test.cjs
bash scripts/test-macos-auth-bridge.sh
printf 'All Orbit tests passed\n'
