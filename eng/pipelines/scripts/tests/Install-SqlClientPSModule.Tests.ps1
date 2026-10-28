# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

BeforeAll {
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
    $installer = Join-Path $repoRoot 'eng\scripts\Install-SqlClientPSModule.ps1'
}

Describe 'Governed PowerShell module installation' {
    BeforeEach {
        Mock Get-PSRepository {}
        Mock Register-PSRepository {}
        Mock Get-Module {}
        Mock Install-Module {}
    }

    It 'registers the repository and immediately uses its name for a pinned install' {
        $events = [System.Collections.Generic.List[string]]::new()
        Mock Register-PSRepository { $events.Add('register') }
        Mock Install-Module { $events.Add('install') }

        & $installer -Name SqlServer -RequiredVersion 22.4.5.1 -AllowClobber

        $events -join ',' | Should -BeExactly 'register,install'
        Should -Invoke Register-PSRepository -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'SqlClient' -and $SourceLocation -like 'https://*/_packaging/*/nuget/v2'
        }
        Should -Invoke Install-Module -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'SqlServer' -and $RequiredVersion -eq '22.4.5.1' -and
            $Repository -eq 'SqlClient' -and $Scope -eq 'CurrentUser' -and
            $Force -and $AllowClobber -and $ErrorAction -eq 'Stop'
        }
    }

    It 'preserves an unpinned install and does not enable command clobbering by default' {
        & $installer -Name OpenSSL

        Should -Invoke Install-Module -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'OpenSSL' -and -not $RequiredVersion -and -not $AllowClobber -and
            $Repository -eq 'SqlClient' -and $ErrorAction -eq 'Stop'
        }
    }

    It 'reuses an installed module when requested (<Version>)' -ForEach @(
        @{ Version = '22.4.5.1' }
        @{ Version = $null }
    ) {
        Mock Get-Module { [pscustomobject]@{ Name = 'SqlServer' } }
        $parameters = @{ Name = 'SqlServer'; SkipIfInstalled = $true }
        if ($Version) { $parameters.RequiredVersion = $Version }

        & $installer @parameters

        Should -Invoke Get-Module -Times 1 -Exactly -ParameterFilter {
            $ListAvailable -and $FullyQualifiedName.Name -eq 'SqlServer' -and
            $FullyQualifiedName.RequiredVersion -eq $Version
        }
        Should -Invoke Get-PSRepository -Times 0 -Exactly
        Should -Invoke Install-Module -Times 0 -Exactly
    }

    It 'installs the exact missing version rather than reusing another installed version' {
        Mock Get-Module {} -ParameterFilter {
            $FullyQualifiedName.RequiredVersion -eq '22.4.5.1'
        }

        & $installer -Name SqlServer -RequiredVersion 22.4.5.1 -SkipIfInstalled

        Should -Invoke Get-Module -Times 1 -Exactly -ParameterFilter {
            $FullyQualifiedName.RequiredVersion -eq '22.4.5.1'
        }
        Should -Invoke Install-Module -Times 1 -Exactly -ParameterFilter {
            $RequiredVersion -eq '22.4.5.1'
        }
    }

    It 'does not skip installation unless requested' {
        Mock Get-Module { throw 'Unconditional installs must not query installed modules' }

        & $installer -Name SqlServer

        Should -Invoke Get-Module -Times 0 -Exactly
        Should -Invoke Install-Module -Times 1 -Exactly
    }

    It 'stops before installation when repository registration fails' {
        Mock Register-PSRepository { throw 'feed registration failed' }

        { & $installer -Name Pester -RequiredVersion 6.2.0 } |
            Should -Throw '*feed registration failed*'
        Should -Invoke Install-Module -Times 0 -Exactly
    }

    It 'propagates installation failures without trying another repository' {
        Mock Install-Module { throw 'governed feed unavailable' }

        { & $installer -Name Pester -RequiredVersion 6.2.0 } |
            Should -Throw '*governed feed unavailable*'
        Should -Invoke Install-Module -Times 1 -Exactly
    }

    It 'rejects malformed module inputs before registering a repository' {
        { & $installer -Name 'module;injected' } | Should -Throw
        { & $installer -Name SqlServer -RequiredVersion 'invalid' } | Should -Throw
        Should -Invoke Register-PSRepository -Times 0 -Exactly
        Should -Invoke Install-Module -Times 0 -Exactly
    }
}
