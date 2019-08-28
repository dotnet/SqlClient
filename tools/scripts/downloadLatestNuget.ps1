# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.
# Script: downloadLatestNuget.ps1
# Author: Keerat Singh
# Date:   07-Dec-2018
# Comments: This script downloads the latest NuGet Binary.
#
param(
    [string]$nugetDestPath,
    [string]$nugetSrcPath="https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"
)
Function DownloadLatestNuget()
{
    if(!$nugetDestPath)
    {
        $nugetDestPath = (Get-location).ToString() +'\.nuget\'
    }
    if (!(Test-Path $nugetDestPath))
    {
        New-Item -ItemType Directory -Path $nugetDestPath
    }
    Write-Output "Source: $nugetSrcPath"
    Write-Output "Destination: $nugetDestPath"
    Start-BitsTransfer -Source $nugetSrcPath -Destination $nugetDestPath\nuget.exe
}
DownloadLatestNuget