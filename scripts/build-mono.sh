#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
msbuild_path="${MSBUILD_PATH:-dotnet msbuild}"

read -r -a msbuild_cmd <<< "$msbuild_path"
command -v "${msbuild_cmd[0]}" >/dev/null || { echo "Build tool not found: $msbuild_path" >&2; exit 1; }

configuration="Debug"
if [[ $# -gt 0 ]]; then
    configuration="$1"
    shift
fi

# Mono's MSBuild may report MSBuildRuntimeType=Core. Pin the legacy target explicitly so a
# preceding dotnet restore (which writes net8 assets) can never poison this build.
"${msbuild_cmd[@]}" "$repo_root/Chummer.Core/Chummer.Core.csproj" /t:Restore /p:ForceNet48=true /p:TargetFrameworks=net48
"${msbuild_cmd[@]}" "$repo_root/RunnersPoint.Api/RunnersPoint.Api.csproj" /t:Restore /p:TargetFrameworks=net48
exec "${msbuild_cmd[@]}" "$repo_root/Chummer/Chummer.csproj" "/p:Configuration=$configuration" /p:Platform=AnyCPU /p:ForceNet48=true /p:TargetFrameworks=net48 /p:FrameworkPathOverride=/usr/lib/mono/4.8-api "$@"
