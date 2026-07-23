#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "$0")" && pwd)"
native_dir="$(cd "$script_dir/.." && pwd)"
repo_dir="$(cd "$native_dir/../.." && pwd)"
configuration="${1:-Release}"
build_dir="$repo_dir/Artifacts/native/mgruntime/iosgles/$configuration"
xcframework="$build_dir/MGOpenGL.xcframework"

case "$configuration" in
  Debug) optimization=(-O0 -g -DDEBUG) ;;
  Release) optimization=(-O3 -DNDEBUG) ;;
  *) echo "Usage: $0 [Debug|Release]" >&2; exit 2 ;;
esac

compile_slice() {
  local name="$1"
  local sdk="$2"
  local target="$3"
  local slice_dir="$build_dir/$name"
  local sdk_root
  sdk_root="$(xcrun --sdk "$sdk" --show-sdk-path)"

  rm -rf "$slice_dir"
  mkdir -p "$slice_dir"

  local common_flags=(
    -std=c++17
    -fvisibility=hidden
    -fvisibility-inlines-hidden
    -target "$target"
    -isysroot "$sdk_root"
    -I "$native_dir/include"
    -I "$script_dir"
    -I "$repo_dir/external/stb"
    -DMG_IOS
    -DMG_OPENGL
    "${optimization[@]}"
  )

  xcrun --sdk "$sdk" clang++ "${common_flags[@]}" \
    -c "$script_dir/MGG_OpenGL.cpp" -o "$slice_dir/MGG_OpenGL.o"
  xcrun --sdk "$sdk" clang++ "${common_flags[@]}" -fobjc-arc \
    -c "$script_dir/MGGL_Host_iOS.mm" -o "$slice_dir/MGGL_Host_iOS.o"
  xcrun --sdk "$sdk" clang++ "${common_flags[@]}" \
    -c "$native_dir/common/MGI.cpp" -o "$slice_dir/MGI.o"
  xcrun libtool -static -o "$slice_dir/libmgruntime.a" \
    "$slice_dir/MGG_OpenGL.o" "$slice_dir/MGGL_Host_iOS.o" "$slice_dir/MGI.o"
}

compile_slice ios-arm64 iphoneos arm64-apple-ios12.0
compile_slice simulator-arm64 iphonesimulator arm64-apple-ios12.0-simulator
compile_slice simulator-x64 iphonesimulator x86_64-apple-ios12.0-simulator

xcrun lipo -create \
  "$build_dir/simulator-arm64/libmgruntime.a" \
  "$build_dir/simulator-x64/libmgruntime.a" \
  -output "$build_dir/libmgruntime-simulator.a"

rm -rf "$xcframework"
xcodebuild -create-xcframework \
  -library "$build_dir/ios-arm64/libmgruntime.a" \
  -library "$build_dir/libmgruntime-simulator.a" \
  -output "$xcframework"

echo "$xcframework"
