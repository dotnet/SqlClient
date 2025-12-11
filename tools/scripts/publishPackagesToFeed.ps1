# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

# Script: publishPackagesToFeed.ps1
# Date:   10-12-2025
# Comments: This script publishes packages to an internal Azure DevOps Feed.

param(
    [bool]$dryRun = $true,
    [string]$internalFeedSource,
    [string]$packagesGlob = "artifacts/packages/**/*.nupkg"
)

Function PublishToInternalFeed() {
    $SRC = $internalFeedSource
    
    if ([string]::IsNullOrEmpty($SRC)) {
        Write-Host "Internal feed source parameter not set." -ForegroundColor Red
        exit 1
    }
    
    Write-Host "[DRY RUN] Listing packages targeted for push to: $internalFeedSource" -ForegroundColor Cyan
    Write-Host "Using glob pattern: $packagesGlob" -ForegroundColor Cyan

    # Parse the glob pattern to extract directory and filename pattern
    $glob = $packagesGlob
    $lastSlashIndex = $glob.LastIndexOf('/')
    
    if ($lastSlashIndex -ge 0) {
        $dir = $glob.Substring(0, $lastSlashIndex)
        $namePattern = $glob.Substring($lastSlashIndex + 1)
    } else {
        $dir = "."
        $namePattern = $glob
    }
    
    # Handle ** wildcard for recursive search
    $recurse = $dir -like '*/**'
    if ($recurse) {
        $dir = $dir -replace '/?\*\*/?', ''
    }
    
    Write-Host "Resolved directory: $dir" -ForegroundColor Yellow
    Write-Host "Filename pattern: $namePattern" -ForegroundColor Yellow

    if (Test-Path $dir -PathType Container) {
        Write-Host "Matched files:" -ForegroundColor Green
        
        # Find matching .nupkg files
        $packages = Get-ChildItem -Path $dir -Filter "*.nupkg" -Recurse:$recurse -File -ErrorAction SilentlyContinue
        
        if ($packages) {
            foreach ($package in $packages) {
                Write-Host "  - $($package.FullName)" -ForegroundColor Gray
            }
            
            if (-not $dryRun) {
                Write-Host "`nPushing packages to feed..." -ForegroundColor Cyan
                foreach ($package in $packages) {
                    Write-Host "Pushing package: $($package.FullName)" -ForegroundColor Yellow
                    dotnet nuget push $package.FullName --source $SRC --api-key az
                    
                    if ($LASTEXITCODE -ne 0) {
                        Write-Host "Failed to push package: $($package.FullName)" -ForegroundColor Red
                    } else {
                        Write-Host "Successfully pushed: $($package.Name)" -ForegroundColor Green
                    }
                }
            } else {
                Write-Host "`n[DRY RUN] No packages were pushed. Set -dryRun `$false to push." -ForegroundColor Yellow
            }
        } else {
            Write-Host "No .nupkg files found matching the pattern." -ForegroundColor Yellow
        }
    } else {
        Write-Host "Directory does not exist: $dir" -ForegroundColor Red
    }
}

PublishToInternalFeed
