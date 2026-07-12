param(
    [Parameter(Mandatory = $true)]
    [string] $BaseRef,

    [ValidateSet("Update", "Check")]
    [string] $Mode = "Update"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$dataRoot = Join-Path $repositoryRoot "Chummer/data/data"
$manifestPath = Join-Path $repositoryRoot "Chummer/data/manifestdata.xml"
$versionPattern = '<version>-(\d+)</version>'

function Get-VersionNumber([string] $Content, [string] $Path) {
    $match = [regex]::Match($Content, $versionPattern)
    if (-not $match.Success) { throw "No data version found in $Path" }
    return [int] $match.Groups[1].Value
}

function Read-TextFile([string] $Path) {
    $reader = [System.IO.StreamReader]::new($Path, $true)
    try { return @($reader.ReadToEnd(), $reader.CurrentEncoding) }
    finally { $reader.Dispose() }
}

Push-Location $repositoryRoot
try {
    git rev-parse --verify $BaseRef 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Base ref '$BaseRef' does not exist." }

    $errors = [System.Collections.Generic.List[string]]::new()
    $changedFiles = @(git diff --name-only --diff-filter=ACMR "$BaseRef...HEAD" -- "Chummer/data/data/*.xml")
    foreach ($relativePath in $changedFiles | Where-Object { $_ -and (Test-Path $_) }) {
        $fileData = Read-TextFile (Join-Path $repositoryRoot $relativePath)
        if (-not [regex]::IsMatch($fileData[0], $versionPattern)) { continue }
        $currentVersion = Get-VersionNumber $fileData[0] $relativePath
        $baseContent = git show "${BaseRef}:$relativePath" 2>$null | Out-String
        $baseVersion = if ($LASTEXITCODE -eq 0 -and [regex]::IsMatch($baseContent, $versionPattern)) { Get-VersionNumber $baseContent $relativePath } else { 0 }

        if ($currentVersion -le $baseVersion) {
            $requiredVersion = $baseVersion + 1
            if ($Mode -eq "Check") {
                $errors.Add("$relativePath must increase its version from -$baseVersion to at least -$requiredVersion.")
            } else {
                $updatedContent = [regex]::Replace($fileData[0], $versionPattern, "<version>-$requiredVersion</version>", 1)
                [System.IO.File]::WriteAllText((Join-Path $repositoryRoot $relativePath), $updatedContent, $fileData[1])
                Write-Host "Updated $relativePath from -$currentVersion to -$requiredVersion"
            }
        } else {
            Write-Host "Kept manually incremented $relativePath at -$currentVersion"
        }
    }

    [xml] $manifest = Get-Content -Raw $manifestPath
    $manifestChanged = $false
    foreach ($relativePath in $changedFiles | Where-Object { $_ -and (Test-Path $_) }) {
        $file = Get-Item (Join-Path $repositoryRoot $relativePath)
        $fileData = Read-TextFile $file.FullName
        if (-not [regex]::IsMatch($fileData[0], $versionPattern)) { continue }
        $version = Get-VersionNumber $fileData[0] $file.FullName
        $manifestName = "data/$($file.Name)"
        $entry = $manifest.SelectSingleNode("/manifest/file[name='$manifestName']")
        if ($null -eq $entry) {
            if ($Mode -eq "Check") { $errors.Add("$manifestName is missing from Chummer/data/manifestdata.xml.") }
            continue
        }
        if ($entry.SelectSingleNode("version").InnerText -ne "-$version") {
            if ($Mode -eq "Check") {
                $errors.Add("$manifestName has version $($entry.SelectSingleNode("version").InnerText) in the manifest, expected -$version.")
            } else {
                $entry.SelectSingleNode("version").InnerText = "-$version"
                $manifestChanged = $true
                Write-Host "Synchronized $manifestName in the data manifest"
            }
        }
    }

    if ($Mode -eq "Update" -and $manifestChanged) {
        $settings = [System.Xml.XmlWriterSettings]::new()
        $settings.Indent = $true
        $settings.IndentChars = "`t"
        $settings.NewLineChars = "`n"
        $settings.Encoding = [System.Text.UTF8Encoding]::new($false)
        $writer = [System.Xml.XmlWriter]::Create($manifestPath, $settings)
        try { $manifest.Save($writer) } finally { $writer.Dispose() }
    }

    if ($errors.Count -gt 0) {
        $errors | ForEach-Object { Write-Error $_ }
        exit 1
    }
}
finally { Pop-Location }
