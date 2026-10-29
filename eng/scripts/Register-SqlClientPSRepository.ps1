# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Registers the public SqlClient feed for PowerShellGet and returns its repository name.
.DESCRIPTION
Reads the repository source from PowerShell.config and uses its NuGet v2 endpoint
anonymously, including its PowerShell Gallery upstream.
An existing registration must match; it is never overwritten.
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
if ($sources.Count -ne 1) {
    throw "Expected exactly one repository in '$ConfigPath'."
}
[uri] $source = $sources[0].GetAttribute('sourceLocation')
if (-not $source.IsAbsoluteUri -or $source.Scheme -ne 'https' -or
    $source.UserInfo -or $source.Query -or $source.Fragment -or
    $source.AbsolutePath -notmatch '/_packaging/[^/]+/nuget/v2/?$') {
    throw "The repository in '$ConfigPath' must have an HTTPS Azure Artifacts NuGet v2 sourceLocation."
}
$sourceLocation = $source.AbsoluteUri.TrimEnd('/')

$repository = Get-PSRepository -ErrorAction Stop | Where-Object { $_.Name -eq $RepositoryName }
if ($repository) {
    if ($repository.SourceLocation.TrimEnd('/') -cne $sourceLocation -or
        $repository.PackageManagementProvider -ne 'NuGet') {
        throw "PowerShell repository '$RepositoryName' does not point to '$sourceLocation' using NuGet. Resolve the conflicting registration before installing modules."
    }
}
else {
    $null = Register-PSRepository -Name $RepositoryName -SourceLocation $sourceLocation `
        -PackageManagementProvider NuGet -InstallationPolicy Trusted -ErrorAction Stop
}

$RepositoryName
