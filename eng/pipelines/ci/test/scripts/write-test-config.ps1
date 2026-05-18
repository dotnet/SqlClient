function Write-TestConfig {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ConfigPath,

        [Parameter(Mandatory = $true)]
        [string]$TcpConnectionString,

        [Parameter(Mandatory = $true)]
        [string]$NpConnectionString,

        [Parameter(Mandatory = $true)]
        [bool]$SupportsIntegratedSecurity,

        [Parameter(Mandatory = $true)]
        [bool]$UseManagedSniOnWindows
    )

    $config = [ordered]@{
        TCPConnectionString                      = $TcpConnectionString
        NPConnectionString                       = $NpConnectionString
        TCPConnectionStringHGSVBS                = ''
        TCPConnectionStringNoneVBS               = ''
        TCPConnectionStringAASSGX                = ''
        EnclaveEnabled                           = $false
        TracingEnabled                           = $false
        AADAuthorityURL                          = ''
        AADPasswordConnectionString              = ''
        AADServicePrincipalId                    = ''
        AADServicePrincipalSecret                = ''
        AzureKeyVaultURL                         = ''
        AzureKeyVaultTenantId                    = ''
        SupportsIntegratedSecurity               = $SupportsIntegratedSecurity
        LocalDbAppName                           = ''
        LocalDbSharedInstanceName                = ''
        SupportsFileStream                       = $false
        FileStreamDirectory                      = ''
        UseManagedSNIOnWindows                   = $UseManagedSniOnWindows
        DNSCachingConnString                     = ''
        DNSCachingServerCR                       = ''
        DNSCachingServerTR                       = ''
        IsDNSCachingSupportedCR                  = $false
        IsDNSCachingSupportedTR                  = $false
        IsAzureSynapse                           = $false
        EnclaveAzureDatabaseConnString           = ''
        ManagedIdentitySupported                 = $false
        UserManagedIdentityClientId              = ''
        PowerShellPath                           = ''
        AliasName                                = ''
        WorkloadIdentityFederationServiceConnectionId = ''
    }

    $directory = Split-Path -Path $ConfigPath -Parent
    New-Item -ItemType Directory -Path $directory -Force | Out-Null

    $json = $config | ConvertTo-Json -Depth 8
    Set-Content -Path $ConfigPath -Value $json -Encoding UTF8

    Write-Host "Wrote test config to $ConfigPath"
}
