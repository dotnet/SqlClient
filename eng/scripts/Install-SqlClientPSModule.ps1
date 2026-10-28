# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Installs a PowerShell module from the governed source in NuGet.config.
.DESCRIPTION
Resolves the repository name and uses it immediately for installation under the
current user. Registration and installation failures propagate without fallback.
Use SkipIfInstalled to reuse an available module, including the exact version when specified.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('\A[A-Za-z0-9][A-Za-z0-9_.-]*\z')]
    [string] $Name,

    [version] $RequiredVersion,

    [switch] $SkipIfInstalled,

    [switch] $AllowClobber
)

$ErrorActionPreference = 'Stop'
$module = @{ ModuleName = $Name }
if ($RequiredVersion) {
    $module.RequiredVersion = $RequiredVersion
}
else {
    $module.ModuleVersion = '0.0'
}
if ($SkipIfInstalled -and (Get-Module -ListAvailable -FullyQualifiedName $module)) {
    return
}

$repositoryName = & (Join-Path $PSScriptRoot 'Register-SqlClientPSRepository.ps1') -RepositoryName SqlClient
$install = @{
    Name = $Name
    Repository = $repositoryName
    Scope = 'CurrentUser'
    Force = $true
    AllowClobber = [bool] $AllowClobber
    ErrorAction = 'Stop'
}
if ($RequiredVersion) {
    $install.RequiredVersion = $RequiredVersion
}
Install-Module @install
