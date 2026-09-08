<#
.SYNOPSIS
    Pester tests for Install-DockerCli.macos.ps1.

.DESCRIPTION
    These cover the bottle selection rules, which are the part that fails by
    silently installing the wrong artifact rather than by crashing: picking the
    newest version that is actually bottled for Intel, never taking an arm64,
    linux or ':all' bottle, and reading a revision build out of the unrevised
    cellar directory.

    'Invoke-RestMethod', 'Invoke-WebRequest' and 'tar' are mocked, so the tests
    need no network and no macOS.
#>

BeforeAll {
    $global:scriptPath = Join-Path $PSScriptRoot '..' 'Install-DockerCli.macos.ps1'

    # A stand-in for the bottle archive. The script verifies what it downloads
    # against the digest the registry advertises, so the tests have to advertise
    # this content's real hash.
    $global:blobBytes = [System.Text.Encoding]::UTF8.GetBytes('not-really-a-bottle')
    $blobHash = (
        [System.Security.Cryptography.SHA256]::HashData($global:blobBytes) |
            ForEach-Object { $_.ToString('x2') }
    ) -join ''
    $global:blobDigest = "sha256:$blobHash"

    function New-Index {
        <#
        .SYNOPSIS
            An OCI image index annotated with the given bottle ref names.
        #>
        param([string[]]$RefName)

        return [pscustomobject]@{
            manifests = @(
                $RefName | ForEach-Object {
                    [pscustomobject]@{
                        digest      = "sha256:digest-$_"
                        annotations = [pscustomobject]@{ 'org.opencontainers.image.ref.name' = $_ }
                    }
                }
            )
        }
    }

    function Get-TarMember {
        <#
        .SYNOPSIS
            The archive member the script asked tar to extract.
        #>
        return @($global:tarArgs)[-1]
    }
}

Describe 'Install-DockerCli.macos.ps1' -Skip:([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne 'X64') {

    BeforeAll {
        Mock -CommandName 'Invoke-RestMethod' -MockWith {
            if ($Uri -like '*/token?*') {
                return [pscustomobject]@{ token = 'test-token' }
            }

            if ($Uri -like '*/tags/list*') {
                if ($ResponseHeadersVariable) {
                    Set-Variable -Name $ResponseHeadersVariable -Value @{} -Scope Global
                }
                return [pscustomobject]@{ tags = $global:tags }
            }

            if ($Uri -like '*/manifests/sha256:*') {
                $global:manifestRequests += $Uri
                return [pscustomobject]@{
                    layers = @([pscustomobject]@{ digest = $global:advertisedDigest })
                }
            }

            $version = $Uri -replace '.*/manifests/', ''
            if (-not $global:refsByVersion.ContainsKey($version)) {
                throw "Unexpected manifest request for '$version'."
            }
            return New-Index -RefName $global:refsByVersion[$version]
        }

        Mock -CommandName 'Invoke-WebRequest' -MockWith {
            [System.IO.File]::WriteAllBytes($OutFile, $global:blobBytes)
        }

        Mock -CommandName 'tar' -MockWith {
            $global:tarArgs = $args
            $global:LASTEXITCODE = 0
        }
    }

    BeforeEach {
        $global:tags = @('29.7.2')
        $global:refsByVersion = @{ '29.7.2' = @('29.7.2.sonoma') }
        $global:advertisedDigest = $global:blobDigest
        $global:manifestRequests = @()
        $global:tarArgs = @()

        $global:destination = Join-Path ([System.IO.Path]::GetTempPath()) "docker-cli-test-$([guid]::NewGuid().ToString('n'))"
    }

    AfterEach {
        if (Test-Path -LiteralPath $global:destination) {
            Remove-Item -LiteralPath $global:destination -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    Context 'Version selection' {

        It 'Takes the newest version that is bottled for Intel' {
            $global:tags = @('29.7.2', '29.8.0')
            $global:refsByVersion = @{
                '29.8.0' = @('29.8.0.arm64_sequoia', '29.8.0.x86_64_linux')
                '29.7.2' = @('29.7.2.sonoma')
            }

            & $global:scriptPath -DestinationPath $global:destination

            Get-TarMember | Should -Be 'docker/29.7.2/bin/docker'
        }

        It 'Ignores tags that are not versions' {
            $global:tags = @('latest', '29.7', '29.7.2-beta', '29.7.2')

            & $global:scriptPath -DestinationPath $global:destination

            Get-TarMember | Should -Be 'docker/29.7.2/bin/docker'
        }

        It 'Ranks a revision build above its base version' {
            $global:tags = @('29.7.2', '29.7.2-1')
            $global:refsByVersion = @{
                '29.7.2-1' = @('29.7.2.sonoma.1')
                '29.7.2'   = @('29.7.2.sonoma')
            }

            & $global:scriptPath -DestinationPath $global:destination

            # Homebrew keeps the cellar directory at the plain version, so the
            # revision must not leak into the member path.
            Get-TarMember | Should -Be 'docker/29.7.2/bin/docker'
            $global:manifestRequests | Should -Contain 'https://ghcr.io/v2/homebrew/core/docker/manifests/sha256:digest-29.7.2.sonoma.1'
        }
    }

    Context 'Platform selection' {

        It 'Prefers the newest macOS codename that is bottled' {
            $global:refsByVersion = @{ '29.7.2' = @('29.7.2.ventura', '29.7.2.sonoma') }

            & $global:scriptPath -DestinationPath $global:destination

            $global:manifestRequests | Should -Contain 'https://ghcr.io/v2/homebrew/core/docker/manifests/sha256:digest-29.7.2.sonoma'
        }

        It 'Never selects an arm64, linux or :all bottle' {
            $global:refsByVersion = @{
                '29.7.2' = @('29.7.2.arm64_sonoma', '29.7.2.arm64_linux', '29.7.2.x86_64_linux', '29.7.2.all')
            }

            { & $global:scriptPath -DestinationPath $global:destination } |
                Should -Throw '*No Intel macOS docker bottle*'
        }
    }

    Context 'Download integrity' {

        It 'Fails when the bottle does not match the advertised digest' {
            $global:advertisedDigest = 'sha256:' + ('0' * 64)

            { & $global:scriptPath -DestinationPath $global:destination } |
                Should -Throw '*digest mismatch*'
        }
    }
}
