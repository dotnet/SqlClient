#################################################################################
# Licensed to the .NET Foundation under one or more agreements.                 #
# The .NET Foundation licenses this file to you under the MIT license.          #
# See the LICENSE file in the project root for more information.                #
#################################################################################

# Pester tests for validate-symbols.ps1
#
# Run with:  pwsh -c "Invoke-Pester ./validate-symbols.Tests.ps1 -Output Detailed"

BeforeAll {
    $Script:ScriptPath = Join-Path $PSScriptRoot '..' 'jobs' 'validate-symbols.ps1'

    # Common parameters reused across tests.
    $Script:CommonParams = @{
        SymbolServerUrl  = 'https://example.com/symbols'
        SymbolServerName = 'TestServer'
        MaxRetries       = 1
        RetryIntervalSeconds = 0
    }

    function Invoke-ValidateSymbols {
        <#
        .SYNOPSIS
            Runs validate-symbols.ps1 as a child process and captures output
            and exit code.
        #>
        param([hashtable]$Params)

        $argList = @('-NoProfile', '-NonInteractive', '-File', $Script:ScriptPath)
        foreach ($kv in $Params.GetEnumerator()) {
            $argList += "-$($kv.Key)"
            $argList += $kv.Value
        }

        $proc = Start-Process -FilePath 'pwsh' `
            -ArgumentList $argList `
            -NoNewWindow `
            -Wait `
            -PassThru `
            -RedirectStandardOutput "$TestDrive/stdout.txt" `
            -RedirectStandardError  "$TestDrive/stderr.txt"

        return @{
            ExitCode = $proc.ExitCode
            StdOut   = (Get-Content "$TestDrive/stdout.txt" -Raw -ErrorAction SilentlyContinue) ?? ''
            StdErr   = (Get-Content "$TestDrive/stderr.txt" -Raw -ErrorAction SilentlyContinue) ?? ''
        }
    }

    function New-FakeNupkg {
        <#
        .SYNOPSIS
            Creates a minimal .nupkg (zip) that contains a placeholder DLL
            at the specified relative path.
        #>
        param(
            [string]$OutputDir,
            [string]$PackageName,
            [string]$Version,
            [string]$DllRelativePath
        )

        $stagingDir = Join-Path $TestDrive 'nupkg_staging'
        if (Test-Path $stagingDir) { Remove-Item $stagingDir -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $stagingDir | Out-Null

        # Create the DLL placeholder inside the staging directory.
        $dllStaging = Join-Path $stagingDir $DllRelativePath
        New-Item -ItemType Directory -Force -Path (Split-Path $dllStaging) | Out-Null
        Set-Content -Path $dllStaging -Value 'FAKE_DLL'

        # Create the zip and rename to .nupkg.
        $zipDest = Join-Path $OutputDir "$PackageName.$Version.nupkg"
        Compress-Archive -Path "$stagingDir/*" -DestinationPath $zipDest -Force

        Remove-Item $stagingDir -Recurse -Force
        return $zipDest
    }
}

# =============================================================================
# 1. Script syntax / parse validation
# =============================================================================

Describe 'Script syntax' {
    It 'parses without errors' {
        $errors = $null
        $null = [System.Management.Automation.Language.Parser]::ParseFile(
            $Script:ScriptPath,
            [ref]$null,
            [ref]$errors
        )
        $errors | Should -BeNullOrEmpty
    }

    It 'contains no non-ASCII characters' {
        $bytes = [System.IO.File]::ReadAllBytes($Script:ScriptPath)
        $nonAscii = $bytes | Where-Object { $_ -gt 127 }
        $nonAscii | Should -BeNullOrEmpty
    }
}

# =============================================================================
# 2. Package discovery and extraction
# =============================================================================

Describe 'Package discovery' {
    It 'exits 1 when no nupkg is found' {
        $artifactDir = Join-Path $TestDrive 'empty_artifacts'
        $extractDir  = Join-Path $TestDrive 'extract_empty'
        New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

        $result = Invoke-ValidateSymbols @{
            ArtifactPath = $artifactDir
            ExtractPath  = $extractDir
            PackageName  = 'NoSuchPackage'
            DllPath      = 'lib/net8.0/NoSuchPackage.dll'
            SymbolServerUrl  = $Script:CommonParams.SymbolServerUrl
            SymbolServerName = $Script:CommonParams.SymbolServerName
            MaxRetries       = '1'
            RetryIntervalSeconds = '0'
        }

        $result.ExitCode | Should -Be 1
        $result.StdOut   | Should -Match 'No NoSuchPackage nupkg found'
    }

    It 'ignores .snupkg files when searching for the package' {
        $artifactDir = Join-Path $TestDrive 'snupkg_artifacts'
        $extractDir  = Join-Path $TestDrive 'extract_snupkg'
        New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

        # Create only a .snupkg — the script should not match it.
        Set-Content -Path (Join-Path $artifactDir 'Pkg.1.0.0.snupkg') -Value 'SNUPKG'

        $result = Invoke-ValidateSymbols @{
            ArtifactPath = $artifactDir
            ExtractPath  = $extractDir
            PackageName  = 'Pkg'
            DllPath      = 'lib/net8.0/Pkg.dll'
            SymbolServerUrl  = $Script:CommonParams.SymbolServerUrl
            SymbolServerName = $Script:CommonParams.SymbolServerName
            MaxRetries       = '1'
            RetryIntervalSeconds = '0'
        }

        $result.ExitCode | Should -Be 1
        $result.StdOut   | Should -Match 'No Pkg nupkg found'
    }

    It 'extracts the nupkg and finds the DLL' {
        $artifactDir = Join-Path $TestDrive 'good_artifacts'
        $extractDir  = Join-Path $TestDrive 'extract_good'
        New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

        $dllRel = Join-Path 'lib' 'net8.0' 'MyPackage.dll'
        New-FakeNupkg -OutputDir $artifactDir `
                      -PackageName 'MyPackage' `
                      -Version '1.0.0' `
                      -DllRelativePath $dllRel

        # The script will extract successfully, then fail looking for
        # symchk (which is expected on non-Windows).
        $result = Invoke-ValidateSymbols @{
            ArtifactPath = $artifactDir
            ExtractPath  = $extractDir
            PackageName  = 'MyPackage'
            DllPath      = $dllRel
            SymbolServerUrl  = $Script:CommonParams.SymbolServerUrl
            SymbolServerName = $Script:CommonParams.SymbolServerName
            MaxRetries       = '1'
            RetryIntervalSeconds = '0'
        }

        # The DLL should have been extracted.
        $extractedDll = Join-Path $extractDir $dllRel
        $extractedDll | Should -Exist

        # On non-Windows, expect symchk-not-found exit.
        # On Windows without Debugging Tools, same result.
        $result.ExitCode | Should -Be 1
        $result.StdOut   | Should -Match 'Found:.*MyPackage\.1\.0\.0\.nupkg'
    }

    It 'skips extraction when DLL already exists' {
        $artifactDir = Join-Path $TestDrive 'pre_extracted_artifacts'
        $extractDir  = Join-Path $TestDrive 'pre_extracted'
        New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

        $dllRel = Join-Path 'lib' 'net8.0' 'Already.dll'

        # Pre-create the DLL so extraction is skipped.
        $dllFull = Join-Path $extractDir $dllRel
        New-Item -ItemType Directory -Force -Path (Split-Path $dllFull) | Out-Null
        Set-Content -Path $dllFull -Value 'PRE_EXISTING'

        $result = Invoke-ValidateSymbols @{
            ArtifactPath = $artifactDir
            ExtractPath  = $extractDir
            PackageName  = 'Already'
            DllPath      = $dllRel
            SymbolServerUrl  = $Script:CommonParams.SymbolServerUrl
            SymbolServerName = $Script:CommonParams.SymbolServerName
            MaxRetries       = '1'
            RetryIntervalSeconds = '0'
        }

        # Should NOT see "Extracting" in output since DLL already existed.
        $result.StdOut | Should -Not -Match 'Extracting Already'
    }

    It 'exits 1 when DLL is missing after extraction' {
        $artifactDir = Join-Path $TestDrive 'wrong_dll_artifacts'
        $extractDir  = Join-Path $TestDrive 'extract_wrong_dll'
        New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

        # Create nupkg with DLL at one path but ask for a different path.
        $actualDll = Join-Path 'lib' 'net8.0' 'Actual.dll'
        New-FakeNupkg -OutputDir $artifactDir `
                      -PackageName 'MissingDll' `
                      -Version '2.0.0' `
                      -DllRelativePath $actualDll

        $requestedDll = Join-Path 'lib' 'net9.0' 'MissingDll.dll'

        $result = Invoke-ValidateSymbols @{
            ArtifactPath = $artifactDir
            ExtractPath  = $extractDir
            PackageName  = 'MissingDll'
            DllPath      = $requestedDll
            SymbolServerUrl  = $Script:CommonParams.SymbolServerUrl
            SymbolServerName = $Script:CommonParams.SymbolServerName
            MaxRetries       = '1'
            RetryIntervalSeconds = '0'
        }

        $result.ExitCode | Should -Be 1
        $result.StdOut   | Should -Match 'DLL not found after extraction'
    }
}

# =============================================================================
# 3. symchk detection
# =============================================================================

Describe 'symchk detection' {
    It 'exits 1 when symchk.exe is not found' {
        $artifactDir = Join-Path $TestDrive 'symchk_artifacts'
        $extractDir  = Join-Path $TestDrive 'symchk_extract'
        New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

        $dllRel = Join-Path 'lib' 'net8.0' 'Test.dll'

        # Pre-create DLL so extraction is skipped and we go straight to
        # symchk detection.
        $dllFull = Join-Path $extractDir $dllRel
        New-Item -ItemType Directory -Force -Path (Split-Path $dllFull) | Out-Null
        Set-Content -Path $dllFull -Value 'FAKE'

        $result = Invoke-ValidateSymbols @{
            ArtifactPath = $artifactDir
            ExtractPath  = $extractDir
            PackageName  = 'Test'
            DllPath      = $dllRel
            SymbolServerUrl  = $Script:CommonParams.SymbolServerUrl
            SymbolServerName = $Script:CommonParams.SymbolServerName
            MaxRetries       = '1'
            RetryIntervalSeconds = '0'
        }

        # Unless running on a Windows box with Debugging Tools installed,
        # symchk.exe won't be found.
        if (-not ($IsWindows -and (
            (Test-Path "${env:ProgramFiles(x86)}\Windows Kits\10\Debuggers\x64\symchk.exe") -or
            (Test-Path "$env:ProgramFiles\Windows Kits\10\Debuggers\x64\symchk.exe")
        ))) {
            $result.ExitCode | Should -Be 1
            $result.StdOut   | Should -Match 'symchk\.exe not found'
        }
    }
}

# =============================================================================
# 4. Symbol verification (retry logic)
#
# These tests create a mock symchk script to simulate pass/fail output.
# A patched copy of validate-symbols.ps1 is generated that points to the
# mock instead of the real Windows SDK symchk.exe.
# =============================================================================

Describe 'Symbol verification with mock symchk' {
    BeforeAll {
        $Script:MockDir = Join-Path $TestDrive 'mock_symchk'
        New-Item -ItemType Directory -Force -Path $Script:MockDir | Out-Null

        $Script:MockSymchk    = Join-Path $Script:MockDir 'symchk.ps1'
        $Script:ControlFile   = Join-Path $Script:MockDir 'control.txt'
        $Script:PatchedScript = Join-Path $TestDrive 'validate-symbols-patched.ps1'

        # Mock symchk: reads one exit-code line from control.txt per
        # invocation, shifts remaining lines for retry support.
        Set-Content -Path $Script:MockSymchk -Value @'
# Mock symchk — positional args: <dllPath> /s <serverArg> /os
$controlFile = Join-Path $PSScriptRoot 'control.txt'
$lines = @(Get-Content $controlFile -ErrorAction Stop)
$code  = [int]$lines[0]
if ($lines.Count -gt 1) {
    Set-Content $controlFile ($lines[1..($lines.Count - 1)])
} else {
    Set-Content $controlFile ''
}
$dll = Split-Path $args[0] -Leaf
if ($code -eq 0) {
    Write-Output "SYMCHK: $dll           PASSED"
    Write-Output "SYMCHK: PASSED files = 1"
    Write-Output "SYMCHK: FAILED files = 0"
    Write-Output "SYMCHK: PASSED + IGNORED files = 1"
} else {
    Write-Output "SYMCHK: $dll           FAILED"
    Write-Output "SYMCHK: PASSED files = 0"
    Write-Output "SYMCHK: FAILED files = 1"
    Write-Output "SYMCHK: PASSED + IGNORED files = 0"
}
exit $code
'@

        # Build the patched script: replace symchk candidate list and
        # symchk invocation so that our .ps1 mock is used.
        $lines = Get-Content $Script:ScriptPath

        # Find and replace the $symchkCandidates block (spans multiple lines).
        $inBlock = $false
        $patchedLines = @()
        foreach ($line in $lines) {
            if ($line -match '^\$symchkCandidates\s*=\s*@\(') {
                $inBlock = $true
                $patchedLines += "`$symchkCandidates = @(`"$($Script:MockSymchk)`")"
                continue
            }
            if ($inBlock) {
                # Skip lines until the closing paren of the array.
                if ($line -match '^\)') { $inBlock = $false }
                continue
            }
            # Replace the symchk invocation to call our .ps1 mock via pwsh.
            if ($line -match '\& \$symchkPath @symchkArgs') {
                $line = $line -replace `
                    '\& \$symchkPath @symchkArgs', `
                    '& pwsh -NoProfile -File $symchkPath @symchkArgs'
            }
            $patchedLines += $line
        }

        Set-Content -Path $Script:PatchedScript -Value $patchedLines

        # Helper function for running the patched script as a subprocess.
        function Script:Invoke-PatchedScript {
            param(
                [string[]]$ControlSequence,
                [int]$MaxRetries = 1
            )

            Set-Content -Path $Script:ControlFile `
                -Value ($ControlSequence -join "`n")

            $guid = [guid]::NewGuid().ToString('N')
            $stdoutFile = Join-Path $TestDrive "out_$guid.txt"
            $stderrFile = Join-Path $TestDrive "err_$guid.txt"

            $proc = Start-Process -FilePath 'pwsh' `
                -ArgumentList @(
                    '-NoProfile', '-NonInteractive',
                    '-File', $Script:PatchedScript,
                    '-ArtifactPath',         $Script:MockArtifactDir,
                    '-ExtractPath',          $Script:MockExtractDir,
                    '-PackageName',          'Mock',
                    '-DllPath',              $Script:MockDllRel,
                    '-SymbolServerUrl',      'https://example.com/symbols',
                    '-SymbolServerName',     'TestServer',
                    '-MaxRetries',           $MaxRetries,
                    '-RetryIntervalSeconds', 0
                ) `
                -NoNewWindow -Wait -PassThru `
                -RedirectStandardOutput $stdoutFile `
                -RedirectStandardError  $stderrFile

            return @{
                ExitCode = $proc.ExitCode
                StdOut   = (Get-Content $stdoutFile -Raw `
                    -ErrorAction SilentlyContinue) ?? ''
                StdErr   = (Get-Content $stderrFile -Raw `
                    -ErrorAction SilentlyContinue) ?? ''
            }
        }
    }

    BeforeEach {
        # Fresh directories for each test.
        $guid = [guid]::NewGuid().ToString('N')
        $Script:MockExtractDir  = Join-Path $TestDrive "extract_$guid"
        $Script:MockArtifactDir = Join-Path $TestDrive "art_$guid"
        New-Item -ItemType Directory -Force -Path $Script:MockArtifactDir | Out-Null

        $Script:MockDllRel = Join-Path 'lib' 'net8.0' 'Mock.dll'
        $dllFull = Join-Path $Script:MockExtractDir $Script:MockDllRel
        New-Item -ItemType Directory -Force -Path (Split-Path $dllFull) | Out-Null
        Set-Content -Path $dllFull -Value 'MOCK_DLL'
    }

    It 'patched script parses without errors' {
        $errors = $null
        $null = [System.Management.Automation.Language.Parser]::ParseFile(
            $Script:PatchedScript, [ref]$null, [ref]$errors
        )
        $errors | Should -BeNullOrEmpty
    }

    It 'exits 0 when symchk passes on first attempt' {
        $result = Invoke-PatchedScript -ControlSequence @('0')

        $result.ExitCode | Should -Be 0
        $result.StdOut   | Should -Match 'Symbols verified successfully'
    }

    It 'exits 1 when symchk fails on all attempts' {
        $result = Invoke-PatchedScript -ControlSequence @('1') -MaxRetries 1

        $result.ExitCode | Should -Be 1
        $result.StdOut   | Should -Match 'could not verify symbols'
    }

    It 'retries and succeeds when symchk fails then passes' {
        $result = Invoke-PatchedScript -ControlSequence @('1', '0') -MaxRetries 2

        $result.ExitCode | Should -Be 0
        $result.StdOut   | Should -Match 'Attempt 1 of 2'
        $result.StdOut   | Should -Match 'Symbols verified successfully'
    }

    It 'exhausts all retries when symchk keeps failing' {
        $result = Invoke-PatchedScript -ControlSequence @('1', '1', '1') -MaxRetries 3

        $result.ExitCode | Should -Be 1
        $result.StdOut   | Should -Match 'Attempt 3 of 3'
        $result.StdOut   | Should -Match 'could not verify symbols.*after 3 attempts'
    }
}
