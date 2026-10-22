<#
.SYNOPSIS
    Pester tests for restore-package-documentation.ps1.

.DESCRIPTION
    The script exists so the packaged XML documentation gate can resolve references into a package
    this run depends on but did not build. What matters is that it collects exactly that package's
    documentation, at exactly the requested version, and leaves nothing behind that a later run
    could mistake for the current one.

    dotnet is mocked throughout, so no network access or feed credentials are required. The mock
    writes the package layout NuGet would have produced, which is what the collection logic reads.
#>

BeforeAll {
    $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..' '..' '..' '..')).Path
    $script:scriptPath = Join-Path $script:repoRoot 'eng/pipelines/onebranch/scripts/restore-package-documentation.ps1'

    function New-TestDirectory {
        $path = Join-Path $TestDrive ([guid]::NewGuid().ToString('n'))
        New-Item -ItemType Directory -Path $path | Out-Null
        return $path
    }

    # Locates --packages in the argument list the script built, so the mock writes where the script
    # will look rather than where the test guessed it would.
    function Get-PackagesPath {
        param([Parameter(Mandatory)][string[]]$Arguments)

        $index = [array]::IndexOf($Arguments, '--packages')
        if ($index -lt 0) {
            throw 'The script did not pass --packages to dotnet.'
        }
        return $Arguments[$index + 1]
    }

    function New-RestoredPackage {
        param(
            [Parameter(Mandatory)][string]$PackagesPath,
            [Parameter(Mandatory)][string]$PackageId,
            [Parameter(Mandatory)][string]$Version,
            [string[]]$Frameworks = @('netstandard2.0', 'net46'),
            [switch]$WithoutDocumentation
        )

        # NuGet lowercases both identifier and version on disk.
        $root = Join-Path $PackagesPath ($PackageId.ToLowerInvariant() + '/' + $Version.ToLowerInvariant())
        foreach ($framework in $Frameworks) {
            $libPath = Join-Path $root "lib/$framework"
            New-Item -ItemType Directory -Path $libPath -Force | Out-Null
            Set-Content -LiteralPath (Join-Path $libPath "$PackageId.dll") -Value 'binary' -Encoding utf8
            if (-not $WithoutDocumentation) {
                Set-Content -LiteralPath (Join-Path $libPath "$PackageId.xml") `
                    -Value "<doc><members><member name=`"T:$PackageId.Sample`" /></members></doc>" `
                    -Encoding utf8
            }
        }
    }
}

