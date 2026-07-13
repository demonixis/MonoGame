#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"
configuration="${1:-Release}"
runtime_identifier="${2:-osx-arm64}"
project="$repo_root/Tests/AppleMetalSmoke/AppleMetalSmoke.csproj"

# Resolve scripts/apple-metal/global.json regardless of the caller's working
# directory. The Apple lane intentionally has an SDK pin separate from the
# repository-wide build.
cd "$script_dir"

dotnet build "$project" \
    -c "$configuration" \
    -r "$runtime_identifier"

smoke_executable="$(find "$repo_root/Tests/Artifacts" \
    -type f \
    -path "*/$configuration/$runtime_identifier/AppleMetalSmoke.app/Contents/MacOS/AppleMetalSmoke" \
    -print \
    -quit)"

if [[ -z "$smoke_executable" ]]; then
    echo "The AppleMetalSmoke app executable was not produced." >&2
    exit 1
fi

"$smoke_executable"
