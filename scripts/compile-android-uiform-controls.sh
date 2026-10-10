#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet_cmd="${DOTNET_ROOT:-}/dotnet"
if [[ ! -x "$dotnet_cmd" ]]; then dotnet_cmd="dotnet"; fi

output_dir="${1:-$repo_root/out/android-uiform-controls-api}"
"$dotnet_cmd" run --project "$repo_root/src/XPScript.Compiler/XPScript.Compiler.csproj" -c Release --no-build -- \
  "$repo_root/samples/android-uiform-controls-api.xps" \
  -o "$output_dir" --runtime=false --platform android-x64 --debug