Describe 'restore-package-documentation.ps1' {

    Context 'argument validation' {

        It 'rejects a version that is not exact' -ForEach @(
            @{ Version = '1.*' }
            @{ Version = '[1.0.0,2.0.0)' }
            @{ Version = '1.0.0,2.0.0' }
        ) {
            # A range would leave undetermined which version answered a reference, which is the one
            # thing this step exists to pin down.
            { & $script:scriptPath -PackageId 'Contoso.Widget' -Version $Version `
                    -DestinationPath (New-TestDirectory) } |
                Should -Throw '*is not an exact version*'
        }

        It 'rejects a NuGet configuration file that does not exist' {
            { & $script:scriptPath -PackageId 'Contoso.Widget' -Version '1.0.0' `
                    -DestinationPath (New-TestDirectory) `
                    -ConfigFile (Join-Path (New-TestDirectory) 'absent.config') } |
                Should -Throw '*was not found*'
        }
    }

    Context 'collection' {

        BeforeEach {
            Mock dotnet {
                $packagesPath = Get-PackagesPath -Arguments $args
                New-RestoredPackage -PackagesPath $packagesPath -PackageId 'Contoso.Widget' -Version '1.0.0'
                $global:LASTEXITCODE = 0
            }
        }

        It 'collects the documentation of every framework the package ships' {
            $destination = New-TestDirectory

            & $script:scriptPath -PackageId 'Contoso.Widget' -Version '1.0.0' -DestinationPath $destination

            Test-Path -LiteralPath (Join-Path $destination 'netstandard2.0/Contoso.Widget.xml') | Should -BeTrue
            Test-Path -LiteralPath (Join-Path $destination 'net46/Contoso.Widget.xml') | Should -BeTrue
        }

        It 'keeps each framework separate so identically named files cannot collide' {
            $destination = New-TestDirectory

            & $script:scriptPath -PackageId 'Contoso.Widget' -Version '1.0.0' -DestinationPath $destination

            @(Get-ChildItem -LiteralPath $destination -Recurse -File -Filter '*.xml').Count | Should -Be 2
        }

        It 'collects documentation only, not the assemblies beside it' {
            $destination = New-TestDirectory

            & $script:scriptPath -PackageId 'Contoso.Widget' -Version '1.0.0' -DestinationPath $destination

            @(Get-ChildItem -LiteralPath $destination -Recurse -File -Filter '*.dll') | Should -BeNullOrEmpty
        }

        It 'removes the restore tree, leaving only the collected documentation' {
            # The restore tree holds the dependencies too; leaving it would let the scan that
            # follows index members from packages this build does not depend on.
            $destination = New-TestDirectory

            & $script:scriptPath -PackageId 'Contoso.Widget' -Version '1.0.0' -DestinationPath $destination

            Test-Path -LiteralPath (Join-Path $destination '.restore') | Should -BeFalse
        }

        It 'replaces an existing destination rather than mixing versions into it' {
            $destination = New-TestDirectory
            New-Item -ItemType Directory -Path (Join-Path $destination 'netstandard2.0') -Force | Out-Null
            Set-Content -LiteralPath (Join-Path $destination 'netstandard2.0/Stale.xml') `
                -Value '<doc />' -Encoding utf8

            & $script:scriptPath -PackageId 'Contoso.Widget' -Version '1.0.0' -DestinationPath $destination

            Test-Path -LiteralPath (Join-Path $destination 'netstandard2.0/Stale.xml') | Should -BeFalse
        }

        It 'requests the exact version rather than a minimum' {
            # A bare version is a minimum in NuGet, which would silently resolve to a newer package
            # than the one the build depends on. Captured to a file inside the mock, because the
            # project is deleted with the restore tree before the script returns, and a mock runs
            # in its own scope so a variable would not survive either.
            $capture = Join-Path (New-TestDirectory) 'captured.csproj'
            $env:RESTORE_DOCS_TEST_CAPTURE = $capture
            Mock dotnet {
                Copy-Item -LiteralPath $args[1] -Destination $env:RESTORE_DOCS_TEST_CAPTURE -Force
                New-RestoredPackage -PackagesPath (Get-PackagesPath -Arguments $args) `
                    -PackageId 'Contoso.Widget' -Version '1.0.0'
                $global:LASTEXITCODE = 0
            }

            try {
                & $script:scriptPath -PackageId 'Contoso.Widget' -Version '1.0.0' -DestinationPath (New-TestDirectory)

                $projectContent = Get-Content -LiteralPath $capture -Raw
                $projectContent | Should -Match '"Contoso\.Widget" Version="\[1\.0\.0\]"'
                # Carrying its own version is an error under a Directory.Packages.props that
                # happens to sit above wherever the destination was placed.
                $projectContent | Should -Match '<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>'
            }
            finally {
                Remove-Item Env:\RESTORE_DOCS_TEST_CAPTURE -ErrorAction SilentlyContinue
            }
        }

        It 'passes the configuration file through when one is supplied' {
            $config = Join-Path (New-TestDirectory) 'NuGet.config'
            Set-Content -LiteralPath $config -Value '<configuration />' -Encoding utf8

            & $script:scriptPath -PackageId 'Contoso.Widget' -Version '1.0.0' `
                -DestinationPath (New-TestDirectory) -ConfigFile $config

            Should -Invoke dotnet -Times 1 -Exactly -ParameterFilter {
                $args -contains '--configfile' -and $args -contains $config
            }
        }
    }

    Context 'failure' {

        It 'throws when the restore fails' {
            Mock dotnet { $global:LASTEXITCODE = 1 }

            { & $script:scriptPath -PackageId 'Contoso.Widget' -Version '1.0.0' `
                    -DestinationPath (New-TestDirectory) } |
                Should -Throw '*failed with exit code 1*'
        }

        It 'throws when the restore succeeds but produces no package folder' {
            Mock dotnet { $global:LASTEXITCODE = 0 }

            { & $script:scriptPath -PackageId 'Contoso.Widget' -Version '1.0.0' `
                    -DestinationPath (New-TestDirectory) } |
                Should -Throw '*no folder for Contoso.Widget was found*'
        }

        It 'throws when the package ships no XML documentation' {
            # Silence here would produce an empty directory and a gate that resolves nothing,
            # reporting the very references the step was added to resolve.
            Mock dotnet {
                $packagesPath = Get-PackagesPath -Arguments $args
                New-RestoredPackage -PackagesPath $packagesPath -PackageId 'Contoso.Widget' `
                    -Version '1.0.0' -WithoutDocumentation
                $global:LASTEXITCODE = 0
            }

            { & $script:scriptPath -PackageId 'Contoso.Widget' -Version '1.0.0' `
                    -DestinationPath (New-TestDirectory) } |
                Should -Throw '*ships no XML documentation*'
        }
    }
}
