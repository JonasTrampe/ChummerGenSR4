<#
.SYNOPSIS
    Publishes Chummer.Avalonia into an isolated scratch folder and runs it from there.

.DESCRIPTION
    Running the app directly from .artifacts/bin (the normal dev build output) locks
    Chummer.Core.dll for as long as the process stays open, which then blocks any
    `dotnet build`/`dotnet test` run in the same repo until the app is closed. Publishing
    to a separate scratch copy first means the app can stay open indefinitely while
    development builds keep working normally.

.PARAMETER Configuration
    Build configuration to publish (default: Debug).
#>
param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$scratchDir = Join-Path $repoRoot ".build-scratch/avalonia-run"
$projectPath = Join-Path $repoRoot "Chummer.Avalonia/Chummer.Avalonia.csproj"

Write-Host "Publishing Chummer.Avalonia ($Configuration) to $scratchDir ..."
dotnet publish $projectPath -c $Configuration -o $scratchDir --no-self-contained

$exePath = Join-Path $scratchDir "Chummer.Avalonia.exe"
if (-not (Test-Path $exePath)) {
    Write-Error "Expected executable not found at $exePath"
}

Write-Host "Launching $exePath ..."
& $exePath
