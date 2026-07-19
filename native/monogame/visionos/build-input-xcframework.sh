#!/bin/bash

# MonoGame - Copyright (C) MonoGame Foundation, Inc
# This file is subject to the terms and conditions defined in
# file 'LICENSE.txt', which is part of this source code package.

set -euo pipefail

configuration="${1:-Release}"
script_dir="$(cd "$(dirname "$0")" && pwd)"
repo_root="$(cd "$script_dir/../../.." && pwd)"
artifact_root="$repo_root/Artifacts/native/mgruntime/visionosinput/$configuration"
object_root="$repo_root/Artifacts/obj/visionosinput/$configuration"
device_sdk="$(xcrun --sdk xros --show-sdk-path)"
simulator_sdk="$(xcrun --sdk xrsimulator --show-sdk-path)"

mkdir -p "$artifact_root" "$object_root/device" "$object_root/simulator-arm64" "$object_root/simulator-x64"

common_flags=(
    -std=c++17
    -fobjc-arc
    -fvisibility=hidden
    -I "$script_dir/include"
)

if [[ "$configuration" == "Release" ]]; then
    common_flags+=(-O2 -DNDEBUG)
else
    common_flags+=(-O0 -g -DDEBUG)
fi

xcrun --sdk xros clang++ "${common_flags[@]}" \
    -target arm64-apple-xros2.0 \
    -isysroot "$device_sdk" \
    -c "$script_dir/MGV_Input.mm" \
    -o "$object_root/device/MGV_Input.o"
xcrun --sdk xros libtool -static \
    -o "$object_root/device/libmgruntime-visionos-input.a" \
    "$object_root/device/MGV_Input.o"

xcrun --sdk xrsimulator clang++ "${common_flags[@]}" \
    -target arm64-apple-xros2.0-simulator \
    -isysroot "$simulator_sdk" \
    -c "$script_dir/MGV_Input.mm" \
    -o "$object_root/simulator-arm64/MGV_Input.o"
xcrun --sdk xrsimulator libtool -static \
    -o "$object_root/simulator-arm64/libmgruntime-visionos-input.a" \
    "$object_root/simulator-arm64/MGV_Input.o"

xcrun --sdk xrsimulator clang++ "${common_flags[@]}" \
    -target x86_64-apple-xros2.0-simulator \
    -isysroot "$simulator_sdk" \
    -c "$script_dir/MGV_Input.mm" \
    -o "$object_root/simulator-x64/MGV_Input.o"
xcrun --sdk xrsimulator libtool -static \
    -o "$object_root/simulator-x64/libmgruntime-visionos-input.a" \
    "$object_root/simulator-x64/MGV_Input.o"

lipo -create \
    "$object_root/simulator-arm64/libmgruntime-visionos-input.a" \
    "$object_root/simulator-x64/libmgruntime-visionos-input.a" \
    -output "$object_root/libmgruntime-visionos-input-simulator.a"

rm -rf "$artifact_root/MGVisionOSInput.xcframework"
xcodebuild -create-xcframework \
    -library "$object_root/device/libmgruntime-visionos-input.a" \
    -headers "$script_dir/include" \
    -library "$object_root/libmgruntime-visionos-input-simulator.a" \
    -headers "$script_dir/include" \
    -output "$artifact_root/MGVisionOSInput.xcframework"

echo "VISIONOS_INPUT_XCFRAMEWORK pass $artifact_root/MGVisionOSInput.xcframework"
