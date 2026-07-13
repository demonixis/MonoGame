#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"
effects_dir="$repo_root/MonoGame.Framework/Platform/Graphics/Effect/Resources"
output_dir="$repo_root/native/monogame/metal"
compiler="$repo_root/Tools/MonoGame.Effect.Compiler/MonoGame.Effect.Compiler.csproj"

# Resolve scripts/apple-metal/global.json regardless of the caller's working
# directory. The Apple lane intentionally has an SDK pin separate from the
# repository-wide build.
cd "$script_dir"

if ! command -v metal-shaderconverter >/dev/null 2>&1; then
    echo "metal-shaderconverter was not found on PATH." >&2
    echo "Install Apple Metal Shader Converter 4.0 before building Metal stock effects." >&2
    exit 1
fi

effects=(
    AlphaTestEffect
    BasicEffect
    DualTextureEffect
    EnvironmentMapEffect
    SkinnedEffect
    SpriteEffect
)

targets=(
    "MetalMacOS:macos"
    "MetaliOS:ios"
)

for target in "${targets[@]}"; do
    profile="${target%%:*}"
    target_dir="$output_dir/${target#*:}"
    mkdir -p "$target_dir"

    for effect in "${effects[@]}"; do
        dotnet run --project "$compiler" -- \
            "$effects_dir/$effect.fx" \
            "$target_dir/$effect.metal.mgfxo.h" \
            "/Profile:$profile"
    done
done
