# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Registers the public SqlClient feed for PowerShellGet and returns its repository name.
.DESCRIPTION
Uses anonymous access to the feed's NuGet v2 endpoint, including its PowerShell
Gallery upstream. An existing registration must match; it is never overwritten.
Registration and lookup failures propagate without falling back to another source.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryName = 'SqlClient'
$sourceLocation = 'https://sqlclientdrivers.pkgs.visualstudio.com/public/_packaging/sqlclient/nuget/v2'

$repository = Get-PSRepository -ErrorAction Stop | Where-Object { $_.Name -eq $repositoryName }
if ($repository) {
    if ($repository.SourceLocation.TrimEnd('/') -cne $sourceLocation -or
        $repository.PackageManagementProvider -ne 'NuGet') {
        throw "PowerShell repository '$repositoryName' does not point to '$sourceLocation' using NuGet. Resolve the conflicting registration before installing modules."
    }
}
else {
    Register-PSRepository -Name $repositoryName -SourceLocation $sourceLocation `
        -PackageManagementProvider NuGet -InstallationPolicy Trusted -ErrorAction Stop
}

$repositoryName
