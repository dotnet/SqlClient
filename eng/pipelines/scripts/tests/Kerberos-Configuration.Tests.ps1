# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Executes the Kerberos pipeline's config steps with placeholders, without domain or SQL access.
#>
BeforeAll {
    $repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
    $pipeline = Get-Content (Join-Path $repoRoot `
        'eng\pipelines\ci\kerberos\sqlclient-ci-kerberos-pipeline.yml') -Raw
    $configSteps = @{}
    foreach ($stage in @('windows', 'linux')) {
        $stageText = [regex]::Match($pipeline,
            "(?ms)^  - stage: $stage\r?\n.*?(?=^  - stage:|\z)").Value
        $step = [regex]::Matches($stageText, (
            '(?m)^          - pwsh: \|\r?\n' +
            '(?<script>(?:^              .*\r?\n)+)' +
            '^            workingDirectory: (?<directory>[^\r\n]+)\r?\n' +
            '^            displayName: Update test config\.json[^\r\n]*\r?\n' +
            '^            env:\r?\n(?<env>(?:^              [^\r\n]+\r?\n)+)'))
        $step.Count | Should -Be 1 -Because "$stage must have one test configuration step"

        $environment = @{}
        foreach ($mapping in [regex]::Matches($step[0].Groups['env'].Value,
            '(?m)^              (?<name>\w+): \$\((?<variable>\w+)\)\r?$')) {
            $environment.Add($mapping.Groups['name'].Value, $mapping.Groups['variable'].Value)
        }

        $configSteps[$stage] = @{
            Script = [scriptblock]::Create($step[0].Groups['script'].Value)
            Directory = $step[0].Groups['directory'].Value
            Environment = $environment
        }
    }
}

Describe 'Kerberos test configuration' {
    It 'maps Kerberos markers through the <Stage> step environment' -ForEach @(
        @{ Stage = 'windows' }
        @{ Stage = 'linux' }
    ) {
        $environment = $configSteps[$Stage].Environment
        $environment['KERBEROS_DOMAIN_USER'] | Should -BeExactly 'KerberosDomainUser'
        $environment['KERBEROS_DOMAIN_PASSWORD'] | Should -BeExactly 'KerberosDomainPassword'
        $environment['REMOTE_TCP_CONN_STRING'] | Should -BeExactly 'REMOTE_TCP_CONN_STRING'
        $environment['REMOTE_NP_CONN_STRING'] | Should -BeExactly 'REMOTE_NP_CONN_STRING'
        if ($Stage -eq 'windows') {
            $environment['MANAGED_SNI'] | Should -BeExactly 'managedSNI'
        }
    }

    It 'generates <Stage> config with SNI=<ManagedSni> and existing markers=<ExistingMarkers>' `
        -ForEach @(
            @{ Stage = 'windows'; ManagedSni = 'false'; ExistingMarkers = $false }
            @{ Stage = 'windows'; ManagedSni = 'true'; ExistingMarkers = $false }
            @{ Stage = 'linux'; ManagedSni = 'false'; ExistingMarkers = $false }
            @{ Stage = 'windows'; ManagedSni = 'false'; ExistingMarkers = $true }
            @{ Stage = 'windows'; ManagedSni = 'true'; ExistingMarkers = $true }
            @{ Stage = 'linux'; ManagedSni = 'false'; ExistingMarkers = $true }
        ) {
        $step = $configSteps[$Stage]
        $variables = @{
            REMOTE_TCP_CONN_STRING = 'Server=tcp:placeholder;Integrated Security=true;'
            REMOTE_NP_CONN_STRING = 'Server=np:placeholder;Integrated Security=true;'
            KerberosDomainUser = 'placeholder\user'
            KerberosDomainPassword = 'placeholder-"quoted"\value$'
            managedSNI = $ManagedSni
        }
        $environmentNames = @(
            'REMOTE_TCP_CONN_STRING', 'REMOTE_NP_CONN_STRING', 'KERBEROS_DOMAIN_USER',
            'KERBEROS_DOMAIN_PASSWORD', 'MANAGED_SNI'
        )
        $savedEnvironment = @{}
        foreach ($name in $environmentNames) {
            $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        }

        Push-Location $TestDrive
        try {
            foreach ($name in $environmentNames) {
                [Environment]::SetEnvironmentVariable($name, $null, 'Process')
            }
            foreach ($mapping in $step.Environment.GetEnumerator()) {
                $variables.ContainsKey($mapping.Value) | Should -BeTrue
                [Environment]::SetEnvironmentVariable(
                    $mapping.Key, $variables[$mapping.Value], 'Process')
            }

            $defaultPath = Join-Path (Join-Path $repoRoot $step.Directory) 'config.default.json'
            $defaults = Get-Content $defaultPath -Raw | ConvertFrom-Json
            if ($ExistingMarkers) {
                $defaults | Add-Member -NotePropertyName KerberosDomainUser `
                    -NotePropertyValue 'stale-placeholder-user' -Force
                $defaults | Add-Member -NotePropertyName KerberosDomainPassword `
                    -NotePropertyValue 'stale-placeholder-password' -Force
            }
            $defaults | ConvertTo-Json | Set-Content 'config.default.json'
            if (Test-Path 'config.json') {
                Remove-Item 'config.json'
            }

            $output = @(& $step.Script *>&1)
            $output.Count | Should -Be 0 -Because 'configuration must not be written to logs'
            $config = Get-Content 'config.json' -Raw | ConvertFrom-Json

            ($config.KerberosDomainUser -ceq $variables.KerberosDomainUser) | Should -BeTrue
            ($config.KerberosDomainPassword -ceq $variables.KerberosDomainPassword) | Should -BeTrue
            # DataTestUtility.IsKerberosTest requires both markers to be nonempty.
            ([string]::IsNullOrEmpty($config.KerberosDomainUser) -or
                [string]::IsNullOrEmpty($config.KerberosDomainPassword)) | Should -BeFalse
            $config.TCPConnectionString | Should -BeExactly $variables.REMOTE_TCP_CONN_STRING
            $config.NPConnectionString | Should -BeExactly $variables.REMOTE_NP_CONN_STRING
            $config.SupportsIntegratedSecurity | Should -BeOfType ([bool])
            $config.SupportsIntegratedSecurity | Should -BeTrue
            $config.UseManagedSNIOnWindows | Should -BeOfType ([bool])
            $config.UseManagedSNIOnWindows | Should -Be ([Convert]::ToBoolean($ManagedSni))

            $updatedProperties = @(
                'TCPConnectionString', 'NPConnectionString', 'SupportsIntegratedSecurity',
                'KerberosDomainUser', 'KerberosDomainPassword', 'UseManagedSNIOnWindows'
            )
            foreach ($property in $defaults.PSObject.Properties) {
                if ($property.Name -notin $updatedProperties) {
                    $config.($property.Name) | Should -Be $property.Value
                }
            }
        }
        finally {
            Pop-Location
            foreach ($name in $environmentNames) {
                [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
            }
        }
    }
}
