# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

BeforeAll {
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
    $restore = Join-Path $repoRoot 'eng\scripts\Restore-PowerShellModules.ps1'
}

Describe 'Manifest-backed PowerShell module restore' {
    BeforeEach {
        $manifestPath = Join-Path $TestDrive 'powershell-modules.json'
        '{"version":1,"modules":{"One":{"version":"1.0.0"},"Two":{"version":"2.0.0"}}}' |
            Set-Content -LiteralPath $manifestPath
        Mock Write-Host {}
        Mock Get-PSResourceRepository {}
        Mock Register-PSResourceRepository {}
        Mock Get-InstalledPSResource {}
        Mock Install-PSResource {}
    }

    It 'restores exact versions with PSResourceGet without clobbering existing commands' {
        & $restore -ManifestPath $manifestPath

        Should -Invoke Install-PSResource -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'One' -and $Version -eq '[1.0.0]' -and
            $Repository -eq 'SqlClient' -and $Scope -eq 'CurrentUser' -and
            $NoClobber
        }
    }

    It 'registers once and restores every exact resource version' {
        $events = [System.Collections.Generic.List[string]]::new()
        Mock Register-PSResourceRepository {
            $events.Add('register')
            "Package source with Name: $Name added successfully."
        }
        Mock Install-PSResource { $events.Add($Name) }

        & $restore -ManifestPath $manifestPath

        $events -join ',' | Should -BeExactly 'register,One,Two'
        Should -Invoke Register-PSResourceRepository -Times 1 -Exactly
        Should -Invoke Install-PSResource -Times 2 -Exactly -ParameterFilter {
            @($Repository).Count -eq 1 -and $Repository[0] -eq 'SqlClient' -and
            $Scope -eq 'CurrentUser' -and
            $ErrorAction -eq 'Stop'
        }
        Should -Invoke Install-PSResource -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'One' -and $Version -eq '[1.0.0]'
        }
        Should -Invoke Install-PSResource -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'Two' -and $Version -eq '[2.0.0]'
        }
    }

    It 'only restores missing exact versions' {
        Mock Get-InstalledPSResource { [pscustomobject]@{ Name = 'One' } } -ParameterFilter {
            $Name -eq 'One' -and $Version -eq '[1.0.0]'
        }

        & $restore -ManifestPath $manifestPath

        Should -Invoke Install-PSResource -Times 1 -Exactly -ParameterFilter { $Name -eq 'Two' }
        Should -Invoke Install-PSResource -Times 0 -Exactly -ParameterFilter { $Name -eq 'One' }
    }

    It 'restores resources reported as not installed by PSResourceGet' {
        Mock Get-InstalledPSResource {
            throw [Microsoft.PowerShell.PSResourceGet.UtilClasses.ResourceNotFoundException]::new(
                'resource not installed')
        }

        & $restore -ManifestPath $manifestPath

        Should -Invoke Install-PSResource -Times 2 -Exactly
    }

    It 'does not contact the repository when all required versions are installed' {
        Mock Get-InstalledPSResource { [pscustomobject]@{ Name = 'installed' } }

        & $restore -ManifestPath $manifestPath

        Should -Invoke Get-PSResourceRepository -Times 0 -Exactly
        Should -Invoke Register-PSResourceRepository -Times 0 -Exactly
        Should -Invoke Install-PSResource -Times 0 -Exactly
    }

    It 'requests the manifest version rather than accepting a different installed version' {
        & $restore -ManifestPath $manifestPath

        Should -Invoke Get-InstalledPSResource -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'One' -and $Version -eq '[1.0.0]' -and $ErrorAction -eq 'Stop'
        }
        Should -Invoke Install-PSResource -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'One' -and $Version -eq '[1.0.0]'
        }
    }

    It 'uses the checked-in manifest independently of the working directory' {
        Mock Get-InstalledPSResource { [pscustomobject]@{ Name = 'installed' } }
        Push-Location $TestDrive
        try {
            & $restore
        }
        finally {
            Pop-Location
        }
        $manifest = Get-Content (Join-Path $repoRoot 'powershell-modules.json') -Raw |
            ConvertFrom-Json
        foreach ($entry in $manifest.modules.PSObject.Properties) {
            $expectedName = $entry.Name
            $expectedVersion = $entry.Value.version
            Should -Invoke Get-InstalledPSResource -Times 1 -Exactly -ParameterFilter {
                $Name -eq $expectedName -and $Version -eq "[$expectedVersion]"
            }
        }
        Should -Invoke Install-PSResource -Times 0 -Exactly
    }

    It 'does not restore the unused OpenSSL wrapper from the checked-in manifest' {
        & $restore

        Should -Invoke Get-InstalledPSResource -Times 0 -Exactly -ParameterFilter {
            $Name -eq 'OpenSSL'
        }
        Should -Invoke Install-PSResource -Times 0 -Exactly -ParameterFilter {
            $Name -eq 'OpenSSL'
        }
    }

    It 'rejects <Problem> before looking up or installing modules' -ForEach @(
        @{ Problem = 'malformed JSON'; Json = '{' }
        @{ Problem = 'unsupported schema'; Json = '{"version":2,"modules":{"One":{"version":"1.0.0"}}}' }
        @{ Problem = 'missing modules'; Json = '{"version":1}' }
        @{ Problem = 'empty modules'; Json = '{"version":1,"modules":{}}' }
        @{ Problem = 'array modules'; Json = '{"version":1,"modules":[]}' }
        @{ Problem = 'invalid name'; Json = '{"version":1,"modules":{"bad;name":{"version":"1.0.0"}}}' }
        @{ Problem = 'missing version'; Json = '{"version":1,"modules":{"One":{}}}' }
        @{ Problem = 'floating version'; Json = '{"version":1,"modules":{"One":{"version":"*"}}}' }
        @{ Problem = 'invalid version'; Json = '{"version":1,"modules":{"One":{"version":"latest"}}}' }
        @{ Problem = 'unsupported property'; Json = '{"version":1,"modules":{"One":{"version":"1.0.0","other":true}}}' }
        @{ Problem = 'invalid later entry'; Json = '{"version":1,"modules":{"One":{"version":"1.0.0"},"Two":{}}}' }
    ) {
        $Json | Set-Content -LiteralPath $manifestPath

        { & $restore -ManifestPath $manifestPath } | Should -Throw
        Should -Invoke Get-InstalledPSResource -Times 0 -Exactly
        Should -Invoke Register-PSResourceRepository -Times 0 -Exactly
        Should -Invoke Install-PSResource -Times 0 -Exactly
    }

    It 'reports a missing manifest without installing anything' {
        { & $restore -ManifestPath (Join-Path $TestDrive 'missing.json') } | Should -Throw
        Should -Invoke Install-PSResource -Times 0 -Exactly
    }

    It 'propagates installed-module discovery failures' {
        Mock Get-InstalledPSResource { throw 'module discovery failed' }

        { & $restore -ManifestPath $manifestPath } | Should -Throw '*module discovery failed*'
        Should -Invoke Register-PSResourceRepository -Times 0 -Exactly
        Should -Invoke Install-PSResource -Times 0 -Exactly
    }

    It 'stops before installation when registration fails' {
        Mock Register-PSResourceRepository { throw 'feed registration failed' }

        { & $restore -ManifestPath $manifestPath } | Should -Throw '*feed registration failed*'
        Should -Invoke Install-PSResource -Times 0 -Exactly
    }

    It 'propagates installation failures without trying another source or continuing' {
        Mock Install-PSResource { throw 'governed feed unavailable' }

        { & $restore -ManifestPath $manifestPath } | Should -Throw '*governed feed unavailable*'
        Should -Invoke Install-PSResource -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'One' -and $Repository -eq 'SqlClient'
        }
    }
}
