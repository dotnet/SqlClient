# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#requires -PSEdition Core
#requires -Version 7.4

<#
.SYNOPSIS
Creates or removes a temporary local Windows account and its local SQL Windows login.
.DESCRIPTION
Run setup and cleanup on the same Windows agent using PowerShell 7.4 or later,
as a local administrator with permission to create and drop SQL logins.
Setup installs the pinned SqlServer module from the pre-registered PowerShellRepository
for the current user if needed. Setup and cleanup use its Invoke-Sqlcmd cmdlet, backed by
Microsoft.Data.SqlClient, with Windows authentication over the configured TCP port.
The local CI server's certificate is trusted explicitly for these encrypted connections.
Setup publishes the account identity before provisioning and registers the
generated password only as a secret Azure DevOps variable. The local account cmdlet
receives a read-only SecureString, which is disposed after creation. Setup publishes
a dedicated TCP/master integrated connection string as netOnlyConnectionString;
the caller maps it to SQLCLIENT_NETONLY_CONNECTION_STRING for manual tests.
Port must match the configured local SQL instance TCP listener. Cleanup accepts
netOnlyUser and netOnlySid from that job; blank/unexpanded values are safe.
No policy, group membership, server role, or database user is changed.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('setup', 'cleanup')]
    [string] $Mode,

    [ValidateNotNullOrEmpty()]
    [string] $PowerShellRepository,

    [ValidatePattern('^[A-Za-z0-9_][A-Za-z0-9_$-]{0,127}$')]
    [string] $InstanceName = 'MSSQLSERVER',

    [ValidateRange(1, 65535)]
    [int] $Port = 1433,

    [AllowEmptyString()]
    [string] $AccountName = '',

    [AllowEmptyString()]
    [string] $AccountSid = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Set-PipelineVariable {
    param([string] $Name, [string] $Value, [switch] $Secret)
    $escaped = $Value.Replace('%', '%AZP25').Replace("`r", '%0D').Replace("`n", '%0A')
    $secretFlag = if ($Secret) { 'true' } else { 'false' }
    Write-Host "##vso[task.setvariable variable=$Name;issecret=$secretFlag]$escaped"
}

function Invoke-NetOnlySql {
    param([string] $Query, [string] $LoginName, [string] $SidHex)
    # Invoke-Sqlcmd has no SQL parameter binding; escape literals and disable SQLCMD expansion.
    $loginLiteral = $LoginName.Replace("'", "''")
    $sidLiteral = $SidHex.Replace("'", "''")
    $batch = @"
DECLARE @login nvarchar(128) = N'$loginLiteral';
DECLARE @sid varchar(184) = '$sidLiteral';
$Query
"@
    Invoke-Sqlcmd -ServerInstance "tcp:localhost,$Port" -Database master -Query $batch `
        -Encrypt Mandatory -TrustServerCertificate -ConnectionTimeout 15 -QueryTimeout 30 `
        -AbortOnError -DisableVariables -DisableCommands -IgnoreProviderContext -ErrorAction Stop
}

function Get-AccountSidHex {
    param([System.Security.Principal.SecurityIdentifier] $Sid)
    $bytes = New-Object byte[] $Sid.BinaryLength
    $Sid.GetBinaryForm($bytes, 0)
    return '0x' + ([System.BitConverter]::ToString($bytes)).Replace('-', '')
}

if ($Mode -eq 'cleanup' -and
    ([string]::IsNullOrWhiteSpace($AccountName) -or $AccountName -eq '$(netOnlyUser)')) {
    Write-Host 'Net-only setup did not publish an account; cleanup has nothing to remove.'
    return
}

