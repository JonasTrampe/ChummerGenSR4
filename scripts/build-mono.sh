#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
msbuild_path="${MSBUILD_PATH:-/usr/bin/msbuild}"

if [[ ! -x "$msbuild_path" ]]; then
    echo "Mono MSBuild was not found at: $msbuild_path" >&2
    echo "Install mono-msbuild or set MSBUILD_PATH to its executable path." >&2
    exit 1
fi

configuration="Debug"
if [[ $# -gt 0 ]]; then
    configuration="$1"
    shift
fi

# Mono's MSBuild may report MSBuildRuntimeType=Core. Pin the legacy target explicitly so a
# preceding dotnet restore (which writes net8 assets) can never poison this build.
"$msbuild_path" "$repo_root/Chummer.Core/Chummer.Core.csproj" /t:Restore /p:TargetFrameworks=net48
exec "$msbuild_path" "$repo_root/Chummer/Chummer.csproj" "/p:Configuration=$configuration" /p:TargetFrameworks=net48 "$@"
