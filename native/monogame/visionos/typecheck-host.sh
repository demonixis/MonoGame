#!/bin/bash

# MonoGame - Copyright (C) MonoGame Foundation, Inc
# This file is subject to the terms and conditions defined in
# file 'LICENSE.txt', which is part of this source code package.

set -euo pipefail

script_dir="$(cd "$(dirname "$0")" && pwd)"
repo_root="$(cd "$script_dir/../../.." && pwd)"
sdk="$(xcrun --sdk xros --show-sdk-path)"
module_cache="$repo_root/Artifacts/obj/visionoshost/ModuleCache"

mkdir -p "$module_cache"

xcrun --sdk xros swiftc \
    -target arm64-apple-xros2.0 \
    -sdk "$sdk" \
    -module-cache-path "$module_cache" \
    -parse-as-library \
    -typecheck \
    -import-objc-header "$script_dir/Host/MonoGameVisionOS-Bridging-Header.h" \
    "$script_dir/Host/MonoGameVisionOSApp.swift"

echo "VISIONOS_HOST_TYPECHECK pass"
