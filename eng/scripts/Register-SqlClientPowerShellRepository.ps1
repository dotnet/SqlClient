# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Registers the configured feed for PSResourceGet and returns its repository name.
.DESCRIPTION
Reads the repository source from PowerShell.config and disables automatic credential providers.
An existing source must match; its trust policy is retained.
Registration and lookup failures propagate without falling back to another source.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_.-]*$')]
    [string] $RepositoryName,

    [ValidateNotNullOrEmpty()]
    [string] $ConfigPath = (Join-Path $PSScriptRoot '..\..\PowerShell.config')
)

$ErrorActionPreference = 'Stop'
[xml] $config = Get-Content -LiteralPath $ConfigPath -Raw -ErrorAction Stop
$sources = $config.SelectNodes('/configuration/repository')
if ($sources.Count -ne 1 -or
    [string]::IsNullOrWhiteSpace($sources[0].GetAttribute('sourceLocation'))) {
    throw "Expected exactly one repository with a sourceLocation in '$ConfigPath'."
}

$sourceLocation = $sources[0].GetAttribute('sourceLocation')
# Older PSResourceGet versions have no automatic credential provider or corresponding parameter.
$credentialParameters = @{}
if ((Get-Command Register-PSResourceRepository -ErrorAction Stop).Parameters.ContainsKey(
        'CredentialProvider')) {
    $credentialParameters.CredentialProvider = 'None'
}

$repository = Get-PSResourceRepository -ErrorAction Stop |
    Where-Object { $_.Name -eq $RepositoryName }
if ($repository) {
    if ($repository.Uri.AbsoluteUri.TrimEnd('/') -cne
        ([uri] $sourceLocation).AbsoluteUri.TrimEnd('/')) {
        throw "Resolve the conflicting registration for PowerShell repository '$RepositoryName'."
    }
    if ($credentialParameters.Count -ne 0 -and $repository.CredentialProvider -ne 'None') {
        $null = Set-PSResourceRepository -Name $RepositoryName @credentialParameters `
            -ErrorAction Stop
    }
}
else {
    $null = Register-PSResourceRepository -Name $RepositoryName -Uri $sourceLocation `
        @credentialParameters -Trusted -ErrorAction Stop
}

$RepositoryName
