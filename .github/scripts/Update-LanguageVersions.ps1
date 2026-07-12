param(
    [Parameter(Mandatory = $true)]
    [string] $BaseRef,

    [ValidateSet("Update", "Check")]
    [string] $Mode = "Update"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$languageRoot = Join-Path $repositoryRoot "Chummer/data/lang"
$manifestPath = Join-Path $repositoryRoot "Chummer/data/manifestlang.xml"
$versionPattern = '<version>-(\d+)</version>'

function Get-VersionNumber([string] $Content, [string] $Path) {
    $match = [regex]::Match($Content, $versionPattern)
    if (-not $match.Success) {
        throw "No language version found in $Path"
    }
    return [int] $match.Groups[1].Value
}

function Read-TextFile([string] $Path) {
    $reader = [System.IO.StreamReader]::new($Path, $true)
    try {
        $content = $reader.ReadToEnd()
        return @($content, $reader.CurrentEncoding)
    }
    finally {
        $reader.Dispose()
    }
}

function Write-TextFile([string] $Path, [string] $Content, [System.Text.Encoding] $Encoding) {
    [System.IO.File]::WriteAllText($Path, $Content, $Encoding)
}

Push-Location $repositoryRoot
try {
    git rev-parse --verify $BaseRef 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Base ref '$BaseRef' does not exist."
    }

    $changedFiles = @(git diff --name-only --diff-filter=ACMR "$BaseRef...HEAD" -- "Chummer/data/lang/*.xml")
    $changedFiles = @($changedFiles | Where-Object { $_ -and (Test-Path $_) })
    $errors = [System.Collections.Generic.List[string]]::new()

    foreach ($relativePath in $changedFiles) {
        $fileData = Read-TextFile (Join-Path $repositoryRoot $relativePath)
        $content = $fileData[0]
        if (-not [regex]::IsMatch($content, $versionPattern)) {
            continue
        }

        $currentVersion = Get-VersionNumber $content $relativePath
        $baseContent = git show "${BaseRef}:$relativePath" 2>$null | Out-String
        $baseVersion = if ($LASTEXITCODE -eq 0 -and [regex]::IsMatch($baseContent, $versionPattern)) {
            Get-VersionNumber $baseContent $relativePath
        } else {
            0
        }

        if ($currentVersion -le $baseVersion) {
            $requiredVersion = $baseVersion + 1
            if ($Mode -eq "Check") {
                $errors.Add("$relativePath must increase its version from -$baseVersion to at least -$requiredVersion.")
            } else {
                $updatedContent = [regex]::Replace($content, $versionPattern, "<version>-$requiredVersion</version>", 1)
                Write-TextFile (Join-Path $repositoryRoot $relativePath) $updatedContent $fileData[1]
                Write-Host "Updated $relativePath from -$currentVersion to -$requiredVersion"
            }
        } else {
            Write-Host "Kept manually incremented $relativePath at -$currentVersion"
        }
    }

    [xml] $manifest = Get-Content -Raw $manifestPath
    foreach ($file in Get-ChildItem $languageRoot -Filter "*.xml" | Sort-Object Name) {
        $fileData = Read-TextFile $file.FullName
        if (-not [regex]::IsMatch($fileData[0], $versionPattern)) {
            continue
        }

        $version = Get-VersionNumber $fileData[0] $file.FullName
        $manifestName = "lang/$($file.Name)"
        $entry = $manifest.SelectSingleNode("/manifest/file[name='$manifestName']")
        if ($null -eq $entry) {
            if ($Mode -eq "Check") {
                $errors.Add("$manifestName is missing from Chummer/data/manifestlang.xml.")
                continue
            }
            $entry = $manifest.CreateElement("file")
            foreach ($elementData in @(
                @("name", $manifestName),
                @("type", "Language File"),
                @("version", "-$version"),
                @("description", "$($file.BaseName) language file"),
                @("notes", "$($file.BaseName) language file")
            )) {
                $element = $manifest.CreateElement($elementData[0])
                $element.InnerText = $elementData[1]
                $entry.AppendChild($element) | Out-Null
            }
            $manifest.DocumentElement.AppendChild($entry) | Out-Null
            Write-Host "Added $manifestName to the language manifest"
        } elseif ($entry.SelectSingleNode("version").InnerText -ne "-$version") {
            if ($Mode -eq "Check") {
                $errors.Add("$manifestName has version $($entry.SelectSingleNode("version").InnerText) in the manifest, expected -$version.")
            } else {
                $entry.SelectSingleNode("version").InnerText = "-$version"
                Write-Host "Synchronized $manifestName in the language manifest"
            }
        }
    }

    if ($Mode -eq "Update") {
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
finally {
    Pop-Location
}
