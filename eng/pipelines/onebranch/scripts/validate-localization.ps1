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
            throw "Resource file '$Path' contains a <data> element without a name or value."
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
    $missingOrEmptyKeys = [System.Collections.Generic.List[string]]::new()
    $englishMatches = [System.Collections.Generic.List[string]]::new()
    foreach ($entry in $englishStrings.GetEnumerator()) {
        if (-not $localizedStrings.ContainsKey($entry.Key) -or
            (-not [string]::IsNullOrEmpty($entry.Value) -and
                [string]::IsNullOrWhiteSpace($localizedStrings[$entry.Key]))) {
            $missingOrEmptyKeys.Add($entry.Key)
        }
        elseif (-not [string]::IsNullOrEmpty($entry.Value) -and
            [System.StringComparer]::Ordinal.Equals($entry.Value, $localizedStrings[$entry.Key])) {
            $englishMatches.Add($entry.Key)
        }
    }

    if ($missingOrEmptyKeys.Count -gt 0) {
        $missingOrEmptyKeys.Sort([System.StringComparer]::Ordinal)
        $failures.Add("$($localizedFile.Name): missing keys or empty values: $($missingOrEmptyKeys -join ', ')")
    }
    if ($englishMatches.Count -gt 0) {
        $englishMatches.Sort([System.StringComparer]::Ordinal)
        $failures.Add("$($localizedFile.Name): untranslated values match Strings.resx: $($englishMatches -join ', ')")
    }
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Host "##vso[task.logissue type=error]$failure"
    }
    $errorNoun = if ($failures.Count -eq 1) { 'error' } else { 'errors' }
    throw "Localization validation failed with $($failures.Count) $errorNoun. Review the preceding errors."
}

$fileNoun = if ($localizedFiles.Count -eq 1) { 'file' } else { 'files' }
Write-Host "Localization validation passed for $($localizedFiles.Count) localized $fileNoun. Resource keys checked: $($englishStrings.Count); no non-empty values match Strings.resx."
