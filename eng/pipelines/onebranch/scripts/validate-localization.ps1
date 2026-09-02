<#
.SYNOPSIS
    Validates localized Strings.*.resx files against Strings.resx.

.PARAMETER ResourcesDirectory
    Directory containing the English and localized Strings.resx files.

.PARAMETER AllowlistPath
    Optional JSON file containing approved English-value matches grouped by localized filename.

.PARAMETER Enforce
    Whether validation findings fail the build. When false, findings are logged as warnings.
#>

# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$ResourcesDirectory,

    [string]$AllowlistPath,

    [bool]$Enforce = $true
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

        if ($strings.ContainsKey($name.Value)) {
            throw "Resource file '$Path' contains duplicate key '$($name.Value)'."
        }

        $strings.Add($name.Value, $value.Value)
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
$localizedFilesByName = [System.Collections.Generic.Dictionary[string, System.IO.FileInfo]]::new([System.StringComparer]::Ordinal)
foreach ($localizedFile in $localizedFiles) {
    $localizedFilesByName.Add($localizedFile.Name, $localizedFile)
}

$allowedEnglishMatches = [System.Collections.Generic.Dictionary[string, System.Collections.Generic.HashSet[string]]]::new([System.StringComparer]::Ordinal)
if (-not [string]::IsNullOrWhiteSpace($AllowlistPath)) {
    if (-not (Test-Path -LiteralPath $AllowlistPath -PathType Leaf)) {
        throw "Localization allowlist file '$AllowlistPath' was not found."
    }

    $configuration = Get-Content -LiteralPath $AllowlistPath -Raw | ConvertFrom-Json
    $englishValueMatchesProperty = $configuration.PSObject.Properties['AllowedEnglishValueMatches']
    if ($null -eq $englishValueMatchesProperty) {
        throw "Localization allowlist file '$AllowlistPath' must define 'AllowedEnglishValueMatches'."
    }

    foreach ($fileProperty in $englishValueMatchesProperty.Value.PSObject.Properties) {
        if (-not $localizedFilesByName.ContainsKey($fileProperty.Name)) {
            throw "Localization allowlist file references unknown resource file '$($fileProperty.Name)'."
        }

        $keys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($key in @($fileProperty.Value)) {
            if ([string]::IsNullOrWhiteSpace($key) -or -not $englishStrings.ContainsKey($key)) {
                throw "Localization allowlist file references unknown or empty resource key '$key' for '$($fileProperty.Name)'."
            }
            if ([string]::IsNullOrEmpty($englishStrings[$key])) {
                throw "Localization allowlist key '$key' for '$($fileProperty.Name)' does not have a non-empty English value."
            }
            if (-not $keys.Add($key)) {
                throw "Localization allowlist file contains duplicate key '$key' for '$($fileProperty.Name)'."
            }
        }
        $allowedEnglishMatches.Add($fileProperty.Name, $keys)
    }
}

$failures = [System.Collections.Generic.List[string]]::new()
$allowedMatchCount = 0
foreach ($localizedFile in $localizedFiles) {
    $localizedStrings = Get-ResourceStrings -Path $localizedFile.FullName
    $missingOrEmptyKeys = [System.Collections.Generic.List[string]]::new()
    $localizedOnlyKeys = [System.Collections.Generic.List[string]]::new()
    $englishMatches = [System.Collections.Generic.List[string]]::new()
    $staleAllowlistKeys = [System.Collections.Generic.List[string]]::new()

    foreach ($localizedKey in $localizedStrings.Keys) {
        if (-not $englishStrings.ContainsKey($localizedKey)) {
            $localizedOnlyKeys.Add($localizedKey)
        }
    }

    foreach ($entry in $englishStrings.GetEnumerator()) {
        if (-not $localizedStrings.ContainsKey($entry.Key) -or
            (-not [string]::IsNullOrEmpty($entry.Value) -and
                [string]::IsNullOrWhiteSpace($localizedStrings[$entry.Key]))) {
            $missingOrEmptyKeys.Add($entry.Key)
        }
        elseif (-not [string]::IsNullOrEmpty($entry.Value) -and
            [System.StringComparer]::Ordinal.Equals($entry.Value, $localizedStrings[$entry.Key])) {
            if ($allowedEnglishMatches.ContainsKey($localizedFile.Name) -and
                $allowedEnglishMatches[$localizedFile.Name].Contains($entry.Key)) {
                $allowedMatchCount++
            }
            else {
                $englishMatches.Add($entry.Key)
            }
        }
        elseif ($allowedEnglishMatches.ContainsKey($localizedFile.Name) -and
            $allowedEnglishMatches[$localizedFile.Name].Contains($entry.Key)) {
            $staleAllowlistKeys.Add($entry.Key)
        }
    }

    if ($missingOrEmptyKeys.Count -gt 0) {
        $missingOrEmptyKeys.Sort([System.StringComparer]::Ordinal)
        $failures.Add("$($localizedFile.Name): missing keys or empty values: $($missingOrEmptyKeys -join ', ')")
    }
    if ($localizedOnlyKeys.Count -gt 0) {
        $localizedOnlyKeys.Sort([System.StringComparer]::Ordinal)
        $failures.Add("$($localizedFile.Name): keys not found in Strings.resx: $($localizedOnlyKeys -join ', ')")
    }
    if ($englishMatches.Count -gt 0) {
        $englishMatches.Sort([System.StringComparer]::Ordinal)
        $failures.Add("$($localizedFile.Name): untranslated values match Strings.resx: $($englishMatches -join ', ')")
    }
    if ($staleAllowlistKeys.Count -gt 0) {
        $staleAllowlistKeys.Sort([System.StringComparer]::Ordinal)
        $failures.Add("$($localizedFile.Name): allowlist entries no longer match Strings.resx: $($staleAllowlistKeys -join ', ')")
    }
}

if ($failures.Count -gt 0) {
    $issueType = if ($Enforce) { 'error' } else { 'warning' }
    foreach ($failure in $failures) {
        Write-Host "##vso[task.logissue type=$issueType]$failure"
    }

    if ($Enforce) {
        $errorNoun = if ($failures.Count -eq 1) { 'error' } else { 'errors' }
        throw "Localization validation failed with $($failures.Count) $errorNoun. Review the preceding errors."
    }

    $issueNoun = if ($failures.Count -eq 1) { 'issue' } else { 'issues' }
    Write-Host "Localization validation found $($failures.Count) $issueNoun. Enforcement is disabled for this build; review the preceding warnings."
    return
}

$fileNoun = if ($localizedFiles.Count -eq 1) { 'file' } else { 'files' }
Write-Host "Localization validation passed for $($localizedFiles.Count) localized $fileNoun. Resource keys checked: $($englishStrings.Count); approved English-value matches allowlisted: $allowedMatchCount."