$operation = 'validate prerequisites'
try {
    if ($env:OS -ne 'Windows_NT' -or -not [Environment]::Is64BitProcess) {
        throw 'A 64-bit PowerShell process on Windows is required.'
    }
    Import-Module Microsoft.PowerShell.LocalAccounts -ErrorAction Stop
    $sqlServerModule = @{ ModuleName = 'SqlServer'; RequiredVersion = '22.4.5.1' }
    if ($Mode -eq 'setup' -and -not (Get-Module -ListAvailable -FullyQualifiedName $sqlServerModule)) {
        $operation = 'install the SqlServer module'
        if ([string]::IsNullOrWhiteSpace($PowerShellRepository)) {
            throw 'PowerShellRepository must name a pre-registered repository to install the SqlServer module.'
        }
        Install-Module -Name $sqlServerModule.ModuleName -RequiredVersion $sqlServerModule.RequiredVersion `
            -Repository $PowerShellRepository -Scope CurrentUser -AllowClobber -Force -ErrorAction Stop
    }
    $operation = 'import the SqlServer module'
    Import-Module -FullyQualifiedName $sqlServerModule -ErrorAction Stop

    if ($Mode -eq 'setup') {
        $operation = 'select a unique local account name'
        $AccountName = 'netonly_' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
        # Check before publishing; an existing account must never become cleanup's target.
        $existing = Get-LocalUser | Where-Object { $_.Name -eq $AccountName }
        if ($existing) {
            throw "Local account '$AccountName' already exists; refusing to reuse it."
        }
        Set-PipelineVariable -Name netOnlyUser -Value $AccountName
        Set-PipelineVariable -Name netOnlyDomain -Value $env:COMPUTERNAME
        Set-PipelineVariable -Name netOnlySid -Value ''

        $operation = "create local account '$AccountName'"
        $random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        try {
            $bytes = New-Object byte[] 32
            $random.GetBytes($bytes)
            # Fixed character classes plus 256 random bits satisfy ordinary complexity rules.
            $password = 'Aa1!' + [Convert]::ToBase64String($bytes)
        }
        finally {
            $random.Dispose()
        }
        Set-PipelineVariable -Name netOnlyPassword -Value $password -Secret
        $securePassword = [System.Security.SecureString]::new()
        try {
            foreach ($character in $password.GetEnumerator()) {
                $securePassword.AppendChar($character)
            }
            $securePassword.MakeReadOnly()
            $password = $null
            $account = New-LocalUser -Name $AccountName -Password $securePassword `
                -Description "SqlClient net-only: $AccountName" -ErrorAction Stop
        }
        finally {
            $password = $null
            $securePassword.Dispose()
        }
        $AccountSid = $account.SID.Value
        Set-PipelineVariable -Name netOnlySid -Value $AccountSid
        $sidHex = Get-AccountSidHex -Sid $account.SID
        $loginName = "$env:COMPUTERNAME\$AccountName"
        $quotedLogin = '[' + $loginName.Replace(']', ']]') + ']'

        $operation = "create SQL Windows login '$loginName' on '$InstanceName'"
        Invoke-NetOnlySql -LoginName $loginName -SidHex $sidHex -Query @"
IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @login)
    THROW 51000, 'Net-only SQL login already exists; refusing to replace it.', 1;
CREATE LOGIN $quotedLogin FROM WINDOWS WITH DEFAULT_DATABASE = [master];
"@
        Set-PipelineVariable -Name netOnlyConnectionString `
            -Value "Data Source=tcp:localhost,$Port;Initial Catalog=master;Integrated Security=true;Encrypt=false"
        Write-Host "Created non-admin local account '$loginName' and Windows SQL login (default database master)."
    }
    else {
        $operation = 'validate cleanup ownership'
        if ($AccountName -notmatch '^netonly_[0-9a-f]{12}$') {
            throw "Refusing cleanup of unexpected account name '$AccountName'."
        }
        $account = Get-LocalUser | Where-Object { $_.Name -eq $AccountName }
        if ($AccountSid -eq '$(netOnlySid)') {
            $AccountSid = ''
        }
        if ($account) {
            if ($account.Description -ne "SqlClient net-only: $AccountName") {
                throw "Refusing to delete local account '$AccountName': ownership description does not match."
            }
            if ($AccountSid -and $account.SID.Value -ne $AccountSid) {
                throw "Refusing to delete local account '$AccountName': SID does not match setup."
            }
            $AccountSid = $account.SID.Value
        }
        $sidHex = ''
        if ($AccountSid) {
            $sid = New-Object System.Security.Principal.SecurityIdentifier $AccountSid
            $sidHex = Get-AccountSidHex -Sid $sid
        }
        $loginName = "$env:COMPUTERNAME\$AccountName"
        $quotedLogin = '[' + $loginName.Replace(']', ']]') + ']'
        $operation = "clean up account '$loginName' on '$InstanceName'"
        $failures = @()
        try {
            Invoke-NetOnlySql -LoginName $loginName -SidHex $sidHex -Query @"
IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @login)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.server_principals
                   WHERE name = @login AND type = 'U'
                     AND sid = CONVERT(varbinary(85), NULLIF(@sid, ''), 1))
        THROW 51001, 'Refusing to drop net-only SQL login: Windows SID does not match setup.', 1;
    DROP LOGIN $quotedLogin;
END;
"@
            Write-Host "SQL login '$loginName' removed (or already absent)."
        }
        catch {
            $failures += "SQL login cleanup on '$InstanceName' failed: $($_.Exception.Message)"
            Write-Host "##[error]$($failures[-1])"
        }
        try {
            if ($account) {
                Remove-LocalUser -SID $account.SID -ErrorAction Stop
            }
            Write-Host "Local account '$AccountName' removed (or already absent)."
        }
        catch {
            $failures += "Local account cleanup failed: $($_.Exception.Message)"
            Write-Host "##[error]$($failures[-1])"
        }
        if ($failures.Count -gt 0) {
            throw ($failures -join [Environment]::NewLine)
        }
        Set-PipelineVariable -Name netOnlyUser -Value ''
        Set-PipelineVariable -Name netOnlySid -Value ''
        Set-PipelineVariable -Name netOnlyDomain -Value ''
        Set-PipelineVariable -Name netOnlyConnectionString -Value ''
        Set-PipelineVariable -Name netOnlyPassword -Value '' -Secret
    }
}
catch {
    Write-Host "##[error]Net-only $Mode failed during '$operation': $($_.Exception.Message)"
    throw
}
