# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.
# Script: Invoke-SqlServerCertificateCommand# 
# Author: SqlClient Team
# Date: March 20, 2024
# Comments: This scripts creates SQL Server SSL Self-Signed Certificate.
# This script is not intended to be used in production environments.

function Invoke-SqlServerCertificateCommand {
  [CmdletBinding()]
  param(
    [Parameter(Mandatory = $false)]
    [string] $certificateName = ".\localhostcert.cer",
    [string] $myCertStoreLocation = "Cert:\LocalMachine\My",
    [string] $rootCertStoreLocation = "Cert:\LocalMachine\Root",
    [string] $sqlAliasName = "SQLAliasName",
    [string] $localhost = "localhost",
    [string] $LoopBackIPV4 = "127.0.0.1",
    [string] $LoopBackIPV6 = "::1"
  )
  Write-Output "Certificate generation started..."
  try {
    # Get FQDN of the machine
    Write-Output "Get FQDN of the machine..."
    $fqdn = [System.Net.Dns]::GetHostByName(($env:computerName)).HostName
    Write-Output "FQDN = $fqdn..."

    $OS = [System.Environment]::OSVersion.Platform
    Write-Output "Operating System is $OS..."

    # Create a self-signed certificate
    if ($OS -eq "Unix") {
        # Create self signed certificate using openssl
        Write-Output "Creating certificate for linux..."
        openssl req -x509 -newkey rsa:4096 -sha256 -days 1095 -nodes -keyout ./localhostcert.key -out ./localhostcert.cer -subj "/CN=$fqdn" -addext "subjectAltName=DNS:$fqdn,DNS:localhost,IP:127.0.0.1,IP:::1"
        # Export the certificate to pfx
        Write-Output "Exporting certificate to pfx..."
        openssl pkcs12 -export -in ./localhostcert.cer -inkey ./localhostcert.key -out ./localhostcert.pfx -password pass:nopassword

        Write-Output "Converting certificate to pem..."
        # Create pem from cer
        cp ./localhostcert.cer ./localhostcert.pem

        # Add trust to the pem certificate
        Write-Output "Adding trust to pem certificate..."
        openssl x509 -trustout -addtrust "serverAuth" -in ./localhostcert.pem
        
        # Import the certificate to the Root store ------------------------------------------------------------------------------
        # NOTE:  The process must have root privileges to add the certificate to the Root store. If not, then use  
        #        "chmod 777 /usr/local/share/ca-certificates" to give read, write and execute privileges to anyone on that folder 
        # Copy the certificate to /usr/local/share/ca-certificates folder while changing the extension to "crt". 
        # Only certificates with extension "crt" gets added for some reason.
        Write-Output "Copy the pem certificate to /usr/local/share/ca-certificates folder..."
        cp ./localhostcert.pem /usr/local/share/ca-certificates/localhostcert.crt

        # Update the certificates store
        Write-Output "Updating the certificates store..."
        update-ca-certificates -v
    } else {
        Write-Output "Creating a self-signed certificate..."
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
          FriendlyName      = "TestTDSServerCertificate"
        }

        $certificate = New-SelfSignedCertificate @params
        Write-Output "Certificate created successfully"
        Write-Output "Certificate Thumbprint: $($certificate.Thumbprint)"

        # Export the certificate to a file
        Write-Output "Exporting the certificate to a file..."
        Export-Certificate -Cert $certificate -FilePath "$certificateName" -Type CERT

        # Import the certificate to the Root store
        Write-Output "Importing the certificate to the Root store..."
        $params = @{
          FilePath          = "$certificateName"
          CertStoreLocation = $rootCertStoreLocation
        }
        Import-Certificate @params

        Write-Output "Converting certificate to pfx..."
        Write-Output "Cert:\LocalMachine\my\$($certificate.Thumbprint)"

        $pwd = ConvertTo-SecureString -String 'nopassword' -Force -AsPlainText
        # Export the certificate to a pfx format
        Export-PfxCertificate -Password $pwd -FilePath ".\localhostcert.pfx" -Cert "Cert:\LocalMachine\my\$($certificate.Thumbprint)"
    } 

    Write-Output "Done creating pfx certificate..."
  }
  catch {
    $e = $_.Exception
    $msg = $e.Message
    while ($e.InnerException) {
      $e = $e.InnerException
      $msg += "`n" + $e.Message
    }

    if ($OS -eq "Unix") {
        # Display the contents of result.txt for debugging
        cat result.txt
    }
    Write-Output "Certificate generation was not successfull. $msg"
  }

  Write-Output "Certificate generation task completed."
}

Invoke-SqlServerCertificateCommand
