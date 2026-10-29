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
        '{"version":1,"modules":{"One":{"version":"1.0.0"},"Two":{"version":"2.0.0","allowClobber":true}}}' |
            Set-Content -LiteralPath $manifestPath
        Mock Write-Host {}
        Mock Get-PSRepository {}
        Mock Register-PSRepository {}
        Mock Get-Module {}
        Mock Install-Module {}
    }

    It 'registers once and restores every exact version with its manifest options' {
        $events = [System.Collections.Generic.List[string]]::new()
        Mock Register-PSRepository { $events.Add('register') }
        Mock Install-Module { $events.Add($Name) }

        & $restore -ManifestPath $manifestPath

        $events -join ',' | Should -BeExactly 'register,One,Two'
        Should -Invoke Register-PSRepository -Times 1 -Exactly
        Should -Invoke Install-Module -Times 2 -Exactly -ParameterFilter {
            $Repository -eq 'SqlClient' -and $Scope -eq 'CurrentUser' -and
            $Force -and $ErrorAction -eq 'Stop'
        }
        Should -Invoke Install-Module -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'One' -and $RequiredVersion -eq '1.0.0' -and -not $AllowClobber
        }
        Should -Invoke Install-Module -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'Two' -and $RequiredVersion -eq '2.0.0' -and $AllowClobber
        }
    }

    It 'only restores missing exact versions' {
        Mock Get-Module { [pscustomobject]@{ Name = 'One' } } -ParameterFilter {
            $ListAvailable -and $FullyQualifiedName.Name -eq 'One' -and
            $FullyQualifiedName.RequiredVersion -eq '1.0.0'
        }

        & $restore -ManifestPath $manifestPath

        Should -Invoke Install-Module -Times 1 -Exactly -ParameterFilter { $Name -eq 'Two' }
        Should -Invoke Install-Module -Times 0 -Exactly -ParameterFilter { $Name -eq 'One' }
    }

    It 'does not contact the repository when all required versions are installed' {
        Mock Get-Module { [pscustomobject]@{ Name = 'installed' } }

        & $restore -ManifestPath $manifestPath

        Should -Invoke Get-PSRepository -Times 0 -Exactly
        Should -Invoke Register-PSRepository -Times 0 -Exactly
        Should -Invoke Install-Module -Times 0 -Exactly
    }

    It 'requests the manifest version rather than accepting a different installed version' {
        & $restore -ManifestPath $manifestPath

        Should -Invoke Get-Module -Times 1 -Exactly -ParameterFilter {
            $ListAvailable -and $FullyQualifiedName.Name -eq 'One' -and
            $FullyQualifiedName.RequiredVersion -eq '1.0.0'
        }
        Should -Invoke Install-Module -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'One' -and $RequiredVersion -eq '1.0.0'
        }
    }

    It 'uses the checked-in manifest independently of the working directory' {
        Mock Get-Module { [pscustomobject]@{ Name = 'installed' } }
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
            Should -Invoke Get-Module -Times 1 -Exactly -ParameterFilter {
                $FullyQualifiedName.Name -eq $expectedName -and
                $FullyQualifiedName.RequiredVersion -eq $expectedVersion
            }
        }
        Should -Invoke Install-Module -Times 0 -Exactly
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
        @{ Problem = 'invalid options'; Json = '{"version":1,"modules":{"One":{"version":"1.0.0","allowClobber":"false"}}}' }
        @{ Problem = 'invalid later entry'; Json = '{"version":1,"modules":{"One":{"version":"1.0.0"},"Two":{}}}' }
    ) {
        $Json | Set-Content -LiteralPath $manifestPath

        { & $restore -ManifestPath $manifestPath } | Should -Throw
        Should -Invoke Get-Module -Times 0 -Exactly
        Should -Invoke Register-PSRepository -Times 0 -Exactly
        Should -Invoke Install-Module -Times 0 -Exactly
    }

    It 'reports a missing manifest without installing anything' {
        { & $restore -ManifestPath (Join-Path $TestDrive 'missing.json') } | Should -Throw
        Should -Invoke Install-Module -Times 0 -Exactly
    }

    It 'propagates installed-module discovery failures' {
        Mock Get-Module { throw 'module discovery failed' }

        { & $restore -ManifestPath $manifestPath } | Should -Throw '*module discovery failed*'
        Should -Invoke Register-PSRepository -Times 0 -Exactly
        Should -Invoke Install-Module -Times 0 -Exactly
    }

    It 'stops before installation when registration fails' {
        Mock Register-PSRepository { throw 'feed registration failed' }

        { & $restore -ManifestPath $manifestPath } | Should -Throw '*feed registration failed*'
        Should -Invoke Install-Module -Times 0 -Exactly
    }

    It 'propagates installation failures without trying another source or continuing' {
        Mock Install-Module { throw 'governed feed unavailable' }

        { & $restore -ManifestPath $manifestPath } | Should -Throw '*governed feed unavailable*'
        Should -Invoke Install-Module -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'One' -and $Repository -eq 'SqlClient'
        }
    }
}
