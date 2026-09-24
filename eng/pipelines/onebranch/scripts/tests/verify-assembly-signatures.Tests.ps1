<#
.SYNOPSIS
    Pester tests for verify-assembly-signatures.ps1.

.NOTES
    Get-AuthenticodeSignature is a Windows-only cmdlet, so a stub is declared when it is absent.
    This lets the tests run on any platform while still exercising the script's real expansion and
    reporting logic; only the signature lookup itself is substituted.
#>

BeforeAll {
    $scriptPath = Join-Path $PSScriptRoot '..' 'verify-assembly-signatures.ps1'

    if (-not (Get-Command 'Get-AuthenticodeSignature' -ErrorAction SilentlyContinue)) {
        function Get-AuthenticodeSignature {
            param([Parameter(Mandatory = $true)][string[]]$FilePath)
            throw 'stub must be mocked'
        }
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    # Builds a real .nupkg so the script's expansion path is genuinely exercised.
    function New-TestPackage {
        param(
            [string]$Name,
            [string[]]$AssemblyPaths = @('lib/net8.0/Test.dll'),
            [string[]]$OtherPaths = @()
        )

        $staging = Join-Path $TestDrive "staging-$Name"
        if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $staging | Out-Null

        foreach ($relative in ($AssemblyPaths + $OtherPaths)) {
            $full = Join-Path $staging $relative
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $full) | Out-Null
            Set-Content -LiteralPath $full -Value 'stub'
        }

        $packagePath = Join-Path $script:packagesPath "$Name.nupkg"
        if (Test-Path -LiteralPath $packagePath) { Remove-Item -LiteralPath $packagePath -Force }
        [System.IO.Compression.ZipFile]::CreateFromDirectory($staging, $packagePath)
        return $packagePath
    }

    function Invoke-VerifyAssemblySignatures {
        param(
            [string]$PackagesPath = $script:packagesPath,
            [string]$ExtractPath = $script:extractPath
        )

        & $scriptPath -PackagesPath $PackagesPath -ExtractPath $ExtractPath *>&1 | Out-String
    }
}

AfterAll {
    Remove-Variable -Name 'verifyAssemblySeen' -Scope Global -ErrorAction SilentlyContinue
}

Describe 'verify-assembly-signatures.ps1' {
    BeforeEach {
        $script:packagesPath = Join-Path $TestDrive 'packages'
        $script:extractPath = Join-Path $TestDrive 'extract'
        foreach ($path in @($script:packagesPath, $script:extractPath)) {
            if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
            New-Item -ItemType Directory -Force -Path $path | Out-Null
        }
    }

    It 'expands packages and verifies every assembly they contain' {
        New-TestPackage -Name 'PackageOne' -AssemblyPaths @('lib/net8.0/One.dll') | Out-Null
        New-TestPackage -Name 'PackageTwo' -AssemblyPaths @('lib/net8.0/Two.dll', 'runtimes/win-x64/native/sni.dll') | Out-Null
        Mock -CommandName 'Get-AuthenticodeSignature' -MockWith {
            $FilePath | ForEach-Object { [pscustomobject]@{ Path = $_; Status = 'Valid' } }
        }

        $output = Invoke-VerifyAssemblySignatures
        $output | Should -Match 'All 3 assemblies are Authenticode signed'
    }

    It 'finds native binaries under runtimes, not just managed assemblies' {
        New-TestPackage -Name 'PackageOne' -AssemblyPaths @('runtimes/win-arm64/native/sni.dll') | Out-Null
        $global:verifyAssemblySeen = @()
        Mock -CommandName 'Get-AuthenticodeSignature' -MockWith {
            $global:verifyAssemblySeen = $FilePath
            $FilePath | ForEach-Object { [pscustomobject]@{ Path = $_; Status = 'Valid' } }
        }

        Invoke-VerifyAssemblySignatures | Out-Null
        ($global:verifyAssemblySeen -join ';') | Should -Match 'sni\.dll'
    }

    It 'ignores non-assembly content' {
        New-TestPackage -Name 'PackageOne' -AssemblyPaths @('lib/net8.0/One.dll') -OtherPaths @('README.md', 'lib/net8.0/One.xml') | Out-Null
        Mock -CommandName 'Get-AuthenticodeSignature' -MockWith {
            $FilePath | ForEach-Object { [pscustomobject]@{ Path = $_; Status = 'Valid' } }
        }

        $output = Invoke-VerifyAssemblySignatures
        $output | Should -Match 'All 1 assemblies are Authenticode signed'
    }

    It 'fails when an assembly is not validly signed' {
        New-TestPackage -Name 'PackageOne' -AssemblyPaths @('lib/net8.0/One.dll', 'lib/net8.0/Two.dll') | Out-Null
        Mock -CommandName 'Get-AuthenticodeSignature' -MockWith {
            $index = 0
            $FilePath | ForEach-Object {
                $status = if ($index -eq 0) { 'Valid' } else { 'NotSigned' }
                $index++
                [pscustomobject]@{ Path = $_; Status = $status }
            }
        }

        { Invoke-VerifyAssemblySignatures } | Should -Throw '*failed for 1 of 2 assemblies*'
    }

    It 'reports every unsigned assembly rather than stopping at the first' {
        New-TestPackage -Name 'PackageOne' -AssemblyPaths @('lib/net8.0/One.dll', 'lib/net8.0/Two.dll') | Out-Null
        Mock -CommandName 'Get-AuthenticodeSignature' -MockWith {
            $FilePath | ForEach-Object { [pscustomobject]@{ Path = $_; Status = 'NotSigned' } }
        }

        { Invoke-VerifyAssemblySignatures } | Should -Throw '*failed for 2 of 2 assemblies*'
    }

    It 'replaces a previous expansion so stale content cannot be verified' {
        New-TestPackage -Name 'PackageOne' -AssemblyPaths @('lib/net8.0/One.dll', 'lib/net8.0/Stale.dll') | Out-Null
        Mock -CommandName 'Get-AuthenticodeSignature' -MockWith {
            $FilePath | ForEach-Object { [pscustomobject]@{ Path = $_; Status = 'Valid' } }
        }
        Invoke-VerifyAssemblySignatures | Out-Null

        # Repack the same package id with fewer assemblies; the stale one must not linger.
        New-TestPackage -Name 'PackageOne' -AssemblyPaths @('lib/net8.0/One.dll') | Out-Null
        $output = Invoke-VerifyAssemblySignatures
        $output | Should -Match 'All 1 assemblies are Authenticode signed'
    }

    It 'throws when no packages are found' {
        { Invoke-VerifyAssemblySignatures } | Should -Throw '*No .nupkg files were found*'
    }

    It 'throws when packages contain no assemblies' {
        New-TestPackage -Name 'PackageOne' -AssemblyPaths @() -OtherPaths @('README.md') | Out-Null

        { Invoke-VerifyAssemblySignatures } | Should -Throw '*No assemblies were found*'
    }
}
