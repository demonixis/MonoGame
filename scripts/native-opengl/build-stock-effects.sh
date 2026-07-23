#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"
effects_dir="$repo_root/MonoGame.Framework/Platform/Graphics/Effect/Resources"
compiler="$repo_root/Tools/MonoGame.Effect.Compiler/MonoGame.Effect.Compiler.csproj"

case "$(uname -s)" in
    Darwin)
        system="macosx"
        architecture=""
        executable="mgfx-spvc"
        ;;
    Linux)
        system="linux"
        case "$(uname -m)" in
            aarch64|arm64) architecture="arm64" ;;
            *) architecture="x64" ;;
        esac
        executable="mgfx-spvc"
        ;;
    MINGW*|MSYS*|CYGWIN*)
        system="windows"
        architecture="x64"
        executable="mgfx-spvc.exe"
        ;;
    *)
        echo "Unsupported host for mgfx-spvc: $(uname -s)" >&2
        exit 1
        ;;
esac

if [[ -n "$architecture" ]]; then
    spvc="$repo_root/Artifacts/native/mgfx-spvc/$system/$architecture/Release/$executable"
else
    spvc="$repo_root/Artifacts/native/mgfx-spvc/$system/Release/$executable"
fi

if [[ ! -x "$spvc" ]]; then
    echo "mgfx-spvc is missing. Build native/pipeline with Premake before compiling stock effects." >&2
    exit 1
fi

dotnet build "$compiler" -c Release --nologo

effects=(
    AlphaTestEffect
    BasicEffect
    DualTextureEffect
    EnvironmentMapEffect
    SkinnedEffect
    SpriteEffect
)

targets=(
    "OpenGL_4_1:gl41:$repo_root/native/monogame/opengl/desktop"
    "OpenGLES_3_0:gles30:$repo_root/native/monogame/opengl/android"
    "OpenGLES_3_0:gles30:$repo_root/native/monogame/opengl/ios"
)

for target in "${targets[@]}"; do
    IFS=: read -r profile suffix output_dir <<< "$target"
    mkdir -p "$output_dir"
    for effect in "${effects[@]}"; do
        (
            cd "$effects_dir"
            MGFX_SPVC_PATH="$spvc" dotnet run --project "$compiler" -c Release --no-build -- \
            "$effect.fx" \
            "$output_dir/$effect.$suffix.mgfxo.h" \
            "/Profile:$profile"
        )
    done
done

validation_output="$repo_root/Artifacts/native/opengl-effects"
mkdir -p "$validation_output"
(
    cd "$repo_root/Tests/Assets/Effects"
    MGFX_SPVC_PATH="$spvc" dotnet run --project "$compiler" -c Release --no-build -- \
        "NativeOpenGLMrt.fx" \
        "$validation_output/NativeOpenGLMrt.mgfxo" \
        "/Profile:OpenGL_4_1"

    MGFX_SPVC_PATH="$spvc" dotnet run --project "$compiler" -c Release --no-build -- \
        "NativeOpenGLCube.fx" \
        "$validation_output/NativeOpenGLCube.mgfxo" \
        "/Profile:OpenGL_4_1"
)
