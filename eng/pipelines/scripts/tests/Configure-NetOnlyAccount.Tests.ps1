# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

BeforeAll {
    $scriptPath = Join-Path $PSScriptRoot '..\..\..\scripts\Configure-NetOnlyAccount.ps1'
    $templatePath = Join-Path $PSScriptRoot '..\..\common\templates\steps\configure-netonly-account-step.yml'
}

Describe 'Net-only provisioning runtime' {
    It 'uses modern PowerShell without the deprecated SQL client' {
        $source = Get-Content -LiteralPath $scriptPath -Raw
        $source | Should -Match '#requires -PSEdition Core'
        $source | Should -Not -Match 'System\.Data\.SqlClient|Add-Type -AssemblyName System.Data'
        Get-Content -LiteralPath $templatePath -Raw | Should -Match 'pwsh: true'
    }
}

Describe 'Net-only provisioning lifecycle' -Skip:(-not $IsWindows) {
    BeforeAll {
        # Stand-ins prevent loading SQL or account-management modules during these tests.
        function Invoke-Sqlcmd {
            [CmdletBinding()]
            param(
                $ServerInstance, $Database, $Query, $ConnectionTimeout, $QueryTimeout, $Encrypt,
                [switch] $TrustServerCertificate, [switch] $AbortOnError,
                [switch] $DisableVariables, [switch] $DisableCommands, [switch] $IgnoreProviderContext
            )
        }
        function Get-LocalUser {}
        function Install-Module {
            [CmdletBinding()]
            param($Name, $RequiredVersion, $Repository, $Scope, [switch] $AllowClobber, [switch] $Force)
        }
        function New-LocalUser {
            param($Name, $Password, $Description, $ErrorAction)
        }
        function Remove-LocalUser {
            param($SID, $ErrorAction)
        }

        $accountName = 'netonly_a0123456789b'
        $sid = [System.Security.Principal.SecurityIdentifier]::new('S-1-5-21-1-2-3-1001')
        $sidBytes = [byte[]]::new($sid.BinaryLength)
        $sid.GetBinaryForm($sidBytes, 0)
        $sidHex = '0x' + [Convert]::ToHexString($sidBytes)
    }

    BeforeEach {
        Mock Write-Host {}
        Mock Import-Module {}
        Mock Install-Module {}
        Mock Get-Module { [pscustomobject]@{ Version = [version]'22.4.5.1' } } -ParameterFilter {
            $FullyQualifiedName.Name -eq 'SqlServer'
        }
        Mock Get-LocalUser {}
        Mock New-LocalUser {
            [pscustomobject]@{ SID = $sid; Description = $Description }
        }
        Mock Remove-LocalUser {}
        Mock Invoke-Sqlcmd {}
    }

    It 'loads the pinned module before creating an account and uses TCP with integrated authentication' {
        & $scriptPath -Mode setup -Port 15433

        Should -Invoke Import-Module -Times 1 -Exactly -ParameterFilter {
            $FullyQualifiedName.Name -eq 'SqlServer' -and
            $FullyQualifiedName.RequiredVersion -eq '22.4.5.1'
        }
        Should -Invoke Install-Module -Times 0 -Exactly
        Should -Invoke Invoke-Sqlcmd -Times 1 -Exactly -ParameterFilter {
            $ServerInstance -eq 'tcp:localhost,15433' -and $Database -eq 'master' -and
            $ConnectionTimeout -eq 15 -and $QueryTimeout -eq 30 -and
            $Encrypt -eq 'Mandatory' -and $TrustServerCertificate -and
            $AbortOnError -and $DisableVariables -and $DisableCommands -and
            $IgnoreProviderContext -and $ErrorAction -eq 'Stop' -and
            $Query.Contains('CREATE LOGIN [') -and $Query.Contains('FROM WINDOWS')
        }
        Should -Invoke New-LocalUser -Times 1 -Exactly
        Should -Invoke Write-Host -Times 1 -Exactly -ParameterFilter {
            "$Object" -like '*variable=netOnlyConnectionString;*Data Source=tcp:localhost,15433;*'
        }
    }

    It 'installs only the pinned module when it is missing' {
        Mock Get-Module {} -ParameterFilter { $FullyQualifiedName.Name -eq 'SqlServer' }

        & $scriptPath -Mode setup

        Should -Invoke Install-Module -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'SqlServer' -and $RequiredVersion -eq '22.4.5.1' -and
            $Repository -eq 'PSGallery' -and $Scope -eq 'CurrentUser' -and $AllowClobber -and $Force
        }
    }

    It 'uses the secret pipeline password as a read-only SecureString and disposes it on <Outcome>' -ForEach @(
        @{ Outcome = 'success'; FailCreation = $false }
        @{ Outcome = 'failure'; FailCreation = $true }
    ) {
        $passwordState = @{ Published = $null; Account = $null }
        Mock Write-Host {
            $passwordState.Published = "$Object".Substring(
                '##vso[task.setvariable variable=netOnlyPassword;issecret=true]'.Length)
        } -ParameterFilter {
            "$Object".StartsWith('##vso[task.setvariable variable=netOnlyPassword;issecret=true]')
        }
        Mock New-LocalUser {
            $passwordState.Account = $Password
            $Password | Should -BeOfType ([System.Security.SecureString])
            $Password.IsReadOnly() | Should -BeTrue
            $Password.Length | Should -Be 48
            $credential = [System.Net.NetworkCredential]::new('', $Password)
            $credential.Password | Should -BeExactly $passwordState.Published
            $credential.Password | Should -Match '^Aa1![A-Za-z0-9+/]{43}=$'
            if ($FailCreation) {
                throw 'account creation failed'
            }
            [pscustomobject]@{ SID = $sid; Description = $Description }
        }

        try {
            if ($FailCreation) {
                { & $scriptPath -Mode setup } | Should -Throw '*account creation failed*'
            }
            else {
                & $scriptPath -Mode setup
            }

            Should -Invoke New-LocalUser -Times 1 -Exactly
            Should -Invoke Write-Host -Times 1 -Exactly -ParameterFilter {
                "$Object".StartsWith('##vso[task.setvariable variable=netOnlyPassword;issecret=true]')
            }
            $passwordState.Account | Should -BeOfType ([System.Security.SecureString])
            $disposalError = { $passwordState.Account.IsReadOnly() } | Should -Throw -PassThru
            $disposalError.Exception.InnerException | Should -BeOfType ([ObjectDisposedException])
        }
        finally {
            $passwordState.Clear()
        }
    }

    It 'does not create an account if module installation fails' {
        Mock Get-Module {} -ParameterFilter { $FullyQualifiedName.Name -eq 'SqlServer' }
        Mock Install-Module { throw 'module installation failed' }

        { & $scriptPath -Mode setup } | Should -Throw '*module installation failed*'
        Should -Invoke New-LocalUser -Times 0 -Exactly
    }

    It 'does not create an account if module import fails' {
        Mock Import-Module { throw 'module import failed' } -ParameterFilter {
            $FullyQualifiedName.Name -eq 'SqlServer'
        }

        { & $scriptPath -Mode setup } | Should -Throw '*module import failed*'
        Should -Invoke New-LocalUser -Times 0 -Exactly
    }

    It 'propagates SQL setup failures after publishing cleanup identity' {
        Mock Invoke-Sqlcmd { throw 'SQL setup failed' }

        { & $scriptPath -Mode setup } | Should -Throw '*SQL setup failed*'
        Should -Invoke Write-Host -Times 1 -Exactly -ParameterFilter {
            "$Object" -eq "##vso[task.setvariable variable=netOnlySid;issecret=false]$sid"
        }
    }

    It 'does not load modules or remove anything when cleanup has no account' -ForEach @(
        @{ Name = '' }
        @{ Name = '$(netOnlyUser)' }
    ) {
        & $scriptPath -Mode cleanup -AccountName $Name

        Should -Invoke Import-Module -Times 0 -Exactly
        Should -Invoke Install-Module -Times 0 -Exactly
        Should -Invoke Invoke-Sqlcmd -Times 0 -Exactly
        Should -Invoke Remove-LocalUser -Times 0 -Exactly
    }

    It 'checks the SQL login SID before dropping it and removes the owned local account' {
        Mock Get-LocalUser {
            [pscustomobject]@{ Name = $accountName; SID = $sid; Description = "SqlClient net-only: $accountName" }
        }

        & $scriptPath -Mode cleanup -AccountName $accountName -AccountSid $sid.Value

        Should -Invoke Install-Module -Times 0 -Exactly
        Should -Invoke Invoke-Sqlcmd -Times 1 -Exactly -ParameterFilter {
            $Query.Contains($sidHex) -and $Query.Contains("name = @login AND type = 'U'") -and
            $Query.Contains("CONVERT(varbinary(85), NULLIF(@sid, ''), 1)") -and
            $Query.Contains('DROP LOGIN [')
        }
        Should -Invoke Remove-LocalUser -Times 1 -Exactly -ParameterFilter {
            $SID.Value -eq 'S-1-5-21-1-2-3-1001'
        }
    }

    It 'still attempts local account cleanup after a SQL failure and reports failure' {
        Mock Get-LocalUser {
            [pscustomobject]@{ Name = $accountName; SID = $sid; Description = "SqlClient net-only: $accountName" }
        }
        Mock Invoke-Sqlcmd { throw 'SQL cleanup failed' }

        { & $scriptPath -Mode cleanup -AccountName $accountName -AccountSid $sid.Value } |
            Should -Throw '*SQL cleanup failed*'
        Should -Invoke Remove-LocalUser -Times 1 -Exactly
    }

    It 'refuses cleanup when local ownership does not match' -ForEach @(
        @{ Description = 'not owned by this job'; AccountSid = 'S-1-5-21-1-2-3-1001' }
        @{ Description = 'SqlClient net-only: netonly_a0123456789b'; AccountSid = 'S-1-5-21-1-2-3-1002' }
    ) {
        Mock Get-LocalUser {
            [pscustomobject]@{ Name = $accountName; SID = $sid; Description = $Description }
        }

        { & $scriptPath -Mode cleanup -AccountName $accountName -AccountSid $AccountSid } |
            Should -Throw '*Refusing to delete local account*'
        Should -Invoke Invoke-Sqlcmd -Times 0 -Exactly
        Should -Invoke Remove-LocalUser -Times 0 -Exactly
    }

    It 'escapes SQL literals and disables SQLCMD substitutions' {
        $originalComputerName = $env:COMPUTERNAME
        try {
            $env:COMPUTERNAME = 'HOST''$(value)]'
            & $scriptPath -Mode setup
        }
        finally {
            $env:COMPUTERNAME = $originalComputerName
        }

        Should -Invoke Invoke-Sqlcmd -Times 1 -Exactly -ParameterFilter {
            $Query.Contains('N''HOST''''$(value)]\netonly_') -and
            $Query.Contains('[HOST''$(value)]]\netonly_') -and
            $DisableVariables -and $DisableCommands
        }
    }
}
