#!/usr/bin/env bash

set -euo pipefail

build_configuration="${1:-Release}"
android_ndk_version="28.2.13676358"
android_ndk_root="${MONOGAME_ANDROID_NDK_ROOT:-}"

if [[ -z "${android_ndk_root}" && -n "${ANDROID_SDK_ROOT:-}" ]]; then
    android_ndk_root="${ANDROID_SDK_ROOT}/ndk/${android_ndk_version}"
fi

if [[ -z "${android_ndk_root}" && -n "${ANDROID_HOME:-}" ]]; then
    android_ndk_root="${ANDROID_HOME}/ndk/${android_ndk_version}"
fi

if [[ ! -f "${android_ndk_root}/build/cmake/android.toolchain.cmake" ]]; then
    echo "Android NDK ${android_ndk_version} was not found. Set MONOGAME_ANDROID_NDK_ROOT to its installation directory." >&2
    exit 1
fi

script_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
build_directory="${script_directory}/../obj/android-vulkan/${build_configuration}"

cmake \
    -S "${script_directory}" \
    -B "${build_directory}" \
    -DCMAKE_TOOLCHAIN_FILE="${android_ndk_root}/build/cmake/android.toolchain.cmake" \
    -DANDROID_ABI=arm64-v8a \
    -DANDROID_PLATFORM=android-29 \
    -DANDROID_STL=c++_static \
    -DCMAKE_BUILD_TYPE="${build_configuration}"

cmake --build "${build_directory}" --config "${build_configuration}" --parallel
