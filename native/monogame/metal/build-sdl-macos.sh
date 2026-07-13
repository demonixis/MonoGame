#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "$0")" && pwd)"
native_dir="$(cd "$script_dir/.." && pwd)"
source_dir="$native_dir/external/sdl2/sdl"
build_dir="$source_dir/build-metal"
configuration="${1:-Release}"

case "$configuration" in
  Debug|Release)
    ;;
  *)
    echo "Usage: $0 [Debug|Release]" >&2
    exit 2
    ;;
esac

cmake -S "$source_dir" -B "$build_dir" \
  -DCMAKE_BUILD_TYPE="$configuration" \
  -DCMAKE_OSX_ARCHITECTURES="arm64;x86_64" \
  -DCMAKE_OSX_DEPLOYMENT_TARGET=15.0 \
  -DSDL_SHARED=OFF \
  -DSDL_STATIC=ON \
  -DSDL_TEST=OFF \
  -DSDL_VULKAN=OFF

cmake --build "$build_dir" --config "$configuration" --target SDL2-static --parallel
