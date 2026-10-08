# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

$ErrorActionPreference = 'Stop'
$targets = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '../build/Microsoft.Data.SqlClient.Extensions.Abstractions.targets'))
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ("sqlclient-version-tests-" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    $family = @('Microsoft.Data.SqlClient', 'Microsoft.Data.SqlClient.Extensions.Azure',
        'Microsoft.Data.SqlClient.Internal.Logging', 'Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider')
    foreach ($kind in @('PackageReference', 'packages.config')) {
        foreach ($package in $family) {
            foreach ($version in @('8.0.0', '7.1.1', '7.0.1', '1.0.0', '8.0.1', '8.0.0-preview1')) {
                $items = @(
                    @{ Id = 'Microsoft.Data.SqlClient.Extensions.Abstractions'; Version = '8.0.0' },
                    @{ Id = $package; Version = $version },
                    @{ Id = 'Microsoft.SqlServer.Server'; Version = '1.0.0' }
                )
                $assets = ''
                if ($kind -eq 'PackageReference') {
                    $assets = '<ItemGroup>' + (($items | ForEach-Object {
                        "<ResolvedCompileFileDefinitions Include='$($_.Id)'><NuGetPackageId>$($_.Id)</NuGetPackageId><NuGetPackageVersion>$($_.Version)</NuGetPackageVersion></ResolvedCompileFileDefinitions>"
                    }) -join '') + '</ItemGroup>'
                } else {
                    $xml = '<packages>' + (($items | ForEach-Object {
                        "<package id='$($_.Id)' version='$($_.Version)' />"
                    }) -join '') + '</packages>'
                    Set-Content -LiteralPath (Join-Path $temp 'packages.config') -Value $xml
                }
                $project = Join-Path $temp 'VersionTest.proj'
                Set-Content -LiteralPath $project -Value "<Project>$assets<Import Project='$targets' /></Project>"
                $result = & dotnet msbuild $project -t:ValidateSqlClientFamilyPackageVersions -v:minimal 2>&1
                $expectedFailure = $version -ne '8.0.0'
                if (($LASTEXITCODE -ne 0) -ne $expectedFailure -or
                    ($expectedFailure -and ($result -join "`n") -notmatch 'SQLCLIENT001')) {
                    throw "Unexpected $kind result for $package $version`n$($result -join "`n")"
                }
                if ($expectedFailure) {
                    & dotnet msbuild $project -t:ValidateSqlClientFamilyPackageVersions -p:SqlClientEnforceFamilyVersions=false -v:quiet
                    if ($LASTEXITCODE -ne 0) { throw 'Emergency opt-out failed.' }
                }
                $packagesConfig = Join-Path $temp 'packages.config'
                if (Test-Path -LiteralPath $packagesConfig) {
                    Remove-Item -LiteralPath $packagesConfig
                }
            }
        }
    }
    Write-Host 'PASS: exact family versions, packages.config, excluded SqlServer, and emergency opt-out.'
} finally {
    # Only remove the unique, fully resolved fixture directory created above.
    Remove-Item -LiteralPath $temp -Recurse -Force
}
