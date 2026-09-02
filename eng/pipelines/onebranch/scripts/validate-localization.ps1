<#
.SYNOPSIS
    Validates localized Strings.*.resx files against Strings.resx.
#>

# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$ResourcesDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ResourceStrings {
    param([Parameter(Mandatory)][string]$Path)

    $document = [System.Xml.Linq.XDocument]::Load($Path)
    $strings = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::Ordinal)
    foreach ($data in $document.Root.Elements('data')) {
        $name = $data.Attribute('name')
        $value = $data.Element('value')
        if ($null -eq $name -or $null -eq $value) {
            continue
        }

        if (-not $strings.TryAdd($name.Value, $value.Value)) {
            throw "Resource file '$Path' contains duplicate key '$($name.Value)'."
        }
    }

    return $strings
}

$resourcesPath = (Resolve-Path -LiteralPath $ResourcesDirectory).Path
$englishPath = Join-Path $resourcesPath 'Strings.resx'
if (-not (Test-Path -LiteralPath $englishPath -PathType Leaf)) {
    throw "English resource file '$englishPath' was not found."
}

$localizedFiles = @(Get-ChildItem -LiteralPath $resourcesPath -Filter 'Strings.*.resx' -File | Sort-Object Name)
if ($localizedFiles.Count -eq 0) {
    throw "No localized Strings.*.resx files were found in '$resourcesPath'."
}

$englishStrings = Get-ResourceStrings -Path $englishPath
$failures = [System.Collections.Generic.List[string]]::new()
foreach ($localizedFile in $localizedFiles) {
    $localizedStrings = Get-ResourceStrings -Path $localizedFile.FullName
    $missingKeys = [System.Collections.Generic.List[string]]::new()
    $englishMatches = [System.Collections.Generic.List[string]]::new()
    foreach ($entry in $englishStrings.GetEnumerator()) {
        if (-not $localizedStrings.ContainsKey($entry.Key)) {
            $missingKeys.Add($entry.Key)
        }
        elseif (-not [string]::IsNullOrEmpty($entry.Value) -and
            [System.StringComparer]::Ordinal.Equals($entry.Value, $localizedStrings[$entry.Key])) {
            $englishMatches.Add($entry.Key)
        }
    }

    if ($missingKeys.Count -gt 0) {
        $missingKeys.Sort([System.StringComparer]::Ordinal)
        $failures.Add("$($localizedFile.Name): missing keys: $($missingKeys -join ', ')")
    }
    if ($englishMatches.Count -gt 0) {
        $englishMatches.Sort([System.StringComparer]::Ordinal)
        $failures.Add("$($localizedFile.Name): values match English: $($englishMatches -join ', ')")
    }
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Host "##vso[task.logissue type=error]$failure"
    }
    throw "Localization validation failed:`n$($failures -join "`n")"
}

Write-Host "Validated $($localizedFiles.Count) localized resource files against $($englishStrings.Count) English strings."
