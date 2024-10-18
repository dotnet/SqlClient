# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.
# Script: Invoke-SqlServerCertificateCommand# 
# Author: SqlClient Team
# Date: July 17, 2023
# Comments: This scripts creates SQL Server SSL Self-Signed Certificate.
# This script is not intended to be used in production environments.

param ($Prefix, $Instance)

function Invoke-SqlServerCertificateCommand {
  [CmdletBinding()]
  param(
    [Parameter(Mandatory = $false)]
    [string] $certificateFolder = "C:\Certificates",
    [string] $certificateName = "SQLClientSelfSignedCertificate.cer",
    [string] $myCertStoreLocation = "Cert:\LocalMachine\My",
    [string] $rootCertStoreLocation = "Cert:\LocalMachine\Root",
    [string] $sqlServerUserAccount = "NT Service\$Prefix$Instance",
    [string] $sqlAliasName = "SQLAliasName",
    [string] $localhost = "localhost",
    [string] $LoopBackIPV4 = "127.0.0.1",
    [string] $LoopBackIPV6 = "::1"
  )
  try {
    Write-Host Certificate folder path is: $certificateFolder
    if (Test-Path -Path $certificateFolder) {
      Write-Host "Certificate folder already exists"
      Remove-Item -Path $certificateFolder -Force -Recurse
    }
    # Create a local folder to store the certificates
    New-Item -Path $certificateFolder -ItemType Directory

    # Get FQDN of the machine
    $fqdn = [System.Net.Dns]::GetHostByName(($env:computerName)).HostName

    # Create a self-signed certificate
    $params = @{
      Type              = "SSLServerAuthentication"
      Subject           = "CN=$fqdn"
      KeyAlgorithm      = "RSA"
      KeyLength         = 2048
      HashAlgorithm     = "SHA256"
      TextExtension     = "2.5.29.37={text}1.3.6.1.5.5.7.3.1", "2.5.29.17={text}DNS=$fqdn&DNS=$localhost&IPAddress=$LoopBackIPV4&DNS=$sqlAliasName&IPAddress=$LoopBackIPV6"
      NotAfter          = (Get-Date).AddMonths(36)
      KeySpec           = "KeyExchange"
      Provider          = "Microsoft RSA SChannel Cryptographic Provider"
      CertStoreLocation = $myCertStoreLocation
    }

    $certificate = New-SelfSignedCertificate @params
    Write-Host "Certificate created successfully"
    Write-Host "Certificate Thumbprint: $($certificate.Thumbprint)"

    [System.Environment]::SetEnvironmentVariable('Thumbprint', "$($certificate.Thumbprint)", [System.EnvironmentVariableTarget]::Machine)

    # Providing Read Access to the certificate for SQL Server Service Account

    $privateKey = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($certificate)
    $containerName = ""
    $identity = $sqlServerUserAccount
    $fileSystemRights = [System.Security.AccessControl.FileSystemRights]::Read
    $type = [System.Security.AccessControl.AccessControlType]::Allow
    $fileSystemAccessRuleArgumentList = @($identity, $fileSystemRights, $type)
    if ($privateKey -is [System.Security.Cryptography.RSACng]) {
      $containerName = $privateKey.Key.UniqueName
    }
    else {
      $containerName = $privateKey.CspKeyContainerInfo.UniqueKeyContainerName
    }
    $fullPath = "C:\ProgramData\Microsoft\Crypto\RSA\MachineKeys\$containerName"

    if (-Not(Test-Path -Path $fullPath -PathType Leaf)) {
      Write-Host "Certificate private key file not found at $fullPath"
      throw "Certificate private key file not found at $fullPath"
    }
  
    # Get Current ACL of the private key
    $acl = Get-Acl -Path $fullPath
  
    # Create a new Access Rule for the SQL Server Service Account
    $accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule -ArgumentList $fileSystemAccessRuleArgumentList
    $acl.AddAccessRule($accessRule)

    # Set the new ACL
    Set-Acl -Path $fullPath -AclObject $acl

    # Export the certificate to a file
    Export-Certificate -Cert $certificate -FilePath "$certificateFolder\$certificateName" -Type CERT

    # Import the certificate to the Root store
    $params = @{
      FilePath          = "$certificateFolder\$certificateName"
      CertStoreLocation = $rootCertStoreLocation
    }
    Import-Certificate @params

    # Import certificate to SQL Server
    $path = Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\*$Instance\MSSQLServer\SuperSocketNetLib"
    Set-ItemProperty -Path $path.PsPath -Name "Certificate" -Type String -Value "$($certificate.Thumbprint)"

    # Set Force Encryption to Yes
    $path = Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\*$Instance\MSSQLServer\SuperSocketNetLib"
    Set-ItemProperty -Path $path.PsPath -Name "ForceEncryption" -Type DWord -value 1

    # Restart SQL Server Service
    Restart-Service -Name "$Prefix$Instance" -Force
    Start-Sleep 10
    Start-Service SQLSERVERAGENT

    # Print out SQL Service status
    $service = Get-Service -Name "$Prefix$Instance"
    Write-Host "SQL Server Service Status: $($service.Status)"
    Write-Host "Self-Signed Certificate created successfully"
  }
  catch {
    $e = $_.Exception
    $msg = $e.Message
    while ($e.InnerException) {
      $e = $e.InnerException
      $msg += "`n" + $e.Message
    }
    Write-Host "Certificate generation was not successful. $msg"
  }
}

Invoke-SqlServerCertificateCommand
