#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"
configuration="${1:-Release}"
runtime_identifier="${2:-iossimulator-arm64}"
device_id="${3:-}"

cd "$script_dir"
dotnet build ../../Tests/AppleMetalSmoke.iOS/AppleMetalSmoke.iOS.csproj \
    -c "$configuration" \
    -r "$runtime_identifier"

app_path="$(find "$repo_root/Tests/Artifacts" -type d -path "*/$configuration/$runtime_identifier/AppleMetalSmoke.iOS.app" -print -quit)"
if [[ -z "$app_path" ]]; then
    echo "AppleMetalSmoke.iOS.app was not produced." >&2
    exit 1
fi

if [[ -z "$device_id" ]]; then
    device_id="$(xcrun simctl list devices available | \
        grep 'iPhone' | \
        sed -nE 's/.*\(([0-9A-F-]{36})\) \((Booted|Shutdown)\).*/\1/p' | \
        head -n 1)"
fi
if [[ -z "$device_id" ]]; then
    echo "No available iPhone simulator was found." >&2
    exit 1
fi

xcrun simctl boot "$device_id" 2>/dev/null || true
xcrun simctl bootstatus "$device_id" -b
xcrun simctl install "$device_id" "$app_path"

set +e
output="$(
    SIMCTL_CHILD_MTL_DEBUG_LAYER=1 \
    SIMCTL_CHILD_MTL_SHADER_VALIDATION=1 \
    xcrun simctl launch \
        --console \
        --terminate-running-process \
        "$device_id" \
        org.monogame.apple-metal-smoke-ios 2>&1
)"
launch_result=$?
set -e

echo "$output"
if [[ $launch_result -ne 0 ]] || \
   [[ "$output" != *"Apple Metal iOS smoke test presented 3 frames."* ]]; then
    echo "Apple Metal iOS simulator smoke test failed." >&2
    exit 1
fi
