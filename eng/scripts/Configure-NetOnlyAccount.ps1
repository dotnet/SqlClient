# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Creates or removes a temporary local Windows account and its local SQL Windows login.
.DESCRIPTION
Run setup and cleanup on the same Windows agent using Windows PowerShell 5.1,
as a local administrator with permission to create and drop SQL logins.
Setup publishes the account identity before provisioning and registers the
generated password only as a secret Azure DevOps variable. It publishes a
dedicated TCP/master integrated connection string as netOnlyConnectionString;
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
    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    # Shared memory forces a local connection without SQL Browser/TCP configuration.
    $builder['Data Source'] = 'lpc:.'
    if ($InstanceName -ne 'MSSQLSERVER') {
        $builder['Data Source'] = "lpc:.\$InstanceName"
    }
    $builder['Initial Catalog'] = 'master'
    $builder['Integrated Security'] = $true
    $builder['Connect Timeout'] = 15
    $connection = New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        try {
            $command.CommandTimeout = 30
            $command.CommandText = $Query
            [void] $command.Parameters.Add('@login', [System.Data.SqlDbType]::NVarChar, 128)
            $command.Parameters['@login'].Value = $LoginName
            [void] $command.Parameters.Add('@sid', [System.Data.SqlDbType]::VarChar, 184)
            $command.Parameters['@sid'].Value = $SidHex
            [void] $command.ExecuteNonQuery()
        }
        finally {
            $command.Dispose()
        }
    }
    finally {
        $connection.Dispose()
    }
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
        throw 'A 64-bit Windows PowerShell process on Windows is required.'
    }
    Import-Module Microsoft.PowerShell.LocalAccounts -ErrorAction Stop
    Add-Type -AssemblyName System.Data

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
        $securePassword = ConvertTo-SecureString -String $password -AsPlainText -Force
        $password = $null
        try {
            $account = New-LocalUser -Name $AccountName -Password $securePassword `
                -Description "SqlClient net-only: $AccountName" -ErrorAction Stop
        }
        finally {
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
